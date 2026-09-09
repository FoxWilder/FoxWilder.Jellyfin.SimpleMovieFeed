using System.Collections.Concurrent;
using System.Net;

namespace Jellyfin.Plugin.SimpleMovieFeed;

public sealed class DownloadManager
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _jobs = new();
    public IReadOnlyDictionary<string, CancellationTokenSource> Jobs => _jobs;

    public async Task<string> StartAsync(FeedMovie movie, string cacheDir, CancellationToken ct)
    {
        Directory.CreateDirectory(cacheDir);
        var safe = string.Concat(movie.Title.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        if (safe.Length > 160) safe = safe[..160];
        var path = Path.Combine(cacheDir, safe + ".download");
        var final = Path.Combine(cacheDir, safe + ".mp4");
        var linked = _jobs.GetOrAdd(movie.Id, _ => CancellationTokenSource.CreateLinkedTokenSource(ct));
        try
        {
            using var http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });
            http.Timeout = Timeout.InfiniteTimeSpan;
            using var response = await http.GetAsync(movie.MediaUrl, HttpCompletionOption.ResponseHeadersRead, linked.Token);
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(linked.Token);
            await using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 1024 * 1024, useAsync: true);
            await input.CopyToAsync(output, 1024 * 1024, linked.Token);
            await output.FlushAsync(linked.Token);
            if (File.Exists(final)) File.Delete(final);
            File.Move(path, final);
            return final;
        }
        finally
        {
            _jobs.TryRemove(movie.Id, out _);
            linked.Dispose();
            if (File.Exists(path)) { try { File.Delete(path); } catch { } }
        }
    }

    public void Cancel(string id) { if (_jobs.TryGetValue(id, out var cts)) cts.Cancel(); }
    public void DeleteMovie(string cacheDir, string title)
    {
        var safe = string.Concat(title.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        foreach (var ext in new[] { ".mp4", ".mkv", ".webm", ".download" })
        {
            var p = Path.Combine(cacheDir, safe + ext);
            if (File.Exists(p)) File.Delete(p);
        }
    }
}
