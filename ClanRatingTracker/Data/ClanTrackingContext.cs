using ClanRatingTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace ClanRatingTracker.Data;

public class ClanTrackingContext : DbContext
{
    public ClanTrackingContext(DbContextOptions<ClanTrackingContext> options) : base(options)
    {
    }

    public DbSet<ClanMember> ClanMembers { get; set; } = null!;
    public DbSet<ClanRating> ClanRatings { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClanTrackingContext).Assembly);
    }
}