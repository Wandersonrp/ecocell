using Ecocell.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecocell.Api.Database.TypeConfiguration;

public sealed class DiscardItemTypeConfiguration : BaseEntityTypeConfiguration<DiscardItem>
{
    public override void Configure(EntityTypeBuilder<DiscardItem> builder)
    {
        base.Configure(builder);
        builder.ToTable("DiscardItems");

        builder.Property(value => value.Material)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(value => value.Quantity).IsRequired();
        builder.Property(value => value.ApproximateWeightKg)
            .HasPrecision(10, 3)
            .IsRequired();

        builder.HasIndex(value => new { value.DiscardId, value.Material })
            .IsUnique();

        builder.HasOne(value => value.MaterialScoreRule)
            .WithMany()
            .HasForeignKey(value => value.MaterialScoreRuleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
