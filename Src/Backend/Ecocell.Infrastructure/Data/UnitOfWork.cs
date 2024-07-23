using Ecocell.Domain.Repositories;
using Ecocell.Infrastructure.Data.Context;

namespace Ecocell.Infrastructure.Data;

public class UnitOfWork : IUnitOfWork, IDisposable
{
    private readonly EcocellDbContext _context;
    private bool _disposed;

    public UnitOfWork(EcocellDbContext context)
    {
        _context = context;
    }

    public async Task CommitAsync()
    {
        await _context.SaveChangesAsync();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _context.Dispose();
            }
        }
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
