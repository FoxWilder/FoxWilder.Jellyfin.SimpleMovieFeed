using System.ComponentModel;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Jellyfin.Plugin.SimpleMovieFeed;

public sealed class QBitTorrentService
{
    private const string BaseUrl = "http://127.0.0.1:8080/api/v2/";

    private const string PluginTag = "simplemoviefeed";

    private const string SecretPath =
        @"C:\ProgramData\Jellyfin\Server\secrets\qbt-api-key.bin";

    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes(
            "Jellyfin.SimpleMovieFeed.qBittorrent.v1");

    private readonly HttpClient _http;
    private readonly string _downloadDirectory;

    public QBitTorrentService(string downloadDirectory)
    {
        _downloadDirectory = downloadDirectory;

        Directory.CreateDirectory(_downloadDirectory);

        _http = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Authentication is initialized lazily on the first qBittorrent request.
    }

    private static string ReadApiKey()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "qBittorrent credential protection requires Windows DPAPI.");
        }

        if (!File.Exists(SecretPath))
        {
            throw new FileNotFoundException(
                "qBittorrent API key file was not found.",
                SecretPath);
        }

        var encrypted = File.ReadAllBytes(SecretPath);

        var decrypted = Unprotect(
            encrypted,
            Entropy);

        return Encoding.UTF8.GetString(decrypted);
    }

    private static byte[] Unprotect(
        byte[] encrypted,
        byte[] entropy)
    {
        var input = new DATA_BLOB();
        var entropyBlob = new DATA_BLOB();
        var output = new DATA_BLOB();

        try
        {
            input = CreateBlob(encrypted);
            entropyBlob = CreateBlob(entropy);

            if (!CryptUnprotectData(
                    ref input,
                    IntPtr.Zero,
                    ref entropyBlob,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    0,
                    ref output))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Windows DPAPI failed to decrypt the qBittorrent API key.");
            }

            var result = new byte[output.cbData];

            if (output.cbData > 0)
            {
                Marshal.Copy(
                    output.pbData,
                    result,
                    0,
                    output.cbData);
            }

            return result;
        }
        finally
        {
            FreeBlob(ref input);
            FreeBlob(ref entropyBlob);
            FreeBlob(ref output);
        }
    }

    private static DATA_BLOB CreateBlob(byte[] data)
    {
        var blob = new DATA_BLOB
        {
            cbData = data.Length,
            pbData = Marshal.AllocHGlobal(data.Length)
        };

        if (data.Length > 0)
        {
            Marshal.Copy(
                data,
                0,
                blob.pbData,
                data.Length);
        }

        return blob;
    }

    private static void FreeBlob(ref DATA_BLOB blob)
    {
        if (blob.pbData != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(blob.pbData);
            blob.pbData = IntPtr.Zero;
            blob.cbData = 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport(
        "crypt32.dll",
        SetLastError = true,
        CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DATA_BLOB pDataIn,
        IntPtr ppszDataDescr,
        ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        ref DATA_BLOB pDataOut);

    private void EnsureAuthenticated()
    {
        if (_http.DefaultRequestHeaders.Authorization is not null)
        {
            return;
        }

        var key = ReadApiKey();

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", key);
    }

    public async Task<string> AddMagnetAsync(
        string magnetLink,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticated();

        var hash = ExtractBtih(magnetLink);

        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new InvalidOperationException(
                "Could not determine torrent hash from magnet link.");
        }

        // Reuse an existing torrent instead of attempting
        // to add the same torrent again.
        var existing =
            await GetTorrentAsync(
                hash,
                cancellationToken);

        if (existing is not null)
        {
            await EnsureStreamingModeAsync(
                hash,
                existing.Value,
                cancellationToken);

            await ApplyPluginTagAsync(
                hash,
                cancellationToken);

            return hash;
        }

        var form = new Dictionary<string, string>
        {
            ["urls"] = magnetLink,
            ["savepath"] = _downloadDirectory,
            ["paused"] = "false",
            ["autoTMM"] = "false",
            ["sequentialDownload"] = "true",
            ["firstLastPiecePrio"] = "true",
            ["tags"] = PluginTag
        };

        using var content =
            new FormUrlEncodedContent(form);

        using var response =
            await _http.PostAsync(
                "torrents/add",
                content,
                cancellationToken);

        var responseBody =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (response.StatusCode ==
            System.Net.HttpStatusCode.Conflict)
        {
            existing =
                await GetTorrentAsync(
                    hash,
                    cancellationToken);

            if (existing is not null)
            {
                await EnsureStreamingModeAsync(
                    hash,
                    existing.Value,
                    cancellationToken);

                await ApplyPluginTagAsync(
                hash,
                cancellationToken);

            return hash;
            }

            throw new HttpRequestException(
                "qBittorrent returned 409 Conflict and the torrent could not be found afterward. " +
                responseBody);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                "qBittorrent torrents/add failed: " +
                (int)response.StatusCode +
                " " +
                response.StatusCode +
                ". " +
                responseBody);
        }

        if (responseBody.Trim().StartsWith(
                "Fails",
                StringComparison.OrdinalIgnoreCase))
        {
            existing =
                await GetTorrentAsync(
                    hash,
                    cancellationToken);

            if (existing is not null)
            {
                await EnsureStreamingModeAsync(
                    hash,
                    existing.Value,
                    cancellationToken);

                await ApplyPluginTagAsync(
                hash,
                cancellationToken);

            return hash;
            }

            throw new InvalidOperationException(
                "qBittorrent rejected the torrent add request: " +
                responseBody.Trim());
        }

        existing =
            await GetTorrentAsync(
                hash,
                cancellationToken);

        if (existing is null)
        {
            throw new InvalidOperationException(
                "qBittorrent reported success, but torrent hash " +
                hash +
                " was not present afterward. Response: " +
                responseBody.Trim());
        }

        await EnsureStreamingModeAsync(
            hash,
            existing.Value,
            cancellationToken);

        await ApplyPluginTagAsync(
                hash,
                cancellationToken);

            return hash;
    }

    private async Task EnsureStreamingModeAsync(
        string hash,
        JsonElement torrent,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticated();

        var sequentialEnabled =
            torrent.TryGetProperty(
                "seq_dl",
                out var sequentialProperty) &&
            sequentialProperty.ValueKind ==
                JsonValueKind.True;

        if (!sequentialEnabled)
        {
            using var content =
                new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["hashes"] = hash
                    });

            using var response =
                await _http.PostAsync(
                    "torrents/toggleSequentialDownload",
                    content,
                    cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        var firstLastEnabled =
            torrent.TryGetProperty(
                "f_l_piece_prio",
                out var firstLastProperty) &&
            firstLastProperty.ValueKind ==
                JsonValueKind.True;

        if (!firstLastEnabled)
        {
            using var content =
                new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["hashes"] = hash
                    });

            using var response =
                await _http.PostAsync(
                    "torrents/toggleFirstLastPiecePrio",
                    content,
                    cancellationToken);

            response.EnsureSuccessStatusCode();
        }
    }
    public Task<JsonElement?> GetStatusAsync(
        string hash,
        CancellationToken cancellationToken)
    {
        return GetTorrentAsync(
            hash,
            cancellationToken);
    }
    public async Task<string> ResolveTorrentGidAsync(
        string hash,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < 60; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var torrent =
                await GetTorrentAsync(
                    hash,
                    cancellationToken);

            if (torrent is not null)
            {
                return hash;
            }

            await Task.Delay(
                TimeSpan.FromSeconds(1),
                cancellationToken);
        }

        throw new TimeoutException(
            "Timed out waiting for qBittorrent torrent metadata.");
    }

    public async Task<JsonElement> GetFilesAsync(
        string hash,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticated();

        using var response =
            await _http.GetAsync(
                "torrents/files?hash=" +
                Uri.EscapeDataString(hash),
                cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        using var document =
            await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

        return document.RootElement.Clone();
    }

    public async Task<JsonElement?> TellStatusAsync(
        string hash,
        CancellationToken cancellationToken)
    {
        return await GetTorrentAsync(
            hash,
            cancellationToken);
    }

    private async Task<JsonElement?> GetTorrentAsync(
        string hash,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticated();

        using var response =
            await _http.GetAsync(
                "torrents/info",
                cancellationToken);

        var rawBody =
            await response.Content.ReadAsStringAsync(
                cancellationToken);


        response.EnsureSuccessStatusCode();

        using var document =
            JsonDocument.Parse(
                rawBody);

        var root =
            document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var torrent in root.EnumerateArray())
        {
            if (!torrent.TryGetProperty(
                    "hash",
                    out var hashProperty))
            {
                continue;
            }

            var torrentHash =
                hashProperty.GetString();

            if (string.Equals(
                    torrentHash,
                    hash,
                    StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    "SimpleMovieFeed: qBittorrent torrent found: " +
                    hash);

                return torrent.Clone();
            }
        }

        var debugPath =
            Path.Combine(
                _downloadDirectory,
                "qbt-debug.txt");

        var debugLines =
            new List<string>
            {
                "==================================================",
                DateTime.Now.ToString("O"),
                "Requested hash: " + hash,
                "qBittorrent returned: " +
                    root.GetArrayLength() +
                    " torrent(s)"
            };

        foreach (var torrent in root.EnumerateArray())
        {
            var listedHash =
                torrent.TryGetProperty(
                    "hash",
                    out var listedHashProperty)
                    ? listedHashProperty.GetString()
                    : null;

            var listedName =
                torrent.TryGetProperty(
                    "name",
                    out var listedNameProperty)
                    ? listedNameProperty.GetString()
                    : null;

            var listedSavePath =
                torrent.TryGetProperty(
                    "save_path",
                    out var listedSavePathProperty)
                    ? listedSavePathProperty.GetString()
                    : null;

            var listedState =
                torrent.TryGetProperty(
                    "state",
                    out var listedStateProperty)
                    ? listedStateProperty.GetString()
                    : null;

            debugLines.Add(
                "name=[" +
                listedName +
                "] hash=[" +
                listedHash +
                "] state=[" +
                listedState +
                "] savePath=[" +
                listedSavePath +
                "]");
        }

        File.AppendAllLines(
            debugPath,
            debugLines);

        return null;
    }
    public async Task StopAsync(
        string hash,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticated();

        var form = new Dictionary<string, string>
        {
            ["hashes"] = hash
        };

        using var content =
            new FormUrlEncodedContent(form);

        using var response =
            await _http.PostAsync(
                "torrents/pause",
                content,
                cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTorrentAsync(
        string hash,
        bool deleteFiles,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();

        var form =
            new Dictionary<string, string>
            {
                ["hashes"] = hash,
                ["deleteFiles"] =
                    deleteFiles
                        ? "true"
                        : "false"
            };

        using var content =
            new FormUrlEncodedContent(form);

        using var response =
            await _http.PostAsync(
                "torrents/delete",
                content,
                cancellationToken);

        var body =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                "qBittorrent torrents/delete failed: " +
                (int)response.StatusCode +
                " " +
                response.StatusCode +
                ". " +
                body);
        }
    }
    private async Task ApplyPluginTagAsync(
        string hash,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticated();

        using var content =
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["hashes"] = hash,
                    ["tags"] = PluginTag
                });

        using var response =
            await _http.PostAsync(
                "torrents/addTags",
                content,
                cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private async Task<JsonElement> GetAllTorrentsAsync(
        CancellationToken cancellationToken)
    {
        EnsureAuthenticated();

        using var response =
            await _http.GetAsync(
                "torrents/info",
                cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        using var document =
            await JsonDocument.ParseAsync(
                stream,
                cancellationToken:
                    cancellationToken);

        return document.RootElement.Clone();
    }

    private bool IsPluginTorrent(
        JsonElement torrent)
    {
        if (
            torrent.TryGetProperty(
                "tags",
                out var tagsProperty))
        {
            var tags =
                tagsProperty.GetString() ??
                "";

            foreach (var tag in
                tags.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries))
            {
                if (string.Equals(
                        tag,
                        PluginTag,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        if (
            torrent.TryGetProperty(
                "save_path",
                out var savePathProperty))
        {
            var savePath =
                savePathProperty.GetString();

            if (!string.IsNullOrWhiteSpace(
                    savePath))
            {
                try
                {
                    var root =
                        Path.GetFullPath(
                            _downloadDirectory)
                        .TrimEnd(
                            Path.DirectorySeparatorChar)
                        + Path.DirectorySeparatorChar;

                    var fullSavePath =
                        Path.GetFullPath(
                            savePath)
                        .TrimEnd(
                            Path.DirectorySeparatorChar)
                        + Path.DirectorySeparatorChar;

                    if (fullSavePath.StartsWith(
                            root,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }
        }

        return false;
    }

    public async Task PauseCompletedPluginTorrentsAsync(
        CancellationToken cancellationToken = default)
    {
        var torrents =
            await GetAllTorrentsAsync(
                cancellationToken);

        if (
            torrents.ValueKind !=
            JsonValueKind.Array)
        {
            return;
        }

        foreach (var torrent in
            torrents.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsPluginTorrent(
                    torrent))
            {
                continue;
            }

            var progress =
                torrent.TryGetProperty(
                    "progress",
                    out var progressProperty)
                    ? progressProperty.GetDouble()
                    : 0;

            if (progress < 0.999999)
            {
                continue;
            }

            var state =
                torrent.TryGetProperty(
                    "state",
                    out var stateProperty)
                    ? stateProperty.GetString() ?? ""
                    : "";

            if (
                state.Contains(
                    "paused",
                    StringComparison.OrdinalIgnoreCase) ||
                state.Contains(
                    "stopped",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var hash =
                torrent.TryGetProperty(
                    "hash",
                    out var hashProperty)
                    ? hashProperty.GetString()
                    : null;

            if (string.IsNullOrWhiteSpace(
                    hash))
            {
                continue;
            }

            await StopAsync(
                hash,
                cancellationToken);

            Console.WriteLine(
                "SimpleMovieFeed: completed torrent paused to prevent seeding: " +
                hash);
        }
    }

    public async Task PurgePluginTorrentsAsync(
        CancellationToken cancellationToken = default)
    {
        var torrents =
            await GetAllTorrentsAsync(
                cancellationToken);

        if (
            torrents.ValueKind !=
            JsonValueKind.Array)
        {
            return;
        }

        var hashes =
            new List<string>();

        foreach (var torrent in
            torrents.EnumerateArray())
        {
            if (!IsPluginTorrent(
                    torrent))
            {
                continue;
            }

            if (
                torrent.TryGetProperty(
                    "hash",
                    out var hashProperty))
            {
                var hash =
                    hashProperty.GetString();

                if (!string.IsNullOrWhiteSpace(
                        hash))
                {
                    hashes.Add(
                        hash);
                }
            }
        }

        foreach (var hash in hashes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await DeleteTorrentAsync(
                    hash,
                    deleteFiles: true,
                    cancellationToken);

                Console.WriteLine(
                    "SimpleMovieFeed: startup removed torrent " +
                    hash);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "SimpleMovieFeed: startup torrent cleanup failed for " +
                    hash +
                    ": " +
                    ex.Message);
            }
        }
    }
    public string ResolveSafePath(string path)
    {
        var root =
            Path.GetFullPath(_downloadDirectory)
                .TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        var full =
            Path.GetFullPath(path);

        if (!full.StartsWith(
                root,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                "Requested path is outside the movie cache.");
        }

        return full;
    }

    public void DeleteCachedFile(string path)
    {
        var safePath = ResolveSafePath(path);

        if (File.Exists(safePath))
        {
            File.Delete(safePath);
        }
    }

    private static string? ExtractBtih(string magnet)
    {
        const string marker = "urn:btih:";

        var index =
            magnet.IndexOf(
                marker,
                StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            return null;
        }

        var start = index + marker.Length;

        var end = magnet.IndexOf('&', start);

        var hash =
            end >= 0
                ? magnet[start..end]
                : magnet[start..];

        hash = Uri.UnescapeDataString(hash);

        if (hash.Length == 40 ||
            hash.Length == 32)
        {
            return hash.ToLowerInvariant();
        }

        return null;
    }
}

















