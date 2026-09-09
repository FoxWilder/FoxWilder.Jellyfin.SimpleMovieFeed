using System.Net.Http.Json;

namespace Jellyfin.Plugin.SimpleMovieFeed;

public class YtsApiService
{
    private readonly HttpClient _httpClient;

    private const string BaseUrl = "https://movies-api.accel.li/api/v2";

    public YtsApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<DashboardMovie>> SearchMoviesAsync(
        string query,
        int page = 1,
        CancellationToken ct = default)
    {
        try
        {
            const int pageSize = 50;

            var allMovies =
                new List<YtsMovie>();

            var currentPage =
                Math.Max(
                    1,
                    page);

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var url =
                    string.Format(
                        "{0}/list_movies.json?query_term={1}&page={2}&limit={3}",
                        BaseUrl,
                        Uri.EscapeDataString(query),
                        currentPage,
                        pageSize);

                var response =
                    await _httpClient.GetFromJsonAsync<YtsApiResponse>(
                        url,
                        cancellationToken: ct);

                var movies =
                    response?.Data?.Movies ??
                    new List<YtsMovie>();

                if (movies.Count == 0)
                {
                    break;
                }

                var existingIds =
                    allMovies
                        .Select(movie => movie.Id)
                        .ToHashSet();

                var newMovies =
                    movies
                        .Where(movie => !existingIds.Contains(movie.Id))
                        .ToList();

                if (newMovies.Count == 0)
                {
                    Console.WriteLine(
                        "SimpleMovieFeed: search page " +
                        currentPage +
                        " contained no new movies; pagination complete.");

                    break;
                }

                allMovies.AddRange(
                    newMovies);

                Console.WriteLine(
                    "SimpleMovieFeed: search page " +
                    currentPage +
                    " returned " +
                    movies.Count +
                    " movies. Total so far: " +
                    allMovies.Count);

                /*
                 * Do NOT stop merely because the API returned fewer
                 * items than requested. This API may clamp the limit
                 * (for example, request 50 but return 30 per page).
                 *
                 * We keep requesting subsequent pages until the API
                 * returns an empty page.
                 */
                currentPage++;

                /*
                 * Safety guard against a broken API.
                 */
                if (currentPage > 200)
                {
                    Console.WriteLine(
                        "SimpleMovieFeed: search stopped at safety limit of 200 pages.");

                    break;
                }
            }

            /*
             * Defensive de-duplication in case the upstream API
             * changes while pages are being fetched.
             */
            var uniqueMovies =
                allMovies
                    .GroupBy(
                        movie => movie.Id)
                    .Select(
                        group => group.First())
                    .ToList();

            Console.WriteLine(
                "SimpleMovieFeed: search completed with " +
                uniqueMovies.Count +
                " unique movies.");

            return ConvertToDashboardMovies(
                uniqueMovies);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "SimpleMovieFeed: YTS search error: " +
                ex);

            return new();
        }
    }

    public async Task<List<DashboardMovie>> GetMovieSuggestionsAsync(
        int movieId,
        CancellationToken ct = default)
    {
        if (movieId <= 0)
        {
            return new();
        }

        try
        {
            var url =
                string.Format(
                    "{0}/movie_suggestions.json?movie_id={1}",
                    BaseUrl,
                    movieId);

            var response =
                await _httpClient.GetFromJsonAsync<YtsApiResponse>(
                    url,
                    cancellationToken: ct);

            var movies =
                response?.Data?.Movies ??
                new List<YtsMovie>();

            Console.WriteLine(
                "SimpleMovieFeed: YTS suggestions for movie " +
                movieId +
                ": " +
                movies.Count);

            return ConvertToDashboardMovies(
                movies);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "SimpleMovieFeed: YTS suggestions error for movie " +
                movieId +
                ": " +
                ex);

            return new();
        }
    }
    public async Task<List<DashboardMovie>> GetLatestMoviesAsync(
        int page = 1,
        CancellationToken ct = default)
    {
        try
        {
            var url = string.Format(
                "{0}/list_movies.json?page={1}&limit=20&sort_by=date_added&order_by=desc",
                BaseUrl,
                page);

            var response = await _httpClient.GetFromJsonAsync<YtsApiResponse>(
                url,
                cancellationToken: ct);

            return ConvertToDashboardMovies(response?.Data?.Movies ?? new());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                string.Format("YTS API Error: {0}", ex.Message));

            return new();
        }
    }

    private List<DashboardMovie> ConvertToDashboardMovies(
        List<YtsMovie> ytsMovies)
    {
        return ytsMovies.Select(m => new DashboardMovie
        {
            Id = m.Id,
            Title = m.Title,
            Description = m.DescriptionFull,

            PosterUrl = string.IsNullOrWhiteSpace(m.LargeCoverImage)
                ? m.MediumCoverImage
                : m.LargeCoverImage,

            BackdropUrl = m.BackgroundImage,
            Rating = m.Rating,
            Year = m.Year,
            Runtime = m.Runtime,
            Genres = m.Genres,

            StreamOptions = m.Torrents.Select(t => new StreamOption
            {
                Quality = t.Quality,
                MagnetLink = GenerateMagnetLink(t.Hash),
                TorrentUrl = t.Url,
                Seeds = t.Seeds,
                Peers = t.Peers
            }).ToList()

        }).ToList();
    }

    private string GenerateMagnetLink(string hash)
    {
        return string.Format(
            "magnet:?xt=urn:btih:{0}&dn=movie&tr=udp://tracker.openbittorrent.com:80&tr=udp://tracker.opentracker.se:80",
            hash);
    }
}




