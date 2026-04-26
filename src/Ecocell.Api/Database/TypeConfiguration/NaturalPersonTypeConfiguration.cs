using Ecocell.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecocell.Api.Database.TypeConfiguration;

public class NaturalPersonTypeConfiguration : IEntityTypeConfiguration<NaturalPerson>
{
    public void Configure(EntityTypeBuilder<NaturalPerson> builder)
    {
        builder.ToTable("NaturalPeople");

        builder.Property(np => np.FullName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(np => np.Cpf)
            .HasMaxLength(11)
            .IsRequired();

        builder.HasIndex(np => np.Cpf);

        builder.Property(np => np.BirthDate)
            .HasColumnType("date")
            .IsRequired();
    }
}
