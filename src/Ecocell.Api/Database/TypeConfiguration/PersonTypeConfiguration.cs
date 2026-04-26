using Ecocell.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecocell.Api.Database.TypeConfiguration;

public class PersonTypeConfiguration : BaseEntityTypeConfiguration<Person>
{
    public override void Configure(EntityTypeBuilder<Person> builder)
    {
        base.Configure(builder);

        builder.HasKey(x => x.Id);

        builder.UseTptMappingStrategy();

        builder.Property(p => p.Id)
            .HasColumnName("PersonId");
        
        builder.ToTable("People");

        builder.HasIndex(p => p.Email);

        builder.Property(p => p.Email)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(p => p.PersonStatus)
            .IsRequired();
    }
}
