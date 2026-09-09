using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.SimpleMovieFeed;

public class PluginConfiguration : BasePluginConfiguration
{
    public const string RssFeedUrlDefault = "https://atlas.rssly.org/feed/0/all/all/0/en";
    public const string MovieSearchApiUrlDefault = "https://yts.gg/api";
    public const string CacheDirectoryDefault = "C:\\JellyfinMovieCache";
    
    public string RssFeedUrl { get; set; } = RssFeedUrlDefault;
    public string MovieSearchApiUrl { get; set; } = MovieSearchApiUrlDefault;
    public string CacheDirectory { get; set; } = CacheDirectoryDefault;
}
