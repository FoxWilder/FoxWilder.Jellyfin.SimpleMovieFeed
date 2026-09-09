using System.Text.RegularExpressions;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.SimpleMovieFeed;

public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static readonly Guid PluginId =
        Guid.Parse("d3d9d8e8-4a53-4a0d-9a5b-8a1b5c3b0d21");

    public static Plugin Instance { get; private set; } = null!;

    private readonly IApplicationPaths _applicationPaths;

    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        _applicationPaths = applicationPaths;

        Directory.CreateDirectory(
            RuntimeSettings.CacheDirectory);

        Directory.CreateDirectory(
            RuntimeSettings.LibraryDirectory);

        PlaybackStateStore.Initialize(
            RuntimeSettings.CacheDirectory);

        InjectWebScript();
    }

    public override string Name => "Simple Movie Feed";

    public override Guid Id => PluginId;

    public override string Description =>
        "Browse and stream movies from YTS with What's New and Search";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        var assembly = typeof(Plugin).Assembly;
        var assemblyName = assembly.GetName().Name!;

        yield return new PluginPageInfo
        {
            Name = "SimpleMovieFeed",
            EmbeddedResourcePath =
                assemblyName + ".Configuration.config.html"
        };

        yield return new PluginPageInfo
        {
            Name = "SimpleMovieFeed.js",
            EmbeddedResourcePath =
                assemblyName + ".SimpleMovieFeed.js"
        };
    }

    private void InjectWebScript()
    {
        try
        {
            var webPath = _applicationPaths.WebPath;

            if (string.IsNullOrWhiteSpace(webPath))
            {
                return;
            }

            var indexPath = Path.Combine(webPath, "index.html");

            if (!File.Exists(indexPath))
            {
                return;
            }

            var content = File.ReadAllText(indexPath);

            const string marker =
                "<!-- SimpleMovieFeed Injection -->";

            if (content.Contains(marker, StringComparison.Ordinal))
            {
                return;
            }

            const string script =
                """
                <!-- SimpleMovieFeed Injection -->
                <script src="configurationpage?name=SimpleMovieFeed.js"></script>
                """;

            var headClose = Regex.Match(
                content,
                "</head>",
                RegexOptions.IgnoreCase);

            if (!headClose.Success)
            {
                return;
            }

            content =
                content.Insert(
                    headClose.Index,
                    script + Environment.NewLine);

            File.WriteAllText(indexPath, content);

            Console.WriteLine(
                "SimpleMovieFeed: injected JavaScript into index.html");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "SimpleMovieFeed: failed to inject JavaScript: " +
                ex.Message);
        }
    }
}
