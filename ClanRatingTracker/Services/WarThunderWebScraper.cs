using ClanRatingTracker.Interfaces;
using ClanRatingTracker.Models;

using HtmlAgilityPack;

using Microsoft.Extensions.Logging;

namespace ClanRatingTracker.Services;

public class WarThunderWebScraper : IWebScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WarThunderWebScraper> _logger;
    
    private const int MAX_RETRY_ATTEMPTS = 3;
    private const int BASE_DELAY_MS = 1000;
    private const string PLAYER_COLUMN_NAME = "player";
    private const string PERSONAL_CLAN_RATING_COLUMN_NAME = "personal clan rating";

    public WarThunderWebScraper(HttpClient httpClient, ILogger<WarThunderWebScraper> logger)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3");
        _logger = logger;
    }

    public async Task<List<ScrapedMemberData>> ExtractClanMembersAsync(string url)
    {
        _logger.LogInformation("Starting clan member extraction from URL: {Url}", url);

        var htmlContent = await FetchHtmlWithRetryAsync(url);
        if (string.IsNullOrEmpty(htmlContent))
        {
            _logger.LogError("Failed to fetch HTML content after all retry attempts");
            return new List<ScrapedMemberData>();
        }

        return ParseClanMembers(htmlContent);
    }

    private async Task<string?> FetchHtmlWithRetryAsync(string url)
    {
        for (int attempt = 1; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
        {
            try
            {
                //_logger.LogDebug("Attempt {Attempt} of {MaxAttempts} to fetch URL: {Url}",
                //    attempt, MAX_RETRY_ATTEMPTS, url);

                //var response = await _httpClient.GetAsync(url);
                //response.EnsureSuccessStatusCode();

                //var content = await response.Content.ReadAsStringAsync();

                //_logger.LogInformation("Successfully fetched HTML content on attempt {Attempt}", attempt);

                // Because we can't pass cloudflare, use local file for parsing.

                var fullFilePath = Path.Combine(AppContext.BaseDirectory, "index.html");
                var content = await File.ReadAllTextAsync(fullFilePath);
                return content;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("HTTP request failed on attempt {Attempt}: {Error}",
                    attempt, ex.Message);

                if (attempt == MAX_RETRY_ATTEMPTS)
                {
                    _logger.LogError(ex, "All retry attempts failed for URL: {Url}", url);
                    return null;
                }

                await DelayWithExponentialBackoffAsync(attempt);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning("Request timeout on attempt {Attempt}: {Error}",
                    attempt, ex.Message);

                if (attempt == MAX_RETRY_ATTEMPTS)
                {
                    _logger.LogError(ex, "All retry attempts failed due to timeout for URL: {Url}", url);
                    return null;
                }

                await DelayWithExponentialBackoffAsync(attempt);
            }
        }

        return null;
    }

    private async Task DelayWithExponentialBackoffAsync(int attempt)
    {
        var delay = TimeSpan.FromMilliseconds(BASE_DELAY_MS * Math.Pow(2, attempt - 1));
        _logger.LogDebug("Waiting {Delay}ms before next retry attempt", delay.TotalMilliseconds);
        await Task.Delay(delay);
    }

    public List<ScrapedMemberData> ParseClanMembers(string htmlContent)
    {
        var members = new List<ScrapedMemberData>();

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            var gridItems = doc.DocumentNode
                .SelectNodes("//div[contains(@class, 'squadrons-members__grid-item')]");

            if (gridItems is null || gridItems.Count == 0)
            {
                _logger.LogWarning("No grid items found in HTML content. The website structure may have changed.");
                return members;
            }

            // First 6 items are headers
            var headers = new string[6];
            for (int i = 0; i < 6; i++)
                headers[i] = gridItems[i].InnerText.Trim();

            var columnCount = headers.Length;

            // Build header map (case-insensitive, no ToLowerInvariant allocation)
            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++)
                headerMap[headers[i]] = i;

            _logger.LogInformation("Detected headers: {Headers}", string.Join(", ", headers));

            var dataItemsCount = gridItems.Count - columnCount;
            if (dataItemsCount <= 0)
                return members;

            members.Capacity = dataItemsCount / columnCount;

            for (int i = columnCount; i + columnCount - 1 < gridItems.Count; i += columnCount)
            {
                try
                {
                    var row = gridItems.Skip(i).Take(columnCount);

                    if (!headerMap.TryGetValue(PLAYER_COLUMN_NAME, out var playerIdx) ||
                        !headerMap.TryGetValue(PERSONAL_CLAN_RATING_COLUMN_NAME, out var ratingIdx))
                        break;

                    var username = ExtractUsernameFromGridItem(gridItems[i + playerIdx]);
                    var rating = ExtractRatingFromGridItem(gridItems[i + ratingIdx]);

                    if (!string.IsNullOrWhiteSpace(username) && rating.HasValue)
                    {
                        members.Add(new ScrapedMemberData
                        {
                            Username = username.Trim(),
                            PersonalClanRating = rating.Value,
                            ScrapedAt = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse member row at index {Index}", i);
                }
            }

            _logger.LogInformation("Successfully parsed {Count} clan members", members.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse HTML content for clan members");
        }

        return members;
    }


    private string? ExtractUsernameFromGridItem(HtmlNode gridItem)
    {
        try
        {
            // Look for anchor tag with username (typical structure: <a href="...">Username</a>)
            var linkNode = gridItem.SelectSingleNode(".//a");
            if (linkNode != null)
            {
                var username = linkNode.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(username) && IsValidUsername(username))
                {
                    return username;
                }
            }

            // Fallback: get direct text content
            var directText = gridItem.InnerText?.Trim();
            if (!string.IsNullOrWhiteSpace(directText) && IsValidUsername(directText))
            {
                return directText;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to extract username from grid item");
            return null;
        }
    }

    private int? ExtractRatingFromGridItem(HtmlNode gridItem)
    {
        try
        {
            var ratingText = gridItem.InnerText?.Trim();
            if (TryParseRating(ratingText, out var rating))
            {
                return rating;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to extract rating from grid item");
            return null;
        }
    }

    private bool IsValidUsername(string username)
    {
        return !string.IsNullOrWhiteSpace(username) &&
               username.Length >= 2 &&
               username.Length <= 100 &&
               !username.Contains("Rating") &&
               !username.Contains("Score");
    }

    private bool TryParseRating(string? text, out int rating)
    {
        rating = 0;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Remove common formatting characters
        var cleanText = text.Replace(",", "").Replace(".", "").Replace(" ", "");

        // Try to parse as integer
        if (int.TryParse(cleanText, out rating))
        {
            // Validate rating range (War Thunder ratings are typically 0-10000+)
            return rating >= 0 && rating <= 50000;
        }

        return false;
    }
}