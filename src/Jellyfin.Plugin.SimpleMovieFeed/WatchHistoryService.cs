using System.Text.Json;

namespace Jellyfin.Plugin.SimpleMovieFeed;

public class WatchHistoryService
{
    private readonly string _historyFile;

    public WatchHistoryService(string cacheDir)
    {
        _historyFile = Path.Combine(cacheDir, "watch_history.json");
    }

    public async Task<UserWatchHistory> GetWatchHistoryAsync(string userId, int movieId)
    {
        var histories = await LoadHistoriesAsync();
        return histories.FirstOrDefault(h => h.UserId == userId && h.MovieId == movieId) ?? new UserWatchHistory { MovieId = movieId };
    }

    public async Task SaveWatchHistoryAsync(UserWatchHistory history)
    {
        var histories = await LoadHistoriesAsync();
        var existing = histories.FirstOrDefault(h => h.UserId == history.UserId && h.MovieId == history.MovieId);
        
        if (existing != null)
        {
            histories.Remove(existing);
        }
        
        history.LastWatched = DateTime.UtcNow;
        histories.Add(history);
        await SaveHistoriesAsync(histories);
    }

    private async Task<List<UserWatchHistory>> LoadHistoriesAsync()
    {
        try
        {
            if (!File.Exists(_historyFile))
                return new();
            
            var json = await File.ReadAllTextAsync(_historyFile);
            return JsonSerializer.Deserialize<List<UserWatchHistory>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private async Task SaveHistoriesAsync(List<UserWatchHistory> histories)
    {
        var json = JsonSerializer.Serialize(histories, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_historyFile, json);
    }
}
