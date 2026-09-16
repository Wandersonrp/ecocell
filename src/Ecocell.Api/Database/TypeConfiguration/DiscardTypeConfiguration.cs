using Ecocell.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecocell.Api.Database.TypeConfiguration;

public sealed class DiscardTypeConfiguration : BaseEntityTypeConfiguration<Discard>
{
    public override void Configure(EntityTypeBuilder<Discard> builder)
    {
        base.Configure(builder);
        builder.ToTable("Discards");

        builder.Property(value => value.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne(value => value.Depositor)
            .WithMany()
            .HasForeignKey(value => value.DepositorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(value => value.CollectorPoint)
            .WithMany()
            .HasForeignKey(value => value.CollectorPointId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(value => value.Items)
            .WithOne(value => value.Discard)
            .HasForeignKey(value => value.DiscardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(value => value.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
