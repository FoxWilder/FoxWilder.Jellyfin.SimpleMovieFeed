using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.SimpleMovieFeed;

public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(
        IServiceCollection serviceCollection,
        IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHttpClient<YtsApiService>();

        serviceCollection.AddSingleton<QBitTorrentService>();
        serviceCollection.AddSingleton<WatchHistoryService>();

        serviceCollection.AddHostedService<
            PlaybackCleanupEntryPoint>();

        serviceCollection.AddSingleton<JellyfinMovieLibraryService>();
    }
}




