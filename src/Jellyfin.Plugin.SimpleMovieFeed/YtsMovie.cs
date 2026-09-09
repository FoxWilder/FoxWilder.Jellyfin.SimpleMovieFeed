using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.SimpleMovieFeed;

public class YtsMovie
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("rating")]
    public double Rating { get; set; }

    [JsonPropertyName("runtime")]
    public int Runtime { get; set; }

    [JsonPropertyName("genres")]
    public List<string> Genres { get; set; } = new();

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("description_full")]
    public string DescriptionFull { get; set; } = "";

    [JsonPropertyName("medium_cover_image")]
    public string MediumCoverImage { get; set; } = "";

    [JsonPropertyName("large_cover_image")]
    public string LargeCoverImage { get; set; } = "";

    [JsonPropertyName("medium_screenshot_image1")]
    public string MediumScreenshotImage1 { get; set; } = "";

    [JsonPropertyName("large_screenshot_image1")]
    public string LargeScreenshotImage1 { get; set; } = "";

    [JsonPropertyName("background_image")]
    public string BackgroundImage { get; set; } = "";

    [JsonPropertyName("torrents")]
    public List<Torrent> Torrents { get; set; } = new();
}

public class Torrent
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = "";

    [JsonPropertyName("quality")]
    public string Quality { get; set; } = "";

    [JsonPropertyName("seeds")]
    public int Seeds { get; set; }

    [JsonPropertyName("peers")]
    public int Peers { get; set; }

    [JsonPropertyName("size")]
    public string Size { get; set; } = "";

    [JsonPropertyName("date_uploaded")]
    public string DateUploaded { get; set; } = "";
}

public class YtsApiResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("data")]
    public YtsData Data { get; set; } = new();
}

public class YtsData
{
    [JsonPropertyName("movies")]
    public List<YtsMovie> Movies { get; set; } = new();

    [JsonPropertyName("movie_count")]
    public int MovieCount { get; set; }
}
