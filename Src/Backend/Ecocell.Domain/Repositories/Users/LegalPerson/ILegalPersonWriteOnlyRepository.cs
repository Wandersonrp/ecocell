namespace Ecocell.Domain.Repositories.Users.LegalPerson;

public interface ILegalPersonWriteOnlyRepository
{
    Task AddAsync(Entities.LegalPerson legalPerson);
}
