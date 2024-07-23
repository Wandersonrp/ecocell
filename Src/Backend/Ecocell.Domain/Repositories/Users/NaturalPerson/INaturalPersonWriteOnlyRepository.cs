namespace Ecocell.Domain.Repositories.Users.NaturalPerson;

public interface INaturalPersonWriteOnlyRepository
{
    Task AddAsync(Entities.NaturalPerson naturalPerson);
}
