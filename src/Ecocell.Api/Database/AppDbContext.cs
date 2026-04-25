using Microsoft.EntityFrameworkCore;

namespace Ecocell.Api.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
}
