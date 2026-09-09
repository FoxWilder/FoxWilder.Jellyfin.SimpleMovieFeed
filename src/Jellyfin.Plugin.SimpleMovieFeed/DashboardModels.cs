namespace Jellyfin.Plugin.SimpleMovieFeed;

public class DashboardMovie
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string PosterUrl { get; set; } = "";
    public string BackdropUrl { get; set; } = "";
    public double Rating { get; set; }
    public int Year { get; set; }
    public int Runtime { get; set; }
    public List<string> Genres { get; set; } = new();
    public List<StreamOption> StreamOptions { get; set; } = new();
}

public class StreamOption
{
    public string Quality { get; set; } = "";
    public string MagnetLink { get; set; } = "";
    public string TorrentUrl { get; set; } = "";
    public int Seeds { get; set; }
    public int Peers { get; set; }
}

public class UserWatchHistory
{
    public int MovieId { get; set; }
    public string UserId { get; set; } = "";
    public long WatchedSeconds { get; set; }
    public long TotalSeconds { get; set; }
    public DateTime LastWatched { get; set; }
}
