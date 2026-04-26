using Ecocell.Api.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Ecocell.UnitTests;

public abstract class TestBase : IDisposable
{
    protected readonly AppDbContext DbContext;
    private readonly SqliteConnection _connection;

    protected TestBase()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        DbContext = new AppDbContext(options);
        DbContext.Database.EnsureCreated();
    }

    protected Mock<ILogger<T>> CreateLoggerMock<T>() => new Mock<ILogger<T>>();

    public void Dispose()
    {
        DbContext.Dispose();
        _connection.Close();
        GC.SuppressFinalize(this);
    }
}
