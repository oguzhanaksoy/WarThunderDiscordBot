using System.ComponentModel.DataAnnotations;

namespace ClanRatingTracker.Models;

public class ClanMember
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;
    
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<ClanRating> Ratings { get; set; } = new List<ClanRating>();
}