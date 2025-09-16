using ClanRatingTracker.Models;

using Discord;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClanRatingTracker.Data.Configurations;

internal class ClanMemberConfiguration : IEntityTypeConfiguration<ClanMember>
{
    public void Configure(EntityTypeBuilder<ClanMember> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Username)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.FirstSeen)
            .IsRequired();

        builder.Property(e => e.LastSeen)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(e => e.Username)
            .IsUnique()
            .HasDatabaseName("IX_ClanMembers_Username");

        builder.HasMany(e => e.Ratings)
            .WithOne(r => r.ClanMember)
            .HasForeignKey(r => r.ClanMemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
