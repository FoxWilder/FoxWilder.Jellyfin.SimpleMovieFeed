using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.SimpleMovieFeed;

[ApiController]
[Route("SimpleMovieFeed")]
public sealed class ApiController : ControllerBase
{
    private readonly YtsApiService _ytsService;
    private readonly QBitTorrentService _aria2Service;
    private readonly WatchHistoryService _historyService;
    private readonly JellyfinMovieLibraryService _libraryService;

    public ApiController(
        YtsApiService ytsService,
        QBitTorrentService aria2Service,
        WatchHistoryService historyService,
        JellyfinMovieLibraryService libraryService)
    {
        _ytsService = ytsService;
        _aria2Service = aria2Service;
        _historyService = historyService;
        _libraryService = libraryService;
    }

    [HttpGet("whatsnew")]
    [AllowAnonymous]
    public async Task<ActionResult<List<DashboardMovie>>> WhatsNew(
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        var movies =
            await _ytsService.GetLatestMoviesAsync(page, ct);

        return Ok(movies);
    }

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<ActionResult<List<DashboardMovie>>> Search(
        [FromQuery] string query,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(
                new { error = "Query parameter is required" });
        }

        var movies =
            await _ytsService.SearchMoviesAsync(
                query,
                page,
                ct);

        return Ok(movies);
    }

    [HttpGet("suggestions/{movieId:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<List<DashboardMovie>>> Suggestions(
        int movieId,
        CancellationToken ct = default)
    {
        if (movieId <= 0)
        {
            return BadRequest(
                new
                {
                    error = "A valid movie ID is required."
                });
        }

        var movies =
            await _ytsService.GetMovieSuggestionsAsync(
                movieId,
                ct);

        return Ok(movies);
    }
    [HttpPost("details")]
    [Authorize]
    public async Task<ActionResult<object>> PrepareMovieDetails(
        [FromBody] StreamRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (request.MovieId <= 0)
            {
                return BadRequest(
                    new
                    {
                        error = "A valid movie ID is required."
                    });
            }

            if (string.IsNullOrWhiteSpace(
                    request.MovieTitle))
            {
                return BadRequest(
                    new
                    {
                        error = "Movie title is required."
                    });
            }

            if (string.IsNullOrWhiteSpace(
                    request.MagnetLink))
            {
                return BadRequest(
                    new
                    {
                        error = "Magnet link is required."
                    });
            }

            /*
             * IMPORTANT:
             * This endpoint intentionally does NOT call qBittorrent.
             *
             * It only creates the tiny persistent Jellyfin .strm
             * placeholder and remembers enough metadata to start
             * the torrent later when Play is pressed.
             */

            var persistentStreamUrl =
                Request.Scheme +
                "://" +
                Request.Host.Value +
                Request.PathBase +
                "/SimpleMovieFeed/stream/movie/" +
                request.MovieId;

            var libraryPath =
                _libraryService.CreateOrUpdateMovie(
                    new DashboardMovie
                    {
                        Id = request.MovieId,
                        Title = request.MovieTitle,
                        Year = request.Year,
                    PosterUrl = request.PosterUrl,
                        Description = request.Description
                    },
                    persistentStreamUrl);

            var jellyfinItemId =
                await _libraryService.FindItemAsync(
                    libraryPath,
                    ct);

            if (!jellyfinItemId.HasValue)
            {
                /*
                 * Brand-new .strm file.
                 *
                 * Do not block this HTTP request waiting for Jellyfin's
                 * library monitor. The web client will retry this lightweight
                 * endpoint until Jellyfin has indexed the placeholder.
                 */
                _libraryService.QueueLibraryScanIfNeeded();

                return StatusCode(
                    StatusCodes.Status202Accepted,
                    new
                    {
                        status = "library_scan_pending",
                        movieId = request.MovieId,
                        movieTitle = request.MovieTitle,
                        libraryPath
                    });
            }

            PlaybackStateStore.RememberMovie(
                jellyfinItemId.Value,
                new StoredMovie(
                    request.MovieId,
                    request.MovieTitle,
                    request.Year,
                    request.MagnetLink,
                    request.Quality));

            Console.WriteLine(
                "SimpleMovieFeed: details prepared without torrent for movie " +
                request.MovieId +
                ", Jellyfin item " +
                jellyfinItemId.Value);

            return Ok(
                new
                {
                    movieId = request.MovieId,
                    movieTitle = request.MovieTitle,
                    year = request.Year,
                    quality = request.Quality,
                    jellyfinItemId =
                        jellyfinItemId.Value,
                    libraryPath
                });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "SimpleMovieFeed: details preparation failed: " +
                ex);

            return BadRequest(
                new
                {
                    error = ex.Message
                });
        }
    }
    [HttpPost("stream/start")]
    [Authorize]
    public async Task<ActionResult<object>> StartTorrentStream(
        [FromBody] StreamRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var jellyfinUserId =
                request.UserId;

            if (jellyfinUserId == Guid.Empty)
            {
                return Unauthorized(
                    new
                    {
                        error =
                            "Jellyfin user ID was not supplied."
                    });
            }

            if (request.StartupId == Guid.Empty)
            {
                return BadRequest(
                    new
                    {
                        error =
                            "Startup ID was not supplied."
                    });
            }

            var metadataGid =
                await _aria2Service.AddMagnetAsync(
                    request.MagnetLink,
                    ct);

            var gid =
                await _aria2Service.ResolveTorrentGidAsync(
                    metadataGid,
                    ct);

            string? videoPath = null;

            for (var attempt = 0; attempt < 1; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                var files =
                    await _aria2Service.GetFilesAsync(
                        gid,
                        ct);

                foreach (var file in files.EnumerateArray())
                {
                    if (!file.TryGetProperty("name", out var nameProperty))
                    {
                        Console.WriteLine(
                            "SimpleMovieFeed: qBittorrent file entry has no 'name' property: " +
                            file.GetRawText());

                        continue;
                    }

                    var relativePath =
                        nameProperty.GetString();

                    if (string.IsNullOrWhiteSpace(relativePath))
                    {
                        Console.WriteLine(
                            "SimpleMovieFeed: qBittorrent returned an empty file name.");

                        continue;
                    }

                    var path =
                        Path.Combine(
                            RuntimeSettings.CacheDirectory,
                            relativePath);

                    Console.WriteLine(
                        "SimpleMovieFeed: qBittorrent file: " +
                        relativePath);

                    var extension =
                        Path.GetExtension(path)
                            .ToLowerInvariant();

                    if (extension is
                        ".mkv" or
                        ".mp4" or
                        ".m4v" or
                        ".webm" or
                        ".avi" or
                        ".mov" or
                        ".ts" or
                        ".mpeg" or
                        ".mpg")
                    {
                        videoPath = path;
                        break;
                    }
                }

                if (videoPath != null)
                {
                    break;
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(1),
                    ct);
            }

            if (videoPath == null)
            {
                return StatusCode(
                    StatusCodes.Status202Accepted,
                    new
                    {
                        gid,
                        status = "starting"
                    });
            }
        if (!System.IO.File.Exists(videoPath))
        {
            var fileName =
                Path.GetFileName(videoPath);

            var actualPath =
                Directory
                    .EnumerateFiles(
                        RuntimeSettings.CacheDirectory,
                        fileName,
                        SearchOption.AllDirectories)
                    .OrderByDescending(
                        System.IO.File.GetLastWriteTimeUtc)
                    .FirstOrDefault();

            if (actualPath != null)
            {
                Console.WriteLine(
                    "SimpleMovieFeed: using actual cached file: " +
                    actualPath);

                videoPath = actualPath;
            }
            else
            {
                return StatusCode(
                    StatusCodes.Status202Accepted,
                    new
                    {
                        gid,
                        status = "downloading",
                        expectedPath = videoPath
                    });
            }
        }

        var safePath =
            _aria2Service.ResolveSafePath(
                videoPath);

            var streamUrl =
            Request.Scheme +
            "://" +
            Request.Host.Value +
            Request.PathBase +
            "/SimpleMovieFeed/stream/file?path=" +
            Uri.EscapeDataString(safePath);

            var persistentStreamUrl =
                Request.Scheme +
                "://" +
                Request.Host.Value +
                Request.PathBase +
                "/SimpleMovieFeed/stream/movie/" +
                request.MovieId;

            var libraryPath =
                _libraryService.CreateOrUpdateMovie(
                new DashboardMovie
                {
                    Id = request.MovieId,
                    Title = request.MovieTitle,
                    Year = request.Year,
                    PosterUrl = request.PosterUrl,
                        Description = request.Description},
                persistentStreamUrl);

            PlaybackStateStore.RegisterPending(
                request.StartupId,
                jellyfinUserId,
                request.MovieId,
                request.MovieTitle,
                request.Year,
                request.MagnetLink,
                request.Quality,
                gid,
                safePath,
                libraryPath);

        var jellyfinItemId =
            await _libraryService.FindItemAsync(
                libraryPath,
                ct);

        if (jellyfinItemId.HasValue)
        {
            PlaybackStateStore.AttachItem(
                jellyfinItemId.Value,
                libraryPath);
        }

        var resumePositionTicks =
            PlaybackStateStore.GetResumePosition(
                jellyfinUserId,
                request.MovieId);
            return Ok(
                new
                {
                    gid,
                    movieId = request.MovieId,
                    movieTitle = request.MovieTitle,
                    filePath = safePath,
                    streamUrl,
                    libraryPath,
                    jellyfinItemId,
                resumePositionTicks
                });
        }
        catch (Exception ex)
        {
            return BadRequest(
                new
                {
                    error = ex.Message
                });
        }
    }

    [HttpGet("resume/{movieId:int}")]
    [Authorize]
    public IActionResult GetResumePosition(
        int movieId,
        [FromQuery] Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return BadRequest(
                new
                {
                    error = "UserId is required."
                });
        }

        var resumePositionTicks =
            PlaybackStateStore.GetResumePosition(
                userId,
                movieId);

        return Ok(
            new
            {
                movieId,
                resumePositionTicks
            });
    }
    [HttpGet("resume/item/{itemId:guid}")]
    [Authorize]
    public IActionResult GetItemResume(
        Guid itemId,
        [FromQuery] Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return BadRequest(
                new
                {
                    error =
                        "UserId is required."
                });
        }

        if (!PlaybackStateStore.TryGetStoredMovie(
                itemId,
                out var movie) ||
            movie is null)
        {
            return Ok(
                new
                {
                    found = false
                });
        }

        var resumePositionTicks =
            PlaybackStateStore.GetResumePosition(
                userId,
                movie.MovieId);

        return Ok(
            new
            {
                found = true,
                movieId = movie.MovieId,
                movieTitle = movie.MovieTitle,
                year = movie.Year,
                magnetLink = movie.MagnetLink,
                quality = movie.Quality,
                resumePositionTicks
            });
    }
    [HttpGet("stream/status/{hash}")]
    [Authorize]
    public async Task<ActionResult<object>> GetStreamStatus(
        string hash,
        [FromQuery] string? libraryPath,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            return BadRequest(
                new { error = "Torrent hash is required." });
        }

        try
        {
            var torrent =
                await _aria2Service.GetStatusAsync(
                    hash,
                    ct);

            if (torrent is null)
            {
                return Ok(
                    new
                    {
                        found = false,
                        status = "waiting",
                        hash
                    });
            }

            var value = torrent.Value;

            var progress =
                value.TryGetProperty("progress", out var p)
                    ? p.GetDouble()
                    : 0;

            var downloadSpeed =
                value.TryGetProperty("dlspeed", out var s)
                    ? s.GetInt64()
                    : 0;

            var eta =
                value.TryGetProperty("eta", out var e)
                    ? e.GetInt64()
                    : 0;

            var downloaded =
                value.TryGetProperty("downloaded", out var d)
                    ? d.GetInt64()
                    : 0;

            var totalSize =
                value.TryGetProperty("total_size", out var t)
                    ? t.GetInt64()
                    : 0;

            var state =
                value.TryGetProperty("state", out var st)
                    ? st.GetString() ?? "unknown"
                    : "unknown";

            var peers =
                value.TryGetProperty(
                    "num_leechs",
                    out var peerProperty)
                    ? peerProperty.GetInt32()
                    : 0;

            var seeds =
                value.TryGetProperty(
                    "num_seeds",
                    out var seedProperty)
                    ? seedProperty.GetInt32()
                    : 0;

            Guid? jellyfinItemId = null;

            if (!string.IsNullOrWhiteSpace(libraryPath))
            {
                jellyfinItemId =
                    await _libraryService.FindItemAsync(
                        libraryPath,
                        ct);

                if (jellyfinItemId.HasValue)
                {
                    PlaybackStateStore.AttachItem(
                        jellyfinItemId.Value,
                        libraryPath);
                }
            }

            return Ok(
                new
                {
                    found = true,
                    hash,
                    progress,
                    percent =
                        Math.Round(
                            progress * 100.0,
                            1),
                    downloadSpeed,
                    eta,
                    downloaded,
                    totalSize,
                    state,
                    peers,
                    seeds,
                    jellyfinItemId,
                    ready = jellyfinItemId.HasValue
                });
        }
        catch (Exception ex)
        {
            return BadRequest(
                new { error = ex.Message });
        }
    }
    [HttpPost("stream/cancel/{hash}")]
    [Authorize]
    public async Task<ActionResult<object>> CancelTorrentStream(
        string hash,
        [FromQuery] Guid userId,
        [FromQuery] int movieId,
        [FromQuery] Guid startupId,
        CancellationToken ct = default)
    {
        if (
            string.IsNullOrWhiteSpace(hash) ||
            userId == Guid.Empty ||
            movieId <= 0 ||
            startupId == Guid.Empty)
        {
            return BadRequest(
                new
                {
                    error =
                        "Torrent hash, user ID, movie ID, and startup ID are required."
                });
        }

        try
        {
            var pendingRemoved =
                PlaybackStateStore.RemovePendingStartup(
                    startupId,
                    userId,
                    movieId,
                    hash);

            var preparedRemoved =
                PlaybackStateStore.RemovePreparedStartup(
                    startupId,
                    userId,
                    movieId,
                    hash);

            if (PlaybackStateStore.IsTorrentInUse(hash))
            {
                Console.WriteLine(
                    "SimpleMovieFeed: cancelled startup retained torrent " +
                    hash +
                    " because it is still registered in use.");

                return Ok(
                    new
                    {
                        cancelled = true,
                        pendingRemoved,
                        preparedRemoved,
                        torrentRemoved = false,
                        reason = "in-use"
                    });
            }

            await _aria2Service.DeleteTorrentAsync(
                hash,
                true,
                ct);

            Console.WriteLine(
                "SimpleMovieFeed: cancelled startup removed torrent " +
                hash +
                ".");

            return Ok(
                new
                {
                    cancelled = true,
                    pendingRemoved,
                    preparedRemoved,
                    torrentRemoved = true
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "SimpleMovieFeed: cancelled startup cleanup failed for " +
                hash +
                ": " +
                ex);

            return BadRequest(
                new
                {
                    error = ex.Message
                });
        }
    }

    [HttpGet("stream/movie/{movieId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> StreamPersistentMovie(
        int movieId,
        CancellationToken ct = default)
    {
        try
        {
            if (!PlaybackStateStore.TryGetStoredMovieByMovieId(
                    movieId,
                    out var movie) ||
                movie is null)
            {
                return NotFound(
                    new
                    {
                        error =
                            "SimpleMovieFeed movie metadata was not found."
                    });
            }

            Console.WriteLine(
                "SimpleMovieFeed: persistent stream requested for movie " +
                movieId +
                " (" +
                movie.MovieTitle +
                ").");

            var hash =
                await _aria2Service.AddMagnetAsync(
                    movie.MagnetLink,
                    ct);

            hash =
                await _aria2Service.ResolveTorrentGidAsync(
                    hash,
                    ct);

            string? videoPath = null;

            var minimumStartupBytes =
                RuntimeSettings.StartupBufferBytes;

            for (
                var attempt = 0;
                attempt < 300;
                attempt++)
            {
                ct.ThrowIfCancellationRequested();

                var files =
                    await _aria2Service.GetFilesAsync(
                        hash,
                        ct);

                string? bestRelativePath = null;
                long bestSize = -1;

                foreach (
                    var file in
                    files.EnumerateArray())
                {
                    if (!file.TryGetProperty(
                            "name",
                            out var nameProperty))
                    {
                        continue;
                    }

                    var relativePath =
                        nameProperty.GetString();

                    if (string.IsNullOrWhiteSpace(
                            relativePath))
                    {
                        continue;
                    }

                    var extension =
                        Path.GetExtension(
                                relativePath)
                            .ToLowerInvariant();

                    var isVideo =
                        extension is
                            ".mkv" or
                            ".mp4" or
                            ".m4v" or
                            ".webm" or
                            ".avi" or
                            ".mov" or
                            ".ts" or
                            ".mpeg" or
                            ".mpg";

                    if (!isVideo)
                    {
                        continue;
                    }

                    var size =
                        file.TryGetProperty(
                            "size",
                            out var sizeProperty)
                            ? sizeProperty.GetInt64()
                            : 0;

                    if (size <= bestSize)
                    {
                        continue;
                    }

                    bestSize =
                        size;

                    bestRelativePath =
                        relativePath;
                }

                if (!string.IsNullOrWhiteSpace(
                        bestRelativePath))
                {
                    var candidate =
                        Path.Combine(
                            RuntimeSettings.CacheDirectory,
                            bestRelativePath);

                    if (System.IO.File.Exists(
                            candidate))
                    {
                        videoPath =
                            candidate;
                    }

                    if (videoPath is null)
                    {
                        var fileName =
                            Path.GetFileName(
                                candidate);

                        videoPath =
                            Directory
                                .EnumerateFiles(
                                    RuntimeSettings.CacheDirectory,
                                    fileName,
                                    SearchOption.AllDirectories)
                                .OrderByDescending(
                                    System.IO.File.GetLastWriteTimeUtc)
                                .FirstOrDefault();
                    }
                }

                var torrent =
                    await _aria2Service.GetStatusAsync(
                        hash,
                        ct);

                long downloaded = 0;

                if (
                    torrent.HasValue &&
                    torrent.Value.TryGetProperty(
                        "downloaded",
                        out var downloadedProperty))
                {
                    downloaded =
                        downloadedProperty.GetInt64();
                }

                if (
                    !string.IsNullOrWhiteSpace(
                        videoPath) &&
                    System.IO.File.Exists(
                        videoPath) &&
                    downloaded >=
                        minimumStartupBytes)
                {
                    break;
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(1),
                    ct);
            }

            if (
                string.IsNullOrWhiteSpace(
                    videoPath) ||
                !System.IO.File.Exists(
                    videoPath))
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error =
                            "Movie did not become ready for playback."
                    });
            }

            var safePath =
                _aria2Service.ResolveSafePath(
                    videoPath);

            var persistentMediaExtension =
                Path.GetExtension(
                        safePath)
                    .ToLowerInvariant();

            var contentType =
                persistentMediaExtension switch
                {
                    ".mp4" => "video/mp4",
                    ".m4v" => "video/x-m4v",
                    ".mkv" => "video/x-matroska",
                    ".webm" => "video/webm",
                    ".avi" => "video/x-msvideo",
                    ".mov" => "video/quicktime",
                    ".ts" => "video/mp2t",
                    ".mpeg" => "video/mpeg",
                    ".mpg" => "video/mpeg",
                    _ => "application/octet-stream"
                };

            Console.WriteLine(
                "SimpleMovieFeed: persistent stream ready: " +
                safePath);

            var stream =
                new FileStream(
                    safePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    bufferSize:
                        1024 * 64,
                    options:
                        FileOptions.Asynchronous |
                        FileOptions.SequentialScan);

            Response.Headers[
                "Accept-Ranges"] =
                "bytes";

            return File(
                stream,
                contentType,
                enableRangeProcessing:
                    true);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "SimpleMovieFeed: persistent stream failed: " +
                ex);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    error =
                        ex.Message
                });
        }
    }
    [HttpGet("stream/file")]
    [AllowAnonymous]
    public IActionResult StreamFile(
        [FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest(
                new { error = "Media path is required." });
        }

        try
        {
            var safePath =
                _aria2Service.ResolveSafePath(path);

            if (!System.IO.File.Exists(safePath))
            {
                return NotFound(
                    new
                    {
                        error =
                            "Media file is not available yet."
                    });
            }

            var extension =
                Path.GetExtension(
                        safePath)
                    .ToLowerInvariant();

            var contentType =
                extension switch
                {
                    ".mp4" => "video/mp4",
                    ".m4v" => "video/x-m4v",
                    ".mkv" => "video/x-matroska",
                    ".webm" => "video/webm",
                    ".avi" => "video/x-msvideo",
                    ".mov" => "video/quicktime",
                    ".ts" => "video/mp2t",
                    ".mpeg" => "video/mpeg",
                    ".mpg" => "video/mpeg",
                    _ => "application/octet-stream"
                };

            var stream =
                new FileStream(
                    safePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    bufferSize: 1024 * 64,
                    options:
                        FileOptions.Asynchronous |
                        FileOptions.SequentialScan);

            Response.Headers["Accept-Ranges"] = "bytes";

            return File(
                stream,
                contentType,
                enableRangeProcessing: true);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (DirectoryNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("runtime")]
    [Authorize]
    public ActionResult<object> GetRuntimeSettings()
    {
        return Ok(
            new
            {
                startupBufferMiB =
                    RuntimeSettings.StartupBufferMiB
            });
    }
    [HttpGet("configuration/status")]
    [Authorize(Policy = "RequiresElevation")]
    public ActionResult<object> GetConfigurationStatus()
    {
        var configuration = RuntimeSettings.Current;

        return Ok(
            new
            {
                credentialConfigured =
                    QBitTorrentService.IsCredentialConfigured,
                credentialStorageSupported =
                    OperatingSystem.IsWindows(),
                cacheDirectory =
                    RuntimeSettings.CacheDirectory,
                libraryDirectory =
                    RuntimeSettings.LibraryDirectory,
                qBitTorrentApiUrl =
                    RuntimeSettings.QBitTorrentApiUri.ToString(),
                startupBufferMiB =
                    RuntimeSettings.StartupBufferMiB,
                cleanupGraceSeconds =
                    (int)RuntimeSettings.CleanupGrace.TotalSeconds,
                qBitTorrentTimeoutSeconds =
                    (int)RuntimeSettings.QBitTorrentTimeout.TotalSeconds,
                rssFeedUrl =
                    RuntimeSettings.RssFeedUrl,
                movieSearchApiUrl =
                    RuntimeSettings.MovieSearchApiUrl,
                restartRequiredSettings = new[]
                {
                    nameof(configuration.CacheDirectory),
                    nameof(configuration.LibraryDirectory),
                    nameof(configuration.QBitTorrentTimeoutSeconds)
                }
            });
    }

    [HttpPost("configuration")]
    [Authorize(Policy = "RequiresElevation")]
    public IActionResult UpdateConfiguration(
        [FromBody] PluginConfiguration request)
    {
        if (request is null)
        {
            return BadRequest(
                new { error = "Configuration is required." });
        }




        if (!TryValidateDirectory(
                request.CacheDirectory,
                out var cacheDirectory))
        {
            return BadRequest(
                new { error = "Cache directory must be a valid absolute path." });
        }

        if (!TryValidateDirectory(
                request.LibraryDirectory,
                out var libraryDirectory))
        {
            return BadRequest(
                new { error = "Movie library directory must be a valid absolute path." });
        }

        if (request.StartupBufferMiB < 1 ||
            request.StartupBufferMiB > 4096)
        {
            return BadRequest(
                new { error = "Startup buffer must be between 1 and 4096 MiB." });
        }

        if (request.CleanupGraceSeconds < 0 ||
            request.CleanupGraceSeconds > 3600)
        {
            return BadRequest(
                new { error = "Cleanup grace period must be between 0 and 3600 seconds." });
        }

        if (request.QBitTorrentTimeoutSeconds < 5 ||
            request.QBitTorrentTimeoutSeconds > 300)
        {
            return BadRequest(
                new { error = "qBittorrent timeout must be between 5 and 300 seconds." });
        }

        request.RssFeedUrl =
            PluginConfiguration.RssFeedUrlDefault;
        request.MovieSearchApiUrl =
            PluginConfiguration.MovieSearchApiUrlDefault;
        request.CacheDirectory = cacheDirectory;
        request.LibraryDirectory = libraryDirectory;
        request.QBitTorrentApiUrl =
            PluginConfiguration.QBitTorrentApiUrlDefault;

        Plugin.Instance.UpdateConfiguration(request);

        return Ok(
            new
            {
                saved = true,
                restartRequiredSettings = new[]
                {
                    nameof(request.CacheDirectory),
                    nameof(request.LibraryDirectory),
                    nameof(request.QBitTorrentTimeoutSeconds)
                }
            });
    }

    private static bool TryValidateHttpUrl(
        string? value,
        out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var selected = value.Trim();

        if (!Uri.TryCreate(
                selected,
                UriKind.Absolute,
                out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        normalized = selected;
        return true;
    }

    private static bool TryValidateDirectory(
        string? value,
        out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var selected = value.Trim();

            if (!Path.IsPathFullyQualified(selected))
            {
                return false;
            }

            normalized = Path.GetFullPath(selected);
            return true;
        }
        catch (Exception ex)
            when (ex is ArgumentException ||
                  ex is NotSupportedException ||
                  ex is PathTooLongException)
        {
            return false;
        }
    }
    [HttpPost("configuration/credential")]
    [Authorize(Policy = "RequiresElevation")]
    public IActionResult UpdateQBitTorrentCredential(
        [FromBody] QBitTorrentCredentialRequest request)
    {
        if (!OperatingSystem.IsWindows())
        {
            return BadRequest(
                new
                {
                    error =
                        "Protected credential storage is currently supported only on Windows."
                });
        }

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return BadRequest(
                new
                {
                    error =
                        "qBittorrent API key must not be empty."
                });
        }

        try
        {
            _aria2Service.UpdateApiKey(request.ApiKey);

            return Ok(
                new
                {
                    credentialConfigured = true
                });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(
                new
                {
                    error = ex.Message
                });
        }
        catch (PlatformNotSupportedException ex)
        {
            return BadRequest(
                new
                {
                    error = ex.Message
                });
        }
    }
    [HttpPost("watchhistory")]
    [Authorize]
    public async Task<IActionResult> UpdateWatchHistory(
        [FromBody] UserWatchHistory history,
        CancellationToken ct = default)
    {
        history.UserId =
            User.FindFirst("sub")?.Value ?? "unknown";

        await _historyService.SaveWatchHistoryAsync(history);

        return Ok();
    }

    [HttpGet("watchhistory/{movieId}")]
    [Authorize]
    public async Task<ActionResult<UserWatchHistory>> GetWatchHistory(
        int movieId,
        CancellationToken ct = default)
    {
        var userId =
            User.FindFirst("sub")?.Value ?? "unknown";

        var history =
            await _historyService.GetWatchHistoryAsync(
                userId,
                movieId);

        return Ok(history);
    }
}

public sealed class QBitTorrentCredentialRequest
{
    public string ApiKey { get; set; } = string.Empty;
}

public class StreamRequest
{
    public Guid UserId { get; set; }

    public Guid StartupId { get; set; }

    public int MovieId { get; set; }

    public string MagnetLink { get; set; } = "";

    public string MovieTitle { get; set; } = "";

    public int Year { get; set; }

    public string Quality { get; set; } = "";

    public string PosterUrl { get; set; } = "";

    public string Description { get; set; } = "";
}







































