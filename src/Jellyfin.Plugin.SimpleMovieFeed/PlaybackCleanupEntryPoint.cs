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
        Guid ItemId,
        ActiveMoviePlayback Record,
        CancellationTokenSource Cancellation);

    private readonly ISessionManager _sessionManager;
    private readonly QBitTorrentService _qbit;
    private readonly IUserManager _userManager;
    private readonly IUserDataManager _userDataManager;

    private readonly ConcurrentDictionary<
        string,
        PendingCleanup> _pendingCleanup =
            new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<
        string,
        SemaphoreSlim> _cleanupLocks =
            new(StringComparer.OrdinalIgnoreCase);

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
                await _qbit.RemoveCompletedPluginTorrentsAsync(
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

            var playSessionId =
                e.PlaySessionId;

            if (string.IsNullOrWhiteSpace(
                    playSessionId))
            {
                return;
            }

            var userId =
                e.Users?
                    .FirstOrDefault()?
                    .Id
                ?? Guid.Empty;

            if (userId == Guid.Empty)
            {
                return;
            }

            if (PlaybackStateStore.TryActivatePrepared(
                    item.Id,
                    userId,
                    playSessionId,
                    out _))
            {
                Console.WriteLine(
                    "SimpleMovieFeed: playback session " +
                    playSessionId +
                    " activated for user " +
                    userId +
                    ", item " +
                    item.Id +
                    ".");

                return;
            }

            /*
             * A Jellyfin playback can restart during the cleanup
             * grace period without another plugin startup request.
             * Recover the stopped record, but only for the same
             * Jellyfin item and user.
             */
            foreach (var entry in _pendingCleanup)
            {
                var pending = entry.Value;

                if (
                    pending.ItemId != item.Id ||
                    pending.Record.UserId != userId)
                {
                    continue;
                }

                if (!_pendingCleanup.TryRemove(
                        entry.Key,
                        out var recovered))
                {
                    continue;
                }

                recovered.Cancellation.Cancel();

                PlaybackStateStore.RestoreActive(
                    playSessionId,
                    recovered.Record);

                recovered.Cancellation.Dispose();

                Console.WriteLine(
                    "SimpleMovieFeed: cleanup cancelled because playback restarted for user " +
                    userId +
                    ", item " +
                    item.Id +
                    ", session " +
                    playSessionId +
                    ".");

                return;
            }
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
                    e.PlaySessionId,
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

            var playSessionId =
                e.PlaySessionId;

            if (!PlaybackStateStore.TryTakeActive(
                    playSessionId,
                    out var record))
            {
                return;
            }

            if (
                record is null ||
                string.IsNullOrWhiteSpace(
                    playSessionId))
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
                "SimpleMovieFeed: playback session " +
                playSessionId +
                " stopped for " +
                item.Name +
                " at " +
                position +
                " ticks. Torrent cleanup scheduled.");

            var cancellation =
                new CancellationTokenSource();

            var pending =
                new PendingCleanup(
                    item.Id,
                    record,
                    cancellation);

            if (_pendingCleanup.TryRemove(
                    playSessionId,
                    out var previous))
            {
                previous.Cancellation.Cancel();
                previous.Cancellation.Dispose();
            }

            _pendingCleanup[
                playSessionId] =
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
                    playSessionId,
                    out var current))
            {
                return;
            }

            if (!ReferenceEquals(
                    current,
                    pending))
            {
                current.Cancellation.Dispose();
                return;
            }

            try
            {
                var cleanupLock =
                    _cleanupLocks.GetOrAdd(
                        record.TorrentHash,
                        _ => new SemaphoreSlim(1, 1));

                await cleanupLock.WaitAsync();

                try
                {
                    /*
                     * This per-torrent lock serializes grace timers.
                     * Each stopped session has already removed its own
                     * pending-cleanup entry before reaching this point.
                     */
                    var siblingCleanupExists =
                        _pendingCleanup.Any(
                            entry =>
                                string.Equals(
                                    entry.Value.Record.TorrentHash,
                                    record.TorrentHash,
                                    StringComparison.OrdinalIgnoreCase));

                    if (
                        siblingCleanupExists ||
                        PlaybackStateStore.IsTorrentInUse(
                            record.TorrentHash))
                    {
                        Console.WriteLine(
                            "SimpleMovieFeed: final cleanup deferred for session " +
                            playSessionId +
                            " because torrent " +
                            record.TorrentHash +
                            " still has another playback or cleanup consumer.");

                        return;
                    }

                    try
                    {
                        await _qbit.DeleteTorrentAsync(
                            record.TorrentHash,
                            deleteFiles: false);
                    }
                    catch (Exception ex)
                    {
                        /*
                         * The completion monitor may already have removed
                         * the qBittorrent registration. Cache deletion must
                         * remain independent of that operation.
                         */
                        Console.WriteLine(
                            "SimpleMovieFeed: final qBittorrent registration removal failed or was already absent for " +
                            record.TorrentHash +
                            "; continuing with retained-cache cleanup: " +
                            ex.Message);
                    }

                    _qbit.DeleteCachedTorrentContent(
                        record.CachePath);

                    Console.WriteLine(
                        "SimpleMovieFeed: final playback consumer ended; retained torrent cache deleted; persistent Jellyfin placeholder retained.");
                }
                finally
                {
                    cleanupLock.Release();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "SimpleMovieFeed: final playback cleanup failed: " +
                    ex);
            }
            finally
            {
                current.Cancellation.Dispose();
            }
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



