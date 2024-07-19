using Ecocell.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecocell.Infrastructure.Data.Context;

public class EcocellDbContext : DbContext
{
    public EcocellDbContext(DbContextOptions<EcocellDbContext> options) : base(options)
    {}

    public DbSet<User> Users { get; set; }

    override protected void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EcocellDbContext).Assembly);

        modelBuilder.Entity<User>(user =>
        {
            user.HasOne(e => e.Document)
                .WithOne(e => e.User)
                .HasForeignKey<Document>(e => e.DocumentId)
                .IsRequired();

            user.HasKey(e => e.UserId)
                .HasName("pk_user");

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

            user.Property(e => e.CreatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamp without time zone");                
        });

        modelBuilder.Entity<Document>(document =>
        {
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
    }
}
