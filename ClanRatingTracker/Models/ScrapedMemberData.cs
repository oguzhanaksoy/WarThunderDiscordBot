namespace ClanRatingTracker.Models;

public class ScrapedMemberData
{
    public string Username { get; set; } = string.Empty;
    public int PersonalClanRating { get; set; }
    public DateTime ScrapedAt { get; set; }
}