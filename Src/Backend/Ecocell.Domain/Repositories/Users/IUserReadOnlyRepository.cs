namespace Ecocell.Domain.Repositories.Users;

public interface IUserReadOnlyRepository
{
    Task<bool> ExistsWithSameEmail(string email);
    Task<bool> ExistsWithSameDocument(string document);
}
