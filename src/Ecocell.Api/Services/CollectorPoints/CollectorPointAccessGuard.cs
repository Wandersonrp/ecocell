using Ecocell.Api.Database;
using Ecocell.Api.Enums;
using Ecocell.Api.Services.CurrentUser;
using Ecocell.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace Ecocell.Api.Services.CollectorPoints;

public interface ICollectorPointAccessGuard
{
    Task<Result> EnsureResponsibleActiveAsync(
        Guid collectorPointId,
        CancellationToken cancellationToken);
}

public sealed class CollectorPointAccessGuard : ICollectorPointAccessGuard
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CollectorPointAccessGuard> _logger;

    public CollectorPointAccessGuard(
        AppDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<CollectorPointAccessGuard> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result> EnsureResponsibleActiveAsync(
        Guid collectorPointId,
        CancellationToken cancellationToken)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync(cancellationToken);

        if (currentUser is null
            || currentUser.PersonType != PersonType.NaturalPerson
            || currentUser.PersonStatus != PersonStatus.Active
            || currentUser.Role != Role.User)
        {
            _logger.LogWarning("Chamador inelegível para operar recurso de Ponto de Coleta.");
            return Result.Failure(Error.Forbidden());
        }

        var status = await _dbContext.LegalPeople
            .AsNoTracking()
            .Where(legalPerson => legalPerson.Id == collectorPointId
                                  && legalPerson.Journey == Journey.CollectPoint
                                  && legalPerson.ResponsiblePersonId == currentUser.Id)
            .Select(legalPerson => (PersonStatus?)legalPerson.PersonStatus)
            .SingleOrDefaultAsync(cancellationToken);

        if (status is null)
        {
            _logger.LogWarning(
                "Ponto de Coleta {CollectorPointId} não localizado no escopo do gestor.",
                collectorPointId);
            return Result.Failure(Error.NotFound("Ponto de coleta não encontrado."));
        }

        if (status != PersonStatus.Active)
        {
            _logger.LogWarning(
                "Ponto de Coleta {CollectorPointId} sem status operacional. Status: {Status}.",
                collectorPointId,
                status);
            return Result.Failure(Error.Conflict("Ponto de coleta não está ativo."));
        }

        return Result.Success();
    }
}
