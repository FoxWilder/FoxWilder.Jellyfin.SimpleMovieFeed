namespace Jellyfin.Plugin.SimpleMovieFeed;

public class TorrentStreamService
{
    private readonly string _cacheDir;

    public TorrentStreamService(string cacheDir)
    {
        _cacheDir = cacheDir;
    }

    public async Task<string> StartStreamAsync(
        string magnetLink,
        string movieTitle,
        CancellationToken ct = default)
    {
        var movieDir = Path.Combine(_cacheDir, SanitizeFileName(movieTitle));
        Directory.CreateDirectory(movieDir);

        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "webtorrent",
                    Arguments = string.Format(
                        "download \"{0}\" --output \"{1}\" --quiet",
                        magnetLink,
                        movieDir),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();

            await Task.Delay(2000, ct);
            return "http://localhost:8000";
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                string.Format(
                    "Failed to start torrent stream: {0}",
                    ex.Message),
                ex);
        }
    }

    public void StopStream(string movieTitle)
    {
        try
        {
            var processes =
                System.Diagnostics.Process.GetProcessesByName("webtorrent");

            foreach (var p in processes)
                p.Kill();
        }
        catch
        {
        }
    }

    public void ClearMovieCache(string movieTitle)
    {
        var movieDir =
            Path.Combine(_cacheDir, SanitizeFileName(movieTitle));

        try
        {
            if (Directory.Exists(movieDir))
                Directory.Delete(movieDir, true);
        }
        catch
        {
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(c, '_');

        return fileName;
    }
}
