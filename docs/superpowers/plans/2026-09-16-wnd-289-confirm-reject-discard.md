# WND-289 — Confirmação e rejeição de descarte — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar a confirmação e a rejeição terminal de descartes por gestores de Pontos de Coleta, com correção integral dos itens, autorização anti-enumeração, concorrência otimista e criação atômica de uma solicitação durável de crédito.

**Architecture:** Dois slices VSA reutilizam `ICollectorPointAccessGuard` e alteram o agregado `Discard` somente enquanto ele está `Pending`. A confirmação substitui os itens usando regras vigentes em `Discard.CreatedAt` e grava `CreditScoreRequest` na mesma transação; a rejeição apenas encerra o descarte. `Discard.Status` funciona como concurrency token e colisões são traduzidas para `409 Conflict`.

**Tech Stack:** .NET 10, C# 14, Carter, Mediator, FluentValidation, EF Core 10, PostgreSQL/Npgsql, SQLite in-memory, xUnit, Shouldly, Moq, Bogus e Testcontainers.

**Spec:** `docs/superpowers/specs/2026-09-16-wnd-289-confirm-reject-discard-design.md`

## Global Constraints

- A implementação é fail-closed: não alterar código da WND-289 enquanto os artefatos obrigatórios da WND-290 não existirem e sua suíte não estiver verde.
- Executar em worktree isolado criado por `superpowers:using-git-worktrees`, baseado no commit que contém a WND-290 concluída; não reutilizar o checkout sujo atual.
- Ler `AGENTS.md`, `.claude/rules/coding-rules.md`, `.claude/rules/unit-tests-rules.md` e `.claude/rules/integration-tests-rules.md` antes do primeiro RED.
- Aplicar Red → Green → Refactor em toda nova funcionalidade e correção descoberta durante a execução.
- Manter VSA: `Command`, `Validator`, `Handler` e endpoint de cada operação no mesmo arquivo do slice.
- DTOs HTTP ficam em `Ecocell.Shared`; `Command` e entidades permanecem internos à API.
- Reutilizar `ICollectorPointAccessGuard`; não duplicar autorização de PC.
- Preservar `403` para chamador inelegível, `404` para descarte inexistente ou PC fora do escopo e `409` para PC inativo, estado terminal, regra histórica ausente ou concorrência.
- Validar o request antes de tocar o banco e autorizar o PC antes de revelar o estado do descarte.
- Resolver `MaterialScoreRule` em `Discard.CreatedAt`, nunca pela regra vigente no instante da confirmação.
- Não adicionar nova dependência, repository, service, mapper, outbox genérica, motivo de rejeição, DTO de resposta ou idempotência HTTP.
- Não alterar cálculo de crédito, saldo, ranking, dispatcher, `CreditScoreJob`, Mobile ou listagem de pendências.
- Todos os summaries, mensagens de erro, logs e commits novos permanecem em PT-BR.
- Usar RTK para busca, Git e testes quando o wrapper suportar o comando.
- Verificação final obrigatória: `rtk dotnet test Ecocell.slnx` com Docker disponível.

## Contrato esperado da WND-290

Este plano consome exatamente esta superfície. Se a implementação aprovada da WND-290 usar nomes ou assinaturas diferentes, interromper a execução e alinhar spec e plano antes de editar a WND-289.

```csharp
namespace Ecocell.Api.Entities;

public sealed class CreditScoreRequest : BaseEntity
{
    private CreditScoreRequest()
    {
    }

    public CreditScoreRequest(Guid discardId)
    {
        if (discardId == Guid.Empty)
            throw new ArgumentException("O identificador do descarte é obrigatório.", nameof(discardId));

        DiscardId = discardId;
    }

    public Guid DiscardId { get; private set; }
    public DateTime? DispatchedAt { get; private set; }
}
```

```csharp
public DbSet<CreditScoreRequest> CreditScoreRequests { get; set; }
```

Requisitos adicionais da dependência:

- FK de `CreditScoreRequest.DiscardId` para `Discard.Id` com exclusão restrita;
- índice único em `CreditScoreRequest.DiscardId`;
- migration aplicada pelos testes de integração;
- dispatcher e `CreditScoreJob` idempotente presentes;
- unicidade persistente que impeça dois créditos para o mesmo descarte.

## Mapa de arquivos

| Arquivo | Responsabilidade |
| --- | --- |
| `src/Ecocell.Api/Entities/Discard.cs` | Invariantes e transições terminais `Confirm`/`Reject`. |
| `src/Ecocell.Api/Database/TypeConfiguration/DiscardTypeConfiguration.cs` | Configurar `Status` como concurrency token. |
| Migration `ConfigureDiscardStatusConcurrency` em `src/Ecocell.Api/Migrations/` | Registrar a mudança de metadata no snapshot, sem alterar schema. |
| `src/Ecocell.Api/Migrations/AppDbContextModelSnapshot.cs` | Refletir `.IsConcurrencyToken()` em `Discard.Status`. |
| `src/Ecocell.Shared/Requests/Discards/RequestConfirmDiscardJson.cs` | Contrato HTTP da lista final confirmada. |
| `src/Ecocell.Api/Features/Discard/ConfirmDiscard.cs` | Command, validação, handler transacional e endpoint de confirmação. |
| `src/Ecocell.Api/Features/Discard/RejectDiscard.cs` | Command, validação, handler e endpoint de rejeição. |
| `tests/Ecocell.UnitTests/Entities/DiscardTests.cs` | Cobrir invariantes e transições do agregado. |
| `tests/Ecocell.UnitTests/Features/Discard/ConfirmDiscardTests.cs` | Cobrir validação, regras históricas, autorização e outbox. |
| `tests/Ecocell.UnitTests/Features/Discard/RejectDiscardTests.cs` | Cobrir rejeição, autorização e estado terminal. |
| `tests/Ecocell.IntegrationTests/IntegrationTestBase.cs` | Helper compartilhado para semear descarte pendente com regras históricas. |
| `tests/Ecocell.IntegrationTests/Features/Discard/ConfirmDiscardTests.cs` | Contrato HTTP, persistência PostgreSQL e concorrência. |
| `tests/Ecocell.IntegrationTests/Features/Discard/RejectDiscardTests.cs` | Contrato HTTP, ausência de outbox e anti-enumeração. |

---

### Task 0: Gate fail-closed da WND-290 e baseline

**Files:**
- Read: `AGENTS.md`
- Read: `.claude/rules/coding-rules.md`
- Read: `.claude/rules/unit-tests-rules.md`
- Read: `.claude/rules/integration-tests-rules.md`
- Read: `docs/superpowers/specs/2026-09-16-wnd-289-confirm-reject-discard-design.md`
- Verify: `src/Ecocell.Api/Entities/CreditScoreRequest.cs`
- Verify: `src/Ecocell.Api/Database/AppDbContext.cs`
- Verify: `src/Ecocell.Api/Jobs/CreditScoreJob.cs`
- Verify: `src/Ecocell.Api/Migrations/`

**Interfaces:**
- Consumes: WND-290 completa e integrada.
- Produces: baseline comprovadamente apta para iniciar o primeiro RED; nenhuma alteração de código.

- [ ] **Step 1: Criar e entrar no worktree isolado**

Usar `superpowers:using-git-worktrees`. A base precisa conter o commit da WND-290. Não inventar nome de branch nem copiar alterações do checkout sujo.

- [ ] **Step 2: Ler regras, spec e estado Git**

Run:

```powershell
rtk read AGENTS.md
rtk read .claude/rules/coding-rules.md
rtk read .claude/rules/unit-tests-rules.md
rtk read .claude/rules/integration-tests-rules.md
rtk read docs/superpowers/specs/2026-09-16-wnd-289-confirm-reject-discard-design.md
rtk git status --short
```

Expected: worktree limpo e regras carregadas.

- [ ] **Step 3: Verificar os artefatos obrigatórios da WND-290**

Run:

```powershell
rtk rg -n "class CreditScoreRequest|CreditScoreRequest\(Guid discardId\)|DispatchedAt" src/Ecocell.Api/Entities
rtk rg -n "DbSet<CreditScoreRequest>|CreditScoreRequests" src/Ecocell.Api/Database/AppDbContext.cs
rtk rg -n "class CreditScoreJob|DiscardId" src/Ecocell.Api/Jobs tests
rtk rg -n "CreditScoreRequests|DiscardId" src/Ecocell.Api/Migrations
```

Expected: entidade, `DbSet`, migration, job, dispatcher e testes localizados. Se qualquer item estiver ausente, parar sem escrever código e concluir a WND-290 primeiro.

- [ ] **Step 4: Conferir as garantias de unicidade e idempotência**

Run:

```powershell
rtk rg -n "HasIndex.*DiscardId|IsUnique|DepositorScoreTransaction|CreditScoreRequest" src/Ecocell.Api/Database src/Ecocell.Api/Entities src/Ecocell.Api/Jobs tests
```

Expected: um pedido por descarte e um efeito de crédito por descarte protegidos no banco. Se a proteção existir apenas em memória, parar e corrigir a WND-290 em seu próprio escopo.

- [ ] **Step 5: Executar a baseline completa**

Run:

```powershell
rtk dotnet test Ecocell.slnx
```

Expected: todos os testes passam, incluindo integração PostgreSQL/Redis. Falha de Docker é bloqueio ambiental, não autorização para pular a suíte.

---

### Task 1: Adicionar transições do agregado e concorrência otimista

**Files:**
- Modify: `tests/Ecocell.UnitTests/Entities/DiscardTests.cs`
- Modify: `src/Ecocell.Api/Entities/Discard.cs`
- Modify: `src/Ecocell.Api/Database/TypeConfiguration/DiscardTypeConfiguration.cs`
- Generate: migration `ConfigureDiscardStatusConcurrency` em `src/Ecocell.Api/Migrations/`
- Modify: `src/Ecocell.Api/Migrations/AppDbContextModelSnapshot.cs`

**Interfaces:**
- Consumes: `DiscardStatus.Pending`, `DiscardStatus.Confirmed`, `DiscardStatus.Rejected`, `DiscardItem`.
- Produces: `void Discard.Confirm(IEnumerable<DiscardItem> finalItems)` e `void Discard.Reject()`.

- [ ] **Step 1: Escrever testes RED das transições**

Adicionar a `DiscardTests.cs`:

```csharp
[Fact]
public void Confirm_ShouldReplaceItemsAndSetConfirmed_WhenDiscardIsPending()
{
    var original = new DiscardItem(Material, 1, 0.100m, Guid.NewGuid());
    var replacementMaterial = Enum.GetValues<ElectronicMaterial>()
        .First(value => value != Material && Convert.ToInt32(value) > 0);
    var replacement = new DiscardItem(replacementMaterial, 3, 0.750m, Guid.NewGuid());
    var discard = new Discard(Guid.NewGuid(), Guid.NewGuid(), [original]);

    discard.Confirm([replacement]);

    discard.Status.ShouldBe(DiscardStatus.Confirmed);
    discard.Items.ShouldHaveSingleItem();
    discard.Items.Single().ShouldBeSameAs(replacement);
    discard.UpdatedAt.ShouldNotBeNull();
}

[Theory]
[InlineData(DiscardStatus.Confirmed)]
[InlineData(DiscardStatus.Rejected)]
public void Confirm_ShouldThrow_WhenDiscardIsNotPending(DiscardStatus terminalStatus)
{
    var discard = PendingDiscard();
    MoveTo(discard, terminalStatus);

    Should.Throw<InvalidOperationException>(() =>
        discard.Confirm([new DiscardItem(Material, 1, 0.200m, Guid.NewGuid())]));
}

[Fact]
public void Confirm_ShouldThrow_WhenFinalItemsAreEmpty()
{
    var discard = PendingDiscard();

    Should.Throw<ArgumentException>(() => discard.Confirm([]));
}

[Fact]
public void Confirm_ShouldThrow_WhenFinalMaterialsAreDuplicated()
{
    var discard = PendingDiscard();
    var first = new DiscardItem(Material, 1, 0.200m, Guid.NewGuid());
    var second = new DiscardItem(Material, 2, 0.400m, Guid.NewGuid());

    Should.Throw<ArgumentException>(() => discard.Confirm([first, second]));
}

[Fact]
public void Reject_ShouldSetRejected_WhenDiscardIsPending()
{
    var discard = PendingDiscard();

    discard.Reject();

    discard.Status.ShouldBe(DiscardStatus.Rejected);
    discard.UpdatedAt.ShouldNotBeNull();
}

[Theory]
[InlineData(DiscardStatus.Confirmed)]
[InlineData(DiscardStatus.Rejected)]
public void Reject_ShouldThrow_WhenDiscardIsNotPending(DiscardStatus terminalStatus)
{
    var discard = PendingDiscard();
    MoveTo(discard, terminalStatus);

    Should.Throw<InvalidOperationException>(discard.Reject);
}
```

Adicionar helpers privados concretos:

```csharp
private static Discard PendingDiscard() =>
    new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        [new DiscardItem(Material, 1, 0.100m, Guid.NewGuid())]);

private static void MoveTo(Discard discard, DiscardStatus status)
{
    if (status == DiscardStatus.Confirmed)
        discard.Confirm([new DiscardItem(Material, 1, 0.100m, Guid.NewGuid())]);
    else
        discard.Reject();
}
```

- [ ] **Step 2: Executar os testes para confirmar o RED**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Ecocell.UnitTests.Entities.DiscardTests"
```

Expected: falha de compilação porque `Discard.Confirm` e `Discard.Reject` ainda não existem.

- [ ] **Step 3: Implementar as invariantes mínimas no agregado**

Refatorar o construtor para reutilizar `ValidateItems` e adicionar:

```csharp
public void Confirm(IEnumerable<DiscardItem> finalItems)
{
    EnsurePending();
    var itemList = ValidateItems(finalItems);

    _items.Clear();
    _items.AddRange(itemList);
    Status = DiscardStatus.Confirmed;
    MarkAsUpdated();
}

public void Reject()
{
    EnsurePending();
    Status = DiscardStatus.Rejected;
    MarkAsUpdated();
}

private void EnsurePending()
{
    if (Status != DiscardStatus.Pending)
        throw new InvalidOperationException("Somente descartes pendentes podem ser alterados.");
}

private static List<DiscardItem> ValidateItems(IEnumerable<DiscardItem> items)
{
    var itemList = items?.ToList()
        ?? throw new ArgumentNullException(nameof(items));

    if (itemList.Count == 0)
        throw new ArgumentException("O descarte deve possuir ao menos um item.", nameof(items));

    if (itemList.Select(item => item.Material).Distinct().Count() != itemList.Count)
        throw new ArgumentException("Cada material pode aparecer somente uma vez.", nameof(items));

    return itemList;
}
```

No construtor, substituir a validação duplicada por:

```csharp
var itemList = ValidateItems(items);
```

- [ ] **Step 4: Configurar `Status` como concurrency token**

Em `DiscardTypeConfiguration.cs`:

```csharp
builder.Property(value => value.Status)
    .HasConversion<string>()
    .HasMaxLength(20)
    .IsRequired()
    .IsConcurrencyToken();
```

- [ ] **Step 5: Gerar a migration de metadata**

Run:

```powershell
rtk dotnet ef migrations add ConfigureDiscardStatusConcurrency --project src/Ecocell.Api --startup-project src/Ecocell.Api
```

Expected: migration gerada e snapshot contendo `.IsConcurrencyToken()` em `Discard.Status`. `Up` e `Down` devem permanecer vazios, pois não existe mudança de schema. Se forem geradas operações SQL, não editar manualmente: revisar a configuração e regenerar.

- [ ] **Step 6: Executar testes e verificação de migration**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Ecocell.UnitTests.Entities.DiscardTests"
rtk dotnet ef migrations has-pending-model-changes --project src/Ecocell.Api --startup-project src/Ecocell.Api
```

Expected: testes passam; EF informa ausência de mudanças pendentes no modelo.

- [ ] **Step 7: Revisar e commitar a tarefa**

Run:

```powershell
rtk git diff --check
rtk git diff -- tests/Ecocell.UnitTests/Entities/DiscardTests.cs src/Ecocell.Api/Entities/Discard.cs src/Ecocell.Api/Database/TypeConfiguration/DiscardTypeConfiguration.cs src/Ecocell.Api/Migrations
rtk git add -- tests/Ecocell.UnitTests/Entities/DiscardTests.cs src/Ecocell.Api/Entities/Discard.cs src/Ecocell.Api/Database/TypeConfiguration/DiscardTypeConfiguration.cs src/Ecocell.Api/Migrations
rtk git commit -m "feat(discard): adiciona transições terminais"
```

Expected: commit contém apenas domínio, configuração, migration e testes desta tarefa.

---

### Task 2: Definir contrato e validação da confirmação

**Files:**
- Create: `src/Ecocell.Shared/Requests/Discards/RequestConfirmDiscardJson.cs`
- Create: `src/Ecocell.Api/Features/Discard/ConfirmDiscard.cs`
- Create: `tests/Ecocell.UnitTests/Features/Discard/ConfirmDiscardTests.cs`

**Interfaces:**
- Consumes: `Ecocell.Shared.Enums.ElectronicMaterial`, `Ecocell.Api.Enums.ElectronicMaterial`, `Result`.
- Produces: `RequestConfirmDiscardJson`, `RequestConfirmDiscardItemJson`, `ConfirmDiscard.ItemCommand`, `ConfirmDiscard.Command` e `ConfirmDiscard.Validator`.

- [ ] **Step 1: Escrever os testes RED do validator**

Criar `ConfirmDiscardTests.cs` estendendo `TestBase`. Iniciar com estes testes:

```csharp
[Fact]
public async Task Validate_ShouldSucceed_WhenCommandIsValid()
{
    var result = await new ConfirmDiscard.Validator()
        .ValidateAsync(ValidCommand(), CancellationToken.None);

    result.IsValid.ShouldBeTrue();
}

[Fact]
public async Task Validate_ShouldFail_WhenDiscardIdIsEmpty()
{
    var result = await new ConfirmDiscard.Validator()
        .ValidateAsync(ValidCommand() with { DiscardId = Guid.Empty }, CancellationToken.None);

    result.IsValid.ShouldBeFalse();
}

[Theory]
[InlineData(0)]
[InlineData(-1)]
public async Task Validate_ShouldFail_WhenQuantityIsNotPositive(int quantity)
{
    var item = ValidItem() with { Quantity = quantity };
    var result = await new ConfirmDiscard.Validator()
        .ValidateAsync(ValidCommand() with { Items = [item] }, CancellationToken.None);

    result.IsValid.ShouldBeFalse();
}

[Theory]
[InlineData("0")]
[InlineData("-0.001")]
[InlineData("0.0001")]
[InlineData("10000000.000")]
public async Task Validate_ShouldFail_WhenWeightIsInvalid(string rawWeight)
{
    var weight = decimal.Parse(rawWeight, CultureInfo.InvariantCulture);
    var item = ValidItem() with { ApproximateWeightKg = weight };
    var result = await new ConfirmDiscard.Validator()
        .ValidateAsync(ValidCommand() with { Items = [item] }, CancellationToken.None);

    result.IsValid.ShouldBeFalse();
}
```

Adicionar os demais casos:

```csharp
[Fact]
public async Task Validate_ShouldFail_WhenItemsAreNull()
{
    var result = await new ConfirmDiscard.Validator()
        .ValidateAsync(ValidCommand() with { Items = null! }, CancellationToken.None);

    result.IsValid.ShouldBeFalse();
}

[Fact]
public async Task Validate_ShouldFail_WhenItemsAreEmpty()
{
    var result = await new ConfirmDiscard.Validator()
        .ValidateAsync(ValidCommand() with { Items = [] }, CancellationToken.None);

    result.IsValid.ShouldBeFalse();
}

[Fact]
public async Task Validate_ShouldFail_WhenItemIsNull()
{
    var result = await new ConfirmDiscard.Validator()
        .ValidateAsync(ValidCommand() with { Items = [null!] }, CancellationToken.None);

    result.IsValid.ShouldBeFalse();
}

[Fact]
public async Task Validate_ShouldFail_WhenMaterialIsUndefined()
{
    var item = ValidItem() with { Material = (ElectronicMaterial)0 };
    var result = await new ConfirmDiscard.Validator()
        .ValidateAsync(ValidCommand() with { Items = [item] }, CancellationToken.None);

    result.IsValid.ShouldBeFalse();
}

[Fact]
public async Task Validate_ShouldFail_WhenMaterialIsDuplicated()
{
    var item = ValidItem();
    var result = await new ConfirmDiscard.Validator()
        .ValidateAsync(ValidCommand() with { Items = [item, item] }, CancellationToken.None);

    result.IsValid.ShouldBeFalse();
}
```

Helpers:

```csharp
private static ElectronicMaterial Material =>
    Enum.GetValues<ElectronicMaterial>()
        .First(value => Convert.ToInt32(value) > 0);

private static ConfirmDiscard.ItemCommand ValidItem() =>
    new()
    {
        Material = Material,
        Quantity = 1,
        ApproximateWeightKg = 0.250m,
    };

private static ConfirmDiscard.Command ValidCommand(Guid? discardId = null) =>
    new()
    {
        DiscardId = discardId ?? Guid.NewGuid(),
        Items = [ValidItem()],
    };
```

- [ ] **Step 2: Executar os testes para confirmar o RED**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Ecocell.UnitTests.Features.Discard.ConfirmDiscardTests"
```

Expected: falha de compilação porque o contrato e o slice ainda não existem.

- [ ] **Step 3: Criar o DTO público**

Criar `RequestConfirmDiscardJson.cs`:

```csharp
using Ecocell.Shared.Enums;

namespace Ecocell.Shared.Requests.Discards;

/// <summary>Solicita a confirmação de um descarte com a composição final recebida.</summary>
public sealed record RequestConfirmDiscardJson
{
    public IReadOnlyList<RequestConfirmDiscardItemJson> Items { get; init; } = [];
}

/// <summary>Informa um material validado pelo Ponto de Coleta.</summary>
public sealed record RequestConfirmDiscardItemJson
{
    public ElectronicMaterial Material { get; init; }
    public int Quantity { get; init; }
    public decimal ApproximateWeightKg { get; init; }
}
```

- [ ] **Step 4: Criar Command e Validator no slice**

Criar `ConfirmDiscard.cs` inicialmente sem handler e endpoint:

```csharp
using Ecocell.Api.Enums;
using Ecocell.Api.Shared;
using FluentValidation;
using Mediator;

namespace Ecocell.Api.Features.Discard;

public static class ConfirmDiscard
{
    public sealed record ItemCommand
    {
        public ElectronicMaterial Material { get; init; }
        public int Quantity { get; init; }
        public decimal ApproximateWeightKg { get; init; }
    }

    public sealed record Command : IRequest<Result>
    {
        public Guid DiscardId { get; init; }
        public IReadOnlyList<ItemCommand> Items { get; init; } = [];
    }

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(value => value.DiscardId)
                .NotEmpty()
                .WithMessage("O identificador do descarte é obrigatório.");

            RuleFor(value => value.Items)
                .NotEmpty()
                .WithMessage("Informe ao menos um item.");

            RuleFor(value => value.Items)
                .Must(items => items is not null
                    && items.All(item => item is not null)
                    && items.Select(item => item!.Material).Distinct().Count() == items.Count)
                .WithMessage("Cada material pode aparecer somente uma vez.");

            RuleForEach(value => value.Items)
                .NotNull()
                .WithMessage("O item do descarte é obrigatório.");

            RuleForEach(value => value.Items).ChildRules(item =>
            {
                item.RuleFor(value => value.Material)
                    .IsInEnum()
                    .Must(value => Convert.ToInt32(value) > 0)
                    .WithMessage("O material é inválido.");
                item.RuleFor(value => value.Quantity)
                    .GreaterThan(0)
                    .WithMessage("A quantidade deve ser maior que zero.");
                item.RuleFor(value => value.ApproximateWeightKg)
                    .GreaterThan(0)
                    .WithMessage("O peso aproximado deve ser maior que zero.")
                    .PrecisionScale(10, 3, false)
                    .WithMessage("O peso aproximado deve ter até 10 dígitos totais e 3 casas decimais.");
            });
        }
    }
}
```

- [ ] **Step 5: Executar os testes e refatorar duplicação acidental**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Ecocell.UnitTests.Features.Discard.ConfirmDiscardTests"
```

Expected: todos os testes de validação passam. Manter o validator dentro do slice; não extrair helper compartilhado com `RegisterDiscard` nesta tarefa.

- [ ] **Step 6: Revisar e commitar a tarefa**

Run:

```powershell
rtk git diff --check
rtk git add -- src/Ecocell.Shared/Requests/Discards/RequestConfirmDiscardJson.cs src/Ecocell.Api/Features/Discard/ConfirmDiscard.cs tests/Ecocell.UnitTests/Features/Discard/ConfirmDiscardTests.cs
rtk git commit -m "feat(discard): define contrato de confirmação"
```

---

### Task 3: Implementar confirmação transacional e outbox

**Files:**
- Modify: `tests/Ecocell.UnitTests/Features/Discard/ConfirmDiscardTests.cs`
- Modify: `src/Ecocell.Api/Features/Discard/ConfirmDiscard.cs`

**Interfaces:**
- Consumes: `Discard.Confirm(IEnumerable<DiscardItem>)`, `ICollectorPointAccessGuard.EnsureResponsibleActiveAsync(Guid, CancellationToken)`, `AppDbContext.CreditScoreRequests`, `new CreditScoreRequest(Guid discardId)`.
- Produces: `ConfirmDiscard.Handler : IRequestHandler<ConfirmDiscard.Command, Result>`.

- [ ] **Step 1: Adicionar setup e teste RED do caminho feliz**

No construtor de `ConfirmDiscardTests`, configurar guard e handler reais:

```csharp
private readonly Mock<ICollectorPointAccessGuard> _guard = new();
private readonly ConfirmDiscard.Handler _handler;

public ConfirmDiscardTests()
{
    _guard.Setup(value => value.EnsureResponsibleActiveAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(Result.Success());

    _handler = new ConfirmDiscard.Handler(
        DbContext,
        CreateLoggerMock<ConfirmDiscard.Handler>().Object,
        new ConfirmDiscard.Validator(),
        _guard.Object);
}
```

Adicionar:

```csharp
[Fact]
public async Task Handle_ShouldPersistFinalItemsAndCreditRequest_WhenCommandIsValid()
{
    var fixture = AddPendingDiscard();
    var finalMaterial = SecondMaterial();
    var finalRule = AddRule(
        fixture.CollectorPointId,
        finalMaterial,
        fixture.Discard.CreatedAt.AddMinutes(-1));
    var command = ValidCommand(fixture.Discard.Id) with
    {
        Items =
        [
            new ConfirmDiscard.ItemCommand
            {
                Material = finalMaterial,
                Quantity = 3,
                ApproximateWeightKg = 0.750m,
            },
        ],
    };

    var result = await _handler.Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DbContext.ChangeTracker.Clear();
    var persisted = await DbContext.Discards
        .Include(value => value.Items)
        .SingleAsync(value => value.Id == fixture.Discard.Id);
    persisted.Status.ShouldBe(DiscardStatus.Confirmed);
    persisted.Items.ShouldHaveSingleItem();
    persisted.Items.Single().Material.ShouldBe(finalMaterial);
    persisted.Items.Single().Quantity.ShouldBe(3);
    persisted.Items.Single().ApproximateWeightKg.ShouldBe(0.750m);
    persisted.Items.Single().MaterialScoreRuleId.ShouldBe(finalRule.Id);
    (await DbContext.CreditScoreRequests.CountAsync(
        value => value.DiscardId == fixture.Discard.Id)).ShouldBe(1);
}
```

Adicionar estes helpers à classe:

```csharp
private sealed record DiscardFixture(
    Ecocell.Api.Entities.Discard Discard,
    Guid CollectorPointId,
    Guid DepositorId);

private DiscardFixture AddPendingDiscard()
{
    var faker = new Faker("pt_BR");
    var depositor = new NaturalPerson(
        faker.Name.FullName(),
        faker.Person.Cpf(includeFormatSymbols: false),
        DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
        Role.User,
        faker.Internet.Email(),
        Journey.Depositor);
    var point = new LegalPerson(
        faker.Company.CompanyName(),
        faker.Company.CompanyName(),
        faker.Company.Cnpj(includeFormatSymbols: false),
        faker.Internet.Email(),
        Journey.CollectPoint,
        responsiblePersonId: depositor.Id);
    point.Approve(Role.Admin);

    var rule = new MaterialScoreRule(
        point.Id,
        Material,
        10m,
        MaterialScoreUnit.PerUnit,
        DateTime.UtcNow.AddDays(-1));
    var discard = new Ecocell.Api.Entities.Discard(
        depositor.Id,
        point.Id,
        [new DiscardItem(Material, 1, 0.250m, rule.Id)]);

    DbContext.NaturalPeople.Add(depositor);
    DbContext.LegalPeople.Add(point);
    DbContext.MaterialScoreRules.Add(rule);
    DbContext.Discards.Add(discard);
    DbContext.SaveChanges();
    return new DiscardFixture(discard, point.Id, depositor.Id);
}

private MaterialScoreRule AddRule(
    Guid collectorPointId,
    ElectronicMaterial material,
    DateTime validFrom,
    DateTime? validTo = null)
{
    var rule = new MaterialScoreRule(
        collectorPointId,
        material,
        10m,
        MaterialScoreUnit.PerUnit,
        validFrom);
    if (validTo is not null)
        rule.Close(validTo.Value);

    DbContext.MaterialScoreRules.Add(rule);
    DbContext.SaveChanges();
    return rule;
}

private static ElectronicMaterial SecondMaterial() =>
    Enum.GetValues<ElectronicMaterial>()
        .First(value => value != Material && Convert.ToInt32(value) > 0);

private static ConfirmDiscard.Command CommandFor(
    Guid discardId,
    ElectronicMaterial material) =>
    new()
    {
        DiscardId = discardId,
        Items =
        [
            new ConfirmDiscard.ItemCommand
            {
                Material = material,
                Quantity = 1,
                ApproximateWeightKg = 0.250m,
            },
        ],
    };
```

Adicionar `using Bogus`, `Bogus.Extensions.Brazil`, `Ecocell.Api.Entities`, `Ecocell.Api.Enums`, `Ecocell.Api.Services.CollectorPoints`, `Microsoft.EntityFrameworkCore`, `Moq` e `Shouldly`.

- [ ] **Step 2: Executar o teste para confirmar o RED**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~ConfirmDiscardTests.Handle_ShouldPersistFinalItemsAndCreditRequest"
```

Expected: falha de compilação porque `ConfirmDiscard.Handler` ainda não existe.

- [ ] **Step 3: Implementar o handler até o caminho feliz ficar verde**

Adicionar ao slice:

```csharp
public sealed class Handler : IRequestHandler<Command, Result>
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<Handler> _logger;
    private readonly IValidator<Command> _validator;
    private readonly ICollectorPointAccessGuard _accessGuard;

    public Handler(
        AppDbContext dbContext,
        ILogger<Handler> logger,
        IValidator<Command> validator,
        ICollectorPointAccessGuard accessGuard)
    {
        _dbContext = dbContext;
        _logger = logger;
        _validator = validator;
        _accessGuard = accessGuard;
    }

    public async ValueTask<Result> Handle(Command request, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return Result.Failure(Error.ErrorOnValidation(
                validation.Errors.Select(value => value.ErrorMessage).ToList()));
        }

        var discard = await _dbContext.Discards
            .Include(value => value.Items)
            .SingleOrDefaultAsync(value => value.Id == request.DiscardId, ct);

        if (discard is null)
            return Result.Failure(Error.NotFound("Descarte não encontrado."));

        var access = await _accessGuard.EnsureResponsibleActiveAsync(
            discard.CollectorPointId,
            ct);
        if (access.IsFailure)
            return access;

        if (discard.Status != DiscardStatus.Pending)
            return Result.Failure(Error.Conflict("O descarte já foi processado."));

        var requestedItems = request.Items.ToArray();
        var materials = requestedItems.Select(value => value.Material).ToArray();
        var rules = await _dbContext.MaterialScoreRules
            .AsNoTracking()
            .Where(value => value.LegalPersonId == discard.CollectorPointId
                && materials.Contains(value.Material)
                && value.ValidFrom <= discard.CreatedAt
                && (value.ValidTo == null || value.ValidTo > discard.CreatedAt))
            .ToListAsync(ct);
        var rulesByMaterial = rules.ToDictionary(value => value.Material);
        var unsupported = materials
            .Where(value => !rulesByMaterial.ContainsKey(value))
            .Distinct()
            .Order()
            .ToArray();

        if (unsupported.Length > 0)
        {
            return Result.Failure(Error.Conflict(
                $"Materiais sem regra vigente na abertura: {string.Join(", ", unsupported)}."));
        }

        var finalItems = requestedItems
            .Select(value => new DiscardItem(
                value.Material,
                value.Quantity,
                value.ApproximateWeightKg,
                rulesByMaterial[value.Material].Id))
            .ToArray();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            _dbContext.DiscardItems.RemoveRange(discard.Items);
            await _dbContext.SaveChangesAsync(ct);

            discard.Confirm(finalItems);
            _dbContext.CreditScoreRequests.Add(new CreditScoreRequest(discard.Id));
            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogWarning(
                exception,
                "Conflito concorrente ao confirmar o descarte {DiscardId}.",
                discard.Id);
            return Result.Failure(Error.Conflict(
                "O descarte foi alterado por outra operação."));
        }

        _logger.LogInformation(
            "Descarte {DiscardId} confirmado pelo Ponto de Coleta {CollectorPointId}.",
            discard.Id,
            discard.CollectorPointId);
        return Result.Success();
    }
}
```

Adicionar os `using` de `Database`, `Entities`, `Services.CollectorPoints`, `Microsoft.EntityFrameworkCore` e `Microsoft.Extensions.Logging`.

- [ ] **Step 4: Executar o caminho feliz para confirmar o GREEN**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~ConfirmDiscardTests.Handle_ShouldPersistFinalItemsAndCreditRequest"
```

Expected: PASS com status, itens finais, regra histórica e outbox persistidos.

- [ ] **Step 5: Escrever RED para regra histórica e atomicidade**

Adicionar testes concretos:

```csharp
[Fact]
public async Task Handle_ShouldUseRuleActiveAtOpening_WhenRuleWasVersionedLater()
{
    var fixture = AddPendingDiscard();
    var original = AddRule(
        fixture.CollectorPointId,
        SecondMaterial(),
        fixture.Discard.CreatedAt.AddHours(-1),
        fixture.Discard.CreatedAt.AddMinutes(1));
    AddRule(
        fixture.CollectorPointId,
        SecondMaterial(),
        fixture.Discard.CreatedAt.AddMinutes(1));

    var command = CommandFor(fixture.Discard.Id, SecondMaterial());
    var result = await _handler.Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DbContext.ChangeTracker.Clear();
    var item = await DbContext.DiscardItems.SingleAsync();
    item.MaterialScoreRuleId.ShouldBe(original.Id);
}

[Fact]
public async Task Handle_ShouldReturnConflictWithoutWriting_WhenMaterialHadNoRuleAtOpening()
{
    var fixture = AddPendingDiscard();
    var material = SecondMaterial();
    AddRule(
        fixture.CollectorPointId,
        material,
        fixture.Discard.CreatedAt.AddMinutes(1));

    var result = await _handler.Handle(
        CommandFor(fixture.Discard.Id, material),
        CancellationToken.None);

    result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    DbContext.ChangeTracker.Clear();
    var persisted = await DbContext.Discards
        .Include(value => value.Items)
        .SingleAsync(value => value.Id == fixture.Discard.Id);
    persisted.Status.ShouldBe(DiscardStatus.Pending);
    (await DbContext.CreditScoreRequests.CountAsync()).ShouldBe(0);
}
```

Adicionar também:

```csharp
[Fact]
public async Task Handle_ShouldReturnValidationErrorWithoutCallingGuard_WhenCommandIsInvalid()
{
    var result = await _handler.Handle(
        ValidCommand() with { DiscardId = Guid.Empty },
        CancellationToken.None);

    result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    _guard.Verify(value => value.EnsureResponsibleActiveAsync(
        It.IsAny<Guid>(),
        It.IsAny<CancellationToken>()), Times.Never);
}

[Fact]
public async Task Handle_ShouldReturnNotFound_WhenDiscardDoesNotExist()
{
    var result = await _handler.Handle(
        ValidCommand(),
        CancellationToken.None);

    result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    _guard.Verify(value => value.EnsureResponsibleActiveAsync(
        It.IsAny<Guid>(),
        It.IsAny<CancellationToken>()), Times.Never);
}

[Fact]
public async Task Handle_ShouldPropagateAccessErrorBeforeCheckingStatus_WhenGuardFails()
{
    var fixture = AddPendingDiscard();
    fixture.Discard.Reject();
    await DbContext.SaveChangesAsync();
    _guard.Setup(value => value.EnsureResponsibleActiveAsync(
            fixture.CollectorPointId,
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(Result.Failure(Error.NotFound("Ponto de coleta não encontrado.")));

    var result = await _handler.Handle(
        ValidCommand(fixture.Discard.Id),
        CancellationToken.None);

    result.Error.Code.ShouldBe(ErrorCodes.NotFound);
}

[Fact]
public async Task Handle_ShouldReturnConflictWithoutWriting_WhenDiscardIsTerminal()
{
    var fixture = AddPendingDiscard();
    fixture.Discard.Reject();
    await DbContext.SaveChangesAsync();

    var result = await _handler.Handle(
        ValidCommand(fixture.Discard.Id),
        CancellationToken.None);

    result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    (await DbContext.CreditScoreRequests.CountAsync()).ShouldBe(0);
}

[Fact]
public async Task Handle_ShouldReplaceMultipleItems_WhenFinalListChangesComposition()
{
    var fixture = AddPendingDiscard();
    var second = SecondMaterial();
    var secondRule = AddRule(
        fixture.CollectorPointId,
        second,
        fixture.Discard.CreatedAt.AddMinutes(-1));
    var command = ValidCommand(fixture.Discard.Id) with
    {
        Items =
        [
            new ConfirmDiscard.ItemCommand
            {
                Material = Material,
                Quantity = 2,
                ApproximateWeightKg = 0.500m,
            },
            new ConfirmDiscard.ItemCommand
            {
                Material = second,
                Quantity = 1,
                ApproximateWeightKg = 1.250m,
            },
        ],
    };

    var result = await _handler.Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DbContext.ChangeTracker.Clear();
    var items = await DbContext.DiscardItems
        .OrderBy(value => value.Material)
        .ToListAsync();
    items.Count.ShouldBe(2);
    items.Single(value => value.Material == second)
        .MaterialScoreRuleId.ShouldBe(secondRule.Id);
}
```

- [ ] **Step 6: Executar os testes para observar RED e depois GREEN**

Run antes dos ajustes finais:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Ecocell.UnitTests.Features.Discard.ConfirmDiscardTests"
```

Expected RED inicial: qualquer branch ou helper ainda ausente falha de forma específica. Completar somente helpers de seed e ajustes mínimos do handler; executar o mesmo comando novamente.

Expected GREEN final: todos os testes de `ConfirmDiscardTests` passam.

- [ ] **Step 7: Revisar transação e commitar a tarefa**

Confirmar no diff que:

- a primeira persistência remove somente os itens antigos dentro da transação;
- a segunda persistência grava itens finais, `Confirmed` e `CreditScoreRequest`;
- qualquer exceção antes do commit reverte a primeira persistência;
- nenhum código chama dispatcher ou `CreditScoreJob` diretamente.

Run:

```powershell
rtk git diff --check
rtk git add -- src/Ecocell.Api/Features/Discard/ConfirmDiscard.cs tests/Ecocell.UnitTests/Features/Discard/ConfirmDiscardTests.cs
rtk git commit -m "feat(discard): implementa confirmação transacional"
```

---

### Task 4: Implementar rejeição terminal

**Files:**
- Create: `src/Ecocell.Api/Features/Discard/RejectDiscard.cs`
- Create: `tests/Ecocell.UnitTests/Features/Discard/RejectDiscardTests.cs`

**Interfaces:**
- Consumes: `Discard.Reject()` e `ICollectorPointAccessGuard.EnsureResponsibleActiveAsync(Guid, CancellationToken)`.
- Produces: `RejectDiscard.Command`, `RejectDiscard.Validator` e `RejectDiscard.Handler`.

- [ ] **Step 1: Escrever testes RED de validação e caminho feliz**

Criar `RejectDiscardTests.cs` estendendo `TestBase`, com guard verde por padrão:

```csharp
[Fact]
public async Task Handle_ShouldPersistRejectedWithoutCreditRequest_WhenDiscardIsPending()
{
    var discard = AddPendingDiscard();

    var result = await CreateHandler().Handle(
        new RejectDiscard.Command(discard.Id),
        CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DbContext.ChangeTracker.Clear();
    var persisted = await DbContext.Discards.SingleAsync(value => value.Id == discard.Id);
    persisted.Status.ShouldBe(DiscardStatus.Rejected);
    (await DbContext.CreditScoreRequests.CountAsync()).ShouldBe(0);
}

[Fact]
public async Task Handle_ShouldReturnValidationErrorWithoutCallingGuard_WhenIdIsEmpty()
{
    var result = await CreateHandler().Handle(
        new RejectDiscard.Command(Guid.Empty),
        CancellationToken.None);

    result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    _guard.Verify(value => value.EnsureResponsibleActiveAsync(
        It.IsAny<Guid>(),
        It.IsAny<CancellationToken>()), Times.Never);
}
```

Adicionar os demais testes:

```csharp
public static TheoryData<Error> AccessErrors => new()
{
    Error.Forbidden(),
    Error.NotFound("Ponto de coleta não encontrado."),
    Error.Conflict("Ponto de coleta não está ativo."),
};

[Fact]
public async Task Handle_ShouldReturnNotFoundWithoutCallingGuard_WhenDiscardDoesNotExist()
{
    var result = await CreateHandler().Handle(
        new RejectDiscard.Command(Guid.NewGuid()),
        CancellationToken.None);

    result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    _guard.Verify(value => value.EnsureResponsibleActiveAsync(
        It.IsAny<Guid>(),
        It.IsAny<CancellationToken>()), Times.Never);
}

[Theory]
[MemberData(nameof(AccessErrors))]
public async Task Handle_ShouldPropagateAccessError_WhenGuardFails(Error error)
{
    var discard = AddPendingDiscard();
    _guard.Setup(value => value.EnsureResponsibleActiveAsync(
            discard.CollectorPointId,
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(Result.Failure(error));

    var result = await CreateHandler().Handle(
        new RejectDiscard.Command(discard.Id),
        CancellationToken.None);

    result.Error.ShouldBe(error);
    DbContext.ChangeTracker.Clear();
    (await DbContext.Discards.SingleAsync(value => value.Id == discard.Id))
        .Status.ShouldBe(DiscardStatus.Pending);
}

[Theory]
[InlineData(DiscardStatus.Confirmed)]
[InlineData(DiscardStatus.Rejected)]
public async Task Handle_ShouldReturnConflict_WhenDiscardIsTerminal(DiscardStatus status)
{
    var discard = AddPendingDiscard();
    if (status == DiscardStatus.Confirmed)
        discard.Confirm(discard.Items.ToArray());
    else
        discard.Reject();
    await DbContext.SaveChangesAsync();

    var result = await CreateHandler().Handle(
        new RejectDiscard.Command(discard.Id),
        CancellationToken.None);

    result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    (await DbContext.CreditScoreRequests.CountAsync()).ShouldBe(0);
}

[Fact]
public async Task Handle_ShouldAuthorizeBeforeRevealingTerminalStatus()
{
    var discard = AddPendingDiscard();
    discard.Reject();
    await DbContext.SaveChangesAsync();
    var hidden = Error.NotFound("Ponto de coleta não encontrado.");
    _guard.Setup(value => value.EnsureResponsibleActiveAsync(
            discard.CollectorPointId,
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(Result.Failure(hidden));

    var result = await CreateHandler().Handle(
        new RejectDiscard.Command(discard.Id),
        CancellationToken.None);

    result.Error.ShouldBe(hidden);
}
```

Usar setup concreto, sem compartilhar helpers com a classe de confirmação:

```csharp
private readonly Mock<ICollectorPointAccessGuard> _guard = new();

public RejectDiscardTests()
{
    _guard.Setup(value => value.EnsureResponsibleActiveAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(Result.Success());
}

private RejectDiscard.Handler CreateHandler() =>
    new(
        DbContext,
        CreateLoggerMock<RejectDiscard.Handler>().Object,
        new RejectDiscard.Validator(),
        _guard.Object);

private Ecocell.Api.Entities.Discard AddPendingDiscard()
{
    var faker = new Faker("pt_BR");
    var depositor = new NaturalPerson(
        faker.Name.FullName(),
        faker.Person.Cpf(includeFormatSymbols: false),
        DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
        Role.User,
        faker.Internet.Email(),
        Journey.Depositor);
    var point = new LegalPerson(
        faker.Company.CompanyName(),
        faker.Company.CompanyName(),
        faker.Company.Cnpj(includeFormatSymbols: false),
        faker.Internet.Email(),
        Journey.CollectPoint,
        responsiblePersonId: depositor.Id);
    point.Approve(Role.Admin);
    var rule = new MaterialScoreRule(
        point.Id,
        ElectronicMaterial.Battery,
        10m,
        MaterialScoreUnit.PerUnit,
        DateTime.UtcNow.AddDays(-1));
    var discard = new Ecocell.Api.Entities.Discard(
        depositor.Id,
        point.Id,
        [new DiscardItem(ElectronicMaterial.Battery, 1, 0.250m, rule.Id)]);

    DbContext.AddRange(depositor, point, rule, discard);
    DbContext.SaveChanges();
    return discard;
}
```

Adicionar os mesmos `using` concretos da classe de confirmação para entidades, enums, guard, EF Core, Moq, Bogus e Shouldly.

- [ ] **Step 2: Executar os testes para confirmar o RED**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Ecocell.UnitTests.Features.Discard.RejectDiscardTests"
```

Expected: falha de compilação porque o slice não existe.

- [ ] **Step 3: Implementar o slice mínimo**

Criar `RejectDiscard.cs`:

```csharp
using Ecocell.Api.Database;
using Ecocell.Api.Enums;
using Ecocell.Api.Services.CollectorPoints;
using Ecocell.Api.Shared;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Ecocell.Api.Features.Discard;

public static class RejectDiscard
{
    public sealed record Command(Guid DiscardId) : IRequest<Result>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(value => value.DiscardId)
                .NotEmpty()
                .WithMessage("O identificador do descarte é obrigatório.");
        }
    }

    public sealed class Handler : IRequestHandler<Command, Result>
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<Handler> _logger;
        private readonly IValidator<Command> _validator;
        private readonly ICollectorPointAccessGuard _accessGuard;

        public Handler(
            AppDbContext dbContext,
            ILogger<Handler> logger,
            IValidator<Command> validator,
            ICollectorPointAccessGuard accessGuard)
        {
            _dbContext = dbContext;
            _logger = logger;
            _validator = validator;
            _accessGuard = accessGuard;
        }

        public async ValueTask<Result> Handle(Command request, CancellationToken ct)
        {
            var validation = await _validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                return Result.Failure(Error.ErrorOnValidation(
                    validation.Errors.Select(value => value.ErrorMessage).ToList()));
            }

            var discard = await _dbContext.Discards
                .SingleOrDefaultAsync(value => value.Id == request.DiscardId, ct);
            if (discard is null)
                return Result.Failure(Error.NotFound("Descarte não encontrado."));

            var access = await _accessGuard.EnsureResponsibleActiveAsync(
                discard.CollectorPointId,
                ct);
            if (access.IsFailure)
                return access;

            if (discard.Status != DiscardStatus.Pending)
                return Result.Failure(Error.Conflict("O descarte já foi processado."));

            discard.Reject();
            try
            {
                await _dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Conflito concorrente ao rejeitar o descarte {DiscardId}.",
                    discard.Id);
                return Result.Failure(Error.Conflict(
                    "O descarte foi alterado por outra operação."));
            }

            _logger.LogInformation(
                "Descarte {DiscardId} rejeitado pelo Ponto de Coleta {CollectorPointId}.",
                discard.Id,
                discard.CollectorPointId);
            return Result.Success();
        }
    }
}
```

- [ ] **Step 4: Executar testes e confirmar o GREEN**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Ecocell.UnitTests.Features.Discard.RejectDiscardTests"
```

Expected: todos os testes passam; nenhum teste encontra `CreditScoreRequest` persistido.

- [ ] **Step 5: Revisar e commitar a tarefa**

Run:

```powershell
rtk git diff --check
rtk git add -- src/Ecocell.Api/Features/Discard/RejectDiscard.cs tests/Ecocell.UnitTests/Features/Discard/RejectDiscardTests.cs
rtk git commit -m "feat(discard): implementa rejeição terminal"
```

---

### Task 5: Expor endpoints e validar o contrato PostgreSQL

**Files:**
- Modify: `src/Ecocell.Api/Features/Discard/ConfirmDiscard.cs`
- Modify: `src/Ecocell.Api/Features/Discard/RejectDiscard.cs`
- Modify: `tests/Ecocell.IntegrationTests/IntegrationTestBase.cs`
- Create: `tests/Ecocell.IntegrationTests/Features/Discard/ConfirmDiscardTests.cs`
- Create: `tests/Ecocell.IntegrationTests/Features/Discard/RejectDiscardTests.cs`

**Interfaces:**
- Consumes: `RequestConfirmDiscardJson`, `ConfirmDiscard.Command`, `RejectDiscard.Command`, autenticação real e migrations.
- Produces: `POST /api/v1/discards/{id}/confirm` e `POST /api/v1/discards/{id}/reject`, ambos com `204 No Content` no sucesso.

- [ ] **Step 1: Adicionar helper de seed compartilhado**

Em `IntegrationTestBase`, adicionar:

```csharp
protected async Task<Discard> CreatePendingDiscardAsync(
    Guid depositorId,
    Guid collectorPointId,
    params ApiEnums.ElectronicMaterial[] materials)
{
    if (materials.Length == 0)
        throw new ArgumentException("Informe ao menos um material.", nameof(materials));

    var validFrom = DateTime.UtcNow.AddDays(-1);
    var rules = materials
        .Distinct()
        .Select(material => new MaterialScoreRule(
            collectorPointId,
            material,
            10m,
            ApiEnums.MaterialScoreUnit.PerUnit,
            validFrom))
        .ToArray();
    var items = rules
        .Select(rule => new DiscardItem(rule.Material, 1, 0.250m, rule.Id))
        .ToArray();
    var discard = new Discard(depositorId, collectorPointId, items);

    await using var scope = Fixture.Factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.MaterialScoreRules.AddRange(rules);
    db.Discards.Add(discard);
    await db.SaveChangesAsync();
    return discard;
}
```

- [ ] **Step 2: Escrever testes HTTP RED da confirmação**

Criar `ConfirmDiscardTests.cs` com:

- `Post_ShouldReturn401_WhenTokenIsMissing`;
- `Post_ShouldReturn204AndPersistFinalItemsAndCreditRequest_WhenRequestIsValid`;
- `Post_ShouldReturn404_WhenDiscardBelongsToAnotherResponsible`;
- `Post_ShouldReturn409_WhenDiscardIsTerminal`;
- `Post_ShouldReturn409_WhenFinalMaterialHadNoRuleAtOpening`;
- `Post_ShouldReplaceItemsWithoutUniqueIndexConflict_WhenMaterialAlreadyExists`.

Usar estes imports e helper:

```csharp
using System.Net;
using System.Net.Http.Json;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Shared.Requests.Discards;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ApiEnums = Ecocell.Api.Enums;
using SharedEnums = Ecocell.Shared.Enums;

private static RequestConfirmDiscardJson ValidRequest(
    SharedEnums.ElectronicMaterial material) =>
    new()
    {
        Items =
        [
            new RequestConfirmDiscardItemJson
            {
                Material = material,
                Quantity = 3,
                ApproximateWeightKg = 0.750m,
            },
        ],
    };
```

Corpos dos testes:

```csharp
[Fact]
public async Task Post_ShouldReturn401_WhenTokenIsMissing()
{
    var response = await Client.PostAsJsonAsync(
        $"api/v1/discards/{Guid.NewGuid()}/confirm",
        ValidRequest(SharedEnums.ElectronicMaterial.Battery));

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
}

[Fact]
public async Task Post_ShouldReturn204AndPersistFinalItemsAndCreditRequest_WhenRequestIsValid()
{
    var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
    var responsibleId = await GetPersonIdByEmailAsync(email);
    var point = await CreateActiveCollectorPointAsync(responsibleId);
    var discard = await CreatePendingDiscardAsync(
        responsibleId,
        point.Id,
        ApiEnums.ElectronicMaterial.Battery);
    using var authClient = CreateAuthenticatedClient(jwt);

    var response = await authClient.PostAsJsonAsync(
        $"api/v1/discards/{discard.Id}/confirm",
        ValidRequest(SharedEnums.ElectronicMaterial.Battery));

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    await using var scope = Fixture.Factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var persisted = await db.Discards
        .Include(value => value.Items)
        .SingleAsync(value => value.Id == discard.Id);
    persisted.Status.ShouldBe(ApiEnums.DiscardStatus.Confirmed);
    persisted.Items.ShouldHaveSingleItem();
    persisted.Items.Single().Quantity.ShouldBe(3);
    persisted.Items.Single().ApproximateWeightKg.ShouldBe(0.750m);
    (await db.CreditScoreRequests.CountAsync(
        value => value.DiscardId == discard.Id)).ShouldBe(1);
}

[Fact]
public async Task Post_ShouldReturn404_WhenDiscardBelongsToAnotherResponsible()
{
    var (_, callerJwt) = await CreateAndLoginNaturalPersonAsync();
    var (ownerEmail, _) = await CreateAndLoginNaturalPersonAsync();
    var ownerId = await GetPersonIdByEmailAsync(ownerEmail);
    var point = await CreateActiveCollectorPointAsync(ownerId);
    var discard = await CreatePendingDiscardAsync(
        ownerId,
        point.Id,
        ApiEnums.ElectronicMaterial.Battery);
    using var authClient = CreateAuthenticatedClient(callerJwt);

    var response = await authClient.PostAsJsonAsync(
        $"api/v1/discards/{discard.Id}/confirm",
        ValidRequest(SharedEnums.ElectronicMaterial.Battery));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
}

[Fact]
public async Task Post_ShouldReturn409_WhenDiscardIsTerminal()
{
    var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
    var responsibleId = await GetPersonIdByEmailAsync(email);
    var point = await CreateActiveCollectorPointAsync(responsibleId);
    var discard = await CreatePendingDiscardAsync(
        responsibleId,
        point.Id,
        ApiEnums.ElectronicMaterial.Battery);
    await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tracked = await db.Discards.SingleAsync(value => value.Id == discard.Id);
        tracked.Reject();
        await db.SaveChangesAsync();
    }
    using var authClient = CreateAuthenticatedClient(jwt);

    var response = await authClient.PostAsJsonAsync(
        $"api/v1/discards/{discard.Id}/confirm",
        ValidRequest(SharedEnums.ElectronicMaterial.Battery));

    response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
}

[Fact]
public async Task Post_ShouldReturn409_WhenFinalMaterialHadNoRuleAtOpening()
{
    var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
    var responsibleId = await GetPersonIdByEmailAsync(email);
    var point = await CreateActiveCollectorPointAsync(responsibleId);
    var discard = await CreatePendingDiscardAsync(
        responsibleId,
        point.Id,
        ApiEnums.ElectronicMaterial.Battery);
    await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.MaterialScoreRules.Add(new MaterialScoreRule(
            point.Id,
            ApiEnums.ElectronicMaterial.Notebook,
            20m,
            ApiEnums.MaterialScoreUnit.PerUnit,
            discard.CreatedAt.AddMinutes(1)));
        await db.SaveChangesAsync();
    }
    using var authClient = CreateAuthenticatedClient(jwt);

    var response = await authClient.PostAsJsonAsync(
        $"api/v1/discards/{discard.Id}/confirm",
        ValidRequest(SharedEnums.ElectronicMaterial.Notebook));

    response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    await using var assertScope = Fixture.Factory.Services.CreateAsyncScope();
    var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
    (await assertDb.Discards.SingleAsync(value => value.Id == discard.Id))
        .Status.ShouldBe(ApiEnums.DiscardStatus.Pending);
    (await assertDb.CreditScoreRequests.CountAsync()).ShouldBe(0);
}

[Fact]
public async Task Post_ShouldReplaceItemsWithoutUniqueIndexConflict_WhenMaterialsAlreadyExist()
{
    var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
    var responsibleId = await GetPersonIdByEmailAsync(email);
    var point = await CreateActiveCollectorPointAsync(responsibleId);
    var discard = await CreatePendingDiscardAsync(
        responsibleId,
        point.Id,
        ApiEnums.ElectronicMaterial.Battery,
        ApiEnums.ElectronicMaterial.Notebook);
    using var authClient = CreateAuthenticatedClient(jwt);
    var request = new RequestConfirmDiscardJson
    {
        Items =
        [
            new RequestConfirmDiscardItemJson
            {
                Material = SharedEnums.ElectronicMaterial.Battery,
                Quantity = 2,
                ApproximateWeightKg = 0.500m,
            },
            new RequestConfirmDiscardItemJson
            {
                Material = SharedEnums.ElectronicMaterial.Notebook,
                Quantity = 1,
                ApproximateWeightKg = 1.500m,
            },
        ],
    };

    var response = await authClient.PostAsJsonAsync(
        $"api/v1/discards/{discard.Id}/confirm",
        request);

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    await using var scope = Fixture.Factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    (await db.DiscardItems.CountAsync(value => value.DiscardId == discard.Id)).ShouldBe(2);
}
```

O caminho feliz deve consultar um novo scope e afirmar, nesta ordem:

```csharp
response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
discard.Status.ShouldBe(ApiEnums.DiscardStatus.Confirmed);
discard.Items.ShouldHaveSingleItem();
discard.Items.Single().Quantity.ShouldBe(3);
(await db.CreditScoreRequests.CountAsync(
    value => value.DiscardId == discardId)).ShouldBe(1);
```

Para regra criada após abertura, criar `MaterialScoreRule` com `ValidFrom = discard.CreatedAt.AddMinutes(1)` e esperar `409` sem alteração do agregado e sem outbox.

- [ ] **Step 3: Escrever testes HTTP RED da rejeição**

Criar `RejectDiscardTests.cs` com:

- `Post_ShouldReturn401_WhenTokenIsMissing`;
- `Post_ShouldReturn204AndPersistRejectedWithoutCreditRequest_WhenDiscardIsPending`;
- `Post_ShouldReturn404_WhenDiscardBelongsToAnotherResponsible`;
- `Post_ShouldReturn409_WhenDiscardIsTerminal`.

Usar os mesmos imports de banco, HTTP, DI, EF Core, Shouldly e aliases do teste de confirmação. Implementar:

```csharp
[Fact]
public async Task Post_ShouldReturn401_WhenTokenIsMissing()
{
    var response = await Client.PostAsync(
        $"api/v1/discards/{Guid.NewGuid()}/reject",
        content: null);

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
}

[Fact]
public async Task Post_ShouldReturn204AndPersistRejectedWithoutCreditRequest_WhenDiscardIsPending()
{
    var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
    var responsibleId = await GetPersonIdByEmailAsync(email);
    var point = await CreateActiveCollectorPointAsync(responsibleId);
    var discard = await CreatePendingDiscardAsync(
        responsibleId,
        point.Id,
        ApiEnums.ElectronicMaterial.Battery);
    using var authClient = CreateAuthenticatedClient(jwt);

    var response = await authClient.PostAsync(
        $"api/v1/discards/{discard.Id}/reject",
        content: null);

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    await using var scope = Fixture.Factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    (await db.Discards.SingleAsync(value => value.Id == discard.Id))
        .Status.ShouldBe(ApiEnums.DiscardStatus.Rejected);
    (await db.CreditScoreRequests.CountAsync(
        value => value.DiscardId == discard.Id)).ShouldBe(0);
}

[Fact]
public async Task Post_ShouldReturn404_WhenDiscardBelongsToAnotherResponsible()
{
    var (_, callerJwt) = await CreateAndLoginNaturalPersonAsync();
    var (ownerEmail, _) = await CreateAndLoginNaturalPersonAsync();
    var ownerId = await GetPersonIdByEmailAsync(ownerEmail);
    var point = await CreateActiveCollectorPointAsync(ownerId);
    var discard = await CreatePendingDiscardAsync(
        ownerId,
        point.Id,
        ApiEnums.ElectronicMaterial.Battery);
    using var authClient = CreateAuthenticatedClient(callerJwt);

    var response = await authClient.PostAsync(
        $"api/v1/discards/{discard.Id}/reject",
        content: null);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
}

[Fact]
public async Task Post_ShouldReturn409_WhenDiscardIsTerminal()
{
    var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
    var responsibleId = await GetPersonIdByEmailAsync(email);
    var point = await CreateActiveCollectorPointAsync(responsibleId);
    var discard = await CreatePendingDiscardAsync(
        responsibleId,
        point.Id,
        ApiEnums.ElectronicMaterial.Battery);
    await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tracked = await db.Discards
            .Include(value => value.Items)
            .SingleAsync(value => value.Id == discard.Id);
        tracked.Confirm(tracked.Items.ToArray());
        await db.SaveChangesAsync();
    }
    using var authClient = CreateAuthenticatedClient(jwt);

    var response = await authClient.PostAsync(
        $"api/v1/discards/{discard.Id}/reject",
        content: null);

    response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
}
```

Após o `204`, recarregar o contexto e afirmar `Rejected` e zero `CreditScoreRequests` para o descarte.

- [ ] **Step 4: Executar integração para confirmar o RED das rotas**

Run:

```powershell
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~Ecocell.IntegrationTests.Features.Discard.ConfirmDiscardTests|FullyQualifiedName~Ecocell.IntegrationTests.Features.Discard.RejectDiscardTests"
```

Expected: rotas retornam `404` porque os módulos Carter ainda não existem.

- [ ] **Step 5: Implementar o endpoint de confirmação**

Ao final de `ConfirmDiscard.cs`, fora da classe estática:

```csharp
public sealed class ConfirmDiscardEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "api/v1/discards/{id:guid}/confirm",
            async (
                Guid id,
                [FromBody] RequestConfirmDiscardJson request,
                ISender sender,
                CancellationToken ct) =>
            {
                var command = new ConfirmDiscard.Command
                {
                    DiscardId = id,
                    Items = (request.Items ?? [])
                        .Select(item => item is null
                            ? null!
                            : new ConfirmDiscard.ItemCommand
                            {
                                Material = (ElectronicMaterial)(int)item.Material,
                                Quantity = item.Quantity,
                                ApproximateWeightKg = item.ApproximateWeightKg,
                            })
                        .ToArray(),
                };

                var result = await sender.Send(command, ct);
                return result.ToProcessResult(StatusCodes.Status204NoContent);
            })
            .WithTags("Discard")
            .WithName("ConfirmDiscard")
            .WithSummary("Confirma um descarte pendente com a composição final validada pelo Ponto de Coleta.")
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

Adicionar `using Carter`, `Ecocell.Api.Extensions`, `Ecocell.Shared.Requests.Discards`, `Microsoft.AspNetCore.Mvc` e `Ecocell.Api.Shared` conforme necessário.

- [ ] **Step 6: Implementar o endpoint de rejeição**

Ao final de `RejectDiscard.cs`:

```csharp
public sealed class RejectDiscardEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
            "api/v1/discards/{id:guid}/reject",
            async (Guid id, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new RejectDiscard.Command(id), ct);
                return result.ToProcessResult(StatusCodes.Status204NoContent);
            })
            .WithTags("Discard")
            .WithName("RejectDiscard")
            .WithSummary("Rejeita um descarte pendente pertencente ao Ponto de Coleta.")
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

- [ ] **Step 7: Executar integração e corrigir somente incompatibilidades reais**

Run:

```powershell
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~Ecocell.IntegrationTests.Features.Discard.ConfirmDiscardTests|FullyQualifiedName~Ecocell.IntegrationTests.Features.Discard.RejectDiscardTests"
```

Expected: todos os testes passam contra PostgreSQL e Redis reais. Não substituir Testcontainers por mocks se houver falha ambiental.

- [ ] **Step 8: Adicionar teste concorrente ponta a ponta**

Em `ConfirmDiscardTests.cs`, adicionar:

```csharp
[Fact]
public async Task Post_ShouldAllowOnlyOneTerminalTransition_WhenConfirmAndRejectRace()
{
    var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
    var responsibleId = await GetPersonIdByEmailAsync(email);
    var point = await CreateActiveCollectorPointAsync(responsibleId);
    var discard = await CreatePendingDiscardAsync(
        responsibleId,
        point.Id,
        ApiEnums.ElectronicMaterial.Battery);
    using var confirmClient = CreateAuthenticatedClient(jwt);
    using var rejectClient = CreateAuthenticatedClient(jwt);
    var request = ValidRequest(SharedEnums.ElectronicMaterial.Battery);

    var responses = await Task.WhenAll(
        confirmClient.PostAsJsonAsync($"api/v1/discards/{discard.Id}/confirm", request),
        rejectClient.PostAsync($"api/v1/discards/{discard.Id}/reject", content: null));

    responses.Count(value => value.StatusCode == HttpStatusCode.NoContent).ShouldBe(1);
    responses.Count(value => value.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);

    await using var scope = Fixture.Factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var persisted = await db.Discards.SingleAsync(value => value.Id == discard.Id);
    var requests = await db.CreditScoreRequests.CountAsync(
        value => value.DiscardId == discard.Id);
    requests.ShouldBe(persisted.Status == ApiEnums.DiscardStatus.Confirmed ? 1 : 0);
}
```

- [ ] **Step 9: Executar três vezes o teste concorrente**

Run três vezes, sem `--no-build` na primeira execução:

```powershell
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~Post_ShouldAllowOnlyOneTerminalTransition_WhenConfirmAndRejectRace"
```

Expected em todas as execuções: exatamente um `204`, exatamente um `409` e consistência entre estado final e quantidade de pedidos de crédito. Se houver flakiness, diagnosticar a causa; não adicionar `Task.Delay`.

- [ ] **Step 10: Revisar e commitar a tarefa**

Run:

```powershell
rtk git diff --check
rtk git add -- src/Ecocell.Api/Features/Discard/ConfirmDiscard.cs src/Ecocell.Api/Features/Discard/RejectDiscard.cs tests/Ecocell.IntegrationTests/IntegrationTestBase.cs tests/Ecocell.IntegrationTests/Features/Discard/ConfirmDiscardTests.cs tests/Ecocell.IntegrationTests/Features/Discard/RejectDiscardTests.cs
rtk git commit -m "test(discard): cobre endpoints de decisão"
```

---

### Task 6: Verificação final e auditoria de escopo

**Files:**
- Verify: todos os arquivos alterados pelas Tasks 1–5.

**Interfaces:**
- Consumes: implementação completa da WND-289.
- Produces: evidência de suíte verde, migrations alinhadas e diff restrito ao escopo aprovado.

- [ ] **Step 1: Executar testes unitários do agregado e dos slices**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Ecocell.UnitTests.Entities.DiscardTests|FullyQualifiedName~Ecocell.UnitTests.Features.Discard.ConfirmDiscardTests|FullyQualifiedName~Ecocell.UnitTests.Features.Discard.RejectDiscardTests"
```

Expected: todos os testes selecionados passam.

- [ ] **Step 2: Executar todos os testes de integração de descarte**

Run:

```powershell
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~Ecocell.IntegrationTests.Features.Discard"
```

Expected: abertura, confirmação e rejeição passam contra PostgreSQL/Redis reais.

- [ ] **Step 3: Validar migrations e build**

Run:

```powershell
rtk dotnet ef migrations has-pending-model-changes --project src/Ecocell.Api --startup-project src/Ecocell.Api
rtk dotnet build Ecocell.slnx
```

Expected: nenhuma mudança pendente no modelo e build sem erros.

- [ ] **Step 4: Executar a suíte completa**

Run:

```powershell
rtk dotnet test Ecocell.slnx
```

Expected: zero testes falhando. Não declarar conclusão com suíte parcial.

- [ ] **Step 5: Auditar arquivos, whitespace e escopo**

Run:

```powershell
rtk git status --short
rtk git diff --check
rtk git log --oneline -6
rtk rg -n "CreditScoreJob|dispatcher|BackgroundJob|Hangfire" src/Ecocell.Api/Features/Discard
```

Expected:

- nenhum arquivo alheio à WND-289 modificado;
- nenhum erro de whitespace;
- commits pequenos das Tasks 1–5 visíveis;
- nenhum acoplamento direto dos slices ao job ou dispatcher;
- `CreditScoreRequest` criado somente no caminho de confirmação.

- [ ] **Step 6: Revisar critérios de aceite linha a linha**

Confirmar com evidência:

- `RequestConfirmDiscardJson` existe em `Ecocell.Shared`;
- confirmação e rejeição retornam `204`;
- guard é executado antes de expor estado terminal;
- confirmação substitui a lista completa;
- regras são resolvidas em `Discard.CreatedAt`;
- rejeição não cria outbox;
- estado terminal retorna `409`;
- concorrência permite um único vencedor;
- nenhum escopo da WND-290 foi reimplementado.

Se algum item não puder ser apontado para um teste ou trecho de código, a implementação ainda não está concluída.
