using Ecocell.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecocell.Api.Database.TypeConfiguration;

public sealed class DepositorTotalScoreTypeConfiguration
    : BaseEntityTypeConfiguration<DepositorTotalScore>
{
    public override void Configure(EntityTypeBuilder<DepositorTotalScore> builder)
    {
        base.Configure(builder);
        builder.ToTable("DepositorTotalScores", table =>
            table.HasCheckConstraint(
                "CK_DepositorTotalScores_TotalPoints_NonNegative",
                "CAST(\"TotalPoints\" AS REAL) >= 0"));

        builder.HasKey(value => value.Id);
        builder.Property(value => value.TotalPoints)
            .HasPrecision(28, 5)
            .IsRequired()
            .IsConcurrencyToken();

        builder.HasIndex(value => value.DepositorId).IsUnique();

        builder.HasOne(value => value.Depositor)
            .WithMany()
            .HasForeignKey(value => value.DepositorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
