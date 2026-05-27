using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversalLIMS.Domain.Laboratory;

namespace UniversalLIMS.Infrastructure.Persistence.Configurations;

public sealed class ExpertConclusionReviewConfiguration : IEntityTypeConfiguration<ExpertConclusionReview>
{
    public void Configure(EntityTypeBuilder<ExpertConclusionReview> builder)
    {
        builder.ToTable("ExpertConclusionReviews");

        builder.HasKey(review => review.Id);

        builder.Property(review => review.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(review => review.ReviewStartedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(review => review.ApprovedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(review => review.ApprovedByUserId)
            .HasMaxLength(450);

        builder.Property(review => review.NotesUk)
            .HasMaxLength(2000);

        builder.Property(review => review.CreatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(review => review.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(review => review.AnnulledAtUtc)
            .HasColumnType("datetime2");

        builder.Property(review => review.AnnulmentReason)
            .HasMaxLength(1000);

        builder.Property(review => review.RowVersion)
            .IsRowVersion();

        builder.HasIndex(review => review.SampleId)
            .IsUnique()
            .HasFilter("[IsAnnulled] = 0");

        builder.HasOne(review => review.Sample)
            .WithMany()
            .HasForeignKey(review => review.SampleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(review => !review.IsAnnulled);
    }
}
