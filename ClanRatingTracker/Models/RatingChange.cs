namespace ClanRatingTracker.Models;

public class RatingChange
{
    public string Username { get; set; } = string.Empty;
    public int OldRating { get; set; }
    public int NewRating { get; set; }
    public int Change { get; set; }
    public DateTime Date { get; set; }
}