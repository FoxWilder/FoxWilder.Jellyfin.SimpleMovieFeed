using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Dto;
using System.Collections.Concurrent;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.Plugin.SimpleMovieFeed;

public sealed class PlaybackCleanupEntryPoint :
    IHostedService,
    IDisposable
{
    private sealed record PendingCleanup(
        ActiveMoviePlayback Record,
        CancellationTokenSource Cancellation);

    private readonly ISessionManager _sessionManager;
    private readonly QBitTorrentService _qbit;
    private readonly IUserManager _userManager;
    private readonly IUserDataManager _userDataManager;

    private readonly ConcurrentDictionary<
        Guid,
        PendingCleanup> _pendingCleanup =
            new();

    private CancellationTokenSource?
        _monitorCancellation;

    private Task?
        _monitorTask;

    public PlaybackCleanupEntryPoint(
        ISessionManager sessionManager,
        QBitTorrentService qbit,
        IUserManager userManager,
        IUserDataManager userDataManager)
    {
        _sessionManager =
            sessionManager;

        _qbit =
            qbit;
        _userManager = userManager;
        _userDataManager = userDataManager;
    }

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        /*
         * Server/plugin startup means every previous streaming
         * session is stale. Remove its qBittorrent data.
         */
        try
        {
            await _qbit.PurgePluginTorrentsAsync(
                cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "SimpleMovieFeed: startup qBittorrent purge failed: " +
                ex.Message);
        }

        CleanupDisposableCache();
        CleanupLegacyLibraryMedia();

        _sessionManager.PlaybackStart +=
            OnPlaybackStart;

        _sessionManager.PlaybackStopped +=
            OnPlaybackStopped;

        _sessionManager.PlaybackProgress +=
            OnPlaybackProgress;

        _monitorCancellation =
            new CancellationTokenSource();

        _monitorTask =
            MonitorCompletedTorrentsAsync(
                _monitorCancellation.Token);

        Console.WriteLine(
            "SimpleMovieFeed: playback cleanup listener active.");
    }

    public async Task StopAsync(
        CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart -=
            OnPlaybackStart;

        _sessionManager.PlaybackStopped -=
            OnPlaybackStopped;

        _sessionManager.PlaybackProgress -=
            OnPlaybackProgress;

        CancelAllPendingCleanup();

        if (_monitorCancellation != null)
        {
            _monitorCancellation.Cancel();
        }

        if (_monitorTask != null)
        {
            try
            {
                await _monitorTask.WaitAsync(
                    cancellationToken);
            }
            catch (
                OperationCanceledException)
            {
            }
        }
    }

    private async Task MonitorCompletedTorrentsAsync(
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _qbit.PauseCompletedPluginTorrentsAsync(
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "SimpleMovieFeed: completed-torrent monitor failed: " +
                    ex.Message);
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void OnPlaybackStart(
        object? sender,
        PlaybackProgressEventArgs e)
    {
        try
        {
            var item = e.Item;

            if (item is null)
            {
                return;
            }

            if (!_pendingCleanup.TryRemove(
                    item.Id,
                    out var pending))
            {
                return;
            }

            pending.Cancellation.Cancel();

            PlaybackStateStore.RestoreActive(
                item.Id,
                pending.Record);

            pending.Cancellation.Dispose();

            Console.WriteLine(
                "SimpleMovieFeed: cleanup cancelled because playback restarted for " +
                item.Name);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "SimpleMovieFeed: PlaybackStart handler failed: " +
                ex);
        }
    }

    private void OnPlaybackProgress(
        object? sender,
        PlaybackProgressEventArgs e)
    {
        try
        {
            var item = e.Item;

            if (item is null)
            {
                return;
            }

            if (!PlaybackStateStore.TryGetActive(
                    item.Id,
                    out var record))
            {
                return;
            }

            if (record is null)
            {
                return;
            }

            var position =
                e.PlaybackPositionTicks ?? 0;

            if (position <= 0)
            {
                return;
            }

            PlaybackStateStore.SaveResumePosition(
                record.UserId,
                record.MovieId,
                position,
                false);

            SyncJellyfinResume(
                record.UserId,
                item,
                position,
                false,
                UserDataSaveReason.PlaybackProgress);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "SimpleMovieFeed: PlaybackProgress handler failed: " +
                ex);
        }
    }

    private async void OnPlaybackStopped(
        object? sender,
        PlaybackStopEventArgs e)
    {
        try
        {
            var item = e.Item;

            if (item is null)
            {
                return;
            }

            if (!PlaybackStateStore.TryTakeActive(
                    item.Id,
                    out var record))
            {
                return;
            }

            if (record is null)
            {
                return;
            }

            var position =
                e.PlaybackPositionTicks ?? 0;

            PlaybackStateStore.SaveResumePosition(
                record.UserId,
                record.MovieId,
                position,
                e.PlayedToCompletion);

            SyncJellyfinResume(
                record.UserId,
                item,
                position,
                e.PlayedToCompletion,
                UserDataSaveReason.PlaybackFinished);

            Console.WriteLine(
                "SimpleMovieFeed: playback stopped for " +
                item.Name +
                " at " +
                position +
                " ticks. Torrent cleanup scheduled.");

            var cancellation =
                new CancellationTokenSource();

            var pending =
                new PendingCleanup(
                    record,
                    cancellation);

            if (_pendingCleanup.TryRemove(
                    item.Id,
                    out var previous))
            {
                previous.Cancellation.Cancel();
                previous.Cancellation.Dispose();
            }

            _pendingCleanup[
                item.Id] =
                pending;

            try
            {
                await Task.Delay(
                    RuntimeSettings.CleanupGrace,
                    cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!_pendingCleanup.TryRemove(
                    item.Id,
                    out var current))
            {
                return;
            }

            if (!ReferenceEquals(
                    current,
                    pending))
            {
                return;
            }

            /*
             * Keep the tiny .strm library item.
             * It is what preserves Jellyfin's native identity and
             * makes standard Continue Watching possible.
             *
             * Only torrent/cache data is deleted.
             */
            try
            {
                await _qbit.DeleteTorrentAsync(
                    record.TorrentHash,
                    deleteFiles: true);

                Console.WriteLine(
                    "SimpleMovieFeed: torrent and cache deleted; persistent Jellyfin placeholder retained.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "SimpleMovieFeed: qBittorrent cleanup failed: " +
                    ex);
            }

            current.Cancellation.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "SimpleMovieFeed: PlaybackStopped handler failed: " +
                ex);
        }
    }

    private static void CleanupDisposableCache()
    {
        try
        {
            Directory.CreateDirectory(
                RuntimeSettings.CacheDirectory);

            foreach (var file in
                Directory.EnumerateFiles(
                    RuntimeSettings.CacheDirectory,
                    "*",
                    SearchOption.AllDirectories))
            {
                /*
                 * Keep persistent plugin state such as resume,
                 * catalog and any watch-history JSON.
                 */
                if (string.Equals(
                        Path.GetExtension(file),
                        ".json",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    File.Delete(
                        file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        "SimpleMovieFeed: unable to delete cache file " +
                        file +
                        ": " +
                        ex.Message);
                }
            }

            foreach (var directory in
                Directory
                    .EnumerateDirectories(
                        RuntimeSettings.CacheDirectory,
                        "*",
                        SearchOption.AllDirectories)
                    .OrderByDescending(
                        path => path.Length))
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(
                            directory)
                        .Any())
                    {
                        Directory.Delete(
                            directory);
                    }
                }
                catch
                {
                }
            }

            Console.WriteLine(
                "SimpleMovieFeed: disposable cache cleared at startup.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "SimpleMovieFeed: startup cache cleanup failed: " +
                ex.Message);
        }
    }

    private static void CleanupLegacyLibraryMedia()
    {
        try
        {
            Directory.CreateDirectory(
                RuntimeSettings.LibraryDirectory);

            foreach (var file in
                Directory.EnumerateFiles(
                    RuntimeSettings.LibraryDirectory,
                    "*",
                    SearchOption.AllDirectories))
            {
                /*
                 * Preserve only tiny .strm placeholders.
                 * Old mp4/mkv hardlinks are removed.
                 */
                if (string.Equals(
                        Path.GetExtension(file),
                        ".strm",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        Path.GetFileName(file),
                        "poster.jpg",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        Path.GetFileName(file),
                        "movie.nfo",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    File.Delete(
                        file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        "SimpleMovieFeed: unable to remove legacy library media " +
                        file +
                        ": " +
                        ex.Message);
                }
            }

            Console.WriteLine(
                "SimpleMovieFeed: temporary library media cleared; .strm placeholders preserved.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "SimpleMovieFeed: startup library cleanup failed: " +
                ex.Message);
        }
    }

    private void SyncJellyfinResume(
        Guid userId,
        MediaBrowser.Controller.Entities.BaseItem item,
        long positionTicks,
        bool playedToCompletion,
        UserDataSaveReason reason)
    {
        try
        {
            if (userId == Guid.Empty)
            {
                return;
            }

            if (positionTicks < 0)
            {
                positionTicks = 0;
            }

            var user =
                _userManager.GetUserById(
                    userId);

            if (user is null)
            {
                Console.WriteLine(
                    "SimpleMovieFeed: Jellyfin UserData sync skipped; user not found: " +
                    userId);

                return;
            }

            var nativePosition =
                playedToCompletion
                    ? 0
                    : positionTicks;

            var update =
                new UpdateUserItemDataDto
                {
                    PlaybackPositionTicks =
                        nativePosition,

                    Played =
                        playedToCompletion,

                    LastPlayedDate =
                        DateTime.UtcNow
                };

            _userDataManager.SaveUserData(
                user,
                item,
                update,
                reason);

            Console.WriteLine(
                "SimpleMovieFeed: Jellyfin UserData synced for user " +
                userId +
                ", item " +
                item.Id +
                ", position " +
                nativePosition +
                ", played=" +
                playedToCompletion);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "SimpleMovieFeed: Jellyfin UserData sync failed: " +
                ex);
        }
    }
    private void CancelAllPendingCleanup()
    {
        foreach (var entry in
            _pendingCleanup)
        {
            entry.Value.Cancellation.Cancel();
            entry.Value.Cancellation.Dispose();
        }

        _pendingCleanup.Clear();
    }

    public void Dispose()
    {
        _sessionManager.PlaybackStart -=
            OnPlaybackStart;

        _sessionManager.PlaybackStopped -=
            OnPlaybackStopped;

        _sessionManager.PlaybackProgress -=
            OnPlaybackProgress;

        if (_monitorCancellation != null)
        {
            _monitorCancellation.Cancel();
            _monitorCancellation.Dispose();
        }

        CancelAllPendingCleanup();
    }
}



