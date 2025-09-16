using ClanRatingTracker.Models;

namespace ClanRatingTracker.Interfaces;

public interface IWebScraper
{
    Task<List<ScrapedMemberData>> ExtractClanMembersAsync(string url);
}