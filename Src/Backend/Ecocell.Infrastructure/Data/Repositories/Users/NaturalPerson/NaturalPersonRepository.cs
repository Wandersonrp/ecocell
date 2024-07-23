using Ecocell.Domain.Repositories.Users.NaturalPerson;
using Ecocell.Infrastructure.Data.Context;

namespace Ecocell.Infrastructure.Data.Repositories.Users.NaturalPerson;

public class NaturalPersonRepository : INaturalPersonReadOnlyRepository, INaturalPersonWriteOnlyRepository
{
    private readonly EcocellDbContext _context;

    public NaturalPersonRepository(EcocellDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Domain.Entities.NaturalPerson naturalPerson)
    {
        await _context.Users.AddAsync(naturalPerson);
    }

    public async Task<bool> ExistsWithSameDocument(string document)
    {
        var exists = _context.Users.Any(e => e.Email.Equals(document));

        return await Task.FromResult(exists);
    }

    public async Task<bool> ExistsWithSameEmail(string email)
    {
        var exists = _context.Users.Any(e => e.Email.Equals(email));

        return await Task.FromResult(exists);
    }
}
