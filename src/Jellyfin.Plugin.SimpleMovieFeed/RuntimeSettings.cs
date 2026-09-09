namespace Jellyfin.Plugin.SimpleMovieFeed;

public static class RuntimeSettings
{
    public static PluginConfiguration Current
    {
        get
        {
            var plugin = Plugin.Instance;

            if (plugin is null)
            {
                return new PluginConfiguration();
            }

            return plugin.Configuration;
        }
    }

    public static string CacheDirectory =>
        NormalizePath(
            Current.CacheDirectory,
            PluginConfiguration.CacheDirectoryDefault);

    public static string LibraryDirectory =>
        NormalizePath(
            Current.LibraryDirectory,
            PluginConfiguration.LibraryDirectoryDefault);

    public static Uri QBitTorrentApiUri
    {
        get
        {
            var value = Current.QBitTorrentApiUrl;

            if (string.IsNullOrWhiteSpace(value))
            {
                value = PluginConfiguration.QBitTorrentApiUrlDefault;
            }

            value = value.Trim();

            if (!value.EndsWith("/", StringComparison.Ordinal))
            {
                value += "/";
            }

            if (!Uri.TryCreate(
                    value,
                    UriKind.Absolute,
                    out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                 uri.Scheme != Uri.UriSchemeHttps))
            {
                return new Uri(
                    PluginConfiguration.QBitTorrentApiUrlDefault);
            }

            return uri;
        }
    }

    public static long StartupBufferBytes =>
        (long)Math.Clamp(
            Current.StartupBufferMiB,
            1,
            4096) * 1024L * 1024L;

    public static int StartupBufferMiB =>
        Math.Clamp(
            Current.StartupBufferMiB,
            1,
            4096);

    public static TimeSpan CleanupGrace =>
        TimeSpan.FromSeconds(
            Math.Clamp(
                Current.CleanupGraceSeconds,
                0,
                3600));

    public static TimeSpan QBitTorrentTimeout =>
        TimeSpan.FromSeconds(
            Math.Clamp(
                Current.QBitTorrentTimeoutSeconds,
                5,
                300));

    public static string MovieSearchApiUrl =>
        string.IsNullOrWhiteSpace(Current.MovieSearchApiUrl)
            ? PluginConfiguration.MovieSearchApiUrlDefault
            : Current.MovieSearchApiUrl.TrimEnd('/');

    public static string RssFeedUrl =>
        string.IsNullOrWhiteSpace(Current.RssFeedUrl)
            ? PluginConfiguration.RssFeedUrlDefault
            : Current.RssFeedUrl.Trim();

    private static string NormalizePath(
        string? value,
        string fallback)
    {
        var selected =
            string.IsNullOrWhiteSpace(value)
                ? fallback
                : value;

        return Path.GetFullPath(selected.Trim());
    }
}
