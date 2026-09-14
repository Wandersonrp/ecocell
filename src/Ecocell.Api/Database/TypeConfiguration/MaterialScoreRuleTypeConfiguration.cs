using Ecocell.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecocell.Api.Database.TypeConfiguration;

/// <summary>Configura a persistência das versões de regra de pontuação.</summary>
public sealed class MaterialScoreRuleTypeConfiguration
    : BaseEntityTypeConfiguration<MaterialScoreRule>
{
    public override void Configure(EntityTypeBuilder<MaterialScoreRule> builder)
    {
        base.Configure(builder);

        builder.ToTable("MaterialScoreRules", table =>
        {
            table.HasCheckConstraint(
                "CK_MaterialScoreRules_Points_Positive",
                "CAST(\"Points\" AS REAL) > 0");
            table.HasCheckConstraint(
                "CK_MaterialScoreRules_Validity",
                "\"ValidTo\" IS NULL OR \"ValidTo\" > \"ValidFrom\"");
        });

        builder.HasKey(rule => rule.Id);

        builder.Property(rule => rule.Material)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(rule => rule.Points)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(rule => rule.Unit)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(rule => rule.ValidFrom)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(rule => rule.ValidTo)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(rule => new { rule.LegalPersonId, rule.Material })
            .IsUnique()
            .HasFilter("\"ValidTo\" IS NULL");

        builder.HasOne(rule => rule.LegalPerson)
            .WithMany()
            .HasForeignKey(rule => rule.LegalPersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
