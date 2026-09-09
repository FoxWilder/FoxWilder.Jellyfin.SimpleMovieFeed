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
        var cacheDir = PluginConfiguration.CacheDirectoryDefault;
        var libraryDir = PluginConfiguration.LibraryDirectoryDefault;

        Directory.CreateDirectory(cacheDir);
        Directory.CreateDirectory(libraryDir);

        PlaybackStateStore.Initialize(
            cacheDir);

        serviceCollection.AddHttpClient<YtsApiService>();

        serviceCollection.AddSingleton<QBitTorrentService>(
            _ => new QBitTorrentService(cacheDir));

        serviceCollection.AddSingleton(
            new TorrentStreamService(cacheDir));

        serviceCollection.AddSingleton(
            new WatchHistoryService(cacheDir));

        serviceCollection.AddHostedService<
            PlaybackCleanupEntryPoint>();

        serviceCollection.AddSingleton<JellyfinMovieLibraryService>(
            sp => new JellyfinMovieLibraryService(
                libraryDir,
                sp.GetRequiredService<MediaBrowser.Controller.Library.ILibraryManager>()));
    }
}




