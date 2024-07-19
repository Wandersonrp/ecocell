using Microsoft.EntityFrameworkCore;

namespace Ecocell.Infrastructure.Data.Context;

public class EcocellDbContext : DbContext
{
    public EcocellDbContext(DbContextOptions<EcocellDbContext> options) : base(options)
    {}

    override protected void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EcocellDbContext).Assembly);
    }
}
