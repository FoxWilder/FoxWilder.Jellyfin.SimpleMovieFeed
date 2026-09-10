using System.Collections.Concurrent;
using System.Text.Json;

namespace Jellyfin.Plugin.SimpleMovieFeed;

public sealed record ActiveMoviePlayback(
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

public static class PlaybackStateStore
{
    private static readonly object ResumeLock = new();
    private static readonly object CatalogLock = new();

    private static readonly ConcurrentDictionary<
        Guid,
        ActiveMoviePlayback> ActiveByItemId =
            new();

    private static readonly ConcurrentDictionary<
        string,
        ActiveMoviePlayback> PendingByLibraryPath =
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
        var record =
            new ActiveMoviePlayback(
                userId,
                movieId,
                movieTitle,
                year,
                magnetLink,
                quality,
                torrentHash,
                cachePath,
                libraryPath);

        PendingByLibraryPath[
            Path.GetFullPath(
                libraryPath)] =
            record;
    }

    public static bool RemovePendingStartup(
        Guid userId,
        int movieId,
        string torrentHash)
    {
        foreach (var entry in PendingByLibraryPath)
        {
            var record = entry.Value;

            if (
                record.UserId != userId ||
                record.MovieId != movieId ||
                !string.Equals(
                    record.TorrentHash,
                    torrentHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (PendingByLibraryPath.TryRemove(
                    entry.Key,
                    out _))
            {
                return true;
            }
        }

        return false;
    }

    public static bool RemovePreparedStartup(
        Guid userId,
        int movieId,
        string torrentHash)
    {
        foreach (var entry in ActiveByItemId)
        {
            var record = entry.Value;

            if (
                record.UserId != userId ||
                record.MovieId != movieId ||
                !string.Equals(
                    record.TorrentHash,
                    torrentHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (ActiveByItemId.TryRemove(
                    entry.Key,
                    out _))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsTorrentInUse(
        string torrentHash)
    {
        foreach (var record in PendingByLibraryPath.Values)
        {
            if (string.Equals(
                    record.TorrentHash,
                    torrentHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var record in ActiveByItemId.Values)
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

    public static void AttachItem(
        Guid jellyfinItemId,
        string libraryPath)
    {
        var fullPath =
            Path.GetFullPath(
                libraryPath);

        if (!PendingByLibraryPath.TryGetValue(
                fullPath,
                out var record))
        {
            return;
        }

        ActiveByItemId[
            jellyfinItemId] =
            record;

        PendingByLibraryPath.TryRemove(
            fullPath,
            out _);

        RememberMovie(
            jellyfinItemId,
            new StoredMovie(
                record.MovieId,
                record.MovieTitle,
                record.Year,
                record.MagnetLink,
                record.Quality));

        Console.WriteLine(
            "SimpleMovieFeed: attached Jellyfin item " +
            jellyfinItemId +
            " to movie " +
            record.MovieId);
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
    public static bool TryGetActive(
        Guid jellyfinItemId,
        out ActiveMoviePlayback? record)
    {
        if (ActiveByItemId.TryGetValue(
                jellyfinItemId,
                out var found))
        {
            record = found;
            return true;
        }

        record = null;
        return false;
    }

    public static bool TryTakeActive(
        Guid jellyfinItemId,
        out ActiveMoviePlayback? record)
    {
        if (ActiveByItemId.TryRemove(
                jellyfinItemId,
                out var found))
        {
            record = found;
            return true;
        }

        record = null;
        return false;
    }

    public static void RestoreActive(
        Guid jellyfinItemId,
        ActiveMoviePlayback record)
    {
        ActiveByItemId[
            jellyfinItemId] =
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

