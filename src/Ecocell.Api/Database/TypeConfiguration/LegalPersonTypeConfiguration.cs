using Ecocell.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecocell.Api.Database.TypeConfiguration;

public class LegalPersonTypeConfiguration : IEntityTypeConfiguration<LegalPerson>
{
    public void Configure(EntityTypeBuilder<LegalPerson> builder)
    {
        builder.ToTable("LegalPeople");

        builder.Property(lp => lp.LegalName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(lp => lp.TradeName)
            .HasMaxLength(255);

        builder.Property(lp => lp.Cnpj)
            .HasMaxLength(14);

        builder.HasIndex(lp => lp.Cnpj);

        builder.Property(lp => lp.Cnae)
            .HasMaxLength(7);

        builder.HasOne(lp => lp.ResponsiblePerson)
            .WithMany(np => np.ManagedCompanies)
            .HasForeignKey(lp => lp.ResponsiblePersonId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
