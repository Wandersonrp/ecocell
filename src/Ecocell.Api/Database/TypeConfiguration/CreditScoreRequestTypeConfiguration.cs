using Ecocell.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecocell.Api.Database.TypeConfiguration;

public sealed class CreditScoreRequestTypeConfiguration
    : BaseEntityTypeConfiguration<CreditScoreRequest>
{
    public override void Configure(EntityTypeBuilder<CreditScoreRequest> builder)
    {
        base.Configure(builder);
        builder.ToTable("CreditScoreRequests");

        builder.HasKey(value => value.Id);
        builder.Property(value => value.DispatchedAt)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(value => value.DiscardId).IsUnique();
        builder.HasIndex(value => value.DispatchedAt);

        builder.HasOne(value => value.Discard)
            .WithMany()
            .HasForeignKey(value => value.DiscardId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
