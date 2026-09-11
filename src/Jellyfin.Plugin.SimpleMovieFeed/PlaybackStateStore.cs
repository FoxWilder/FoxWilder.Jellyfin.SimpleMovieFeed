using System.Collections.Concurrent;
using System.Text.Json;

namespace Jellyfin.Plugin.SimpleMovieFeed;

public sealed record ActiveMoviePlayback(
    Guid StartupId,
    Guid UserId,
    int MovieId,
    string MovieTitle,
    int Year,
    string MagnetLink,
    string Quality,
    string TorrentHash,
    string CachePath,
    string LibraryPath);

public sealed record StoredMovie(
    int MovieId,
    string MovieTitle,
    int Year,
    string MagnetLink,
    string Quality);

public sealed record RetainedCompletedCache(
    int MovieId,
    string MagnetLink,
    string TorrentHash,
    string CachePath,
    long FileSize);

public static class PlaybackStateStore
{
    private static readonly object ResumeLock = new();
    private static readonly object CatalogLock = new();

    private static readonly ConcurrentDictionary<
        string,
        ActiveMoviePlayback> ActiveByPlaySessionId =
            new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<
        Guid,
        ConcurrentDictionary<Guid, ActiveMoviePlayback>>
        PreparedByItemId =
            new();

    private static readonly ConcurrentDictionary<
        string,
        ConcurrentDictionary<Guid, ActiveMoviePlayback>>
        PendingByLibraryPath =
            new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<
        string,
        RetainedCompletedCache>
        RetainedCompletedByTorrentHash =
            new(StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, long> _resume =
        new(StringComparer.OrdinalIgnoreCase);

    private static Dictionary<Guid, StoredMovie> _catalog =
        new();

    private static string _resumeFile = "";
    private static string _catalogFile = "";

    public static void Initialize(
        string cacheDirectory)
    {
        Directory.CreateDirectory(
            cacheDirectory);

        _resumeFile =
            Path.Combine(
                cacheDirectory,
                "simplemoviefeed-resume.json");

        _catalogFile =
            Path.Combine(
                cacheDirectory,
                "simplemoviefeed-catalog.json");

        LoadResume();
        LoadCatalog();
    }

    private static void LoadResume()
    {
        lock (ResumeLock)
        {
            if (!File.Exists(_resumeFile))
            {
                _resume =
                    new(
                        StringComparer.OrdinalIgnoreCase);

                return;
            }

            try
            {
                var json =
                    File.ReadAllText(
                        _resumeFile);

                var loaded =
                    JsonSerializer.Deserialize<
                        Dictionary<string, long>>(
                            json)
                    ?? new();

                _resume =
                    new Dictionary<string, long>(
                        loaded,
                        StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "SimpleMovieFeed: unable to load resume state: " +
                    ex.Message);

                _resume =
                    new(
                        StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private static void LoadCatalog()
    {
        lock (CatalogLock)
        {
            if (!File.Exists(_catalogFile))
            {
                _catalog =
                    new();

                return;
            }

            try
            {
                var json =
                    File.ReadAllText(
                        _catalogFile);

                _catalog =
                    JsonSerializer.Deserialize<
                        Dictionary<Guid, StoredMovie>>(
                            json)
                    ?? new();
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "SimpleMovieFeed: unable to load movie catalog: " +
                    ex.Message);

                _catalog =
                    new();
            }
        }
    }

    private static string GetResumeKey(
        Guid userId,
        int movieId)
    {
        return
            userId.ToString("N") +
            ":" +
            movieId;
    }

    public static void RegisterPending(
        Guid startupId,
        Guid userId,
        int movieId,
        string movieTitle,
        int year,
        string magnetLink,
        string quality,
        string torrentHash,
        string cachePath,
        string libraryPath)
    {
        if (startupId == Guid.Empty)
        {
            throw new ArgumentException(
                "Startup ID is required.",
                nameof(startupId));
        }

        /*
         * /stream/start can be retried after Jellyfin has already
         * activated this logical browser startup. Do not recreate
         * pending/prepared state for an exact StartupId that is
         * already active.
         */
        var alreadyActive =
            ActiveByPlaySessionId.Values
                .FirstOrDefault(
                    active =>
                        active.StartupId == startupId);

        if (alreadyActive is not null)
        {
            if (
                alreadyActive.UserId != userId ||
                alreadyActive.MovieId != movieId ||
                !string.Equals(
                    alreadyActive.TorrentHash,
                    torrentHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Startup ID is already active for a different playback.");
            }

            return;
        }

        var record =
            new ActiveMoviePlayback(
                startupId,
                userId,
                movieId,
                movieTitle,
                year,
                magnetLink,
                quality,
                torrentHash,
                cachePath,
                libraryPath);

        var fullPath =
            Path.GetFullPath(
                libraryPath);

        var pending =
            PendingByLibraryPath.GetOrAdd(
                fullPath,
                _ => new());
        pending[
            startupId] =
            record;

        /*
         * Activation may race the registration above. If this exact
         * startup became active between the first active check and
         * the pending write, remove only its own duplicate.
         */
        if (
            ActiveByPlaySessionId.Values.Any(
                active =>
                    active.StartupId == startupId))
        {
            pending.TryRemove(
                startupId,
                out _);
        }
    }

    public static bool RemovePendingStartup(
        Guid startupId,
        Guid userId,
        int movieId,
        string torrentHash)
    {
        if (startupId == Guid.Empty)
        {
            return false;
        }

        foreach (var pending in PendingByLibraryPath.Values)
        {
            if (!pending.TryGetValue(
                    startupId,
                    out var record))
            {
                continue;
            }

            if (
                record.UserId != userId ||
                record.MovieId != movieId ||
                !string.Equals(
                    record.TorrentHash,
                    torrentHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return pending.TryRemove(
                startupId,
                out _);
        }

        return false;
    }

    public static bool RemovePreparedStartup(
        Guid startupId,
        Guid userId,
        int movieId,
        string torrentHash)
    {
        if (startupId == Guid.Empty)
        {
            return false;
        }

        foreach (var prepared in PreparedByItemId.Values)
        {
            if (!prepared.TryGetValue(
                    startupId,
                    out var record))
            {
                continue;
            }

            if (
                record.UserId != userId ||
                record.MovieId != movieId ||
                !string.Equals(
                    record.TorrentHash,
                    torrentHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return prepared.TryRemove(
                startupId,
                out _);
        }

        return false;
    }

    public static bool IsTorrentInUse(
        string torrentHash)
    {
        foreach (var pending in PendingByLibraryPath.Values)
        {
            foreach (var record in pending.Values)
            {
                if (string.Equals(
                        record.TorrentHash,
                        torrentHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        foreach (var prepared in PreparedByItemId.Values)
        {
            foreach (var record in prepared.Values)
            {
                if (string.Equals(
                        record.TorrentHash,
                        torrentHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        foreach (var record in ActiveByPlaySessionId.Values)
        {
            if (string.Equals(
                    record.TorrentHash,
                    torrentHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static ActiveMoviePlayback? FindPlaybackByTorrentHash(
        string torrentHash)
    {
        foreach (var record in ActiveByPlaySessionId.Values)
        {
            if (string.Equals(
                    record.TorrentHash,
                    torrentHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                return record;
            }
        }

        foreach (var prepared in PreparedByItemId.Values)
        {
            foreach (var record in prepared.Values)
            {
                if (string.Equals(
                        record.TorrentHash,
                        torrentHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return record;
                }
            }
        }

        foreach (var pending in PendingByLibraryPath.Values)
        {
            foreach (var record in pending.Values)
            {
                if (string.Equals(
                        record.TorrentHash,
                        torrentHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return record;
                }
            }
        }

        return null;
    }

    public static bool MarkRetainedCompletedCache(
        string torrentHash)
    {
        if (string.IsNullOrWhiteSpace(torrentHash))
        {
            return false;
        }

        var playback =
            FindPlaybackByTorrentHash(
                torrentHash);

        if (playback is null)
        {
            return false;
        }

        try
        {
            var fullPath =
                Path.GetFullPath(
                    playback.CachePath);

            if (!File.Exists(fullPath))
            {
                return false;
            }

            var fileSize =
                new FileInfo(fullPath).Length;

            if (fileSize <= 0)
            {
                return false;
            }

            RetainedCompletedByTorrentHash[
                torrentHash] =
                new RetainedCompletedCache(
                    playback.MovieId,
                    playback.MagnetLink,
                    torrentHash,
                    fullPath,
                    fileSize);

            Console.WriteLine(
                "SimpleMovieFeed: verified completed cache retained for torrent " +
                torrentHash +
                ".");

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryGetRetainedCompletedCache(
        string torrentHash,
        out RetainedCompletedCache? cache)
    {
        cache = null;

        if (string.IsNullOrWhiteSpace(torrentHash))
        {
            return false;
        }

        if (!RetainedCompletedByTorrentHash.TryGetValue(
                torrentHash,
                out var retained))
        {
            return false;
        }

        try
        {
            var fullPath =
                Path.GetFullPath(
                    retained.CachePath);

            if (!File.Exists(fullPath))
            {
                RetainedCompletedByTorrentHash.TryRemove(
                    torrentHash,
                    out _);

                return false;
            }

            var currentSize =
                new FileInfo(fullPath).Length;

            if (
                currentSize <= 0 ||
                currentSize != retained.FileSize)
            {
                RetainedCompletedByTorrentHash.TryRemove(
                    torrentHash,
                    out _);

                return false;
            }

            cache =
                retained with
                {
                    CachePath = fullPath
                };

            return true;
        }
        catch
        {
            RetainedCompletedByTorrentHash.TryRemove(
                torrentHash,
                out _);

            return false;
        }
    }

    public static void ClearRetainedCompletedCacheForPath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string fullPath;

        try
        {
            fullPath =
                Path.GetFullPath(path);
        }
        catch
        {
            return;
        }

        foreach (var entry in RetainedCompletedByTorrentHash)
        {
            string retainedPath;

            try
            {
                retainedPath =
                    Path.GetFullPath(
                        entry.Value.CachePath);
            }
            catch
            {
                continue;
            }

            if (!string.Equals(
                    retainedPath,
                    fullPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            RetainedCompletedByTorrentHash.TryRemove(
                entry.Key,
                out _);
        }
    }
    public static void AttachItem(
        Guid jellyfinItemId,
        string libraryPath)
    {
        var fullPath =
            Path.GetFullPath(
                libraryPath);

        if (!PendingByLibraryPath.TryRemove(
                fullPath,
                out var pending))
        {
            return;
        }

        var prepared =
            PreparedByItemId.GetOrAdd(
                jellyfinItemId,
                _ => new());

        foreach (var entry in pending)
        {
            var record = entry.Value;

            prepared[
                entry.Key] =
                record;

            RememberMovie(
                jellyfinItemId,
                new StoredMovie(
                    record.MovieId,
                    record.MovieTitle,
                    record.Year,
                    record.MagnetLink,
                    record.Quality));

            Console.WriteLine(
                "SimpleMovieFeed: prepared Jellyfin item " +
                jellyfinItemId +
                " for user " +
                record.UserId +
                ", movie " +
                record.MovieId);
        }
    }
    public static void RememberMovie(
        Guid jellyfinItemId,
        StoredMovie movie)
    {
        lock (CatalogLock)
        {
            _catalog[
                jellyfinItemId] =
                movie;

            SaveCatalogLocked();
        }
    }

    public static bool TryGetStoredMovie(
        Guid jellyfinItemId,
        out StoredMovie? movie)
    {
        lock (CatalogLock)
        {
            if (_catalog.TryGetValue(
                    jellyfinItemId,
                    out var found))
            {
                movie = found;
                return true;
            }
        }

        movie = null;
        return false;
    }

    public static bool TryGetStoredMovieByMovieId(
        int movieId,
        out StoredMovie? movie)
    {
        lock (CatalogLock)
        {
            foreach (var entry in _catalog.Values)
            {
                if (entry.MovieId == movieId)
                {
                    movie = entry;
                    return true;
                }
            }
        }

        movie = null;
        return false;
    }
    public static bool TryActivatePrepared(
        Guid jellyfinItemId,
        Guid userId,
        string playSessionId,
        out ActiveMoviePlayback? record)
    {
        record = null;

        if (
            userId == Guid.Empty ||
            string.IsNullOrWhiteSpace(
                playSessionId))
        {
            return false;
        }
        if (ActiveByPlaySessionId.TryGetValue(
                playSessionId,
                out var alreadyActive))
        {
            /*
             * A repeated /stream/start may have recreated state for
             * this same logical startup. Remove only this StartupId;
             * other clients playing the same user/movie are preserved.
             */
            if (PreparedByItemId.TryGetValue(
                    jellyfinItemId,
                    out var duplicatePrepared))
            {
                duplicatePrepared.TryRemove(
                    alreadyActive.StartupId,
                    out _);
            }

            RemovePendingStartup(
                alreadyActive.StartupId,
                alreadyActive.UserId,
                alreadyActive.MovieId,
                alreadyActive.TorrentHash);

            record = alreadyActive;
            return true;
        }

        if (!PreparedByItemId.TryGetValue(
                jellyfinItemId,
                out var prepared))
        {
            return false;
        }

        foreach (var entry in prepared)
        {
            if (entry.Value.UserId != userId)
            {
                continue;
            }

            if (!prepared.TryRemove(
                    entry.Key,
                    out var activated))
            {
                continue;
            }

            ActiveByPlaySessionId[
                playSessionId] =
                activated;

            /*
             * Registration can race activation. Remove an exact
             * pending duplicate belonging to the StartupId that was
             * just activated.
             */
            RemovePendingStartup(
                activated.StartupId,
                activated.UserId,
                activated.MovieId,
                activated.TorrentHash);

            record = activated;
            return true;
        }

        return false;
    }

    public static bool TryGetActive(
        string? playSessionId,
        out ActiveMoviePlayback? record)
    {
        if (
            !string.IsNullOrWhiteSpace(
                playSessionId) &&
            ActiveByPlaySessionId.TryGetValue(
                playSessionId,
                out var found))
        {
            record = found;
            return true;
        }

        record = null;
        return false;
    }

    public static bool TryTakeActive(
        string? playSessionId,
        out ActiveMoviePlayback? record)
    {
        if (
            !string.IsNullOrWhiteSpace(
                playSessionId) &&
            ActiveByPlaySessionId.TryRemove(
                playSessionId,
                out var found))
        {
            record = found;
            return true;
        }

        record = null;
        return false;
    }

    public static void RestoreActive(
        string playSessionId,
        ActiveMoviePlayback record)
    {
        if (string.IsNullOrWhiteSpace(
                playSessionId))
        {
            return;
        }

        ActiveByPlaySessionId[
            playSessionId] =
            record;
    }
    public static long GetResumePosition(
        Guid userId,
        int movieId)
    {
        var key =
            GetResumeKey(
                userId,
                movieId);

        lock (ResumeLock)
        {
            return _resume.TryGetValue(
                    key,
                    out var ticks)
                ? ticks
                : 0;
        }
    }

    public static void SaveResumePosition(
        Guid userId,
        int movieId,
        long playbackPositionTicks,
        bool playedToCompletion)
    {
        var key =
            GetResumeKey(
                userId,
                movieId);

        lock (ResumeLock)
        {
            var existingTicks =
                _resume.TryGetValue(
                    key,
                    out var existing)
                ? existing
                : 0;

            if (playedToCompletion)
            {
                _resume.Remove(
                    key);

                Console.WriteLine(
                    "SimpleMovieFeed: resume cleared for completed movie " +
                    movieId);
            }
            else if (
                playbackPositionTicks >
                existingTicks)
            {
                _resume[
                    key] =
                    playbackPositionTicks;

                Console.WriteLine(
                    "SimpleMovieFeed: resume advanced for movie " +
                    movieId +
                    " from " +
                    existingTicks +
                    " to " +
                    playbackPositionTicks);
            }
            else if (
                playbackPositionTicks > 0 &&
                playbackPositionTicks <
                existingTicks)
            {
                Console.WriteLine(
                    "SimpleMovieFeed: resume preserved for movie " +
                    movieId +
                    " at " +
                    existingTicks +
                    "; ignored earlier position " +
                    playbackPositionTicks);
            }

            SaveResumeLocked();
        }
    }

    private static void SaveResumeLocked()
    {
        if (string.IsNullOrWhiteSpace(
                _resumeFile))
        {
            return;
        }

        var json =
            JsonSerializer.Serialize(
                _resume,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        File.WriteAllText(
            _resumeFile,
            json);
    }

    private static void SaveCatalogLocked()
    {
        if (string.IsNullOrWhiteSpace(
                _catalogFile))
        {
            return;
        }

        var json =
            JsonSerializer.Serialize(
                _catalog,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        File.WriteAllText(
            _catalogFile,
            json);
    }
}

