namespace ClanRatingTracker.Models;

public class MemberProcessingResult
{
    public List<RatingChange> RatingChanges { get; set; } = new List<RatingChange>();
    public List<string> NewMembers { get; set; } = new List<string>();
    public List<string> InactiveMembers { get; set; } = new List<string>();
}