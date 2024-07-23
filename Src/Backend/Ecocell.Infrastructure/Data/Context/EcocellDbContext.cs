using Ecocell.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecocell.Infrastructure.Data.Context;

public class EcocellDbContext : DbContext
{
    public EcocellDbContext(DbContextOptions<EcocellDbContext> options) : base(options)
    {}

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EcocellDbContext).Assembly);            

        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("Users");
            
            user.HasOne(e => e.Document)
                .WithOne(e => e.User)
                .HasForeignKey<Document>(e => e.DocumentId)
                .IsRequired();

            user.Property(e => e.DocumentId)
                .HasColumnName("document_id");

            user.HasKey(e => e.UserId);

            user.Property(e => e.UserId)
                .HasColumnName("user_id");                

            user.Property(e => e.Name)
                .HasColumnType("varchar(100)")
                .HasColumnName("name")
                .IsRequired();

            user.Property(e => e.Email)
                .HasColumnType("varchar(100)")
                .HasColumnName("email")
                .IsRequired();

            user.Property(e => e.Password)
                .HasColumnName("password")
                .IsRequired();

            user.Property(e => e.AvatarUrl)
                .HasColumnName("avatar_url");

            user.Property(e => e.UserType)
                .HasColumnName("user_type")
                .HasConversion<string>()
                .IsRequired();

            user.Property(e => e.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .IsRequired();

            user.Property(e => e.Role)
                .HasColumnName("role")
                .HasConversion<string>()
                .IsRequired();

            user.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp without time zone")
                .IsRequired();

            user.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamp without time zone");             
        });        

        modelBuilder.Entity<Document>(document =>
        {
            document.ToTable("Documents");

            document.HasKey(e => e.DocumentId)
                .HasName("pk_document");
                
            document.Property(e => e.DocumentId)
                .HasColumnName("document_id");

            document.Property(e => e.DocumentType)
                .HasColumnName("document_type")
                .HasConversion<string>()
                .IsRequired();

            document.Property(e => e.Text)
                .HasColumnName("text")
                .HasColumnType("varchar(14)")
                .IsRequired();
        });

        modelBuilder.Entity<NaturalPerson>(naturalPerson =>
        {
            naturalPerson.ToTable("NaturalPeople");            

            naturalPerson.HasOne<User>()
                .WithOne()
                .HasForeignKey<NaturalPerson>(e => e.UserId)
                .HasConstraintName("fk_natural_person_user");

            naturalPerson.Property(e => e.IsDiscarding)
                .HasColumnName("is_discarding")
                .HasColumnType("boolean")
                .IsRequired();

            naturalPerson.Property(e => e.BirthDate)
                .HasColumnName("birth_date")
                .HasColumnType("date");
        });        

        modelBuilder.Entity<LegalPerson>(legalPerson =>
        {
            legalPerson.ToTable("LegalPeople");            

            legalPerson.HasOne<User>()
                .WithOne()
                .HasForeignKey<LegalPerson>(e => e.UserId)
                .HasConstraintName("fk_legal_person_user");

            legalPerson.Property(e => e.CorporateName)
                .HasColumnName("corporate_name")
                .HasColumnType("varchar(100)")
                .IsRequired();

            legalPerson.Property(e => e.CompanyDescription)
                .HasColumnName("company_description")
                .HasColumnType("varchar(200)");

            legalPerson.Property(e => e.CompanyStartDate)
                .HasColumnName("company_start_date")
                .HasColumnType("date");

            legalPerson.Property(e => e.Phone)
                .HasColumnName("phone")
                .HasColumnType("varchar(20)");

            legalPerson.Property(e => e.PrincipalCnae)
                .HasColumnName("principal_cnae")
                .HasColumnType("varchar(10)")
                .IsRequired();

            legalPerson.Property(e => e.CompanyStatus)
                .HasColumnName("company_status")
                .HasConversion<string>()
                .IsRequired();

            legalPerson.Property(e => e.IsDiscarding)
                .HasColumnName("is_discarding")
                .HasColumnType("boolean")
                .IsRequired();

            legalPerson.Property(e => e.IsCollector)
                .HasColumnName("is_collector")
                .HasColumnType("boolean")
                .IsRequired();

            legalPerson.Property(e => e.IsDiscardingPoint)
                .HasColumnName("is_discarding_point")
                .HasColumnType("boolean")
                .IsRequired();

            legalPerson.Property(e => e.AddressId)
                .HasColumnName("address_id")
                .IsRequired();

            legalPerson.HasOne(e => e.Address)
                .WithOne(e => e.LegalPerson)
                .HasForeignKey<LegalPerson>(e => e.AddressId);
        });        

        modelBuilder.Entity<Address>(address =>
        {
            address.ToTable("Addresses");

            address.HasKey(e => e.AddressId)
                .HasName("pk_address");

            address.Property(e => e.AddressId)
                .HasColumnName("address_id");

            address.Property(e => e.Street)
                .HasColumnName("street")
                .HasColumnType("varchar(100)")
                .IsRequired();

            address.Property(e => e.Number)
                .HasColumnName("number")
                .HasColumnType("varchar(15)")
                .IsRequired();

            address.Property(e => e.Complement)
                .HasColumnName("complement")
                .HasColumnType("varchar(50)");

            address.Property(e => e.Neighborhood)
                .HasColumnName("neighborhood")
                .HasColumnType("varchar(50)")
                .IsRequired();

            address.Property(e => e.ZipCode)
                .HasColumnName("zip_code")
                .HasColumnType("varchar(8)");

            address.Property(e => e.City)
                .HasColumnName("city")
                .HasColumnType("varchar(100)")
                .IsRequired();

            address.Property(e => e.State)
                .HasColumnName("state")
                .HasColumnType("varchar(50)")
                .IsRequired();

            address.Property(e => e.Country)
                .HasColumnName("country")
                .HasColumnType("varchar(50)")
                .IsRequired();
        });
    }
}
