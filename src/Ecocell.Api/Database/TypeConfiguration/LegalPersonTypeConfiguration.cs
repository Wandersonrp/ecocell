using Ecocell.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecocell.Api.Database.TypeConfiguration;

/// <summary>
/// Configuração EF Core para a entidade <see cref="LegalPerson"/>, incluindo mapeamento
/// de propriedades, índices, restrições e relacionamentos.
/// </summary>
public class LegalPersonTypeConfiguration : IEntityTypeConfiguration<LegalPerson>
{
    /// <summary>
    /// Configura o mapeamento da entidade <see cref="LegalPerson"/> no banco de dados.
    /// </summary>
    /// <param name="builder">Construtor de configuração da entidade.</param>
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

        builder.HasOne(lp => lp.Address)
            .WithMany()
            .HasForeignKey(lp => lp.AddressId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
