using System.IdentityModel.Tokens.Jwt;
using Ecocell.Api.Database;
using Microsoft.EntityFrameworkCore;

namespace Ecocell.Api.Services.CurrentUser;

/// <summary>
/// Implementação de <see cref="ICurrentUserService"/> que lê o claim <c>Sid</c> do JWT
/// via <see cref="IHttpContextAccessor"/> e consulta o banco com projeção Select.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _dbContext;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, AppDbContext dbContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken ct = default)
    {
        var sidValue = _httpContextAccessor.HttpContext?
            .User.FindFirst(JwtRegisteredClaimNames.Sid)?.Value;

        if (!Guid.TryParse(sidValue, out var userId))
            return null;

        return await _dbContext.People
            .Where(p => p.Id == userId)
            .Select(p => new CurrentUserDto
            {
                Id = p.Id,
                Email = p.Email,
                Role = p.Role,
                PersonStatus = p.PersonStatus,
                PersonType = p.PersonType,
                Journey = p.Journey
            })
            .FirstOrDefaultAsync(ct);
    }
}