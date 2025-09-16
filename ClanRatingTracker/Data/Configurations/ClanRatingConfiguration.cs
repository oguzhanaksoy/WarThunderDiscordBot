using ClanRatingTracker.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClanRatingTracker.Data.Configurations;

internal class ClanRatingConfiguration : IEntityTypeConfiguration<ClanRating>
{
    public void Configure(EntityTypeBuilder<ClanRating> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ClanMemberId)
            .IsRequired();

        builder.Property(e => e.PersonalClanRating)
            .IsRequired();

        builder.Property(e => e.RecordedAt)
            .IsRequired();

        builder.HasIndex(e => new { e.ClanMemberId, e.RecordedAt })
            .HasDatabaseName("IX_ClanRatings_MemberId_RecordedAt");

        builder.HasIndex(e => e.RecordedAt)
            .HasDatabaseName("IX_ClanRatings_RecordedAt");
    }
}
