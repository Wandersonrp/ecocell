using Ecocell.Domain.Repositories.Users.LegalPerson;
using Ecocell.Infrastructure.Data.Context;

namespace Ecocell.Infrastructure.Data.Repositories.Users.LegalPerson;

public class LegalPersonRepository : ILegalPersonReadOnlyRepository, ILegalPersonWriteOnlyRepository
{
    private readonly EcocellDbContext _context;

    public LegalPersonRepository(EcocellDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Domain.Entities.LegalPerson legalPerson)
    {
        await _context.Users.AddAsync(legalPerson);
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
