using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.SimpleMovieFeed;

public class PluginConfiguration : BasePluginConfiguration
{
    public const string RssFeedUrlDefault =
        "https://atlas.rssly.org/feed/0/all/all/0/en";

    public const string MovieSearchApiUrlDefault =
        "https://movies-api.accel.li/api/v2";

    public const string CacheDirectoryDefault =
        "C:\\JellyfinMovieCache";

    public const string LibraryDirectoryDefault =
        "C:\\JellyfinMovieFeedLibrary";

    public const string QBitTorrentApiUrlDefault =
        "http://127.0.0.1:8080/api/v2/";

    public const int StartupBufferMiBDefault = 256;
    public const int CleanupGraceSecondsDefault = 30;
    public const int QBitTorrentTimeoutSecondsDefault = 30;

    public string RssFeedUrl { get; set; } =
        RssFeedUrlDefault;

    public string MovieSearchApiUrl { get; set; } =
        MovieSearchApiUrlDefault;

    public string CacheDirectory { get; set; } =
        CacheDirectoryDefault;

    public string LibraryDirectory { get; set; } =
        LibraryDirectoryDefault;

    public string QBitTorrentApiUrl { get; set; } =
        QBitTorrentApiUrlDefault;

    public int StartupBufferMiB { get; set; } =
        StartupBufferMiBDefault;

    public int CleanupGraceSeconds { get; set; } =
        CleanupGraceSecondsDefault;

    public int QBitTorrentTimeoutSeconds { get; set; } =
        QBitTorrentTimeoutSecondsDefault;
}
