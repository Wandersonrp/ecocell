# WND-290 — Job de crédito de pontuação — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar o processamento assíncrono, durável e idempotente da pontuação de descartes confirmados, com ledger imutável, saldo global e aplicação da RN013.

**Architecture:** Uma outbox específica (`CreditScoreRequest`) é consumida por `CreditScoreDispatcher`, um `BackgroundService` nativo que cria um escopo por pedido e chama `CreditScoreJob`. O job calcula a pontuação pelas regras históricas do descarte e grava transação, saldo e conclusão do pedido na mesma transação; índices únicos e concorrência otimista impedem duplicação e perda de atualização.

**Tech Stack:** .NET 10, C# 14, `BackgroundService`, EF Core 10, PostgreSQL/Npgsql, SQLite in-memory, xUnit, Shouldly, Moq, Bogus e Testcontainers.

**Spec:** `docs/superpowers/specs/2026-09-16-wnd-290-credit-score-job-design.md`

## Global Constraints

- Executar em worktree isolado criado por `superpowers:using-git-worktrees`, baseado no commit `efada74`; não reutilizar o checkout sujo atual.
- Ler `AGENTS.md`, `.claude/rules/coding-rules.md`, `.claude/rules/unit-tests-rules.md`, `.claude/rules/integration-tests-rules.md` e a spec antes do primeiro RED.
- Aplicar Red → Green → Refactor em toda nova funcionalidade e correção descoberta durante a execução.
- Não antecipar `Discard.Confirm`, endpoints ou qualquer código da WND-289; testes semeiam `Confirmed` via `DbContext.Entry(...).Property(...).CurrentValue`.
- Usar `BackgroundService` nativo; não adicionar Hangfire, fila externa ou nova dependência NuGet.
- Transação e saldo usam `decimal(28,5)` sem arredondamento.
- Somente `Role.User` recebe pontos; `Admin` e `Support` concluem o pedido sem transação nem saldo.
- O saldo é global: uma linha de `DepositorTotalScore` por depositante.
- `DepositorScoreTransaction` é imutável e única por `DiscardId`.
- `CreditScoreRequest.DispatchedAt` só é preenchido na transação que conclui crédito, recuperação idempotente ou supressão pela RN013.
- Falhas deixam o pedido pendente; não adicionar retry imediato, contador, dead-letter, endpoint administrativo ou métricas próprias.
- Não criar interface com uma implementação, repository, mapper, outbox genérica ou camada adicional.
- Desabilitar somente a execução automática do hosted service em `Testing`; `CreditScoreJob` e `CreditScoreDispatcher` continuam resolvíveis para testes diretos.
- Todos os summaries, mensagens, logs e commits novos permanecem em PT-BR.
- Usar RTK para busca, Git e testes quando o wrapper suportar o comando.
- Após cada tarefa de código, executar a suíte completa `rtk dotnet test Ecocell.slnx` antes do commit.
- Verificação final obrigatória: `rtk dotnet test Ecocell.slnx` com Docker disponível.

## Mapa de arquivos

| Arquivo | Responsabilidade |
| --- | --- |
| `src/Ecocell.Api/Entities/CreditScoreRequest.cs` | Pedido durável de crédito e conclusão UTC. |
| `src/Ecocell.Api/Entities/DepositorScoreTransaction.cs` | Ledger imutável, único por descarte. |
| `src/Ecocell.Api/Entities/DepositorTotalScore.cs` | Saldo global e incremento protegido. |
| `src/Ecocell.Api/Database/TypeConfiguration/CreditScoreRequestTypeConfiguration.cs` | FK e índices do pedido. |
| `src/Ecocell.Api/Database/TypeConfiguration/DepositorScoreTransactionTypeConfiguration.cs` | FKs, precisão, check e unicidade da transação. |
| `src/Ecocell.Api/Database/TypeConfiguration/DepositorTotalScoreTypeConfiguration.cs` | FK, precisão, check, unicidade e concurrency token do saldo. |
| `src/Ecocell.Api/Database/AppDbContext.cs` | Três novos `DbSet`. |
| `src/Ecocell.Api/Jobs/CreditScoreJob.cs` | Cálculo e persistência transacional do crédito. |
| `src/Ecocell.Api/Jobs/CreditScoreDispatcher.cs` | Polling, lote e isolamento de falhas. |
| `src/Ecocell.Api/Extensions/DependencyInjectionExtensions.cs` | Registros scoped/singleton e ativação condicional do hosted service. |
| `src/Ecocell.Api/Migrations/*AddCreditScoreProcessing*` | Tabelas, FKs, índices, checks e snapshot. |
| `tests/Ecocell.UnitTests/Entities/*Score*Tests.cs` | Invariantes das três entidades. |
| `tests/Ecocell.UnitTests/Database/CreditScorePersistenceTests.cs` | Metadata relacional e concorrência do modelo. |
| `tests/Ecocell.UnitTests/Jobs/CreditScoreJobTests.cs` | Cálculo, RN013, atomicidade e idempotência. |
| `tests/Ecocell.UnitTests/Extensions/DependencyInjectionExtensionsTests.cs` | Registro do job e ativação por ambiente. |
| `tests/Ecocell.IntegrationTests/Jobs/CreditScoreDispatcherTests.cs` | Continuidade do lote após falha. |
| `tests/Ecocell.IntegrationTests/Jobs/CreditScoreConcurrencyTests.cs` | Concorrência real no PostgreSQL. |
| `tests/Ecocell.IntegrationTests/Database/CreditScorePersistenceTests.cs` | Garantias persistentes dos índices únicos. |
| `tests/Ecocell.IntegrationTests/Infrastructure/CreditScoreWriteBarrierInterceptor.cs` | Barreira determinística para corridas PostgreSQL. |

---

### Task 0: Preparar worktree isolado e validar baseline

**Files:**
- Read: `AGENTS.md`
- Read: `.claude/rules/coding-rules.md`
- Read: `.claude/rules/unit-tests-rules.md`
- Read: `.claude/rules/integration-tests-rules.md`
- Read: `docs/superpowers/specs/2026-09-16-wnd-290-credit-score-job-design.md`
- Verify: `src/Ecocell.Api/Entities/Discard.cs`
- Verify: `src/Ecocell.Api/Entities/DiscardItem.cs`
- Verify: `src/Ecocell.Api/Entities/MaterialScoreRule.cs`

**Interfaces:**
- Consumes: commit `efada74`, WND-288 e WND-168 concluídas.
- Produces: worktree limpo na branch `codex/wnd-290-credit-score-job`, com baseline verde e sem alteração de código.

- [ ] **Step 1: Criar o worktree isolado**

Usar `superpowers:using-git-worktrees` para criar a branch `codex/wnd-290-credit-score-job` a partir de `efada74`. Não copiar mudanças do checkout original.

- [ ] **Step 2: Confirmar isolamento e limpeza**

Run:

```powershell
rtk git rev-parse HEAD
rtk git branch --show-current
rtk git status --short
```

Expected: `HEAD` começa em `efada74`, branch `codex/wnd-290-credit-score-job` e status vazio.

- [ ] **Step 3: Ler as fontes normativas**

Run:

```powershell
rtk read AGENTS.md
rtk read .claude/rules/coding-rules.md
rtk read .claude/rules/unit-tests-rules.md
rtk read .claude/rules/integration-tests-rules.md
rtk read docs/superpowers/specs/2026-09-16-wnd-290-credit-score-job-design.md
```

Expected: regras e spec carregadas antes de qualquer RED.

- [ ] **Step 4: Confirmar que o escopo ainda não existe**

Run:

```powershell
rtk rg -n "CreditScoreRequest|DepositorScoreTransaction|DepositorTotalScore|CreditScoreJob|CreditScoreDispatcher" src tests
rtk rg -n "Hangfire|BackgroundJob|RecurringJob" src tests
```

Expected: nenhum artefato da WND-290 e nenhuma dependência Hangfire. Se outra implementação já existir, interromper e reconciliar spec, plano e código.

- [ ] **Step 5: Executar a baseline completa**

Run:

```powershell
rtk dotnet test Ecocell.slnx
```

Expected: zero falhas, incluindo integração PostgreSQL/Redis. Falha de Docker é bloqueio ambiental; não pular integração.

---

### Task 1: Criar o modelo de domínio do crédito

**Files:**
- Create: `src/Ecocell.Api/Entities/CreditScoreRequest.cs`
- Create: `src/Ecocell.Api/Entities/DepositorScoreTransaction.cs`
- Create: `src/Ecocell.Api/Entities/DepositorTotalScore.cs`
- Create: `tests/Ecocell.UnitTests/Entities/CreditScoreRequestTests.cs`
- Create: `tests/Ecocell.UnitTests/Entities/DepositorScoreTransactionTests.cs`
- Create: `tests/Ecocell.UnitTests/Entities/DepositorTotalScoreTests.cs`

**Interfaces:**
- Consumes: `BaseEntity`, `Discard`, `NaturalPerson`.
- Produces: `CreditScoreRequest(Guid discardId)`, `void MarkAsDispatched(DateTime dispatchedAt)`, `DepositorScoreTransaction(Guid discardId, Guid depositorId, decimal points)`, `DepositorTotalScore(Guid depositorId, decimal initialPoints)` e `void Credit(decimal points)`.

- [ ] **Step 1: Escrever os testes RED de `CreditScoreRequest`**

Criar `CreditScoreRequestTests.cs`:

```csharp
using Ecocell.Api.Entities;
using Shouldly;

namespace Ecocell.UnitTests.Entities;

public class CreditScoreRequestTests
{
    [Fact]
    public void Constructor_ShouldStoreDiscardId_WhenDiscardIdIsValid()
    {
        var discardId = Guid.NewGuid();

        var request = new CreditScoreRequest(discardId);

        request.DiscardId.ShouldBe(discardId);
        request.DispatchedAt.ShouldBeNull();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDiscardIdIsEmpty()
    {
        Should.Throw<ArgumentException>(() => new CreditScoreRequest(Guid.Empty));
    }

    [Fact]
    public void MarkAsDispatched_ShouldStoreUtcTimestamp_WhenRequestIsPending()
    {
        var request = new CreditScoreRequest(Guid.NewGuid());
        var dispatchedAt = new DateTime(2026, 9, 16, 18, 0, 0, DateTimeKind.Utc);

        request.MarkAsDispatched(dispatchedAt);

        request.DispatchedAt.ShouldBe(dispatchedAt);
        request.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public void MarkAsDispatched_ShouldThrow_WhenTimestampIsNotUtc()
    {
        var request = new CreditScoreRequest(Guid.NewGuid());
        var local = new DateTime(2026, 9, 16, 18, 0, 0, DateTimeKind.Local);

        Should.Throw<ArgumentException>(() => request.MarkAsDispatched(local));
    }

    [Fact]
    public void MarkAsDispatched_ShouldThrow_WhenRequestWasAlreadyDispatched()
    {
        var request = new CreditScoreRequest(Guid.NewGuid());
        request.MarkAsDispatched(DateTime.UtcNow);

        Should.Throw<InvalidOperationException>(() =>
            request.MarkAsDispatched(DateTime.UtcNow));
    }
}
```

- [ ] **Step 2: Escrever os testes RED da transação e do saldo**

Criar `DepositorScoreTransactionTests.cs`:

```csharp
using Ecocell.Api.Entities;
using Shouldly;

namespace Ecocell.UnitTests.Entities;

public class DepositorScoreTransactionTests
{
    [Fact]
    public void Constructor_ShouldStoreValues_WhenValuesAreValid()
    {
        var discardId = Guid.NewGuid();
        var depositorId = Guid.NewGuid();

        var transaction = new DepositorScoreTransaction(discardId, depositorId, 12.34567m);

        transaction.DiscardId.ShouldBe(discardId);
        transaction.DepositorId.ShouldBe(depositorId);
        transaction.Points.ShouldBe(12.34567m);
    }

    [Theory]
    [InlineData("discard")]
    [InlineData("depositor")]
    public void Constructor_ShouldThrow_WhenIdentifierIsEmpty(string identifier)
    {
        var discardId = identifier == "discard" ? Guid.Empty : Guid.NewGuid();
        var depositorId = identifier == "depositor" ? Guid.Empty : Guid.NewGuid();

        Should.Throw<ArgumentException>(() =>
            new DepositorScoreTransaction(discardId, depositorId, 1m));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.00001")]
    public void Constructor_ShouldThrow_WhenPointsAreNotPositive(string rawPoints)
    {
        var points = decimal.Parse(rawPoints, System.Globalization.CultureInfo.InvariantCulture);

        Should.Throw<ArgumentOutOfRangeException>(() =>
            new DepositorScoreTransaction(Guid.NewGuid(), Guid.NewGuid(), points));
    }
}
```

Criar `DepositorTotalScoreTests.cs`:

```csharp
using Ecocell.Api.Entities;
using Shouldly;

namespace Ecocell.UnitTests.Entities;

public class DepositorTotalScoreTests
{
    [Fact]
    public void Constructor_ShouldStoreInitialPoints_WhenValuesAreValid()
    {
        var depositorId = Guid.NewGuid();

        var total = new DepositorTotalScore(depositorId, 10.12345m);

        total.DepositorId.ShouldBe(depositorId);
        total.TotalPoints.ShouldBe(10.12345m);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDepositorIdIsEmpty()
    {
        Should.Throw<ArgumentException>(() =>
            new DepositorTotalScore(Guid.Empty, 1m));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.00001")]
    public void Constructor_ShouldThrow_WhenInitialPointsAreNotPositive(string rawPoints)
    {
        var points = decimal.Parse(rawPoints, System.Globalization.CultureInfo.InvariantCulture);

        Should.Throw<ArgumentOutOfRangeException>(() =>
            new DepositorTotalScore(Guid.NewGuid(), points));
    }

    [Fact]
    public void Credit_ShouldAccumulatePoints_WhenPointsArePositive()
    {
        var total = new DepositorTotalScore(Guid.NewGuid(), 10.12345m);

        total.Credit(2.00001m);

        total.TotalPoints.ShouldBe(12.12346m);
        total.UpdatedAt.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.00001")]
    public void Credit_ShouldThrow_WhenPointsAreNotPositive(string rawPoints)
    {
        var total = new DepositorTotalScore(Guid.NewGuid(), 1m);
        var points = decimal.Parse(rawPoints, System.Globalization.CultureInfo.InvariantCulture);

        Should.Throw<ArgumentOutOfRangeException>(() => total.Credit(points));
    }
}
```

- [ ] **Step 3: Executar os testes para confirmar o RED**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Ecocell.UnitTests.Entities.CreditScoreRequestTests|FullyQualifiedName~Ecocell.UnitTests.Entities.DepositorScoreTransactionTests|FullyQualifiedName~Ecocell.UnitTests.Entities.DepositorTotalScoreTests"
```

Expected: falha de compilação porque as três entidades ainda não existem.

- [ ] **Step 4: Implementar as entidades mínimas**

Criar `CreditScoreRequest.cs`:

```csharp
namespace Ecocell.Api.Entities;

/// <summary>Solicitação durável de processamento do crédito de um descarte confirmado.</summary>
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
    public Discard Discard { get; private set; } = null!;
    public DateTime? DispatchedAt { get; private set; }

    public void MarkAsDispatched(DateTime dispatchedAt)
    {
        if (DispatchedAt is not null)
            throw new InvalidOperationException("A solicitação de crédito já foi processada.");
        if (dispatchedAt.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A data deve estar em UTC.", nameof(dispatchedAt));

        DispatchedAt = dispatchedAt;
        MarkAsUpdated();
    }
}
```

Criar `DepositorScoreTransaction.cs`:

```csharp
namespace Ecocell.Api.Entities;

/// <summary>Crédito imutável concedido ao depositante por um descarte confirmado.</summary>
public sealed class DepositorScoreTransaction : BaseEntity
{
    private DepositorScoreTransaction()
    {
    }

    public DepositorScoreTransaction(Guid discardId, Guid depositorId, decimal points)
    {
        if (discardId == Guid.Empty)
            throw new ArgumentException("O identificador do descarte é obrigatório.", nameof(discardId));
        if (depositorId == Guid.Empty)
            throw new ArgumentException("O identificador do depositante é obrigatório.", nameof(depositorId));
        if (points <= 0)
            throw new ArgumentOutOfRangeException(nameof(points), "A pontuação deve ser maior que zero.");

        DiscardId = discardId;
        DepositorId = depositorId;
        Points = points;
    }

    public Guid DiscardId { get; private set; }
    public Discard Discard { get; private set; } = null!;
    public Guid DepositorId { get; private set; }
    public NaturalPerson Depositor { get; private set; } = null!;
    public decimal Points { get; private set; }
}
```

Criar `DepositorTotalScore.cs`:

```csharp
namespace Ecocell.Api.Entities;

/// <summary>Saldo global materializado de pontos de um depositante.</summary>
public sealed class DepositorTotalScore : BaseEntity
{
    private DepositorTotalScore()
    {
    }

    public DepositorTotalScore(Guid depositorId, decimal initialPoints)
    {
        if (depositorId == Guid.Empty)
            throw new ArgumentException("O identificador do depositante é obrigatório.", nameof(depositorId));
        if (initialPoints <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(initialPoints),
                "A pontuação inicial deve ser maior que zero.");

        DepositorId = depositorId;
        TotalPoints = initialPoints;
    }

    public Guid DepositorId { get; private set; }
    public NaturalPerson Depositor { get; private set; } = null!;
    public decimal TotalPoints { get; private set; }

    public void Credit(decimal points)
    {
        if (points <= 0)
            throw new ArgumentOutOfRangeException(nameof(points), "A pontuação deve ser maior que zero.");

        TotalPoints += points;
        MarkAsUpdated();
    }
}
```

- [ ] **Step 5: Executar os testes das entidades para confirmar o GREEN**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Ecocell.UnitTests.Entities.CreditScoreRequestTests|FullyQualifiedName~Ecocell.UnitTests.Entities.DepositorScoreTransactionTests|FullyQualifiedName~Ecocell.UnitTests.Entities.DepositorTotalScoreTests"
```

Expected: todos os testes selecionados passam.

- [ ] **Step 6: Executar a suíte completa**

Run:

```powershell
rtk dotnet test Ecocell.slnx
```

Expected: zero falhas.

- [ ] **Step 7: Revisar e commitar a tarefa**

Run:

```powershell
rtk git diff --check
rtk git add -- src/Ecocell.Api/Entities/CreditScoreRequest.cs src/Ecocell.Api/Entities/DepositorScoreTransaction.cs src/Ecocell.Api/Entities/DepositorTotalScore.cs tests/Ecocell.UnitTests/Entities/CreditScoreRequestTests.cs tests/Ecocell.UnitTests/Entities/DepositorScoreTransactionTests.cs tests/Ecocell.UnitTests/Entities/DepositorTotalScoreTests.cs
rtk git diff --cached --check
rtk git commit -m "feat(score): adiciona modelo de credito"
```

---

### Task 2: Mapear persistência e gerar migration

**Files:**
- Create: `src/Ecocell.Api/Database/TypeConfiguration/CreditScoreRequestTypeConfiguration.cs`
- Create: `src/Ecocell.Api/Database/TypeConfiguration/DepositorScoreTransactionTypeConfiguration.cs`
- Create: `src/Ecocell.Api/Database/TypeConfiguration/DepositorTotalScoreTypeConfiguration.cs`
- Modify: `src/Ecocell.Api/Database/AppDbContext.cs`
- Create: `tests/Ecocell.UnitTests/Database/CreditScorePersistenceTests.cs`
- Generate: `src/Ecocell.Api/Migrations/*_AddCreditScoreProcessing.cs`
- Generate: `src/Ecocell.Api/Migrations/*_AddCreditScoreProcessing.Designer.cs`
- Modify: `src/Ecocell.Api/Migrations/AppDbContextModelSnapshot.cs`

**Interfaces:**
- Consumes: entidades da Task 1.
- Produces: três `DbSet`, schema relacional, `TotalPoints` como concurrency token e migration `AddCreditScoreProcessing`.

- [ ] **Step 1: Escrever testes RED da metadata**

Criar `CreditScorePersistenceTests.cs`:

```csharp
using Ecocell.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Ecocell.UnitTests.Database;

public class CreditScorePersistenceTests : TestBase
{
    [Fact]
    public void Model_ShouldConfigureCreditRequestIndexes()
    {
        var entity = DbContext.Model.FindEntityType(typeof(CreditScoreRequest));
        entity.ShouldNotBeNull();

        entity!.GetIndexes()
            .Single(index => index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(CreditScoreRequest.DiscardId)]))
            .IsUnique.ShouldBeTrue();
        entity.GetIndexes().ShouldContain(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(CreditScoreRequest.DispatchedAt)]));
    }

    [Fact]
    public void Model_ShouldConfigureTransactionPrecisionAndUniqueDiscard()
    {
        var entity = DbContext.Model.FindEntityType(typeof(DepositorScoreTransaction));
        entity.ShouldNotBeNull();

        var points = entity!.FindProperty(nameof(DepositorScoreTransaction.Points));
        points.ShouldNotBeNull();
        points!.GetPrecision().ShouldBe(28);
        points.GetScale().ShouldBe(5);
        entity.GetIndexes()
            .Single(index => index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(DepositorScoreTransaction.DiscardId)]))
            .IsUnique.ShouldBeTrue();
    }

    [Fact]
    public void Model_ShouldConfigureTotalPrecisionUniquenessAndConcurrency()
    {
        var entity = DbContext.Model.FindEntityType(typeof(DepositorTotalScore));
        entity.ShouldNotBeNull();

        var totalPoints = entity!.FindProperty(nameof(DepositorTotalScore.TotalPoints));
        totalPoints.ShouldNotBeNull();
        totalPoints!.GetPrecision().ShouldBe(28);
        totalPoints.GetScale().ShouldBe(5);
        totalPoints.IsConcurrencyToken.ShouldBeTrue();
        entity.GetIndexes()
            .Single(index => index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(DepositorTotalScore.DepositorId)]))
            .IsUnique.ShouldBeTrue();
    }

    [Fact]
    public void Model_ShouldUseRestrictDeleteForCreditRelationships()
    {
        var entityTypes = new[]
        {
            DbContext.Model.FindEntityType(typeof(CreditScoreRequest)),
            DbContext.Model.FindEntityType(typeof(DepositorScoreTransaction)),
            DbContext.Model.FindEntityType(typeof(DepositorTotalScore)),
        };

        entityTypes.ShouldAllBe(entity => entity is not null);
        entityTypes.SelectMany(entity => entity!.GetForeignKeys())
            .ShouldAllBe(foreignKey => foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 2: Executar os testes para confirmar o RED**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Ecocell.UnitTests.Database.CreditScorePersistenceTests"
```

Expected: testes falham porque as entidades ainda não fazem parte do modelo.

- [ ] **Step 3: Adicionar os `DbSet`**

Em `AppDbContext.cs`, adicionar:

```csharp
public DbSet<CreditScoreRequest> CreditScoreRequests { get; set; }
public DbSet<DepositorScoreTransaction> DepositorScoreTransactions { get; set; }
public DbSet<DepositorTotalScore> DepositorTotalScores { get; set; }
```

- [ ] **Step 4: Criar as configurações EF Core**

Criar `CreditScoreRequestTypeConfiguration.cs`:

```csharp
using Ecocell.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecocell.Api.Database.TypeConfiguration;

public sealed class CreditScoreRequestTypeConfiguration
    : BaseEntityTypeConfiguration<CreditScoreRequest>
{
    public override void Configure(EntityTypeBuilder<CreditScoreRequest> builder)
    {
        base.Configure(builder);
        builder.ToTable("CreditScoreRequests");

        builder.HasKey(value => value.Id);
        builder.Property(value => value.DispatchedAt)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(value => value.DiscardId).IsUnique();
        builder.HasIndex(value => value.DispatchedAt);

        builder.HasOne(value => value.Discard)
            .WithMany()
            .HasForeignKey(value => value.DiscardId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

Criar `DepositorScoreTransactionTypeConfiguration.cs`:

```csharp
using Ecocell.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecocell.Api.Database.TypeConfiguration;

public sealed class DepositorScoreTransactionTypeConfiguration
    : BaseEntityTypeConfiguration<DepositorScoreTransaction>
{
    public override void Configure(EntityTypeBuilder<DepositorScoreTransaction> builder)
    {
        base.Configure(builder);
        builder.ToTable("DepositorScoreTransactions", table =>
            table.HasCheckConstraint(
                "CK_DepositorScoreTransactions_Points_Positive",
                "CAST(\"Points\" AS REAL) > 0"));

        builder.HasKey(value => value.Id);
        builder.Property(value => value.Points)
            .HasPrecision(28, 5)
            .IsRequired();

        builder.HasIndex(value => value.DiscardId).IsUnique();
        builder.HasIndex(value => value.DepositorId);

        builder.HasOne(value => value.Discard)
            .WithMany()
            .HasForeignKey(value => value.DiscardId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(value => value.Depositor)
            .WithMany()
            .HasForeignKey(value => value.DepositorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

Criar `DepositorTotalScoreTypeConfiguration.cs`:

```csharp
using Ecocell.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecocell.Api.Database.TypeConfiguration;

public sealed class DepositorTotalScoreTypeConfiguration
    : BaseEntityTypeConfiguration<DepositorTotalScore>
{
    public override void Configure(EntityTypeBuilder<DepositorTotalScore> builder)
    {
        base.Configure(builder);
        builder.ToTable("DepositorTotalScores", table =>
            table.HasCheckConstraint(
                "CK_DepositorTotalScores_TotalPoints_NonNegative",
                "CAST(\"TotalPoints\" AS REAL) >= 0"));

        builder.HasKey(value => value.Id);
        builder.Property(value => value.TotalPoints)
            .HasPrecision(28, 5)
            .IsRequired()
            .IsConcurrencyToken();

        builder.HasIndex(value => value.DepositorId).IsUnique();

        builder.HasOne(value => value.Depositor)
            .WithMany()
            .HasForeignKey(value => value.DepositorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 5: Executar os testes de metadata para confirmar o GREEN**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Ecocell.UnitTests.Database.CreditScorePersistenceTests"
```

Expected: todos os testes selecionados passam.

- [ ] **Step 6: Gerar a migration**

Run:

```powershell
rtk dotnet ef migrations add AddCreditScoreProcessing --project src/Ecocell.Api --startup-project src/Ecocell.Api
```

Expected: migration cria `CreditScoreRequests`, `DepositorScoreTransactions` e `DepositorTotalScores`, com FKs restritas, precisões `numeric(28,5)`, checks e índices definidos na spec.

- [ ] **Step 7: Validar migration e suíte completa**

Run:

```powershell
rtk dotnet ef migrations has-pending-model-changes --project src/Ecocell.Api --startup-project src/Ecocell.Api
rtk dotnet test Ecocell.slnx
```

Expected: nenhuma mudança pendente no modelo e zero testes falhando.

- [ ] **Step 8: Revisar e commitar a tarefa**

Run:

```powershell
rtk git diff --check
rtk git add -- src/Ecocell.Api/Database/AppDbContext.cs src/Ecocell.Api/Database/TypeConfiguration/CreditScoreRequestTypeConfiguration.cs src/Ecocell.Api/Database/TypeConfiguration/DepositorScoreTransactionTypeConfiguration.cs src/Ecocell.Api/Database/TypeConfiguration/DepositorTotalScoreTypeConfiguration.cs src/Ecocell.Api/Migrations tests/Ecocell.UnitTests/Database/CreditScorePersistenceTests.cs
rtk git diff --cached --check
rtk git commit -m "feat(score): persiste processamento de credito"
```

---

### Task 3: Implementar cálculo e crédito do caminho feliz

**Files:**
- Create: `src/Ecocell.Api/Jobs/CreditScoreJob.cs`
- Create: `tests/Ecocell.UnitTests/Jobs/CreditScoreJobTests.cs`

**Interfaces:**
- Consumes: entidades e `DbSet` das Tasks 1–2, `DiscardStatus.Confirmed`, `MaterialScoreUnit`, `TimeProvider`.
- Produces: `Task CreditScoreJob.ExecuteAsync(Guid creditScoreRequestId, CancellationToken cancellationToken)` e helper de teste `AddScoreRequestAsync(Role, bool, params ScoreItemSeed[])`.

- [ ] **Step 1: Criar o setup de teste e o RED do caminho feliz**

Criar `CreditScoreJobTests.cs` com os imports e helpers abaixo:

```csharp
using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Jobs;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Ecocell.UnitTests.Jobs;

public class CreditScoreJobTests : TestBase
{
    private sealed record ScoreItemSeed(
        ElectronicMaterial Material,
        decimal Points,
        MaterialScoreUnit Unit,
        int Quantity,
        decimal WeightKg);

    [Fact]
    public async Task Execute_ShouldCreditPoints_WhenDiscardIsConfirmed()
    {
        var request = await AddScoreRequestAsync(
            Role.User,
            confirmed: true,
            new ScoreItemSeed(
                ElectronicMaterial.Battery,
                10m,
                MaterialScoreUnit.PerUnit,
                2,
                0.250m));

        await CreateJob().ExecuteAsync(request.Id, CancellationToken.None);

        var transaction = await DbContext.DepositorScoreTransactions.SingleAsync();
        transaction.DiscardId.ShouldBe(request.DiscardId);
        transaction.Points.ShouldBe(20m);
        (await DbContext.DepositorTotalScores.SingleAsync()).TotalPoints.ShouldBe(20m);
        (await DbContext.CreditScoreRequests.SingleAsync()).DispatchedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Execute_ShouldUseWeight_WhenRuleIsPerKilogram()
    {
        var request = await AddScoreRequestAsync(
            Role.User,
            confirmed: true,
            new ScoreItemSeed(
                ElectronicMaterial.Battery,
                2.75m,
                MaterialScoreUnit.PerKilogram,
                1,
                0.333m));

        await CreateJob().ExecuteAsync(request.Id, CancellationToken.None);

        (await DbContext.DepositorScoreTransactions.SingleAsync())
            .Points.ShouldBe(0.91575m);
        (await DbContext.DepositorTotalScores.SingleAsync())
            .TotalPoints.ShouldBe(0.91575m);
    }

    [Fact]
    public async Task Execute_ShouldSumPoints_WhenDiscardHasMultipleItems()
    {
        var request = await AddScoreRequestAsync(
            Role.User,
            confirmed: true,
            new ScoreItemSeed(
                ElectronicMaterial.Battery,
                10m,
                MaterialScoreUnit.PerUnit,
                2,
                0.250m),
            new ScoreItemSeed(
                ElectronicMaterial.Notebook,
                3m,
                MaterialScoreUnit.PerKilogram,
                1,
                0.500m));

        await CreateJob().ExecuteAsync(request.Id, CancellationToken.None);

        (await DbContext.DepositorScoreTransactions.SingleAsync())
            .Points.ShouldBe(21.50000m);
        (await DbContext.DepositorTotalScores.SingleAsync())
            .TotalPoints.ShouldBe(21.50000m);
    }

    private CreditScoreJob CreateJob() =>
        new(
            DbContext,
            CreateLoggerMock<CreditScoreJob>().Object,
            TimeProvider.System);

    private async Task<CreditScoreRequest> AddScoreRequestAsync(
        Role role,
        bool confirmed,
        params ScoreItemSeed[] seeds)
    {
        var faker = new Faker("pt_BR");
        var depositor = new NaturalPerson(
            faker.Name.FullName(),
            faker.Person.Cpf(includeFormatSymbols: false),
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
            role,
            faker.Internet.Email(),
            Journey.Depositor);
        var collectorPoint = new LegalPerson(
            faker.Company.CompanyName(),
            faker.Company.CompanyName(),
            faker.Company.Cnpj(includeFormatSymbols: false),
            faker.Internet.Email(),
            Journey.CollectPoint);
        var rules = seeds.Select(seed => new MaterialScoreRule(
            collectorPoint.Id,
            seed.Material,
            seed.Points,
            seed.Unit,
            DateTime.UtcNow.AddDays(-1))).ToArray();
        var items = seeds.Zip(rules, (seed, rule) => new DiscardItem(
            seed.Material,
            seed.Quantity,
            seed.WeightKg,
            rule.Id)).ToArray();
        var discard = new Discard(depositor.Id, collectorPoint.Id, items);
        var request = new CreditScoreRequest(discard.Id);

        DbContext.NaturalPeople.Add(depositor);
        DbContext.LegalPeople.Add(collectorPoint);
        DbContext.MaterialScoreRules.AddRange(rules);
        DbContext.Discards.Add(discard);
        DbContext.CreditScoreRequests.Add(request);
        if (confirmed)
        {
            DbContext.Entry(discard)
                .Property(value => value.Status)
                .CurrentValue = DiscardStatus.Confirmed;
        }

        await DbContext.SaveChangesAsync();
        return request;
    }
}
```

- [ ] **Step 2: Executar os testes para confirmar o RED**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Ecocell.UnitTests.Jobs.CreditScoreJobTests"
```

Expected: falha de compilação porque `CreditScoreJob` ainda não existe.

- [ ] **Step 3: Implementar o caminho feliz do job**

Criar `CreditScoreJob.cs`:

```csharp
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ecocell.Api.Jobs;

public sealed class CreditScoreJob
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<CreditScoreJob> _logger;
    private readonly TimeProvider _timeProvider;

    public CreditScoreJob(
        AppDbContext dbContext,
        ILogger<CreditScoreJob> logger,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(
        Guid creditScoreRequestId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var request = await _dbContext.CreditScoreRequests
            .Include(value => value.Discard)
                .ThenInclude(value => value.Depositor)
            .Include(value => value.Discard)
                .ThenInclude(value => value.Items)
                    .ThenInclude(value => value.MaterialScoreRule)
            .SingleAsync(value => value.Id == creditScoreRequestId, cancellationToken);

        var points = request.Discard.Items.Sum(CalculatePoints);
        var total = await _dbContext.DepositorTotalScores.SingleOrDefaultAsync(
            value => value.DepositorId == request.Discard.DepositorId,
            cancellationToken);

        _dbContext.DepositorScoreTransactions.Add(new DepositorScoreTransaction(
            request.DiscardId,
            request.Discard.DepositorId,
            points));

        if (total is null)
        {
            _dbContext.DepositorTotalScores.Add(new DepositorTotalScore(
                request.Discard.DepositorId,
                points));
        }
        else
        {
            total.Credit(points);
        }

        request.MarkAsDispatched(_timeProvider.GetUtcNow().UtcDateTime);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Crédito processado. Solicitação: {CreditScoreRequestId}; descarte: {DiscardId}; pontos: {Points}.",
            request.Id,
            request.DiscardId,
            points);
    }

    private static decimal CalculatePoints(DiscardItem item) =>
        item.MaterialScoreRule.Unit switch
        {
            MaterialScoreUnit.PerUnit => item.MaterialScoreRule.Points * item.Quantity,
            MaterialScoreUnit.PerKilogram =>
                item.MaterialScoreRule.Points * item.ApproximateWeightKg,
            _ => throw new InvalidOperationException("A unidade de pontuação é inválida."),
        };
}
```

- [ ] **Step 4: Executar os testes do caminho feliz para confirmar o GREEN**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Ecocell.UnitTests.Jobs.CreditScoreJobTests"
```

Expected: três testes passam, incluindo `0.91575` sem arredondamento.

- [ ] **Step 5: Executar a suíte completa**

Run:

```powershell
rtk dotnet test Ecocell.slnx
```

Expected: zero falhas.

- [ ] **Step 6: Revisar e commitar a tarefa**

Run:

```powershell
rtk git diff --check
rtk git add -- src/Ecocell.Api/Jobs/CreditScoreJob.cs tests/Ecocell.UnitTests/Jobs/CreditScoreJobTests.cs
rtk git diff --cached --check
rtk git commit -m "feat(score): calcula credito de descartes"
```

---

### Task 4: Aplicar RN013, idempotência e rollback

**Files:**
- Modify: `src/Ecocell.Api/Jobs/CreditScoreJob.cs`
- Modify: `tests/Ecocell.UnitTests/Jobs/CreditScoreJobTests.cs`

**Interfaces:**
- Consumes: `CreditScoreJob.ExecuteAsync` da Task 3.
- Produces: no-op para pedido ausente/concluído, supressão para `Admin`/`Support`, recuperação por transação existente e falha atômica para descarte não confirmado.

- [ ] **Step 1: Escrever os testes RED da RN013**

Adicionar a `CreditScoreJobTests.cs`:

```csharp
[Theory]
[InlineData(Role.Admin)]
[InlineData(Role.Support)]
public async Task Execute_ShouldNotCreditPoints_WhenPersonIsAdminOrSupport(Role role)
{
    var request = await AddScoreRequestAsync(
        role,
        confirmed: true,
        new ScoreItemSeed(
            ElectronicMaterial.Battery,
            10m,
            MaterialScoreUnit.PerUnit,
            2,
            0.250m));

    await CreateJob().ExecuteAsync(request.Id, CancellationToken.None);

    (await DbContext.DepositorScoreTransactions.CountAsync()).ShouldBe(0);
    (await DbContext.DepositorTotalScores.CountAsync()).ShouldBe(0);
    (await DbContext.CreditScoreRequests.SingleAsync()).DispatchedAt.ShouldNotBeNull();
}
```

- [ ] **Step 2: Escrever os testes RED de idempotência e estado inválido**

Adicionar este teste dentro da classe, antes do `}` final:

```csharp
[Fact]
public async Task Execute_ShouldReturnWithoutChanges_WhenRequestDoesNotExist()
{
    await CreateJob().ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

    (await DbContext.DepositorScoreTransactions.CountAsync()).ShouldBe(0);
    (await DbContext.DepositorTotalScores.CountAsync()).ShouldBe(0);
}

[Fact]
public async Task Execute_ShouldReturnWithoutChanges_WhenRequestWasDispatched()
{
    var request = await AddScoreRequestAsync(
        Role.User,
        confirmed: true,
        new ScoreItemSeed(
            ElectronicMaterial.Battery,
            10m,
            MaterialScoreUnit.PerUnit,
            1,
            0.250m));
    request.MarkAsDispatched(DateTime.UtcNow);
    await DbContext.SaveChangesAsync();

    await CreateJob().ExecuteAsync(request.Id, CancellationToken.None);

    (await DbContext.DepositorScoreTransactions.CountAsync()).ShouldBe(0);
    (await DbContext.DepositorTotalScores.CountAsync()).ShouldBe(0);
}

[Fact]
public async Task Execute_ShouldMarkRequestDispatchedWithoutChangingBalance_WhenTransactionExists()
{
    var request = await AddScoreRequestAsync(
        Role.User,
        confirmed: true,
        new ScoreItemSeed(
            ElectronicMaterial.Battery,
            10m,
            MaterialScoreUnit.PerUnit,
            2,
            0.250m));
    var discard = await DbContext.Discards.SingleAsync();
    DbContext.DepositorScoreTransactions.Add(
        new DepositorScoreTransaction(discard.Id, discard.DepositorId, 20m));
    DbContext.DepositorTotalScores.Add(
        new DepositorTotalScore(discard.DepositorId, 20m));
    await DbContext.SaveChangesAsync();

    await CreateJob().ExecuteAsync(request.Id, CancellationToken.None);

    (await DbContext.DepositorScoreTransactions.CountAsync()).ShouldBe(1);
    (await DbContext.DepositorTotalScores.SingleAsync()).TotalPoints.ShouldBe(20m);
    (await DbContext.CreditScoreRequests.SingleAsync()).DispatchedAt.ShouldNotBeNull();
}

[Fact]
public async Task Execute_ShouldRollbackAndKeepRequestPending_WhenDiscardIsNotConfirmed()
{
    var request = await AddScoreRequestAsync(
        Role.User,
        confirmed: false,
        new ScoreItemSeed(
            ElectronicMaterial.Battery,
            10m,
            MaterialScoreUnit.PerUnit,
            2,
            0.250m));

    await Should.ThrowAsync<InvalidOperationException>(() =>
        CreateJob().ExecuteAsync(request.Id, CancellationToken.None));

    (await DbContext.DepositorScoreTransactions.CountAsync()).ShouldBe(0);
    (await DbContext.DepositorTotalScores.CountAsync()).ShouldBe(0);
    (await DbContext.CreditScoreRequests.SingleAsync()).DispatchedAt.ShouldBeNull();
}
```

- [ ] **Step 3: Executar os testes para confirmar o RED**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Ecocell.UnitTests.Jobs.CreditScoreJobTests"
```

Expected: falhas em pedido ausente, RN013, recuperação idempotente e estado não confirmado.

- [ ] **Step 4: Implementar os guards antes do cálculo**

Em `ExecuteAsync`, substituir `SingleAsync` por `SingleOrDefaultAsync` e inserir, imediatamente após a consulta:

```csharp
if (request is null || request.DispatchedAt is not null)
    return;

if (request.Discard.Status != DiscardStatus.Confirmed)
{
    _logger.LogError(
        "Solicitação {CreditScoreRequestId} referencia descarte não confirmado {DiscardId}.",
        request.Id,
        request.DiscardId);
    throw new InvalidOperationException(
        "Somente descartes confirmados podem gerar crédito de pontuação.");
}

var dispatchedAt = _timeProvider.GetUtcNow().UtcDateTime;
var transactionExists = await _dbContext.DepositorScoreTransactions.AnyAsync(
    value => value.DiscardId == request.DiscardId,
    cancellationToken);
if (transactionExists)
{
    request.MarkAsDispatched(dispatchedAt);
    await _dbContext.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    _logger.LogInformation(
        "Solicitação {CreditScoreRequestId} recuperada para descarte já creditado {DiscardId}.",
        request.Id,
        request.DiscardId);
    return;
}

if (request.Discard.Depositor.Role is Role.Admin or Role.Support)
{
    request.MarkAsDispatched(dispatchedAt);
    await _dbContext.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    _logger.LogInformation(
        "Crédito suprimido pela RN013. Solicitação: {CreditScoreRequestId}; descarte: {DiscardId}; role: {Role}.",
        request.Id,
        request.DiscardId,
        request.Discard.Depositor.Role);
    return;
}
```

No caminho feliz, reutilizar `dispatchedAt`:

```csharp
request.MarkAsDispatched(dispatchedAt);
```

- [ ] **Step 5: Executar os testes para confirmar o GREEN**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Ecocell.UnitTests.Jobs.CreditScoreJobTests"
```

Expected: todos os testes do job passam.

- [ ] **Step 6: Executar a suíte completa**

Run:

```powershell
rtk dotnet test Ecocell.slnx
```

Expected: zero falhas.

- [ ] **Step 7: Revisar e commitar a tarefa**

Run:

```powershell
rtk git diff --check
rtk git add -- src/Ecocell.Api/Jobs/CreditScoreJob.cs tests/Ecocell.UnitTests/Jobs/CreditScoreJobTests.cs
rtk git diff --cached --check
rtk git commit -m "feat(score): aplica elegibilidade e idempotencia"
```

---

### Task 5: Implementar dispatcher e ativação por ambiente

**Files:**
- Create: `src/Ecocell.Api/Jobs/CreditScoreDispatcher.cs`
- Modify: `src/Ecocell.Api/Extensions/DependencyInjectionExtensions.cs`
- Modify: `tests/Ecocell.UnitTests/Extensions/DependencyInjectionExtensionsTests.cs`
- Create: `tests/Ecocell.IntegrationTests/Jobs/CreditScoreDispatcherTests.cs`

**Interfaces:**
- Consumes: `CreditScoreJob.ExecuteAsync` e `AppDbContext.CreditScoreRequests`.
- Produces: `internal Task CreditScoreDispatcher.DispatchPendingAsync(CancellationToken)`, polling a cada 5 segundos, lote máximo 50, execução sequencial e registro condicional como hosted service.

- [ ] **Step 1: Escrever testes RED do registro por ambiente**

Adicionar a `DependencyInjectionExtensionsTests.cs`:

```csharp
using Ecocell.Api.Jobs;

[Fact]
public void AddScoreProcessing_ShouldNotRegisterHostedService_WhenEnvironmentIsTesting()
{
    var services = InvokeAddScoreProcessing("Testing");

    services.ShouldContain(descriptor =>
        descriptor.ServiceType == typeof(CreditScoreJob)
        && descriptor.Lifetime == ServiceLifetime.Scoped);
    services.ShouldContain(descriptor =>
        descriptor.ServiceType == typeof(CreditScoreDispatcher)
        && descriptor.Lifetime == ServiceLifetime.Singleton);
    services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(IHostedService));
}

[Fact]
public void AddScoreProcessing_ShouldRegisterHostedService_WhenEnvironmentIsDevelopment()
{
    var services = InvokeAddScoreProcessing(Environments.Development);

    services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IHostedService));
}

private static IServiceCollection InvokeAddScoreProcessing(string environmentName)
{
    var services = new ServiceCollection();
    services.AddLogging();
    var environment = new Mock<IHostEnvironment>();
    environment.SetupGet(value => value.EnvironmentName).Returns(environmentName);
    var method = typeof(DependencyInjectionExtensions).GetMethod(
        "AddScoreProcessing",
        BindingFlags.NonPublic | BindingFlags.Static);
    method.ShouldNotBeNull();

    method!.Invoke(null, new object[] { services, environment.Object });
    return services;
}
```

- [ ] **Step 2: Executar o teste para confirmar o RED**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Ecocell.UnitTests.Extensions.DependencyInjectionExtensionsTests"
```

Expected: novos testes falham porque `CreditScoreDispatcher` e `AddScoreProcessing` não existem.

- [ ] **Step 3: Criar o dispatcher**

Criar `CreditScoreDispatcher.cs`:

```csharp
using Ecocell.Api.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ecocell.Api.Jobs;

internal sealed class CreditScoreDispatcher : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 50;
    private static readonly string[] RetryableUniqueConstraints =
    [
        "IX_DepositorScoreTransactions_DiscardId",
        "IX_DepositorTotalScores_DepositorId",
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CreditScoreDispatcher> _logger;

    public CreditScoreDispatcher(
        IServiceScopeFactory scopeFactory,
        ILogger<CreditScoreDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await DispatchPendingAsync(stoppingToken);
            using var timer = new PeriodicTimer(PollingInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await DispatchPendingAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    internal async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        List<PendingRequest> pending;
        await using (var queryScope = _scopeFactory.CreateAsyncScope())
        {
            var db = queryScope.ServiceProvider.GetRequiredService<AppDbContext>();
            pending = await db.CreditScoreRequests
                .AsNoTracking()
                .Where(value => value.DispatchedAt == null)
                .OrderBy(value => value.CreatedAt)
                .ThenBy(value => value.Id)
                .Take(BatchSize)
                .Select(value => new PendingRequest(value.Id, value.DiscardId))
                .ToListAsync(cancellationToken);
        }

        foreach (var request in pending)
        {
            try
            {
                await using var processingScope = _scopeFactory.CreateAsyncScope();
                var job = processingScope.ServiceProvider.GetRequiredService<CreditScoreJob>();
                await job.ExecuteAsync(request.Id, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (DbUpdateConcurrencyException exception)
            {
                LogRetryableConflict(exception, request);
            }
            catch (DbUpdateException exception) when (IsRetryableUniqueConflict(exception))
            {
                LogRetryableConflict(exception, request);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Falha ao processar solicitação {CreditScoreRequestId} do descarte {DiscardId}; pedido permanecerá pendente.",
                    request.Id,
                    request.DiscardId);
            }
        }
    }

    private void LogRetryableConflict(Exception exception, PendingRequest request) =>
        _logger.LogWarning(
            exception,
            "Conflito ao processar solicitação {CreditScoreRequestId} do descarte {DiscardId}; novo ciclo tentará novamente.",
            request.Id,
            request.DiscardId);

    private static bool IsRetryableUniqueConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState == PostgresErrorCodes.UniqueViolation
        && RetryableUniqueConstraints.Contains(postgres.ConstraintName);

    private sealed record PendingRequest(Guid Id, Guid DiscardId);
}
```

- [ ] **Step 4: Registrar job e dispatcher**

Em `AddApi`, chamar `AddScoreProcessing(services, environment)` após `AddServices`.

Adicionar a `DependencyInjectionExtensions.cs`:

```csharp
private static void AddScoreProcessing(
    IServiceCollection services,
    IHostEnvironment environment)
{
    services.AddScoped<CreditScoreJob>();
    services.AddSingleton<CreditScoreDispatcher>();

    if (!environment.IsEnvironment("Testing"))
    {
        services.AddHostedService(provider =>
            provider.GetRequiredService<CreditScoreDispatcher>());
    }
}
```

Adicionar `using Ecocell.Api.Jobs;`.

- [ ] **Step 5: Executar os testes de DI para confirmar o GREEN**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Ecocell.UnitTests.Extensions.DependencyInjectionExtensionsTests"
```

Expected: ambiente `Testing` resolve dispatcher sem iniciar hosted service; Development registra `IHostedService`.

- [ ] **Step 6: Escrever o teste de integração RED da continuidade do lote**

Criar `CreditScoreDispatcherTests.cs`. O helper deve semear dois pedidos em ordem: primeiro `Pending`, depois `Confirmed`, usando `DbContext.Entry(discard).Property(value => value.Status).CurrentValue` somente para o segundo.

```csharp
using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Ecocell.IntegrationTests.Jobs;

[Collection(nameof(IntegrationTestCollection))]
public class CreditScoreDispatcherTests : IntegrationTestBase
{
    public CreditScoreDispatcherTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task DispatchPending_ShouldContinue_WhenOneRequestFails()
    {
        Guid invalidRequestId;
        Guid validRequestId;
        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            invalidRequestId = (await AddRequestAsync(db, confirmed: false)).Id;
            validRequestId = (await AddRequestAsync(db, confirmed: true)).Id;
        }

        var dispatcher = Fixture.Factory.Services.GetRequiredService<CreditScoreDispatcher>();
        await dispatcher.DispatchPendingAsync(CancellationToken.None);

        await using var assertScope = Fixture.Factory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await assertDb.CreditScoreRequests.FindAsync(invalidRequestId))!
            .DispatchedAt.ShouldBeNull();
        (await assertDb.CreditScoreRequests.FindAsync(validRequestId))!
            .DispatchedAt.ShouldNotBeNull();
        (await assertDb.DepositorScoreTransactions.CountAsync()).ShouldBe(1);
    }

    private static async Task<CreditScoreRequest> AddRequestAsync(
        AppDbContext db,
        bool confirmed)
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
            Journey.CollectPoint);
        var rule = new MaterialScoreRule(
            point.Id,
            ElectronicMaterial.Battery,
            10m,
            MaterialScoreUnit.PerUnit,
            DateTime.UtcNow.AddDays(-1));
        var discard = new Discard(
            depositor.Id,
            point.Id,
            [new DiscardItem(ElectronicMaterial.Battery, 2, 0.250m, rule.Id)]);
        var request = new CreditScoreRequest(discard.Id);

        db.NaturalPeople.Add(depositor);
        db.LegalPeople.Add(point);
        db.MaterialScoreRules.Add(rule);
        db.Discards.Add(discard);
        db.CreditScoreRequests.Add(request);
        if (confirmed)
        {
            db.Entry(discard).Property(value => value.Status).CurrentValue =
                DiscardStatus.Confirmed;
        }

        await db.SaveChangesAsync();
        return request;
    }
}
```

- [ ] **Step 7: Executar o teste de integração do dispatcher**

Run:

```powershell
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~Ecocell.IntegrationTests.Jobs.CreditScoreDispatcherTests"
```

Expected: pedido inválido permanece pendente; pedido válido é processado; o lote continua.

- [ ] **Step 8: Executar a suíte completa**

Run:

```powershell
rtk dotnet test Ecocell.slnx
```

Expected: zero falhas.

- [ ] **Step 9: Revisar e commitar a tarefa**

Run:

```powershell
rtk git diff --check
rtk git add -- src/Ecocell.Api/Jobs/CreditScoreDispatcher.cs src/Ecocell.Api/Extensions/DependencyInjectionExtensions.cs tests/Ecocell.UnitTests/Extensions/DependencyInjectionExtensionsTests.cs tests/Ecocell.IntegrationTests/Jobs/CreditScoreDispatcherTests.cs
rtk git diff --cached --check
rtk git commit -m "feat(score): adiciona dispatcher de credito"
```

---

### Task 6: Validar unicidade e concorrência no PostgreSQL

**Files:**
- Create: `tests/Ecocell.IntegrationTests/Infrastructure/CreditScoreWriteBarrierInterceptor.cs`
- Create: `tests/Ecocell.IntegrationTests/Database/CreditScorePersistenceTests.cs`
- Create: `tests/Ecocell.IntegrationTests/Jobs/CreditScoreConcurrencyTests.cs`

**Interfaces:**
- Consumes: migration, job e registros DI das Tasks 2–5.
- Produces: evidência PostgreSQL de unicidade por descarte, unicidade por depositante e retry natural após conflito de saldo.

- [ ] **Step 1: Criar a barreira determinística de escrita**

Criar `CreditScoreWriteBarrierInterceptor.cs`:

```csharp
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ecocell.IntegrationTests.Infrastructure;

internal sealed class CreditScoreWriteBarrierInterceptor(string commandMarker)
    : DbCommandInterceptor
{
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
        if (!commandText.Contains(commandMarker, StringComparison.Ordinal))
            return;

        if (Interlocked.Increment(ref _arrivalCount) == 2)
            _bothArrived.TrySetResult(true);

        await _bothArrived.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
    }
}
```

- [ ] **Step 2: Adicionar testes PostgreSQL dos índices únicos**

Criar `tests/Ecocell.IntegrationTests/Database/CreditScorePersistenceTests.cs`:

```csharp
using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Ecocell.IntegrationTests.Database;

[Collection(nameof(IntegrationTestCollection))]
public class CreditScorePersistenceTests : IntegrationTestBase
{
    public CreditScorePersistenceTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task SaveChanges_ShouldRejectDuplicateCreditRequest_ForSameDiscard()
    {
        var seeded = await SeedGraphAsync();
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.CreditScoreRequests.AddRange(
            new CreditScoreRequest(seeded.DiscardId),
            new CreditScoreRequest(seeded.DiscardId));

        await Should.ThrowAsync<DbUpdateException>(async () =>
            await db.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_ShouldRejectDuplicateScoreTransaction_ForSameDiscard()
    {
        var seeded = await SeedGraphAsync();
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.DepositorScoreTransactions.AddRange(
            new DepositorScoreTransaction(seeded.DiscardId, seeded.DepositorId, 10m),
            new DepositorScoreTransaction(seeded.DiscardId, seeded.DepositorId, 10m));

        await Should.ThrowAsync<DbUpdateException>(async () =>
            await db.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_ShouldRejectDuplicateTotalScore_ForSameDepositor()
    {
        var seeded = await SeedGraphAsync();
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.DepositorTotalScores.AddRange(
            new DepositorTotalScore(seeded.DepositorId, 10m),
            new DepositorTotalScore(seeded.DepositorId, 20m));

        await Should.ThrowAsync<DbUpdateException>(async () =>
            await db.SaveChangesAsync());
    }

    private async Task<PersistenceSeed> SeedGraphAsync()
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
            Journey.CollectPoint);
        var rule = new MaterialScoreRule(
            point.Id,
            ElectronicMaterial.Battery,
            10m,
            MaterialScoreUnit.PerUnit,
            DateTime.UtcNow.AddDays(-1));
        var discard = new Discard(
            depositor.Id,
            point.Id,
            [new DiscardItem(ElectronicMaterial.Battery, 1, 0.100m, rule.Id)]);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(depositor, point, rule, discard);
        await db.SaveChangesAsync();

        return new PersistenceSeed(depositor.Id, discard.Id);
    }

    private sealed record PersistenceSeed(Guid DepositorId, Guid DiscardId);
}
```

- [ ] **Step 3: Adicionar teste concorrente para duas execuções do mesmo descarte**

Criar `CreditScoreConcurrencyTests.cs` com os imports, a classe e o primeiro teste:

```csharp
using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Jobs;
using Ecocell.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Ecocell.IntegrationTests.Jobs;

[Collection(nameof(IntegrationTestCollection))]
public class CreditScoreConcurrencyTests : IntegrationTestBase
{
    public CreditScoreConcurrencyTests(IntegrationTestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Execute_ShouldCreditOnlyOnce_WhenSameRequestRunsConcurrently()
    {
        var requestId = await SeedConfirmedRequestAsync(points: 10m, quantity: 2);
        var interceptor = new CreditScoreWriteBarrierInterceptor(
            "INSERT INTO \"DepositorScoreTransactions\"");
        using var factory = Fixture.CreateDbInterceptedFactory(interceptor);

        var outcomes = await Task.WhenAll(
            ExecuteCapturingAsync(factory.Services, requestId),
            ExecuteCapturingAsync(factory.Services, requestId));

        outcomes.Count(exception => exception is null).ShouldBe(1);
        outcomes.Count(exception => exception is DbUpdateException).ShouldBe(1);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.DepositorScoreTransactions.CountAsync()).ShouldBe(1);
        (await db.DepositorTotalScores.SingleAsync()).TotalPoints.ShouldBe(20m);
        (await db.CreditScoreRequests.SingleAsync()).DispatchedAt.ShouldNotBeNull();
    }
}
```

- [ ] **Step 4: Adicionar teste concorrente para descartes do mesmo depositante**

Adicionar:

```csharp
[Fact]
public async Task Execute_ShouldPreserveSum_WhenDifferentRequestsUpdateSameBalance()
{
    var seeded = await SeedTwoConfirmedRequestsWithExistingBalanceAsync(
        initialBalance: 5m,
        firstPoints: 10m,
        secondPoints: 20m);
    var interceptor = new CreditScoreWriteBarrierInterceptor(
        "UPDATE \"DepositorTotalScores\"");
    using var factory = Fixture.CreateDbInterceptedFactory(interceptor);

    var outcomes = await Task.WhenAll(
        ExecuteCapturingAsync(factory.Services, seeded.FirstRequestId),
        ExecuteCapturingAsync(factory.Services, seeded.SecondRequestId));

    outcomes.Count(exception => exception is null).ShouldBe(1);
    outcomes.Count(exception => exception is DbUpdateConcurrencyException).ShouldBe(1);

    await using (var retryScope = factory.Services.CreateAsyncScope())
    {
        var retryDb = retryScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pendingId = await retryDb.CreditScoreRequests
            .Where(value => value.DispatchedAt == null)
            .Select(value => value.Id)
            .SingleAsync();
        var job = retryScope.ServiceProvider.GetRequiredService<CreditScoreJob>();
        await job.ExecuteAsync(pendingId, CancellationToken.None);
    }

    await using var assertScope = factory.Services.CreateAsyncScope();
    var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
    (await assertDb.DepositorScoreTransactions.CountAsync()).ShouldBe(2);
    (await assertDb.DepositorTotalScores.SingleAsync()).TotalPoints.ShouldBe(35m);
    (await assertDb.CreditScoreRequests.CountAsync(
        value => value.DispatchedAt != null)).ShouldBe(2);
}
```

Adicionar também os helpers abaixo dentro da classe, antes do `}` final:

```csharp
private static async Task<Exception?> ExecuteCapturingAsync(
    IServiceProvider services,
    Guid requestId)
{
    await using var scope = services.CreateAsyncScope();
    var job = scope.ServiceProvider.GetRequiredService<CreditScoreJob>();
    try
    {
        await job.ExecuteAsync(requestId, CancellationToken.None);
        return null;
    }
    catch (Exception exception)
    {
        return exception;
    }
}

private async Task<Guid> SeedConfirmedRequestAsync(decimal points, int quantity)
{
    var faker = new Faker("pt_BR");
    var depositor = CreateDepositor(faker);
    var point = CreateCollectPoint(faker);
    var rule = new MaterialScoreRule(
        point.Id,
        ElectronicMaterial.Battery,
        points,
        MaterialScoreUnit.PerUnit,
        DateTime.UtcNow.AddDays(-1));
    var discard = new Discard(
        depositor.Id,
        point.Id,
        [new DiscardItem(ElectronicMaterial.Battery, quantity, 0.100m, rule.Id)]);
    var request = new CreditScoreRequest(discard.Id);

    await using var scope = Fixture.Factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.AddRange(depositor, point, rule, discard, request);
    db.Entry(discard).Property(value => value.Status).CurrentValue =
        DiscardStatus.Confirmed;
    await db.SaveChangesAsync();

    return request.Id;
}

private async Task<ConcurrentRequestSeed> SeedTwoConfirmedRequestsWithExistingBalanceAsync(
    decimal initialBalance,
    decimal firstPoints,
    decimal secondPoints)
{
    var faker = new Faker("pt_BR");
    var depositor = CreateDepositor(faker);
    var point = CreateCollectPoint(faker);
    var firstRule = new MaterialScoreRule(
        point.Id,
        ElectronicMaterial.Battery,
        firstPoints,
        MaterialScoreUnit.PerUnit,
        DateTime.UtcNow.AddDays(-1));
    var secondRule = new MaterialScoreRule(
        point.Id,
        ElectronicMaterial.CellPhone,
        secondPoints,
        MaterialScoreUnit.PerUnit,
        DateTime.UtcNow.AddDays(-1));
    var firstDiscard = new Discard(
        depositor.Id,
        point.Id,
        [new DiscardItem(ElectronicMaterial.Battery, 1, 0.100m, firstRule.Id)]);
    var secondDiscard = new Discard(
        depositor.Id,
        point.Id,
        [new DiscardItem(ElectronicMaterial.CellPhone, 1, 0.200m, secondRule.Id)]);
    var firstRequest = new CreditScoreRequest(firstDiscard.Id);
    var secondRequest = new CreditScoreRequest(secondDiscard.Id);
    var balance = new DepositorTotalScore(depositor.Id, initialBalance);

    await using var scope = Fixture.Factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.AddRange(
        depositor,
        point,
        firstRule,
        secondRule,
        firstDiscard,
        secondDiscard,
        firstRequest,
        secondRequest,
        balance);
    db.Entry(firstDiscard).Property(value => value.Status).CurrentValue =
        DiscardStatus.Confirmed;
    db.Entry(secondDiscard).Property(value => value.Status).CurrentValue =
        DiscardStatus.Confirmed;
    await db.SaveChangesAsync();

    return new ConcurrentRequestSeed(firstRequest.Id, secondRequest.Id);
}

private static NaturalPerson CreateDepositor(Faker faker) =>
    new(
        faker.Name.FullName(),
        faker.Person.Cpf(includeFormatSymbols: false),
        DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
        Role.User,
        faker.Internet.Email(),
        Journey.Depositor);

private static LegalPerson CreateCollectPoint(Faker faker) =>
    new(
        faker.Company.CompanyName(),
        faker.Company.CompanyName(),
        faker.Company.Cnpj(includeFormatSymbols: false),
        faker.Internet.Email(),
        Journey.CollectPoint);

private sealed record ConcurrentRequestSeed(
    Guid FirstRequestId,
    Guid SecondRequestId);
```

- [ ] **Step 5: Executar os testes PostgreSQL adicionados**

Run:

```powershell
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~Ecocell.IntegrationTests.Database.CreditScorePersistenceTests|FullyQualifiedName~Ecocell.IntegrationTests.Jobs.CreditScoreConcurrencyTests"
```

Expected: os três índices rejeitam duplicidade; no mesmo descarte há um vencedor e uma violação única; no mesmo saldo há um vencedor, uma `DbUpdateConcurrencyException` e soma final `35m` após o retry.

- [ ] **Step 6: Executar três vezes os testes concorrentes**

Run três vezes:

```powershell
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~Ecocell.IntegrationTests.Jobs.CreditScoreConcurrencyTests"
```

Expected em todas as execuções: mesmo descarte gera um crédito; descartes diferentes preservam soma após retry.

- [ ] **Step 7: Executar a suíte completa**

Run:

```powershell
rtk dotnet test Ecocell.slnx
```

Expected: zero falhas.

- [ ] **Step 8: Revisar e commitar a tarefa**

Run:

```powershell
rtk git diff --check
rtk git add -- tests/Ecocell.IntegrationTests/Infrastructure/CreditScoreWriteBarrierInterceptor.cs tests/Ecocell.IntegrationTests/Database/CreditScorePersistenceTests.cs tests/Ecocell.IntegrationTests/Jobs/CreditScoreConcurrencyTests.cs src/Ecocell.Api/Jobs/CreditScoreDispatcher.cs
rtk git diff --cached --check
rtk git commit -m "test(score): cobre concorrencia do credito"
```

---

### Task 7: Verificação final e auditoria de escopo

**Files:**
- Verify: todos os arquivos alterados pelas Tasks 1–6.

**Interfaces:**
- Consumes: implementação completa da WND-290.
- Produces: evidência de suíte verde, migrations alinhadas, DI correta e diff restrito ao escopo aprovado.

- [ ] **Step 1: Executar testes unitários focados**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~CreditScoreRequestTests|FullyQualifiedName~DepositorScoreTransactionTests|FullyQualifiedName~DepositorTotalScoreTests|FullyQualifiedName~CreditScorePersistenceTests|FullyQualifiedName~CreditScoreJobTests|FullyQualifiedName~DependencyInjectionExtensionsTests"
```

Expected: todos os testes selecionados passam.

- [ ] **Step 2: Executar testes de integração focados**

Run:

```powershell
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~Ecocell.IntegrationTests.Jobs.CreditScoreDispatcherTests|FullyQualifiedName~Ecocell.IntegrationTests.Jobs.CreditScoreConcurrencyTests|FullyQualifiedName~Ecocell.IntegrationTests.Database.CreditScorePersistenceTests"
```

Expected: dispatcher, constraints e concorrência passam contra PostgreSQL real.

- [ ] **Step 3: Validar migration e build**

Run:

```powershell
rtk dotnet ef migrations has-pending-model-changes --project src/Ecocell.Api --startup-project src/Ecocell.Api
rtk dotnet build Ecocell.slnx
```

Expected: nenhuma mudança pendente e build sem erros.

- [ ] **Step 4: Executar a suíte completa**

Run:

```powershell
rtk dotnet test Ecocell.slnx
```

Expected: zero testes falhando. Não declarar conclusão com suíte parcial.

- [ ] **Step 5: Auditar arquivos, dependências e escopo**

Run:

```powershell
rtk git status --short
rtk git diff --check
rtk git log --oneline -8
rtk rg -n "Hangfire|BackgroundJob|RecurringJob|RabbitMQ|MassTransit" src tests
rtk rg -n "CreditScoreRequest|DepositorScoreTransaction|DepositorTotalScore|CreditScoreJob|CreditScoreDispatcher" src tests
```

Expected:

- nenhum arquivo alheio à WND-290 modificado;
- nenhum erro de whitespace;
- nenhuma nova dependência de scheduler ou fila;
- commits pequenos das Tasks 1–6 visíveis;
- nenhum endpoint, DTO, ranking, cupom, Mobile ou código da WND-289 incluído.

- [ ] **Step 6: Revisar critérios de aceite linha a linha**

Confirmar com teste ou trecho de código:

- `CreditScoreRequest` é persistente e único por descarte;
- dispatcher inicia imediatamente, usa intervalo de 5 segundos e lote de 50;
- hosted service não inicia em `Testing`;
- `PerUnit` e `PerKilogram` preservam cinco casas;
- `Admin` e `Support` concluem sem crédito;
- ledger é único por descarte;
- saldo é único por depositante e usa concurrency token;
- transação, saldo e `DispatchedAt` são atômicos;
- falha mantém pedido pendente e não interrompe lote;
- processamento concorrente não duplica crédito nem perde saldo;
- WND-289 pode construir `new CreditScoreRequest(discardId)` e acessar `DbSet<CreditScoreRequest>`;
- nenhum item fora do escopo foi implementado.

Se algum item não tiver evidência, a implementação ainda não está concluída.
