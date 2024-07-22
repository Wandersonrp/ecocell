namespace Ecocell.Domain.Repositories.Users;

public interface IUserReadOnlyRepository
{
    Task<bool> ExistsWithSameEmail(string email);
}
