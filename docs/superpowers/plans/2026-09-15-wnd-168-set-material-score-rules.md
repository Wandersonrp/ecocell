# US011.B — Definição das regras de pontuação pelo PC — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar a substituição completa, versionada, idempotente e concorrente-segura da tabela vigente de pontuação de um Ponto de Coleta gerenciado pela pessoa física autenticada.

**Architecture:** Um endpoint Carter PC-scoped converte o contrato Shared para um comando VSA. Um guard reutilizável resolve identidade, responsabilidade, jornada e status; o handler calcula um diff sobre as regras abertas, aplica encerramentos e inclusões em um único `SaveChangesAsync`, e traduz somente a colisão do índice aberto para `409 Conflict`.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs + Carter, Mediator, FluentValidation 12, EF Core 10, Npgsql/PostgreSQL, SQLite in-memory, xUnit, Shouldly, Moq, Bogus e Testcontainers.

**Spec:** `docs/superpowers/specs/2026-09-15-wnd-168-set-material-score-rules-design.md`

## Global Constraints

- Aplicar TDD Red → Green → Refactor em cada task; nenhum código de produção nasce antes do teste que exige seu comportamento.
- Manter VSA: comando, validator, handler e endpoint da escrita em `SetMaterialScoreRules.cs`.
- Usar diretamente `AppDbContext`; não criar repository, pipeline, middleware, lock distribuído ou dependência externa.
- Rota exata: `PUT /api/collector-points/{collectorPointId:guid}/score-rules`.
- Sucesso exato: `204 No Content`, inclusive no-op idempotente.
- Payload representa a tabela vigente completa; lista vazia permanece inválida.
- Reusar `MaterialScoreRule`, `ElectronicMaterial`, `MaterialScoreUnit`, `Close(DateTime)` e o índice já entregues pela US011.A.
- Não alterar entidade, enums, migration, snapshot, consulta US011.C, Mobile US011.D ou cálculo de pontos.
- Preservar `404` para PC inexistente, jornada errada ou outro responsável; `403` para chamador inelegível; `409` para PC próprio inativo, regressão de relógio ou colisão concorrente.
- Capturar um único instante UTC por lote alterado; igualdade com o maior `ValidFrom` fechado avança 10 ticks, equivalentes a 1 microssegundo; relógio anterior retorna `409` sem escrita.
- Traduzir somente `PostgresErrorCodes.UniqueViolation` da constraint `IX_MaterialScoreRules_LegalPersonId_Material`; qualquer outra falha de persistência deve continuar excepcional.
- Não adicionar pacote NuGet. `TimeProvider` vem da BCL e Npgsql já é dependência da API.
- Os arquivos `.Codex/rules/*.md` citados pelo `AGENTS.md` estão ausentes neste checkout. Se existirem no início da execução, lê-los integralmente antes de editar e aplicar suas regras; se continuarem ausentes, seguir `AGENTS.md`, a spec e os padrões existentes.
- O worktree já contém mudanças não relacionadas, inclusive em `DependencyInjectionExtensions.cs`. Nunca usar staging amplo. Em arquivo sobreposto, revisar o diff base e adicionar somente os hunks desta task.
- Usar RTK para Git, busca, build e testes quando suportado.

## Execution Preflight

- [ ] Confirmar branch, spec e dependências da US011.A.

```powershell
rtk git status --short
rtk git log -8 --oneline
rtk rg -n "class MaterialScoreRule|enum ElectronicMaterial|enum MaterialScoreUnit|DbSet<MaterialScoreRule>" src tests
Get-Content -Raw docs/superpowers/specs/2026-09-15-wnd-168-set-material-score-rules-design.md
```

Expected: branch `feature/descarte-qr-code`; spec presente no commit `0810a44`; entidade, enums, `DbSet`, índice e migration da US011.A presentes. Interromper antes do RED se algum artefato estiver ausente ou tiver assinatura incompatível.

- [ ] Registrar o diff preexistente dos arquivos que serão modificados.

```powershell
rtk git diff -- src/Ecocell.Api/Extensions/DependencyInjectionExtensions.cs
rtk git diff -- src/Ecocell.Api/Features/CollectorPoint/GeneratePcQrCode.cs
rtk git diff -- tests/Ecocell.UnitTests/Features/CollectorPoint/GeneratePcQrCodeTests.cs
rtk git diff -- tests/Ecocell.IntegrationTests/IntegrationTestFixture.cs
rtk git diff -- tests/Ecocell.IntegrationTests/Features/CollectorPoint/GeneratePcQrCodeTests.cs
```

Expected: identificar e preservar qualquer mudança do usuário. Não prosseguir se houver sobreposição sem uma separação segura de hunks.

- [ ] Executar baseline unitário.

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore
```

Expected: PASS. Se MSBuild falhar com `UnauthorizedAccessException` em `MSBuildTemp`, repetir o mesmo comando com execução elevada; não tratar bloqueio do sandbox como falha de produto.

---

### Task 1: Contrato Shared e validator do comando

**Files:**

- Create: `src/Ecocell.Shared/Requests/CollectorPoints/RequestSetMaterialScoreRulesJson.cs`
- Create: `src/Ecocell.Api/Features/CollectorPoint/SetMaterialScoreRules.cs`
- Create: `tests/Ecocell.UnitTests/Features/CollectorPoint/SetMaterialScoreRulesValidatorTests.cs`

**Interfaces:**

- Consumes: `Ecocell.Shared.Enums.ElectronicMaterial`, `Ecocell.Shared.Enums.MaterialScoreUnit`, `Ecocell.Api.Enums.ElectronicMaterial`, `Ecocell.Api.Enums.MaterialScoreUnit`, `Mediator.IRequest<Result>`.
- Produces: `RequestSetMaterialScoreRulesJson`, `RequestMaterialScoreRuleJson`, `SetMaterialScoreRules.RuleInput`, `SetMaterialScoreRules.Command` e `SetMaterialScoreRules.Validator`.

- [ ] **Step 1: Criar os testes RED do validator**

Criar `tests/Ecocell.UnitTests/Features/CollectorPoint/SetMaterialScoreRulesValidatorTests.cs`:

```csharp
using Ecocell.Api.Enums;
using Ecocell.Api.Features.CollectorPoint;
using Shouldly;

namespace Ecocell.UnitTests.Features.CollectorPoint;

public class SetMaterialScoreRulesValidatorTests
{
    private readonly SetMaterialScoreRules.Validator _validator = new();

    public static TheoryData<decimal> InvalidPoints => new()
    {
        0m,
        -1m,
        100_000_000m,
        1.001m,
    };

    [Fact]
    public async Task Validate_ShouldSucceed_WhenCommandIsValid()
    {
        var result = await _validator.ValidateAsync(ValidCommand());

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenCollectorPointIdIsEmpty()
    {
        var command = ValidCommand() with { CollectorPointId = Guid.Empty };

        var result = await _validator.ValidateAsync(command);

        result.Errors.ShouldContain(x => x.PropertyName == nameof(SetMaterialScoreRules.Command.CollectorPointId));
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenRulesIsNull()
    {
        var command = ValidCommand() with { Rules = null };

        var result = await _validator.ValidateAsync(command);

        result.Errors.ShouldContain(x => x.ErrorMessage == "Informe de 1 a 7 regras de pontuação.");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenRulesIsEmpty()
    {
        var command = ValidCommand() with { Rules = [] };

        var result = await _validator.ValidateAsync(command);

        result.Errors.ShouldContain(x => x.ErrorMessage == "Informe de 1 a 7 regras de pontuação.");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenRulesHasMoreThanSevenItems()
    {
        var rules = Enumerable.Range(0, 8)
            .Select(index => new SetMaterialScoreRules.RuleInput(
                (ElectronicMaterial)((index % 7) + 1),
                index + 1,
                MaterialScoreUnit.PerUnit))
            .ToList();
        var command = new SetMaterialScoreRules.Command(Guid.NewGuid(), rules);

        var result = await _validator.ValidateAsync(command);

        result.Errors.ShouldContain(x => x.ErrorMessage == "Informe de 1 a 7 regras de pontuação.");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenMaterialIsDuplicated()
    {
        var duplicate = new SetMaterialScoreRules.RuleInput(
            ElectronicMaterial.Battery,
            20m,
            MaterialScoreUnit.PerKilogram);
        var command = ValidCommand() with { Rules = [ValidRule(), duplicate] };

        var result = await _validator.ValidateAsync(command);

        result.Errors.ShouldContain(x => x.ErrorMessage == "Cada material pode aparecer somente uma vez.");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenMaterialIsUndefined()
    {
        var command = ValidCommand() with
        {
            Rules = [ValidRule() with { Material = (ElectronicMaterial)999 }],
        };

        var result = await _validator.ValidateAsync(command);

        result.Errors.ShouldContain(x => x.ErrorMessage == "O material informado é inválido.");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenUnitIsUndefined()
    {
        var command = ValidCommand() with
        {
            Rules = [ValidRule() with { Unit = (MaterialScoreUnit)999 }],
        };

        var result = await _validator.ValidateAsync(command);

        result.Errors.ShouldContain(x => x.ErrorMessage == "A unidade de pontuação informada é inválida.");
    }

    [Theory]
    [MemberData(nameof(InvalidPoints))]
    public async Task Validate_ShouldFail_WhenPointsDoesNotFitNumericTenTwo(decimal points)
    {
        var command = ValidCommand() with { Rules = [ValidRule() with { Points = points }] };

        var result = await _validator.ValidateAsync(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(x => x.PropertyName.EndsWith(nameof(SetMaterialScoreRules.RuleInput.Points)));
    }

    private static SetMaterialScoreRules.Command ValidCommand() =>
        new(Guid.NewGuid(), [ValidRule()]);

    private static SetMaterialScoreRules.RuleInput ValidRule() =>
        new(ElectronicMaterial.Battery, 10.50m, MaterialScoreUnit.PerUnit);
}
```

- [ ] **Step 2: Executar RED**

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~SetMaterialScoreRulesValidatorTests"
```

Expected: FAIL de compilação porque `SetMaterialScoreRules` ainda não existe.

- [ ] **Step 3: Criar o contrato Shared mínimo**

Criar `src/Ecocell.Shared/Requests/CollectorPoints/RequestSetMaterialScoreRulesJson.cs`:

```csharp
using Ecocell.Shared.Enums;

namespace Ecocell.Shared.Requests.CollectorPoints;

/// <summary>Substitui a tabela vigente de pontuação de um Ponto de Coleta.</summary>
public sealed record RequestSetMaterialScoreRulesJson
{
    public List<RequestMaterialScoreRuleJson> Rules { get; init; } = [];
}

/// <summary>Define o valor vigente de um material aceito.</summary>
public sealed record RequestMaterialScoreRuleJson
{
    public ElectronicMaterial Material { get; init; }
    public decimal Points { get; init; }
    public MaterialScoreUnit Unit { get; init; }
}
```

- [ ] **Step 4: Criar comando e validator mínimos**

Criar `src/Ecocell.Api/Features/CollectorPoint/SetMaterialScoreRules.cs`:

```csharp
using Ecocell.Api.Enums;
using Ecocell.Api.Shared;
using FluentValidation;
using Mediator;

namespace Ecocell.Api.Features.CollectorPoint;

public static class SetMaterialScoreRules
{
    private const int MaximumRules = 7;

    public sealed record RuleInput(
        ElectronicMaterial Material,
        decimal Points,
        MaterialScoreUnit Unit);

    public sealed record Command(
        Guid CollectorPointId,
        IReadOnlyList<RuleInput>? Rules) : IRequest<Result>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.CollectorPointId)
                .NotEmpty()
                .WithMessage("O identificador do ponto de coleta é obrigatório.");

            RuleFor(x => x.Rules)
                .NotNull()
                .Must(rules => rules is { Count: >= 1 and <= MaximumRules })
                .WithMessage("Informe de 1 a 7 regras de pontuação.");

            When(x => x.Rules is not null, () =>
            {
                RuleFor(x => x.Rules!)
                    .Must(rules => rules.DistinctBy(rule => rule.Material).Count() == rules.Count)
                    .WithMessage("Cada material pode aparecer somente uma vez.");

                RuleForEach(x => x.Rules!).ChildRules(rule =>
                {
                    rule.RuleFor(x => x.Material)
                        .IsInEnum()
                        .WithMessage("O material informado é inválido.");

                    rule.RuleFor(x => x.Points)
                        .GreaterThan(0m)
                        .WithMessage("A pontuação deve ser maior que zero.")
                        .PrecisionScale(10, 2, false)
                        .WithMessage("A pontuação deve ter até 10 dígitos totais e 2 casas decimais.");

                    rule.RuleFor(x => x.Unit)
                        .IsInEnum()
                        .WithMessage("A unidade de pontuação informada é inválida.");
                });
            });
        }
    }
}
```

- [ ] **Step 5: Executar GREEN focado**

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~SetMaterialScoreRulesValidatorTests"
```

Expected: todos os testes de `SetMaterialScoreRulesValidatorTests` passam.

- [ ] **Step 6: Revisar contrato e validator**

```powershell
rtk rg -n "RequestSetMaterialScoreRulesJson|RequestMaterialScoreRuleJson|MaximumRules|PrecisionScale|DistinctBy" src tests
rtk git diff --check -- src/Ecocell.Shared/Requests/CollectorPoints/RequestSetMaterialScoreRulesJson.cs src/Ecocell.Api/Features/CollectorPoint/SetMaterialScoreRules.cs tests/Ecocell.UnitTests/Features/CollectorPoint/SetMaterialScoreRulesValidatorTests.cs
```

Expected: tipos públicos usam enums Shared; comando usa enums internos; nenhuma dependência nova; diff sem erro de whitespace.

- [ ] **Step 7: Commit seletivo**

```powershell
rtk git add -- src/Ecocell.Shared/Requests/CollectorPoints/RequestSetMaterialScoreRulesJson.cs src/Ecocell.Api/Features/CollectorPoint/SetMaterialScoreRules.cs tests/Ecocell.UnitTests/Features/CollectorPoint/SetMaterialScoreRulesValidatorTests.cs
rtk git diff --cached --check
rtk git diff --cached --name-only
rtk git commit -m "feat(score): adiciona contrato de definicao das regras"
```

Expected no staging: somente os três arquivos da Task 1.

---

### Task 2: Guard reutilizável de acesso PC-scoped

**Files:**

- Create: `src/Ecocell.Api/Services/CollectorPoints/CollectorPointAccessGuard.cs`
- Create: `tests/Ecocell.UnitTests/Services/CollectorPoints/CollectorPointAccessGuardTests.cs`
- Modify: `src/Ecocell.Api/Extensions/DependencyInjectionExtensions.cs:1-53`

**Interfaces:**

- Consumes: `ICurrentUserService.GetCurrentUserAsync(CancellationToken)`, `AppDbContext.LegalPeople`, `Result`, `Error`, `Journey.CollectPoint`, `PersonType.NaturalPerson`, `PersonStatus.Active`, `Role.User`.
- Produces: `ICollectorPointAccessGuard.EnsureResponsibleActiveAsync(Guid, CancellationToken)` e implementação scoped `CollectorPointAccessGuard`.

- [ ] **Step 1: Criar os testes RED do guard**

Criar `tests/Ecocell.UnitTests/Services/CollectorPoints/CollectorPointAccessGuardTests.cs` com fixture SQLite, usuário atual mockado e os cenários abaixo:

```csharp
using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Services.CollectorPoints;
using Ecocell.Api.Services.CurrentUser;
using Ecocell.Api.Shared;
using Moq;
using Shouldly;

namespace Ecocell.UnitTests.Services.CollectorPoints;

public class CollectorPointAccessGuardTests : TestBase
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly CollectorPointAccessGuard _guard;
    private readonly Guid _responsibleId;

    public CollectorPointAccessGuardTests()
    {
        _responsibleId = AddNaturalPerson();
        SetCurrentUser(_responsibleId, Role.User, PersonStatus.Active, PersonType.NaturalPerson);
        _guard = new CollectorPointAccessGuard(
            DbContext,
            _currentUser.Object,
            CreateLoggerMock<CollectorPointAccessGuard>().Object);
    }

    [Fact]
    public async Task EnsureResponsibleActiveAsync_ShouldSucceed_WhenCallerOwnsActiveCollectorPoint()
    {
        var collectorPoint = AddLegalPerson(_responsibleId, Journey.CollectPoint, PersonStatus.Active);

        var result = await _guard.EnsureResponsibleActiveAsync(collectorPoint.Id, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureResponsibleActiveAsync_ShouldReturnForbidden_WhenCurrentUserIsMissing()
    {
        _currentUser.Setup(x => x.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrentUserDto?)null);

        var result = await _guard.EnsureResponsibleActiveAsync(Guid.NewGuid(), CancellationToken.None);

        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }

    [Theory]
    [InlineData(Role.Admin, PersonStatus.Active, PersonType.NaturalPerson)]
    [InlineData(Role.Support, PersonStatus.Active, PersonType.NaturalPerson)]
    [InlineData(Role.User, PersonStatus.Suspended, PersonType.NaturalPerson)]
    [InlineData(Role.User, PersonStatus.Active, PersonType.LegalPerson)]
    public async Task EnsureResponsibleActiveAsync_ShouldReturnForbidden_WhenCallerIsIneligible(
        Role role,
        PersonStatus status,
        PersonType personType)
    {
        SetCurrentUser(_responsibleId, role, status, personType);

        var result = await _guard.EnsureResponsibleActiveAsync(Guid.NewGuid(), CancellationToken.None);

        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }

    [Fact]
    public async Task EnsureResponsibleActiveAsync_ShouldReturnNotFound_WhenCollectorPointDoesNotExist()
    {
        var result = await _guard.EnsureResponsibleActiveAsync(Guid.NewGuid(), CancellationToken.None);

        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task EnsureResponsibleActiveAsync_ShouldReturnNotFound_WhenLegalPersonHasAnotherJourney()
    {
        var collector = AddLegalPerson(_responsibleId, Journey.Collector, PersonStatus.Active);

        var result = await _guard.EnsureResponsibleActiveAsync(collector.Id, CancellationToken.None);

        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task EnsureResponsibleActiveAsync_ShouldReturnNotFound_WhenAnotherPersonOwnsCollectorPoint()
    {
        var collectorPoint = AddLegalPerson(AddNaturalPerson(), Journey.CollectPoint, PersonStatus.Active);

        var result = await _guard.EnsureResponsibleActiveAsync(collectorPoint.Id, CancellationToken.None);

        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    }

    [Theory]
    [InlineData(PersonStatus.PendingApproval)]
    [InlineData(PersonStatus.Suspended)]
    [InlineData(PersonStatus.Refused)]
    [InlineData(PersonStatus.Inactive)]
    [InlineData(PersonStatus.AwaitingConfirmation)]
    public async Task EnsureResponsibleActiveAsync_ShouldReturnConflict_WhenOwnedCollectorPointIsNotActive(
        PersonStatus status)
    {
        var collectorPoint = AddLegalPerson(_responsibleId, Journey.CollectPoint, status);

        var result = await _guard.EnsureResponsibleActiveAsync(collectorPoint.Id, CancellationToken.None);

        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    }

    private void SetCurrentUser(Guid id, Role role, PersonStatus status, PersonType personType)
    {
        _currentUser.Setup(x => x.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentUserDto
            {
                Id = id,
                Role = role,
                PersonStatus = status,
                PersonType = personType,
                Journey = Journey.Depositor,
                Email = new Faker().Internet.Email(),
            });
    }

    private Guid AddNaturalPerson()
    {
        var faker = new Faker("pt_BR");
        var person = new NaturalPerson(
            faker.Name.FullName(),
            faker.Person.Cpf(includeFormatSymbols: false),
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)),
            Role.User,
            faker.Internet.Email(),
            Journey.Depositor);
        DbContext.NaturalPeople.Add(person);
        DbContext.SaveChanges();
        return person.Id;
    }

    private LegalPerson AddLegalPerson(Guid responsibleId, Journey journey, PersonStatus status)
    {
        var faker = new Faker("pt_BR");
        var legalPerson = new LegalPerson(
            faker.Company.CompanyName(),
            faker.Company.CompanyName(),
            faker.Company.Cnpj(includeFormatSymbols: false),
            faker.Internet.Email(),
            journey,
            responsiblePersonId: responsibleId);
        DbContext.LegalPeople.Add(legalPerson);

        if (status == PersonStatus.Active)
            legalPerson.Approve(Role.Admin);
        else if (status == PersonStatus.Refused)
            legalPerson.Reject(Role.Admin);
        else if (status == PersonStatus.Suspended)
        {
            legalPerson.Approve(Role.Admin);
            legalPerson.Block(Role.Admin);
        }
        else if (status != PersonStatus.PendingApproval)
            DbContext.Entry(legalPerson).Property(x => x.PersonStatus).CurrentValue = status;

        DbContext.SaveChanges();
        return legalPerson;
    }
}
```

- [ ] **Step 2: Executar RED**

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~CollectorPointAccessGuardTests"
```

Expected: FAIL de compilação porque o guard ainda não existe.

- [ ] **Step 3: Implementar o guard mínimo**

Criar `src/Ecocell.Api/Services/CollectorPoints/CollectorPointAccessGuard.cs`:

```csharp
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
```

- [ ] **Step 4: Registrar guard e relógio no DI**

Adicionar o namespace:

```csharp
using Ecocell.Api.Services.CollectorPoints;
```

Após o registro de `ICurrentUserService`, adicionar somente estas linhas:

```csharp
services.AddScoped<ICollectorPointAccessGuard, CollectorPointAccessGuard>();
services.AddSingleton(TimeProvider.System);
```

Não reformatar nem reordenar o restante de `DependencyInjectionExtensions.cs`.

- [ ] **Step 5: Executar GREEN focado e suíte unitária**

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~CollectorPointAccessGuardTests"
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore
```

Expected: guard e suíte unitária completos passam.

- [ ] **Step 6: Revisar a consulta anti-enumeração e o diff sobreposto**

```powershell
rtk rg -n "ResponsiblePersonId|Journey.CollectPoint|PersonStatus.Active|Role.User|AsNoTracking" src/Ecocell.Api/Services/CollectorPoints/CollectorPointAccessGuard.cs
rtk git diff -- src/Ecocell.Api/Extensions/DependencyInjectionExtensions.cs
rtk git diff --check -- src/Ecocell.Api/Services/CollectorPoints/CollectorPointAccessGuard.cs src/Ecocell.Api/Extensions/DependencyInjectionExtensions.cs tests/Ecocell.UnitTests/Services/CollectorPoints/CollectorPointAccessGuardTests.cs
```

Expected: existência, jornada e dono aparecem na mesma consulta; status é verificado depois; o diff de DI mantém todas as mudanças preexistentes do usuário.

- [ ] **Step 7: Commit seletivo sem capturar mudanças antigas do DI**

```powershell
rtk git add -- src/Ecocell.Api/Services/CollectorPoints/CollectorPointAccessGuard.cs tests/Ecocell.UnitTests/Services/CollectorPoints/CollectorPointAccessGuardTests.cs
git add -p -- src/Ecocell.Api/Extensions/DependencyInjectionExtensions.cs
rtk git diff --cached --check
rtk git diff --cached -- src/Ecocell.Api/Extensions/DependencyInjectionExtensions.cs
rtk git diff --cached --name-only
rtk git commit -m "feat(auth): adiciona guard de acesso a ponto de coleta"
```

Ao executar `git add -p`, selecionar somente o novo `using Ecocell.Api.Services.CollectorPoints;` e os registros de `ICollectorPointAccessGuard` e `TimeProvider`. Expected no staging: guard, teste do guard e somente esses hunks de DI.

---


### Task 3: Migrar `GeneratePcQrCode` para o guard

**Files:**

- Modify: `src/Ecocell.Api/Features/CollectorPoint/GeneratePcQrCode.cs:1-84`
- Modify: `tests/Ecocell.UnitTests/Features/CollectorPoint/GeneratePcQrCodeTests.cs:1-188`
- Verify: `tests/Ecocell.IntegrationTests/Features/CollectorPoint/GeneratePcQrCodeTests.cs`

**Interfaces:**

- Consumes: `ICollectorPointAccessGuard.EnsureResponsibleActiveAsync(Guid, CancellationToken)` da Task 2.
- Produces: `GeneratePcQrCode.Handler` dependente somente de logger, validator e guard; contrato HTTP do QR inalterado.

- [ ] **Step 1: Reescrever testes unitários para exigir o guard**

Substituir `tests/Ecocell.UnitTests/Features/CollectorPoint/GeneratePcQrCodeTests.cs` por testes focados na responsabilidade própria do slice:

```csharp
using Ecocell.Api.Features.CollectorPoint;
using Ecocell.Api.Services.CollectorPoints;
using Ecocell.Api.Shared;
using Moq;
using Shouldly;

namespace Ecocell.UnitTests.Features.CollectorPoint;

public class GeneratePcQrCodeTests : TestBase
{
    private readonly Mock<ICollectorPointAccessGuard> _guard = new();
    private readonly GeneratePcQrCode.Handler _handler;

    public static TheoryData<Error> AccessErrors => new()
    {
        Error.Forbidden(),
        Error.NotFound("Ponto de coleta não encontrado."),
        Error.Conflict("Ponto de coleta não está ativo."),
    };

    public GeneratePcQrCodeTests()
    {
        _guard.Setup(x => x.EnsureResponsibleActiveAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _handler = new GeneratePcQrCode.Handler(
            CreateLoggerMock<GeneratePcQrCode.Handler>().Object,
            new GeneratePcQrCode.Validator(),
            _guard.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnQrString_WhenAccessGuardSucceeds()
    {
        var collectorPointId = Guid.NewGuid();

        var result = await _handler.Handle(
            new GeneratePcQrCode.Query(collectorPointId),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Qr.ShouldBe($"ecocell://pc/{collectorPointId}");
        _guard.Verify(x => x.EnsureResponsibleActiveAsync(
            collectorPointId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [MemberData(nameof(AccessErrors))]
    public async Task Handle_ShouldPropagateAccessError_WhenGuardFails(Error error)
    {
        _guard.Setup(x => x.EnsureResponsibleActiveAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(error));

        var result = await _handler.Handle(
            new GeneratePcQrCode.Query(Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationErrorWithoutCallingGuard_WhenIdIsEmpty()
    {
        var result = await _handler.Handle(
            new GeneratePcQrCode.Query(Guid.Empty),
            CancellationToken.None);

        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
        _guard.Verify(x => x.EnsureResponsibleActiveAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

Excluir setup de `AppDbContext`, `ICurrentUserService`, pessoas e PJs desse arquivo; esses cenários agora pertencem aos testes do guard.

- [ ] **Step 2: Executar RED**

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~GeneratePcQrCodeTests"
```

Expected: FAIL de compilação porque o construtor atual do handler ainda exige `AppDbContext` e `ICurrentUserService`.

- [ ] **Step 3: Reduzir o handler para validator + guard + composição do QR**

Em `GeneratePcQrCode.cs`, remover `AppDbContext`, `ICurrentUserService`, enums e EF Core. Manter endpoint e validator. O handler deve ficar com este comportamento:

```csharp
public sealed class Handler : IRequestHandler<Query, ResultT<ResponseCollectorPointQrCode>>
{
    private readonly ILogger<Handler> _logger;
    private readonly IValidator<Query> _validator;
    private readonly ICollectorPointAccessGuard _accessGuard;

    public Handler(
        ILogger<Handler> logger,
        IValidator<Query> validator,
        ICollectorPointAccessGuard accessGuard)
    {
        _logger = logger;
        _validator = validator;
        _accessGuard = accessGuard;
    }

    public async ValueTask<ResultT<ResponseCollectorPointQrCode>> Handle(
        Query request,
        CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            var messages = validation.Errors.Select(error => error.ErrorMessage).ToList();
            return ResultT<ResponseCollectorPointQrCode>.Failure(Error.ErrorOnValidation(messages));
        }

        var access = await _accessGuard.EnsureResponsibleActiveAsync(
            request.CollectorPointId,
            cancellationToken);
        if (access.IsFailure)
        {
            _logger.LogWarning(
                "Acesso negado ao gerar QR do Ponto de Coleta {CollectorPointId}. Código: {Code}.",
                request.CollectorPointId,
                access.Error.Code);
            return ResultT<ResponseCollectorPointQrCode>.Failure(access.Error);
        }

        return ResultT<ResponseCollectorPointQrCode>.Success(
            new ResponseCollectorPointQrCode
            {
                Qr = $"ecocell://pc/{request.CollectorPointId}",
            });
    }
}
```

Adicionar `using Ecocell.Api.Services.CollectorPoints;`. Não alterar `GeneratePcQrCodeEndpoint`.

- [ ] **Step 4: Executar GREEN unitário e regressão HTTP do QR**

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~GeneratePcQrCodeTests"
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~GeneratePcQrCodeTests"
```

Expected: testes unitários e integração existentes do QR passam sem alteração de rota, corpo ou status.

- [ ] **Step 5: Refactor e verificação de duplicidade removida**

```powershell
rtk rg -n "ICurrentUserService|ResponsiblePersonId|Journey.CollectPoint|PersonStatus.Active|AppDbContext" src/Ecocell.Api/Features/CollectorPoint/GeneratePcQrCode.cs
rtk rg -n "ICollectorPointAccessGuard" src/Ecocell.Api/Features/CollectorPoint/GeneratePcQrCode.cs tests/Ecocell.UnitTests/Features/CollectorPoint/GeneratePcQrCodeTests.cs
rtk git diff --check -- src/Ecocell.Api/Features/CollectorPoint/GeneratePcQrCode.cs tests/Ecocell.UnitTests/Features/CollectorPoint/GeneratePcQrCodeTests.cs
```

Expected: primeira busca não encontra autorização duplicada; segunda encontra guard no handler e no teste.

- [ ] **Step 6: Commit seletivo**

```powershell
rtk git add -- src/Ecocell.Api/Features/CollectorPoint/GeneratePcQrCode.cs tests/Ecocell.UnitTests/Features/CollectorPoint/GeneratePcQrCodeTests.cs
rtk git diff --cached --check
rtk git diff --cached --name-only
rtk git commit -m "refactor(qr): reutiliza guard de ponto de coleta"
```

Expected no staging: somente handler QR e seu teste unitário. O teste de integração permanece sem alteração se o contrato foi preservado.

---

### Task 4: Handler de diff temporal e persistência atômica

**Files:**

- Modify: `src/Ecocell.Api/Features/CollectorPoint/SetMaterialScoreRules.cs`
- Create: `tests/Ecocell.UnitTests/Features/CollectorPoint/SetMaterialScoreRulesTests.cs`

**Interfaces:**

- Consumes: `SetMaterialScoreRules.Command`, `SetMaterialScoreRules.Validator`, `ICollectorPointAccessGuard`, `TimeProvider`, `MaterialScoreRule.Close(DateTime)`, `AppDbContext.MaterialScoreRules` e `Result`.
- Produces: `SetMaterialScoreRules.Handler.Handle(Command, CancellationToken)`; constante privada `OpenRuleIndexName = "IX_MaterialScoreRules_LegalPersonId_Material"` e filtro privado da violação PostgreSQL.

- [ ] **Step 1: Criar fixture e RED de criação inicial**

Criar `tests/Ecocell.UnitTests/Features/CollectorPoint/SetMaterialScoreRulesTests.cs` com o primeiro teste e os helpers:

```csharp
using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Features.CollectorPoint;
using Ecocell.Api.Services.CollectorPoints;
using Ecocell.Api.Shared;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Ecocell.UnitTests.Features.CollectorPoint;

public class SetMaterialScoreRulesTests : TestBase
{
    private static readonly DateTime UtcNow = new(2026, 9, 15, 15, 0, 0, DateTimeKind.Utc);
    private readonly Mock<ICollectorPointAccessGuard> _guard = new();

    public SetMaterialScoreRulesTests()
    {
        _guard.Setup(x => x.EnsureResponsibleActiveAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
    }

    [Fact]
    public async Task Handle_ShouldCreateCurrentRules_WhenCollectorPointHasNoRules()
    {
        var collectorPoint = AddCollectorPoint();
        var handler = CreateHandler(UtcNow);
        var command = Command(
            collectorPoint.Id,
            Rule(ElectronicMaterial.Battery, 10m, MaterialScoreUnit.PerUnit),
            Rule(ElectronicMaterial.Notebook, 25m, MaterialScoreUnit.PerKilogram));

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var rules = await DbContext.MaterialScoreRules.OrderBy(x => x.Material).ToListAsync();
        rules.Count.ShouldBe(2);
        rules.ShouldAllBe(x => x.ValidFrom == UtcNow && x.ValidTo is null);
    }

    private SetMaterialScoreRules.Handler CreateHandler(DateTime utcNow) =>
        new(
            DbContext,
            CreateLoggerMock<SetMaterialScoreRules.Handler>().Object,
            new SetMaterialScoreRules.Validator(),
            _guard.Object,
            new FixedTimeProvider(new DateTimeOffset(utcNow)));

    private LegalPerson AddCollectorPoint()
    {
        var faker = new Faker("pt_BR");
        var collectorPoint = new LegalPerson(
            faker.Company.CompanyName(),
            faker.Company.CompanyName(),
            faker.Company.Cnpj(includeFormatSymbols: false),
            faker.Internet.Email(),
            Journey.CollectPoint);
        DbContext.LegalPeople.Add(collectorPoint);
        DbContext.SaveChanges();
        return collectorPoint;
    }

    private MaterialScoreRule AddRule(
        Guid collectorPointId,
        ElectronicMaterial material,
        decimal points,
        MaterialScoreUnit unit,
        DateTime validFrom)
    {
        var rule = new MaterialScoreRule(collectorPointId, material, points, unit, validFrom);
        DbContext.MaterialScoreRules.Add(rule);
        DbContext.SaveChanges();
        return rule;
    }

    private static SetMaterialScoreRules.Command Command(
        Guid collectorPointId,
        params SetMaterialScoreRules.RuleInput[] rules) =>
        new(collectorPointId, rules);

    private static SetMaterialScoreRules.RuleInput Rule(
        ElectronicMaterial material,
        decimal points,
        MaterialScoreUnit unit) =>
        new(material, points, unit);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
```

- [ ] **Step 2: Executar RED de criação**

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~SetMaterialScoreRulesTests.Handle_ShouldCreateCurrentRules"
```

Expected: FAIL de compilação porque `SetMaterialScoreRules.Handler` ainda não existe.

- [ ] **Step 3: Implementar o handler mínimo para criação inicial**

Adicionar ao static class `SetMaterialScoreRules`, depois do validator, um handler que valida, executa o guard, carrega regras abertas e cria itens ausentes. Usar as dependências e assinatura completas agora para evitar retrabalho:

```csharp
public sealed class Handler : IRequestHandler<Command, Result>
{
    private const string OpenRuleIndexName = "IX_MaterialScoreRules_LegalPersonId_Material";
    private readonly AppDbContext _dbContext;
    private readonly ILogger<Handler> _logger;
    private readonly IValidator<Command> _validator;
    private readonly ICollectorPointAccessGuard _accessGuard;
    private readonly TimeProvider _timeProvider;

    public Handler(
        AppDbContext dbContext,
        ILogger<Handler> logger,
        IValidator<Command> validator,
        ICollectorPointAccessGuard accessGuard,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _logger = logger;
        _validator = validator;
        _accessGuard = accessGuard;
        _timeProvider = timeProvider;
    }

    public async ValueTask<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(Error.ErrorOnValidation(
                validation.Errors.Select(error => error.ErrorMessage).ToList()));
        }

        var access = await _accessGuard.EnsureResponsibleActiveAsync(
            request.CollectorPointId,
            cancellationToken);
        if (access.IsFailure)
            return access;

        var currentRules = await _dbContext.MaterialScoreRules
            .Where(rule => rule.LegalPersonId == request.CollectorPointId
                           && rule.ValidTo == null)
            .ToListAsync(cancellationToken);
        var requestedRules = request.Rules!.ToDictionary(rule => rule.Material);
        var effectiveAt = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var requested in requestedRules.Values)
        {
            if (currentRules.All(current => current.Material != requested.Material))
            {
                _dbContext.MaterialScoreRules.Add(new MaterialScoreRule(
                    request.CollectorPointId,
                    requested.Material,
                    requested.Points,
                    requested.Unit,
                    effectiveAt));
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

Adicionar os `using` necessários: `Ecocell.Api.Database`, `Ecocell.Api.Entities`, `Ecocell.Api.Services.CollectorPoints`, `Microsoft.EntityFrameworkCore` e `Npgsql`. O `using Npgsql` será consumido no passo de concorrência desta task.

- [ ] **Step 4: Executar GREEN de criação**

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~SetMaterialScoreRulesTests.Handle_ShouldCreateCurrentRules"
```

Expected: PASS.

- [ ] **Step 5: Adicionar RED para diff misto, idempotência e histórico**

Adicionar estes testes ao mesmo arquivo:

```csharp
[Fact]
public async Task Handle_ShouldApplyMixedDiffAndPreserveHistory_WhenTableChanges()
{
    var collectorPoint = AddCollectorPoint();
    var validFrom = UtcNow.AddHours(-1);
    var battery = AddRule(collectorPoint.Id, ElectronicMaterial.Battery, 10m, MaterialScoreUnit.PerUnit, validFrom);
    var notebook = AddRule(collectorPoint.Id, ElectronicMaterial.Notebook, 20m, MaterialScoreUnit.PerUnit, validFrom);
    var printer = AddRule(collectorPoint.Id, ElectronicMaterial.Printer, 30m, MaterialScoreUnit.PerUnit, validFrom);
    var handler = CreateHandler(UtcNow);
    var command = Command(
        collectorPoint.Id,
        Rule(ElectronicMaterial.Battery, 10m, MaterialScoreUnit.PerUnit),
        Rule(ElectronicMaterial.Notebook, 25m, MaterialScoreUnit.PerKilogram),
        Rule(ElectronicMaterial.CellPhone, 15m, MaterialScoreUnit.PerUnit));

    var result = await handler.Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DbContext.ChangeTracker.Clear();
    var allRules = await DbContext.MaterialScoreRules
        .Where(x => x.LegalPersonId == collectorPoint.Id)
        .ToListAsync();
    allRules.Count.ShouldBe(5);
    allRules.Count(x => x.ValidTo is null).ShouldBe(3);
    allRules.Single(x => x.Id == battery.Id).ValidTo.ShouldBeNull();
    allRules.Single(x => x.Id == notebook.Id).ValidTo.ShouldBe(UtcNow);
    allRules.Single(x => x.Id == printer.Id).ValidTo.ShouldBe(UtcNow);
    allRules.Single(x => x.Material == ElectronicMaterial.Notebook && x.ValidTo is null)
        .ValidFrom.ShouldBe(UtcNow);
    allRules.Single(x => x.Material == ElectronicMaterial.CellPhone && x.ValidTo is null)
        .ValidFrom.ShouldBe(UtcNow);
}

[Fact]
public async Task Handle_ShouldNotCreateHistory_WhenPayloadMatchesCurrentRules()
{
    var collectorPoint = AddCollectorPoint();
    var existing = AddRule(
        collectorPoint.Id,
        ElectronicMaterial.Battery,
        10m,
        MaterialScoreUnit.PerUnit,
        UtcNow.AddHours(-1));
    var handler = CreateHandler(UtcNow);

    var result = await handler.Handle(
        Command(collectorPoint.Id, Rule(ElectronicMaterial.Battery, 10m, MaterialScoreUnit.PerUnit)),
        CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    var persisted = await DbContext.MaterialScoreRules.SingleAsync();
    persisted.Id.ShouldBe(existing.Id);
    persisted.ValidTo.ShouldBeNull();
    persisted.UpdatedAt.ShouldBeNull();
}

[Fact]
public async Task Handle_ShouldReturnWithoutWriting_WhenAccessGuardFails()
{
    var collectorPoint = AddCollectorPoint();
    _guard.Setup(x => x.EnsureResponsibleActiveAsync(
            collectorPoint.Id,
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(Result.Failure(Error.NotFound("Ponto de coleta não encontrado.")));
    var handler = CreateHandler(UtcNow);

    var result = await handler.Handle(
        Command(collectorPoint.Id, Rule(ElectronicMaterial.Battery, 10m, MaterialScoreUnit.PerUnit)),
        CancellationToken.None);

    result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    (await DbContext.MaterialScoreRules.CountAsync()).ShouldBe(0);
}
```

- [ ] **Step 6: Executar RED do diff**

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~SetMaterialScoreRulesTests"
```

Expected: criação continua PASS; diff misto e no-op FAIL porque o handler ainda não fecha, substitui ou evita escrita.

- [ ] **Step 7: Implementar o diff completo antes de mutar entidades**

Substituir o corpo após a consulta de `currentRules` pela lógica abaixo:

```csharp
var currentByMaterial = currentRules.ToDictionary(rule => rule.Material);
var requestedByMaterial = request.Rules!.ToDictionary(rule => rule.Material);
var toClose = new List<MaterialScoreRule>();
var toCreate = new List<RuleInput>();
var unchangedCount = 0;
var replacedCount = 0;
var closedCount = 0;
var createdCount = 0;

foreach (var current in currentRules)
{
    if (!requestedByMaterial.TryGetValue(current.Material, out var requested))
    {
        toClose.Add(current);
        closedCount++;
        continue;
    }

    if (current.Points == requested.Points && current.Unit == requested.Unit)
    {
        unchangedCount++;
        continue;
    }

    toClose.Add(current);
    toCreate.Add(requested);
    replacedCount++;
}

foreach (var requested in requestedByMaterial.Values)
{
    if (!currentByMaterial.ContainsKey(requested.Material))
    {
        toCreate.Add(requested);
        createdCount++;
    }
}

if (toClose.Count == 0 && toCreate.Count == 0)
{
    _logger.LogInformation(
        "Tabela de pontuação do PC {CollectorPointId} já estava atual. Inalteradas: {UnchangedCount}.",
        request.CollectorPointId,
        unchangedCount);
    return Result.Success();
}

var effectiveAt = _timeProvider.GetUtcNow().UtcDateTime;
if (toClose.Count > 0)
{
    var latestValidFrom = toClose.Max(rule => rule.ValidFrom);
    if (effectiveAt < latestValidFrom)
    {
        _logger.LogWarning(
            "Relógio regrediu ao versionar regras do PC {CollectorPointId}. Agora: {Now}; última vigência: {LatestValidFrom}.",
            request.CollectorPointId,
            effectiveAt,
            latestValidFrom);
        return Result.Failure(Error.Conflict(
            "Não foi possível versionar as regras devido à ordem temporal."));
    }

    if (effectiveAt == latestValidFrom)
        effectiveAt = latestValidFrom.AddTicks(10);
}

foreach (var current in toClose)
    current.Close(effectiveAt);

foreach (var requested in toCreate)
{
    _dbContext.MaterialScoreRules.Add(new MaterialScoreRule(
        request.CollectorPointId,
        requested.Material,
        requested.Points,
        requested.Unit,
        effectiveAt));
}

try
{
    await _dbContext.SaveChangesAsync(cancellationToken);
}
catch (DbUpdateException exception) when (IsOpenRuleConflict(exception))
{
    _logger.LogWarning(
        exception,
        "Conflito concorrente ao versionar regras do PC {CollectorPointId}.",
        request.CollectorPointId);
    return Result.Failure(Error.Conflict(
        "A tabela foi alterada por outra operação. Recarregue e tente novamente."));
}

_logger.LogInformation(
    "Tabela do PC {CollectorPointId} atualizada. Novas: {CreatedCount}; substituídas: {ReplacedCount}; encerradas: {ClosedCount}; inalteradas: {UnchangedCount}.",
    request.CollectorPointId,
    createdCount,
    replacedCount,
    closedCount,
    unchangedCount);

return Result.Success();
```

Adicionar ao handler:

```csharp
private static bool IsOpenRuleConflict(DbUpdateException exception) =>
    exception.InnerException is PostgresException postgresException
    && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
    && postgresException.ConstraintName == OpenRuleIndexName;
```

- [ ] **Step 8: Executar GREEN do diff**

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~SetMaterialScoreRulesTests"
```

Expected: criação, diff misto, histórico, no-op e falha do guard passam.

- [ ] **Step 9: Adicionar RED temporal**

Adicionar:

```csharp
[Fact]
public async Task Handle_ShouldAdvanceOneMicrosecond_WhenClockEqualsCurrentValidFrom()
{
    var collectorPoint = AddCollectorPoint();
    var current = AddRule(
        collectorPoint.Id,
        ElectronicMaterial.Battery,
        10m,
        MaterialScoreUnit.PerUnit,
        UtcNow);
    var handler = CreateHandler(UtcNow);

    var result = await handler.Handle(
        Command(collectorPoint.Id, Rule(ElectronicMaterial.Battery, 20m, MaterialScoreUnit.PerUnit)),
        CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DbContext.ChangeTracker.Clear();
    var rules = await DbContext.MaterialScoreRules.OrderBy(x => x.ValidFrom).ToListAsync();
    var effectiveAt = UtcNow.AddTicks(10);
    rules.Single(x => x.Id == current.Id).ValidTo.ShouldBe(effectiveAt);
    rules.Single(x => x.ValidTo is null).ValidFrom.ShouldBe(effectiveAt);
}

[Fact]
public async Task Handle_ShouldReturnConflictWithoutWriting_WhenClockRegresses()
{
    var collectorPoint = AddCollectorPoint();
    var current = AddRule(
        collectorPoint.Id,
        ElectronicMaterial.Battery,
        10m,
        MaterialScoreUnit.PerUnit,
        UtcNow.AddMinutes(1));
    var handler = CreateHandler(UtcNow);

    var result = await handler.Handle(
        Command(collectorPoint.Id, Rule(ElectronicMaterial.Battery, 20m, MaterialScoreUnit.PerUnit)),
        CancellationToken.None);

    result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    DbContext.ChangeTracker.Clear();
    var persisted = await DbContext.MaterialScoreRules.SingleAsync();
    persisted.Id.ShouldBe(current.Id);
    persisted.Points.ShouldBe(10m);
    persisted.ValidTo.ShouldBeNull();
}
```

- [ ] **Step 10: Executar testes temporais e suíte unitária**

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~SetMaterialScoreRulesTests"
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore
```

Expected: todos passam. Se o teste de igualdade falhar por precisão SQLite, verificar que os 10 ticks chegam ao banco como 1 microssegundo; não relaxar a invariante de `Close`.

- [ ] **Step 11: Refactor e revisão de uma única escrita**

```powershell
rtk rg -n "SaveChangesAsync|BeginTransaction|ExecuteUpdate|DateTime.UtcNow|GetUtcNow|AddTicks\(10\)|PostgresErrorCodes.UniqueViolation|OpenRuleIndexName" src/Ecocell.Api/Features/CollectorPoint/SetMaterialScoreRules.cs
rtk git diff --check -- src/Ecocell.Api/Features/CollectorPoint/SetMaterialScoreRules.cs tests/Ecocell.UnitTests/Features/CollectorPoint/SetMaterialScoreRulesTests.cs
```

Expected: exatamente um `SaveChangesAsync` no handler; nenhum `BeginTransaction`, `ExecuteUpdate` ou `DateTime.UtcNow`; filtro de `23505` inclui nome exato do índice.

- [ ] **Step 12: Commit seletivo**

```powershell
rtk git add -- src/Ecocell.Api/Features/CollectorPoint/SetMaterialScoreRules.cs tests/Ecocell.UnitTests/Features/CollectorPoint/SetMaterialScoreRulesTests.cs
rtk git diff --cached --check
rtk git diff --cached --name-only
rtk git commit -m "feat(score): versiona tabela vigente por diff"
```

Expected no staging: somente slice e testes do handler.

---

### Task 5: Endpoint HTTP e integração PostgreSQL do fluxo funcional

**Files:**

- Modify: `src/Ecocell.Api/Features/CollectorPoint/SetMaterialScoreRules.cs`
- Create: `tests/Ecocell.IntegrationTests/Features/CollectorPoint/SetMaterialScoreRulesTests.cs`

**Interfaces:**

- Consumes: contrato Shared da Task 1, `SetMaterialScoreRules.Command` e handler da Task 4, `AuthorizationPolicies.Authenticated`, helpers de `IntegrationTestBase`.
- Produces: `SetMaterialScoreRulesEndpoint` em `PUT /api/collector-points/{collectorPointId:guid}/score-rules` com `204`, `400`, `401`, `403`, `404` e `409`.

- [ ] **Step 1: Criar testes RED do endpoint e do versionamento PostgreSQL**

Criar `tests/Ecocell.IntegrationTests/Features/CollectorPoint/SetMaterialScoreRulesTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Ecocell.Api.Database;
using Ecocell.Shared.Requests.CollectorPoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ApiEnums = Ecocell.Api.Enums;
using SharedEnums = Ecocell.Shared.Enums;

namespace Ecocell.IntegrationTests.Features.CollectorPoint;

[Collection(nameof(IntegrationTestCollection))]
public class SetMaterialScoreRulesTests : IntegrationTestBase
{
    public SetMaterialScoreRulesTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Put_ShouldReturn401_WhenNoToken()
    {
        var response = await Client.PutAsJsonAsync(
            $"api/collector-points/{Guid.NewGuid()}/score-rules",
            Request(Rule(SharedEnums.ElectronicMaterial.Battery, 10m, SharedEnums.MaterialScoreUnit.PerUnit)));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_ShouldReturn204AndRemainIdempotent_WhenRequestIsValid()
    {
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var responsibleId = await GetPersonIdByEmailAsync(email);
        var collectorPoint = await CreateActiveCollectorPointAsync(responsibleId);
        using var authClient = CreateAuthenticatedClient(jwt);
        var request = Request(
            Rule(SharedEnums.ElectronicMaterial.Battery, 10m, SharedEnums.MaterialScoreUnit.PerUnit),
            Rule(SharedEnums.ElectronicMaterial.Notebook, 25m, SharedEnums.MaterialScoreUnit.PerKilogram));

        var first = await authClient.PutAsJsonAsync(
            $"api/collector-points/{collectorPoint.Id}/score-rules",
            request);
        var second = await authClient.PutAsJsonAsync(
            $"api/collector-points/{collectorPoint.Id}/score-rules",
            request);

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rules = await db.MaterialScoreRules
            .Where(x => x.LegalPersonId == collectorPoint.Id)
            .ToListAsync();
        rules.Count.ShouldBe(2);
        rules.ShouldAllBe(x => x.ValidTo is null);
    }

    [Fact]
    public async Task Put_ShouldReturn400_WhenRulesIsEmpty()
    {
        var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
        using var authClient = CreateAuthenticatedClient(jwt);

        var response = await authClient.PutAsJsonAsync(
            $"api/collector-points/{Guid.NewGuid()}/score-rules",
            new RequestSetMaterialScoreRulesJson { Rules = [] });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_ShouldReturn400_WhenEnumValueIsUndefined()
    {
        var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
        using var authClient = CreateAuthenticatedClient(jwt);
        var request = Request(Rule(
            (SharedEnums.ElectronicMaterial)999,
            10m,
            SharedEnums.MaterialScoreUnit.PerUnit));

        var response = await authClient.PutAsJsonAsync(
            $"api/collector-points/{Guid.NewGuid()}/score-rules",
            request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_ShouldReturn403_WhenCallerHasPrivilegedRole()
    {
        var (email, jwt) = await CreatePrivilegedUserAndLoginAsync(ApiEnums.Role.Admin);
        var responsibleId = await GetPersonIdByEmailAsync(email);
        var collectorPoint = await CreateActiveCollectorPointAsync(responsibleId);
        using var authClient = CreateAuthenticatedClient(jwt);

        var response = await authClient.PutAsJsonAsync(
            $"api/collector-points/{collectorPoint.Id}/score-rules",
            Request(Rule(SharedEnums.ElectronicMaterial.Battery, 10m, SharedEnums.MaterialScoreUnit.PerUnit)));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Put_ShouldReturn404_WhenAnotherPersonOwnsCollectorPoint()
    {
        var (ownerEmail, _) = await CreateAndLoginNaturalPersonAsync();
        var ownerId = await GetPersonIdByEmailAsync(ownerEmail);
        var collectorPoint = await CreateActiveCollectorPointAsync(ownerId);
        var (_, callerJwt) = await CreateAndLoginNaturalPersonAsync();
        using var authClient = CreateAuthenticatedClient(callerJwt);

        var response = await authClient.PutAsJsonAsync(
            $"api/collector-points/{collectorPoint.Id}/score-rules",
            Request(Rule(SharedEnums.ElectronicMaterial.Battery, 10m, SharedEnums.MaterialScoreUnit.PerUnit)));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_ShouldReturn409_WhenOwnedCollectorPointIsPendingApproval()
    {
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var responsibleId = await GetPersonIdByEmailAsync(email);
        var collectorPoint = await CreatePendingLegalPersonAsync(responsibleId);
        using var authClient = CreateAuthenticatedClient(jwt);

        var response = await authClient.PutAsJsonAsync(
            $"api/collector-points/{collectorPoint.Id}/score-rules",
            Request(Rule(SharedEnums.ElectronicMaterial.Battery, 10m, SharedEnums.MaterialScoreUnit.PerUnit)));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Put_ShouldApplyFullTableDiffAndPreserveHistory_WhenTableChanges()
    {
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var responsibleId = await GetPersonIdByEmailAsync(email);
        var collectorPoint = await CreateActiveCollectorPointAsync(responsibleId);
        using var authClient = CreateAuthenticatedClient(jwt);
        var firstRequest = Request(
            Rule(SharedEnums.ElectronicMaterial.Battery, 10m, SharedEnums.MaterialScoreUnit.PerUnit),
            Rule(SharedEnums.ElectronicMaterial.Notebook, 20m, SharedEnums.MaterialScoreUnit.PerUnit),
            Rule(SharedEnums.ElectronicMaterial.Printer, 30m, SharedEnums.MaterialScoreUnit.PerUnit));
        var secondRequest = Request(
            Rule(SharedEnums.ElectronicMaterial.Battery, 10m, SharedEnums.MaterialScoreUnit.PerUnit),
            Rule(SharedEnums.ElectronicMaterial.Notebook, 25m, SharedEnums.MaterialScoreUnit.PerKilogram),
            Rule(SharedEnums.ElectronicMaterial.CellPhone, 15m, SharedEnums.MaterialScoreUnit.PerUnit));

        var first = await authClient.PutAsJsonAsync(
            $"api/collector-points/{collectorPoint.Id}/score-rules",
            firstRequest);
        var second = await authClient.PutAsJsonAsync(
            $"api/collector-points/{collectorPoint.Id}/score-rules",
            secondRequest);

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rules = await db.MaterialScoreRules
            .AsNoTracking()
            .Where(x => x.LegalPersonId == collectorPoint.Id)
            .ToListAsync();
        rules.Count.ShouldBe(5);
        rules.Count(x => x.ValidTo is null).ShouldBe(3);
        rules.Single(x => x.Material == ApiEnums.ElectronicMaterial.Battery).ValidTo.ShouldBeNull();
        rules.Single(x => x.Material == ApiEnums.ElectronicMaterial.Printer).ValidTo.ShouldNotBeNull();
        rules.Count(x => x.Material == ApiEnums.ElectronicMaterial.Notebook).ShouldBe(2);
        rules.Single(x => x.Material == ApiEnums.ElectronicMaterial.Notebook && x.ValidTo is null)
            .Points.ShouldBe(25m);
        rules.Single(x => x.Material == ApiEnums.ElectronicMaterial.CellPhone && x.ValidTo is null)
            .Points.ShouldBe(15m);
    }

    private static RequestSetMaterialScoreRulesJson Request(
        params RequestMaterialScoreRuleJson[] rules) =>
        new() { Rules = [.. rules] };

    private static RequestMaterialScoreRuleJson Rule(
        SharedEnums.ElectronicMaterial material,
        decimal points,
        SharedEnums.MaterialScoreUnit unit) =>
        new()
        {
            Material = material,
            Points = points,
            Unit = unit,
        };
}
```

- [ ] **Step 2: Executar RED HTTP**

```powershell
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~SetMaterialScoreRulesTests"
```

Expected: requisições retornam `404` porque o endpoint ainda não foi registrado.

- [ ] **Step 3: Adicionar endpoint Carter ao slice**

Adicionar ao fim de `SetMaterialScoreRules.cs`, fora do static class:

```csharp
public sealed class SetMaterialScoreRulesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut(
            "api/collector-points/{collectorPointId:guid}/score-rules",
            async (
                Guid collectorPointId,
                [FromBody] RequestSetMaterialScoreRulesJson request,
                ISender sender) =>
            {
                var rules = request.Rules?.Select(rule =>
                        new SetMaterialScoreRules.RuleInput(
                            (ElectronicMaterial)(int)rule.Material,
                            rule.Points,
                            (MaterialScoreUnit)(int)rule.Unit))
                    .ToList();

                var result = await sender.Send(
                    new SetMaterialScoreRules.Command(collectorPointId, rules));
                return result.ToProcessResult(StatusCodes.Status204NoContent);
            })
            .WithTags("CollectorPoint")
            .WithName("SetMaterialScoreRules")
            .WithSummary("Substitui a tabela vigente de pontuação de um Ponto de Coleta.")
            .RequireAuthorization(AuthorizationPolicies.Authenticated)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }
}
```

Adicionar os `using`:

```csharp
using Carter;
using Ecocell.Api.Extensions;
using Ecocell.Shared.Requests.CollectorPoints;
using Microsoft.AspNetCore.Mvc;
```

- [ ] **Step 4: Executar GREEN HTTP funcional**

```powershell
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~SetMaterialScoreRulesTests"
```

Expected: todos os testes funcionais passam. Em especial, o teste de diff confirma no PostgreSQL que fechamento e inserção funcionam no único `SaveChangesAsync` com o índice filtrado.

Se o teste falhar porque o provider envia o `INSERT` antes do `UPDATE`, interromper a execução e revisar a spec. Não introduzir silenciosamente dois `SaveChangesAsync` ou transação manual.

- [ ] **Step 5: Executar regressão de CollectorPoint**

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Features.CollectorPoint|FullyQualifiedName~Services.CollectorPoints"
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~Features.CollectorPoint"
```

Expected: US011.B, lista de PCs geridos e QR passam.

- [ ] **Step 6: Revisar contrato e ausência de escopo extra**

```powershell
rtk rg -n "MapPut|collectorPointId:guid|Status204NoContent|RequestSetMaterialScoreRulesJson|ToProcessResult" src/Ecocell.Api/Features/CollectorPoint/SetMaterialScoreRules.cs
rtk rg -n "repository|MapGet|ResponseMaterial|migration|ExecuteUpdate|BeginTransaction" src/Ecocell.Api/Features/CollectorPoint/SetMaterialScoreRules.cs src/Ecocell.Shared/Requests/CollectorPoints/RequestSetMaterialScoreRulesJson.cs
rtk git diff --check -- src/Ecocell.Api/Features/CollectorPoint/SetMaterialScoreRules.cs tests/Ecocell.IntegrationTests/Features/CollectorPoint/SetMaterialScoreRulesTests.cs
```

Expected: primeira busca confirma rota e `204`; segunda não encontra repository, leitura pública, response DTO, migration, bulk update ou transação manual.

- [ ] **Step 7: Commit seletivo**

```powershell
rtk git add -- src/Ecocell.Api/Features/CollectorPoint/SetMaterialScoreRules.cs tests/Ecocell.IntegrationTests/Features/CollectorPoint/SetMaterialScoreRulesTests.cs
rtk git diff --cached --check
rtk git diff --cached --name-only
rtk git commit -m "feat(score): expoe definicao das regras do ponto"
```

Expected no staging: somente endpoint/slice e testes de integração funcionais.

---

### Task 6: Concorrência determinística e verificação final

**Files:**

- Create: `tests/Ecocell.IntegrationTests/Infrastructure/MaterialScoreRuleInsertBarrierInterceptor.cs`
- Modify: `tests/Ecocell.IntegrationTests/IntegrationTestFixture.cs:1-119`
- Modify: `tests/Ecocell.IntegrationTests/Features/CollectorPoint/SetMaterialScoreRulesTests.cs`
- Verify: `src/Ecocell.Api/Migrations/`
- Verify: `src/Ecocell.Api/Migrations/AppDbContextModelSnapshot.cs`

**Interfaces:**

- Consumes: filtro `23505` do handler, `DbCommandInterceptor`, connection string PostgreSQL da fixture e endpoint da Task 5.
- Produces: `MaterialScoreRuleInsertBarrierInterceptor` apenas no projeto de integração e `IntegrationTestFixture.CreateDbInterceptedFactory(DbCommandInterceptor)`; prova HTTP determinística de um `204`, um `409` e uma única regra aberta.

- [ ] **Step 1: Criar o interceptor de barreira somente para testes**

Criar `tests/Ecocell.IntegrationTests/Infrastructure/MaterialScoreRuleInsertBarrierInterceptor.cs`:

```csharp
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ecocell.IntegrationTests.Infrastructure;

internal sealed class MaterialScoreRuleInsertBarrierInterceptor : DbCommandInterceptor
{
    private const string InsertMarker = "INSERT INTO \"MaterialScoreRules\"";
    private readonly TaskCompletionSource<bool> _bothArrived =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrivalCount;

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        await WaitForBothWritersAsync(command.CommandText, cancellationToken);
        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await WaitForBothWritersAsync(command.CommandText, cancellationToken);
        return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    private async Task WaitForBothWritersAsync(
        string commandText,
        CancellationToken cancellationToken)
    {
        if (!commandText.Contains(InsertMarker, StringComparison.Ordinal))
            return;

        if (Interlocked.Increment(ref _arrivalCount) == 2)
            _bothArrived.TrySetResult(true);

        await _bothArrived.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
    }
}
```

O interceptor sincroniza somente comandos que inserem `MaterialScoreRules`. Não colocá-lo em produção nem na factory padrão.

- [ ] **Step 2: Expor uma factory de integração com interceptor de banco**

Em `IntegrationTestFixture.cs`, adicionar:

```csharp
using Microsoft.EntityFrameworkCore.Diagnostics;
```

Adicionar o método público antes de `ConfigureTestServices`:

```csharp
public WebApplicationFactory<Program> CreateDbInterceptedFactory(DbCommandInterceptor interceptor)
{
    return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:Enabled"] = "false",
            });
        });
        builder.ConfigureServices(services => ConfigureTestServices(services, interceptor));
    });
}
```

Manter a assinatura usada pelas factories existentes como overload delegador:

```csharp
private void ConfigureTestServices(IServiceCollection services) =>
    ConfigureTestServices(services, interceptor: null);

private void ConfigureTestServices(
    IServiceCollection services,
    DbCommandInterceptor? interceptor)
{
```

No bloco que recria o `AppDbContext`, substituir a lambda de uma linha por:

```csharp
services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(_postgres.GetConnectionString());
    if (interceptor is not null)
        options.AddInterceptors(interceptor);
});
```

Manter sem alteração os overrides de Redis, e-mail e geocodificação dentro do overload com dois parâmetros.

- [ ] **Step 3: Adicionar RED da colisão HTTP real**

Adicionar ao topo de `SetMaterialScoreRulesTests.cs`:

```csharp
using System.Net.Http.Headers;
using Ecocell.IntegrationTests.Infrastructure;
```

Adicionar o teste:

```csharp
[Fact]
public async Task Put_ShouldReturnOne204AndOne409_WhenTwoWritesStartFromSameState()
{
    var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
    var responsibleId = await GetPersonIdByEmailAsync(email);
    var collectorPoint = await CreateActiveCollectorPointAsync(responsibleId);
    var interceptor = new MaterialScoreRuleInsertBarrierInterceptor();
    using var factory = Fixture.CreateDbInterceptedFactory(interceptor);
    using var firstClient = factory.CreateClient();
    using var secondClient = factory.CreateClient();
    firstClient.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", jwt);
    secondClient.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", jwt);

    var firstCall = firstClient.PutAsJsonAsync(
        $"api/collector-points/{collectorPoint.Id}/score-rules",
        Request(Rule(SharedEnums.ElectronicMaterial.Battery, 10m, SharedEnums.MaterialScoreUnit.PerUnit)));
    var secondCall = secondClient.PutAsJsonAsync(
        $"api/collector-points/{collectorPoint.Id}/score-rules",
        Request(Rule(SharedEnums.ElectronicMaterial.Battery, 20m, SharedEnums.MaterialScoreUnit.PerUnit)));

    var responses = await Task.WhenAll(firstCall, secondCall);

    responses.Count(response => response.StatusCode == HttpStatusCode.NoContent).ShouldBe(1);
    responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var persisted = await db.MaterialScoreRules
        .AsNoTracking()
        .Where(x => x.LegalPersonId == collectorPoint.Id)
        .ToListAsync();
    persisted.Count.ShouldBe(1);
    persisted.Single().ValidTo.ShouldBeNull();
    new[] { 10m, 20m }.ShouldContain(persisted.Single().Points);

    foreach (var response in responses)
        response.Dispose();
}
```

- [ ] **Step 4: Executar RED da concorrência**

```powershell
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~Put_ShouldReturnOne204AndOne409"
```

Expected antes do interceptor/factory completos: FAIL de compilação. Depois da infraestrutura compilar, o teste deve falhar se a violação específica não for traduzida para `409` ou se ocorrer estado parcial.

- [ ] **Step 5: Executar GREEN da concorrência e repetir para detectar flake**

```powershell
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~Put_ShouldReturnOne204AndOne409"
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~Put_ShouldReturnOne204AndOne409"
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~Put_ShouldReturnOne204AndOne409"
```

Expected em todas as execuções: um `204`, um `409`, uma única regra aberta e nenhum timeout de 10 segundos.

Se a barreira não interceptar o tipo de comando emitido pelo Npgsql, inspecionar o SQL capturado e ajustar somente os overrides `ReaderExecutingAsync`/`NonQueryExecutingAsync`; não adicionar hooks ao código de produção.

- [ ] **Step 6: Executar suítes completas**

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj
rtk dotnet test Ecocell.slnx
```

Expected: todos os testes passam; integração requer Docker.

- [ ] **Step 7: Confirmar que o modelo não exige migration**

```powershell
dotnet ef migrations has-pending-model-changes --project src/Ecocell.Api --startup-project src/Ecocell.Api
rtk git diff --name-only -- src/Ecocell.Api/Migrations
```

Expected: `No changes have been made to the model since the last migration.` e nenhum arquivo de migration/snapshot alterado.

- [ ] **Step 8: Fazer auditoria final de escopo e qualidade**

```powershell
rtk rg -n "TODO|FIXME|NotImplementedException" src/Ecocell.Shared/Requests/CollectorPoints/RequestSetMaterialScoreRulesJson.cs src/Ecocell.Api/Services/CollectorPoints/CollectorPointAccessGuard.cs src/Ecocell.Api/Features/CollectorPoint/SetMaterialScoreRules.cs src/Ecocell.Api/Features/CollectorPoint/GeneratePcQrCode.cs tests/Ecocell.UnitTests/Services/CollectorPoints/CollectorPointAccessGuardTests.cs tests/Ecocell.UnitTests/Features/CollectorPoint/SetMaterialScoreRulesValidatorTests.cs tests/Ecocell.UnitTests/Features/CollectorPoint/SetMaterialScoreRulesTests.cs tests/Ecocell.IntegrationTests/Infrastructure/MaterialScoreRuleInsertBarrierInterceptor.cs tests/Ecocell.IntegrationTests/Features/CollectorPoint/SetMaterialScoreRulesTests.cs
rtk git diff --check
rtk git status --short
```

Expected: nenhuma marca incompleta; diff sem whitespace inválido; mudanças não relacionadas continuam preservadas e fora dos commits da task.

- [ ] **Step 9: Commit seletivo da prova de concorrência**

```powershell
rtk git add -- tests/Ecocell.IntegrationTests/Infrastructure/MaterialScoreRuleInsertBarrierInterceptor.cs tests/Ecocell.IntegrationTests/IntegrationTestFixture.cs tests/Ecocell.IntegrationTests/Features/CollectorPoint/SetMaterialScoreRulesTests.cs
rtk git diff --cached --check
rtk git diff --cached --name-only
rtk git commit -m "test(score): cobre concorrencia na definicao das regras"
```

Expected no staging: somente interceptor, fixture e teste de integração. Se `IntegrationTestFixture.cs` tiver mudança preexistente no início da execução, usar `git add -p` para selecionar exclusivamente o overload e o registro do interceptor.

- [ ] **Step 10: Verificar histórico e resultado final**

```powershell
rtk git log -8 --oneline
rtk git status --short
rtk git diff --cached --check
```

Expected: commits pequenos das Tasks 1–6 visíveis; índice vazio; somente mudanças preexistentes ou explicitamente não commitadas permanecem no worktree.

## Definition of Done

- [ ] Contrato Shared usa objeto raiz `rules` e enums públicos existentes.
- [ ] Validator rejeita lista nula/vazia, mais de sete itens, duplicidade, enum inválido e decimal fora de `numeric(10,2)`.
- [ ] Guard único aplica `403`, `404` anti-enumeração e `409` por PC não ativo.
- [ ] QR reutiliza o guard sem mudar seu contrato HTTP.
- [ ] Handler aplica diff completo, preserva histórico e não grava no-op.
- [ ] Todas as mudanças de um lote compartilham um instante UTC monotônico.
- [ ] Handler usa exatamente um `SaveChangesAsync` e nenhuma transação manual.
- [ ] Somente colisão `23505` do índice aberto vira `409`.
- [ ] Endpoint retorna `204` e expõe os status previstos na spec.
- [ ] Teste PostgreSQL determinístico prova um vencedor, um `409` e ausência de estado parcial.
- [ ] UnitTests, IntegrationTests e `Ecocell.slnx` passam.
- [ ] EF Core informa ausência de mudanças pendentes no modelo.
- [ ] Nenhuma migration, consulta pública, Mobile, repository ou nova dependência foi adicionada.
- [ ] Commits contêm somente arquivos/hunks da US011.B; mudanças preexistentes permanecem intactas.
