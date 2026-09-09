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

    public static Uri QBitTorrentApiUri =>
        new(
            PluginConfiguration.QBitTorrentApiUrlDefault);

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
        PluginConfiguration.MovieSearchApiUrlDefault;

    public static string RssFeedUrl =>
        PluginConfiguration.RssFeedUrlDefault;

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
