using System.Xml.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.SimpleMovieFeed;

public sealed record FeedMovie(string Id, string Title, string MediaUrl, string? ImageUrl, string? Description);

public static class FeedService
{
    public static async Task<List<FeedMovie>> ReadAsync(string url, CancellationToken ct)
    {
        using var http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });
        http.Timeout = TimeSpan.FromSeconds(30);
        var xml = await http.GetStringAsync(url, ct);
        var doc = XDocument.Parse(xml);
        var items = doc.Descendants("item");
        var list = new List<FeedMovie>();
        foreach (var item in items)
        {
            var title = (string?)item.Element("title") ?? "Untitled";
            var enclosure = item.Element("enclosure");
            var media = (string?)enclosure?.Attribute("url");
            if (string.IsNullOrWhiteSpace(media)) continue;
            var guid = (string?)item.Element("guid") ?? media;
            var id = Regex.Replace(guid, "[^A-Za-z0-9_-]", "_");
            var desc = (string?)item.Element("description");
            var image = (string?)item.Element("image")?.Element("url") ?? (string?)item.Element("thumbnail")?.Attribute("url");
            list.Add(new FeedMovie(id, title, media, image, WebUtility.HtmlDecode(desc ?? "")));
        }
        return list;
    }
}
