namespace Ecocell.Domain.Repositories;

public interface IUnitOfWork
{
    Task CommitAsync();
}
