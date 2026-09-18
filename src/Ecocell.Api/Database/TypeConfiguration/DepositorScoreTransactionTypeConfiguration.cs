using Ecocell.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecocell.Api.Database.TypeConfiguration;

public sealed class DepositorScoreTransactionTypeConfiguration
    : BaseEntityTypeConfiguration<DepositorScoreTransaction>
{
    public override void Configure(EntityTypeBuilder<DepositorScoreTransaction> builder)
    {
        base.Configure(builder);
        builder.ToTable("DepositorScoreTransactions", table =>
            table.HasCheckConstraint(
                "CK_DepositorScoreTransactions_Points_Positive",
                "CAST(\"Points\" AS REAL) > 0"));

        builder.HasKey(value => value.Id);
        builder.Property(value => value.Points)
            .HasPrecision(28, 5)
            .IsRequired();

        builder.HasIndex(value => value.DiscardId).IsUnique();
        builder.HasIndex(value => value.DepositorId);

        builder.HasOne(value => value.Discard)
            .WithMany()
            .HasForeignKey(value => value.DiscardId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(value => value.Depositor)
            .WithMany()
            .HasForeignKey(value => value.DepositorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
