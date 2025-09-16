using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClanRatingTracker.Models;

public class ClanRating
{
    public int Id { get; set; }
    
    [Required]
    public int ClanMemberId { get; set; }
    
    public int PersonalClanRating { get; set; }
    
    public DateTime RecordedAt { get; set; }
    
    // Navigation property
    [ForeignKey("ClanMemberId")]
    public virtual ClanMember ClanMember { get; set; } = null!;
}