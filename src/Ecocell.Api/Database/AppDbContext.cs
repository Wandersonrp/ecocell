using Ecocell.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ecocell.Api.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Person> People { get; set; } 
    public DbSet<NaturalPerson> NaturalPeople { get; set; }
    public DbSet<LegalPerson> LegalPeople { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
