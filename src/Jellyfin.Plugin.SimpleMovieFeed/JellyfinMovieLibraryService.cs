using System.Text;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.SimpleMovieFeed;

public sealed class JellyfinMovieLibraryService
{
    private readonly string _libraryDirectory;
    private readonly ILibraryManager _libraryManager;

    private static readonly HttpClient PosterHttpClient =
        new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

    public JellyfinMovieLibraryService(
        string libraryDirectory,
        ILibraryManager libraryManager)
    {
        _libraryDirectory =
            Path.GetFullPath(
                libraryDirectory);

        _libraryManager =
            libraryManager;

        Directory.CreateDirectory(
            _libraryDirectory);
    }

    public string CreateOrUpdateMovie(
        DashboardMovie movie,
        string streamUrl)
    {
        if (string.IsNullOrWhiteSpace(
                streamUrl))
        {
            throw new ArgumentException(
                "Stream URL is required.",
                nameof(streamUrl));
        }

        var safeName =
            MakeSafeFileName(
                movie.Title +
                " (" +
                movie.Year +
                ")");

        var movieDirectory =
            Path.Combine(
                _libraryDirectory,
                safeName);

        Directory.CreateDirectory(
            movieDirectory);

        var strmPath =
            Path.Combine(
                movieDirectory,
                safeName + ".strm");

        /*
         * Delete old hardlink/media files from the previous
         * implementation, but retain the persistent .strm.
         */
        foreach (var file in
            Directory.EnumerateFiles(
                movieDirectory,
                "*",
                SearchOption.TopDirectoryOnly))
        {
            if (string.Equals(
                    Path.GetFullPath(file),
                    Path.GetFullPath(strmPath),
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
                System.IO.File.Delete(
                    file);
            }
            catch
            {
            }
        }

        System.IO.File.WriteAllText(
            strmPath,
            streamUrl + Environment.NewLine,
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false));

        /*
         * Jellyfin recognises poster.jpg beside the .strm as
         * local primary artwork. Keep it persistent so the same
         * artwork is used by native details and Continue Watching.
         */
        if (!string.IsNullOrWhiteSpace(movie.PosterUrl))
        {
            try
            {
                var posterPath =
                    Path.Combine(
                        movieDirectory,
                        "poster.jpg");

                using var response =
                    PosterHttpClient
                        .GetAsync(movie.PosterUrl)
                        .GetAwaiter()
                        .GetResult();

                response.EnsureSuccessStatusCode();

                var posterBytes =
                    response.Content
                        .ReadAsByteArrayAsync()
                        .GetAwaiter()
                        .GetResult();

                if (posterBytes.Length > 0)
                {
                    System.IO.File.WriteAllBytes(
                        posterPath,
                        posterBytes);

                    Console.WriteLine(
                        "SimpleMovieFeed: saved local poster for " +
                        movie.Title);
                }
            }
            catch (Exception ex)
            {
                /*
                 * Artwork failure must never prevent details,
                 * playback or resume from working.
                 */
                Console.WriteLine(
                    "SimpleMovieFeed: unable to save poster for " +
                    movie.Title +
                    ": " +
                    ex.Message);
            }
        }

        /*
         * Persist lightweight local metadata so Jellyfin can show
         * the YTS overview without retaining the actual movie.
         */
        try
        {
            var nfoPath =
                Path.Combine(
                    movieDirectory,
                    "movie.nfo");

            var escapedTitle =
                System.Security.SecurityElement.Escape(
                    movie.Title) ?? "";

            var escapedDescription =
                System.Security.SecurityElement.Escape(
                    movie.Description) ?? "";

            var nfo =
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                Environment.NewLine +
                "<movie>" +
                Environment.NewLine +
                "  <title>" +
                escapedTitle +
                "</title>" +
                Environment.NewLine +
                "  <year>" +
                movie.Year +
                "</year>" +
                Environment.NewLine +
                "  <plot>" +
                escapedDescription +
                "</plot>" +
                Environment.NewLine +
                "  <outline>" +
                escapedDescription +
                "</outline>" +
                Environment.NewLine +
                "</movie>" +
                Environment.NewLine;

            System.IO.File.WriteAllText(
                nfoPath,
                nfo,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            Console.WriteLine(
                "SimpleMovieFeed: saved local NFO for " +
                movie.Title);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "SimpleMovieFeed: unable to save local NFO for " +
                movie.Title +
                ": " +
                ex.Message);
        }

        return strmPath;
    }

    public Task<Guid?> FindItemAsync(
        string libraryPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath =
            Path.GetFullPath(
                libraryPath);

        var item =
            _libraryManager.FindByPath(
                fullPath,
                false);

        return Task.FromResult<Guid?>(
            item?.Id);
    }
    public void QueueLibraryScanIfNeeded()
    {
        if (!_libraryManager.IsScanRunning)
        {
            _libraryManager.QueueLibraryScan();
        }
    }

    public async Task<Guid?> WaitForItemAsync(
        string libraryPath,
        CancellationToken cancellationToken = default)
    {
        var fullPath =
            Path.GetFullPath(
                libraryPath);

        for (var attempt = 0;
             attempt < 30;
             attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item =
                _libraryManager.FindByPath(
                    fullPath,
                    false);

            if (item != null)
            {
                return item.Id;
            }

            await Task.Delay(
                TimeSpan.FromSeconds(1),
                cancellationToken);
        }

        return null;
    }

    private static string MakeSafeFileName(
        string value)
    {
        var invalid =
            Path.GetInvalidFileNameChars();

        var builder =
            new StringBuilder(
                value.Length);

        foreach (var character in value)
        {
            if (invalid.Contains(
                    character))
            {
                builder.Append('_');
            }
            else
            {
                builder.Append(
                    character);
            }
        }

        return builder
            .ToString()
            .Trim()
            .TrimEnd('.');
    }
}




