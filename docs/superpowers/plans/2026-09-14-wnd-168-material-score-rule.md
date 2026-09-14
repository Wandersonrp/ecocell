# WND-168 — US011.A — Regra de pontuação por material Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar a fundação versionada da tabela de pontuação por material de cada Ponto de Coleta, com contratos compartilhados, invariantes de domínio, persistência relacional e migration.

**Architecture:** Adicionar `MaterialScoreRule` como entidade temporal independente ligada a `LegalPerson`. Cada alteração futura criará uma nova linha e fechará a anterior; o banco impedirá duas regras abertas para o mesmo Ponto de Coleta e material por meio de índice único filtrado.

**Tech Stack:** .NET 10, C# 14, EF Core 10, Npgsql, SQLite in-memory, xUnit, Shouldly e Bogus.

**Spec:** `docs/superpowers/specs/2026-09-14-wnd-168-material-score-rule-design.md`

## Global Constraints

* Aplicar TDD estrito: RED antes de produção, GREEN mínimo e REFACTOR com testes verdes.
* Manter nomes e valores numéricos idênticos entre os enums internos e públicos.
* Reservar `0` como valor inválido para `ElectronicMaterial` e `MaterialScoreUnit`.
* Persistir `Material` e `Unit` como strings; não criar check constraint enumerando valores.
* Toda regra nasce aberta. Não aceitar `ValidTo` no construtor público.
* Usar intervalo semiaberto `[ValidFrom, ValidTo)` e aceitar somente `DateTimeKind.Utc`.
* As propriedades da política não podem ser alteradas depois da construção; expor apenas `Close(DateTime)`.
* O banco deve impedir mais de uma regra aberta por `LegalPersonId + Material`.
* A FK conhece somente `LegalPerson` e usa `DeleteBehavior.Restrict`; jornada, status e autorização pertencem à US011.B.
* Não criar endpoint, DTO, handler, repository, service, tela Mobile, cálculo de pontos ou crédito nesta task.
* Usar `AppDbContext` diretamente nos testes de persistência e estender `TestBase`.
* Escrever summaries de produção em PT-BR e preservar as convenções VSA existentes.
* Preservar toda alteração preexistente no worktree. Usar `git add` apenas com caminhos explícitos.
* A migration deve conter somente `MaterialScoreRules`. Se capturar alterações não relacionadas do worktree, parar e reconciliar antes do commit.
* Verificação final obrigatória: `dotnet test Ecocell.slnx`. A suíte de integração exige Docker disponível.

---

## File Map

### Criar

* `src/Ecocell.Api/Enums/ElectronicMaterial.cs`
* `src/Ecocell.Api/Enums/MaterialScoreUnit.cs`
* `src/Ecocell.Shared/Enums/ElectronicMaterial.cs`
* `src/Ecocell.Shared/Enums/MaterialScoreUnit.cs`
* `src/Ecocell.Api/Entities/MaterialScoreRule.cs`
* `src/Ecocell.Api/Database/TypeConfiguration/MaterialScoreRuleTypeConfiguration.cs`
* `tests/Ecocell.UnitTests/Shared/Enums/MaterialScoreEnumsTests.cs`
* `tests/Ecocell.UnitTests/Entities/MaterialScoreRuleTests.cs`
* `tests/Ecocell.UnitTests/Database/MaterialScoreRulePersistenceTests.cs`
* migration `AddMaterialScoreRules` gerada pelo EF Core

### Modificar

* `src/Ecocell.Api/Database/AppDbContext.cs`
* `src/Ecocell.Api/Migrations/AppDbContextModelSnapshot.cs`

---

### Task 1: Contratos internos e públicos de material e unidade

**Files:**

* Create: `tests/Ecocell.UnitTests/Shared/Enums/MaterialScoreEnumsTests.cs`
* Create: `src/Ecocell.Api/Enums/ElectronicMaterial.cs`
* Create: `src/Ecocell.Api/Enums/MaterialScoreUnit.cs`
* Create: `src/Ecocell.Shared/Enums/ElectronicMaterial.cs`
* Create: `src/Ecocell.Shared/Enums/MaterialScoreUnit.cs`

**Interfaces:**

* Produces: `Ecocell.Api.Enums.ElectronicMaterial`.
* Produces: `Ecocell.Api.Enums.MaterialScoreUnit`.
* Produces: contratos públicos homônimos serializados como strings.
* Guarantees: paridade exata de nomes e valores entre API e Shared.

- [ ] **Step 1: Inventariar o worktree e executar o baseline unitário**

```powershell
git status --short
git branch --show-current
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore
```

Expected: branch conhecida, alterações preexistentes registradas e suíte unitária passando. Não limpar nem incluir alterações alheias.

- [ ] **Step 2: Escrever o teste de contrato que falha**

Criar `tests/Ecocell.UnitTests/Shared/Enums/MaterialScoreEnumsTests.cs`:

```csharp
using System.Text.Json;
using Shouldly;
using ApiEnums = Ecocell.Api.Enums;
using SharedEnums = Ecocell.Shared.Enums;

namespace Ecocell.UnitTests.Shared.Enums;

public class MaterialScoreEnumsTests
{
    [Fact]
    public void ElectronicMaterial_ShouldKeepPublicAndInternalContractsAligned()
    {
        var expected = new (string Name, int Value)[]
        {
            ("Battery", 1),
            ("CellPhone", 2),
            ("PortableGameConsole", 3),
            ("Headphones", 4),
            ("Printer", 5),
            ("Notebook", 6),
            ("SmallAppliance", 7),
        };

        ReadValues<ApiEnums.ElectronicMaterial>().ShouldBe(expected);
        ReadValues<SharedEnums.ElectronicMaterial>().ShouldBe(expected);
    }

    [Fact]
    public void MaterialScoreUnit_ShouldKeepPublicAndInternalContractsAligned()
    {
        var expected = new (string Name, int Value)[]
        {
            ("PerUnit", 1),
            ("PerKilogram", 2),
        };

        ReadValues<ApiEnums.MaterialScoreUnit>().ShouldBe(expected);
        ReadValues<SharedEnums.MaterialScoreUnit>().ShouldBe(expected);
    }

    [Fact]
    public void PublicEnums_ShouldSerializeAsStrings()
    {
        JsonSerializer.Serialize(SharedEnums.ElectronicMaterial.CellPhone)
            .ShouldBe("\"CellPhone\"");
        JsonSerializer.Serialize(SharedEnums.MaterialScoreUnit.PerKilogram)
            .ShouldBe("\"PerKilogram\"");
    }

    private static (string Name, int Value)[] ReadValues<TEnum>()
        where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>()
            .Select(value => (value.ToString(), Convert.ToInt32(value)))
            .ToArray();
}
```

- [ ] **Step 3: Executar o teste e confirmar RED**

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore --filter "FullyQualifiedName~MaterialScoreEnumsTests"
```

Expected: FAIL de compilação porque os quatro enums ainda não existem. Uma falha diferente deve ser diagnosticada antes de seguir.

- [ ] **Step 4: Criar os enums internos mínimos**

Criar `src/Ecocell.Api/Enums/ElectronicMaterial.cs`:

```csharp
namespace Ecocell.Api.Enums;

/// <summary>Materiais eletrônicos aceitos pelas regras de pontuação.</summary>
public enum ElectronicMaterial
{
    /// <summary>Pilha ou bateria.</summary>
    Battery = 1,
    /// <summary>Telefone celular.</summary>
    CellPhone = 2,
    /// <summary>Console portátil de jogos.</summary>
    PortableGameConsole = 3,
    /// <summary>Fone de ouvido.</summary>
    Headphones = 4,
    /// <summary>Impressora.</summary>
    Printer = 5,
    /// <summary>Notebook.</summary>
    Notebook = 6,
    /// <summary>Pequeno eletrodoméstico.</summary>
    SmallAppliance = 7,
}
```

Criar `src/Ecocell.Api/Enums/MaterialScoreUnit.cs`:

```csharp
namespace Ecocell.Api.Enums;

/// <summary>Unidade usada para conceder pontos por material.</summary>
public enum MaterialScoreUnit
{
    /// <summary>Pontos concedidos por unidade.</summary>
    PerUnit = 1,
    /// <summary>Pontos concedidos por quilograma.</summary>
    PerKilogram = 2,
}
```

- [ ] **Step 5: Criar os enums públicos mínimos**

Criar `src/Ecocell.Shared/Enums/ElectronicMaterial.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Ecocell.Shared.Enums;

/// <summary>Materiais eletrônicos aceitos pelas regras de pontuação.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ElectronicMaterial
{
    Battery = 1,
    CellPhone = 2,
    PortableGameConsole = 3,
    Headphones = 4,
    Printer = 5,
    Notebook = 6,
    SmallAppliance = 7,
}
```

Criar `src/Ecocell.Shared/Enums/MaterialScoreUnit.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Ecocell.Shared.Enums;

/// <summary>Unidade usada para conceder pontos por material.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaterialScoreUnit
{
    PerUnit = 1,
    PerKilogram = 2,
}
```

- [ ] **Step 6: Executar o teste focado e a suíte unitária**

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore --filter "FullyQualifiedName~MaterialScoreEnumsTests"
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore
git diff --check -- src/Ecocell.Api/Enums/ElectronicMaterial.cs src/Ecocell.Api/Enums/MaterialScoreUnit.cs src/Ecocell.Shared/Enums/ElectronicMaterial.cs src/Ecocell.Shared/Enums/MaterialScoreUnit.cs tests/Ecocell.UnitTests/Shared/Enums/MaterialScoreEnumsTests.cs
```

Expected: PASS em ambos os testes e nenhuma inconsistência de whitespace.

- [ ] **Step 7: Commitar apenas os contratos**

```powershell
git add -- src/Ecocell.Api/Enums/ElectronicMaterial.cs src/Ecocell.Api/Enums/MaterialScoreUnit.cs src/Ecocell.Shared/Enums/ElectronicMaterial.cs src/Ecocell.Shared/Enums/MaterialScoreUnit.cs tests/Ecocell.UnitTests/Shared/Enums/MaterialScoreEnumsTests.cs
git diff --cached --check
git diff --cached --stat
git commit -m "feat(score): adiciona contratos de material e unidade"
```

Expected: commit contém somente os cinco arquivos desta task.

---

### Task 2: Entidade temporal e invariantes de domínio

**Files:**

* Create: `tests/Ecocell.UnitTests/Entities/MaterialScoreRuleTests.cs`
* Create: `src/Ecocell.Api/Entities/MaterialScoreRule.cs`

**Interfaces:**

* Consumes: enums internos criados na Task 1.
* Produces: `MaterialScoreRule(Guid, ElectronicMaterial, decimal, MaterialScoreUnit, DateTime)`.
* Produces: `void Close(DateTime closedAt)`.
* Guarantees: regra aberta na criação, UTC obrigatório, valores definidos e vigência imutável após fechamento.

- [ ] **Step 1: Escrever os testes de domínio que falham**

Criar `tests/Ecocell.UnitTests/Entities/MaterialScoreRuleTests.cs`:

```csharp
using System.Globalization;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Shouldly;

namespace Ecocell.UnitTests.Entities;

public class MaterialScoreRuleTests
{
    private static readonly DateTime ValidFrom =
        new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_ShouldCreateOpenRule_WhenDataIsValid()
    {
        var legalPersonId = Guid.NewGuid();

        var rule = new MaterialScoreRule(
            legalPersonId,
            ElectronicMaterial.Battery,
            12.50m,
            MaterialScoreUnit.PerUnit,
            ValidFrom);

        rule.LegalPersonId.ShouldBe(legalPersonId);
        rule.Material.ShouldBe(ElectronicMaterial.Battery);
        rule.Points.ShouldBe(12.50m);
        rule.Unit.ShouldBe(MaterialScoreUnit.PerUnit);
        rule.ValidFrom.ShouldBe(ValidFrom);
        rule.ValidTo.ShouldBeNull();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLegalPersonIdIsEmpty()
    {
        Should.Throw<ArgumentException>(() => new MaterialScoreRule(
            Guid.Empty,
            ElectronicMaterial.Battery,
            10m,
            MaterialScoreUnit.PerUnit,
            ValidFrom));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void Constructor_ShouldThrow_WhenMaterialIsInvalid(int value)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new MaterialScoreRule(
            Guid.NewGuid(),
            (ElectronicMaterial)value,
            10m,
            MaterialScoreUnit.PerUnit,
            ValidFrom));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void Constructor_ShouldThrow_WhenUnitIsInvalid(int value)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new MaterialScoreRule(
            Guid.NewGuid(),
            ElectronicMaterial.Battery,
            10m,
            (MaterialScoreUnit)value,
            ValidFrom));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    public void Constructor_ShouldThrow_WhenPointsAreNotPositive(string value)
    {
        var points = decimal.Parse(value, CultureInfo.InvariantCulture);

        Should.Throw<ArgumentOutOfRangeException>(() => new MaterialScoreRule(
            Guid.NewGuid(),
            ElectronicMaterial.Battery,
            points,
            MaterialScoreUnit.PerUnit,
            ValidFrom));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Constructor_ShouldThrow_WhenValidFromIsNotUtc(DateTimeKind kind)
    {
        var invalidDate = DateTime.SpecifyKind(new DateTime(2026, 9, 14, 12, 0, 0), kind);

        Should.Throw<ArgumentException>(() => new MaterialScoreRule(
            Guid.NewGuid(),
            ElectronicMaterial.Battery,
            10m,
            MaterialScoreUnit.PerUnit,
            invalidDate));
    }

    [Fact]
    public void Close_ShouldSetValidToAndUpdatedAt_WhenDateIsValid()
    {
        var rule = CreateRule();
        var closedAt = ValidFrom.AddHours(1);
        var beforeClose = DateTime.UtcNow;

        rule.Close(closedAt);

        rule.ValidTo.ShouldBe(closedAt);
        rule.UpdatedAt.ShouldNotBeNull();
        rule.UpdatedAt.Value.ShouldBeGreaterThanOrEqualTo(beforeClose);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Close_ShouldThrow_WhenDateIsNotAfterValidFrom(int seconds)
    {
        var rule = CreateRule();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            rule.Close(ValidFrom.AddSeconds(seconds)));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Close_ShouldThrow_WhenDateIsNotUtc(DateTimeKind kind)
    {
        var rule = CreateRule();
        var invalidDate = DateTime.SpecifyKind(ValidFrom.AddHours(1), kind);

        Should.Throw<ArgumentException>(() => rule.Close(invalidDate));
    }

    [Fact]
    public void Close_ShouldThrow_WhenRuleIsAlreadyClosed()
    {
        var rule = CreateRule();
        rule.Close(ValidFrom.AddHours(1));

        Should.Throw<InvalidOperationException>(() =>
            rule.Close(ValidFrom.AddHours(2)));
    }

    private static MaterialScoreRule CreateRule() =>
        new(
            Guid.NewGuid(),
            ElectronicMaterial.Battery,
            10m,
            MaterialScoreUnit.PerUnit,
            ValidFrom);
}
```

- [ ] **Step 2: Executar o teste e confirmar RED**

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore --filter "FullyQualifiedName~MaterialScoreRuleTests"
```

Expected: FAIL de compilação porque `MaterialScoreRule` ainda não existe.

- [ ] **Step 3: Implementar a entidade mínima**

Criar `src/Ecocell.Api/Entities/MaterialScoreRule.cs`:

```csharp
using Ecocell.Api.Enums;

namespace Ecocell.Api.Entities;

/// <summary>Versão temporal da regra de pontuação de um material em um Ponto de Coleta.</summary>
public sealed class MaterialScoreRule : BaseEntity
{
    private MaterialScoreRule()
    {
    }

    /// <summary>Cria uma regra aberta com início de vigência em UTC.</summary>
    public MaterialScoreRule(
        Guid legalPersonId,
        ElectronicMaterial material,
        decimal points,
        MaterialScoreUnit unit,
        DateTime validFrom)
    {
        if (legalPersonId == Guid.Empty)
            throw new ArgumentException("O identificador da pessoa jurídica é obrigatório.", nameof(legalPersonId));

        if (!Enum.IsDefined(typeof(ElectronicMaterial), material))
            throw new ArgumentOutOfRangeException(nameof(material), "O material informado é inválido.");

        if (points <= 0)
            throw new ArgumentOutOfRangeException(nameof(points), "A pontuação deve ser maior que zero.");

        if (!Enum.IsDefined(typeof(MaterialScoreUnit), unit))
            throw new ArgumentOutOfRangeException(nameof(unit), "A unidade de pontuação informada é inválida.");

        EnsureUtc(validFrom, nameof(validFrom));

        LegalPersonId = legalPersonId;
        Material = material;
        Points = points;
        Unit = unit;
        ValidFrom = validFrom;
    }

    public Guid LegalPersonId { get; private set; }
    public LegalPerson LegalPerson { get; private set; } = null!;
    public ElectronicMaterial Material { get; private set; }
    public decimal Points { get; private set; }
    public MaterialScoreUnit Unit { get; private set; }
    public DateTime ValidFrom { get; private set; }
    public DateTime? ValidTo { get; private set; }

    /// <summary>Encerra a vigência da regra sem apagar seu histórico.</summary>
    public void Close(DateTime closedAt)
    {
        if (ValidTo is not null)
            throw new InvalidOperationException("A regra de pontuação já está encerrada.");

        EnsureUtc(closedAt, nameof(closedAt));

        if (closedAt <= ValidFrom)
            throw new ArgumentOutOfRangeException(
                nameof(closedAt),
                "O encerramento deve ser posterior ao início da vigência.");

        ValidTo = closedAt;
        MarkAsUpdated();
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A data deve estar em UTC.", parameterName);
    }
}
```

- [ ] **Step 4: Executar GREEN e revisar encapsulamento**

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore --filter "FullyQualifiedName~MaterialScoreRuleTests"
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore
rg -n "public set|ValidTo.*constructor|Update|Change" src/Ecocell.Api/Entities/MaterialScoreRule.cs
git diff --check -- src/Ecocell.Api/Entities/MaterialScoreRule.cs tests/Ecocell.UnitTests/Entities/MaterialScoreRuleTests.cs
```

Expected: testes PASS; nenhum setter público, mutador genérico ou parâmetro `ValidTo` no construtor.

- [ ] **Step 5: Commitar entidade e testes**

```powershell
git add -- src/Ecocell.Api/Entities/MaterialScoreRule.cs tests/Ecocell.UnitTests/Entities/MaterialScoreRuleTests.cs
git diff --cached --check
git diff --cached --stat
git commit -m "feat(score): adiciona regra temporal de pontuacao"
```

Expected: commit contém somente a entidade e seus testes.

---

### Task 3: Mapeamento EF Core e comportamento relacional

**Files:**

* Create: `tests/Ecocell.UnitTests/Database/MaterialScoreRulePersistenceTests.cs`
* Create: `src/Ecocell.Api/Database/TypeConfiguration/MaterialScoreRuleTypeConfiguration.cs`
* Modify: `src/Ecocell.Api/Database/AppDbContext.cs`

**Interfaces:**

* Consumes: `MaterialScoreRule`, `LegalPerson` e `BaseEntityTypeConfiguration<T>`.
* Produces: `AppDbContext.MaterialScoreRules`.
* Guarantees: FK restrita, enums textuais, precisão `(10,2)`, checks temporais e unicidade da regra aberta.

- [ ] **Step 1: Escrever os testes de persistência que falham**

Criar `tests/Ecocell.UnitTests/Database/MaterialScoreRulePersistenceTests.cs`:

```csharp
using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Ecocell.UnitTests.Database;

public class MaterialScoreRulePersistenceTests : TestBase
{
    private static readonly DateTime ValidFrom =
        new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Save_ShouldRoundTripRuleAndStoreEnumsAsStrings()
    {
        var collectPoint = CreateCollectPoint();
        var rule = CreateRule(collectPoint.Id);
        DbContext.LegalPeople.Add(collectPoint);
        DbContext.MaterialScoreRules.Add(rule);

        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var persisted = await DbContext.MaterialScoreRules.SingleAsync(x => x.Id == rule.Id);
        persisted.LegalPersonId.ShouldBe(collectPoint.Id);
        persisted.Material.ShouldBe(ElectronicMaterial.Battery);
        persisted.Points.ShouldBe(10.25m);
        persisted.Unit.ShouldBe(MaterialScoreUnit.PerUnit);
        persisted.ValidFrom.ShouldBe(ValidFrom);
        persisted.ValidTo.ShouldBeNull();

        await using var command = DbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT "Material", "Unit"
            FROM "MaterialScoreRules"
            WHERE "Id" = $id;
            """;
        command.Parameters.Add(new SqliteParameter("$id", rule.Id));

        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeTrue();
        reader.GetString(0).ShouldBe("Battery");
        reader.GetString(1).ShouldBe("PerUnit");
    }

    [Fact]
    public void Model_ShouldConfigureLengthsAndPointsPrecision()
    {
        var entityType = DbContext.Model.FindEntityType(typeof(MaterialScoreRule));
        entityType.ShouldNotBeNull();

        var material = entityType.FindProperty(nameof(MaterialScoreRule.Material));
        var unit = entityType.FindProperty(nameof(MaterialScoreRule.Unit));
        var points = entityType.FindProperty(nameof(MaterialScoreRule.Points));

        material.ShouldNotBeNull();
        unit.ShouldNotBeNull();
        points.ShouldNotBeNull();
        material.GetMaxLength().ShouldBe(50);
        unit.GetMaxLength().ShouldBe(20);
        points.GetPrecision().ShouldBe(10);
        points.GetScale().ShouldBe(2);
    }

    [Fact]
    public async Task Save_ShouldRejectTwoOpenRulesForSameCollectPointAndMaterial()
    {
        var collectPoint = CreateCollectPoint();
        DbContext.LegalPeople.Add(collectPoint);
        DbContext.MaterialScoreRules.AddRange(
            CreateRule(collectPoint.Id),
            CreateRule(collectPoint.Id, 20m, ValidFrom.AddMinutes(1)));

        await Should.ThrowAsync<DbUpdateException>(() => DbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Save_ShouldAcceptNewVersionAfterPreviousRuleIsClosed()
    {
        var collectPoint = CreateCollectPoint();
        var firstRule = CreateRule(collectPoint.Id);
        DbContext.LegalPeople.Add(collectPoint);
        DbContext.MaterialScoreRules.Add(firstRule);
        await DbContext.SaveChangesAsync();

        var changedAt = ValidFrom.AddHours(1);
        firstRule.Close(changedAt);
        DbContext.MaterialScoreRules.Add(CreateRule(collectPoint.Id, 20m, changedAt));
        await DbContext.SaveChangesAsync();

        (await DbContext.MaterialScoreRules.CountAsync()).ShouldBe(2);
        (await DbContext.MaterialScoreRules.CountAsync(x => x.ValidTo == null)).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteLegalPerson_ShouldFail_WhenRuleReferencesIt()
    {
        var collectPoint = CreateCollectPoint();
        DbContext.LegalPeople.Add(collectPoint);
        DbContext.MaterialScoreRules.Add(CreateRule(collectPoint.Id));
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var persistedPoint = await DbContext.LegalPeople.SingleAsync(x => x.Id == collectPoint.Id);
        DbContext.LegalPeople.Remove(persistedPoint);

        await Should.ThrowAsync<DbUpdateException>(() => DbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Database_ShouldRejectNonPositivePoints()
    {
        var collectPoint = await PersistCollectPointAsync();

        await Should.ThrowAsync<SqliteException>(() =>
            InsertRawRuleAsync(collectPoint.Id, 0m, ValidFrom, null));
    }

    [Fact]
    public async Task Database_ShouldRejectValidToNotAfterValidFrom()
    {
        var collectPoint = await PersistCollectPointAsync();

        await Should.ThrowAsync<SqliteException>(() =>
            InsertRawRuleAsync(collectPoint.Id, 10m, ValidFrom, ValidFrom));
    }

    private async Task<LegalPerson> PersistCollectPointAsync()
    {
        var collectPoint = CreateCollectPoint();
        DbContext.LegalPeople.Add(collectPoint);
        await DbContext.SaveChangesAsync();
        return collectPoint;
    }

    private Task<int> InsertRawRuleAsync(
        Guid legalPersonId,
        decimal points,
        DateTime validFrom,
        DateTime? validTo)
    {
        DateTime? updatedAt = null;

        return DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "MaterialScoreRules"
                ("Id", "LegalPersonId", "Material", "Points", "Unit",
                 "ValidFrom", "ValidTo", "CreatedAt", "UpdatedAt")
            VALUES
                ({Guid.NewGuid()}, {legalPersonId}, {"Battery"}, {points}, {"PerUnit"},
                 {validFrom}, {validTo}, {DateTime.UtcNow}, {updatedAt});
            """);
    }

    private static MaterialScoreRule CreateRule(
        Guid legalPersonId,
        decimal points = 10.25m,
        DateTime? validFrom = null) =>
        new(
            legalPersonId,
            ElectronicMaterial.Battery,
            points,
            MaterialScoreUnit.PerUnit,
            validFrom ?? ValidFrom);

    private static LegalPerson CreateCollectPoint()
    {
        var faker = new Faker("pt_BR");

        return new LegalPerson(
            faker.Company.CompanyName(),
            faker.Company.CompanyName(),
            faker.Company.Cnpj(includeFormatSymbols: false),
            faker.Internet.Email(),
            Journey.CollectPoint);
    }
}
```

- [ ] **Step 2: Executar o teste e confirmar RED**

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore --filter "FullyQualifiedName~MaterialScoreRulePersistenceTests"
```

Expected: FAIL de compilação porque `AppDbContext.MaterialScoreRules` ainda não existe.

- [ ] **Step 3: Criar o mapeamento mínimo**

Criar `src/Ecocell.Api/Database/TypeConfiguration/MaterialScoreRuleTypeConfiguration.cs`:

```csharp
using Ecocell.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecocell.Api.Database.TypeConfiguration;

/// <summary>Configura a persistência das versões de regra de pontuação.</summary>
public sealed class MaterialScoreRuleTypeConfiguration
    : BaseEntityTypeConfiguration<MaterialScoreRule>
{
    public override void Configure(EntityTypeBuilder<MaterialScoreRule> builder)
    {
        base.Configure(builder);

        builder.ToTable("MaterialScoreRules", table =>
        {
            table.HasCheckConstraint(
                "CK_MaterialScoreRules_Points_Positive",
                "\"Points\" > 0");
            table.HasCheckConstraint(
                "CK_MaterialScoreRules_Validity",
                "\"ValidTo\" IS NULL OR \"ValidTo\" > \"ValidFrom\"");
        });

        builder.HasKey(rule => rule.Id);

        builder.Property(rule => rule.Material)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(rule => rule.Points)
            .HasPrecision(10, 2)
            .IsRequired();

        builder.Property(rule => rule.Unit)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(rule => rule.ValidFrom)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(rule => rule.ValidTo)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(rule => new { rule.LegalPersonId, rule.Material })
            .IsUnique()
            .HasFilter("\"ValidTo\" IS NULL");

        builder.HasOne(rule => rule.LegalPerson)
            .WithMany()
            .HasForeignKey(rule => rule.LegalPersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 4: Registrar o DbSet**

Adicionar a `src/Ecocell.Api/Database/AppDbContext.cs`, junto aos demais `DbSet`:

```csharp
public DbSet<MaterialScoreRule> MaterialScoreRules { get; set; }
```

- [ ] **Step 5: Executar GREEN relacional**

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore --filter "FullyQualifiedName~MaterialScoreRulePersistenceTests"
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore
git diff --check -- src/Ecocell.Api/Database/AppDbContext.cs src/Ecocell.Api/Database/TypeConfiguration/MaterialScoreRuleTypeConfiguration.cs tests/Ecocell.UnitTests/Database/MaterialScoreRulePersistenceTests.cs
```

Expected: todos os testes PASS, incluindo checks, FK restrita, índice filtrado e leitura textual dos enums.

- [ ] **Step 6: Revisar o mapeamento contra a specification**

```powershell
rg -n "HasConversion<string>|HasMaxLength|HasPrecision|HasColumnType|HasFilter|HasCheckConstraint|DeleteBehavior.Restrict" src/Ecocell.Api/Database/TypeConfiguration/MaterialScoreRuleTypeConfiguration.cs
```

Expected:

* `Material`: string, máximo 50 e obrigatório;
* `Unit`: string, máximo 20 e obrigatório;
* `Points`: precisão 10, escala 2, obrigatório e maior que zero;
* `ValidFrom`/`ValidTo`: `timestamp with time zone`;
* unicidade filtrada por `LegalPersonId + Material` com `ValidTo IS NULL`;
* FK obrigatória com delete restrito;
* nenhum check enumerando os valores de material ou unidade.

- [ ] **Step 7: Commitar mapeamento e testes**

```powershell
git add -- src/Ecocell.Api/Database/AppDbContext.cs src/Ecocell.Api/Database/TypeConfiguration/MaterialScoreRuleTypeConfiguration.cs tests/Ecocell.UnitTests/Database/MaterialScoreRulePersistenceTests.cs
git diff --cached --check
git diff --cached --stat
git commit -m "feat(score): mapeia regra de pontuacao"
```

Expected: commit contém somente o DbSet, a configuração e os testes relacionais.

---

### Task 4: Migration, snapshot e verificação final

**Files:**

* Generate: `src/Ecocell.Api/Migrations/*_AddMaterialScoreRules.cs`
* Generate: `src/Ecocell.Api/Migrations/*_AddMaterialScoreRules.Designer.cs`
* Modify: `src/Ecocell.Api/Migrations/AppDbContextModelSnapshot.cs`

**Interfaces:**

* Consumes: modelo EF Core concluído na Task 3.
* Produces: migration reversível `AddMaterialScoreRules`.
* Guarantees: schema PostgreSQL equivalente ao modelo testado e sem alterações colaterais.

- [ ] **Step 1: Registrar novamente o estado sujo antes da geração**

```powershell
git status --short
git diff -- src/Ecocell.Api/Migrations/AppDbContextModelSnapshot.cs
```

Expected: entender qualquer alteração preexistente no snapshot antes de gerar. Se já houver edição não relacionada, não sobrescrever nem misturar sem reconciliação explícita.

- [ ] **Step 2: Gerar a migration pelo EF Core**

```powershell
dotnet ef migrations add AddMaterialScoreRules --project src/Ecocell.Api --startup-project src/Ecocell.Api
```

Expected: migration, designer e snapshot gerados com sucesso.

- [ ] **Step 3: Inspecionar integralmente os artefatos gerados**

```powershell
Get-ChildItem src/Ecocell.Api/Migrations/*_AddMaterialScoreRules*.cs | Select-Object -ExpandProperty FullName
git diff -- src/Ecocell.Api/Migrations
rg -n "MaterialScoreRules|CK_MaterialScoreRules|LegalPersonId|Material|Points|Unit|ValidFrom|ValidTo|CreateIndex|ReferentialAction.Restrict|DropTable" src/Ecocell.Api/Migrations
```

Expected no `Up`:

* `CreateTable("MaterialScoreRules")`;
* `Id`, `CreatedAt` e `UpdatedAt` herdados;
* `LegalPersonId` obrigatório com FK para `LegalPeople("PersonId")` e `Restrict`;
* `Material varchar(50)` e `Unit varchar(20)`;
* `Points numeric(10,2)`;
* `ValidFrom timestamp with time zone` obrigatório;
* `ValidTo timestamp with time zone` opcional;
* os dois check constraints;
* índice único `(LegalPersonId, Material)` filtrado por `"ValidTo" IS NULL`.

Expected no `Down`: apenas remoção segura da tabela `MaterialScoreRules`.

Se aparecer qualquer operação sobre outra tabela, coluna ou índice, remover a migration recém-gerada com `dotnet ef migrations remove`, reconciliar o modelo sujo e gerar novamente. Não editar uma migration contaminada para mascarar o problema.

- [ ] **Step 4: Confirmar que o snapshot representa o modelo e não há mudanças pendentes**

```powershell
dotnet ef migrations has-pending-model-changes --project src/Ecocell.Api --startup-project src/Ecocell.Api
```

Expected: mensagem informando que o modelo não possui mudanças pendentes e exit code 0.

- [ ] **Step 5: Executar toda a verificação automatizada**

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore
dotnet test Ecocell.slnx
git diff --check
```

Expected: todas as suítes PASS e nenhuma inconsistência de whitespace. Se a integração falhar por Docker indisponível, iniciar/restaurar o ambiente e repetir; não declarar conclusão sem a suíte completa verde.

- [ ] **Step 6: Auditar escopo e ausência de atalhos**

```powershell
rg -n "TODO|FIXME|NotImplementedException" src/Ecocell.Api/Enums/ElectronicMaterial.cs src/Ecocell.Api/Enums/MaterialScoreUnit.cs src/Ecocell.Shared/Enums/ElectronicMaterial.cs src/Ecocell.Shared/Enums/MaterialScoreUnit.cs src/Ecocell.Api/Entities/MaterialScoreRule.cs src/Ecocell.Api/Database/TypeConfiguration/MaterialScoreRuleTypeConfiguration.cs tests/Ecocell.UnitTests/Shared/Enums/MaterialScoreEnumsTests.cs tests/Ecocell.UnitTests/Entities/MaterialScoreRuleTests.cs tests/Ecocell.UnitTests/Database/MaterialScoreRulePersistenceTests.cs
git status --short
```

Expected: nenhum placeholder; somente os arquivos planejados e alterações preexistentes conhecidas aparecem.

- [ ] **Step 7: Commitar apenas a migration e o snapshot**

Resolver os dois nomes gerados, validar a quantidade e só então executar o staging limitado:

```powershell
$scoreMigrationFiles = @(Get-ChildItem -Path "src/Ecocell.Api/Migrations/*_AddMaterialScoreRules*.cs")
if ($scoreMigrationFiles.Count -ne 2) { throw "Esperados dois arquivos da migration AddMaterialScoreRules." }
git add -- $scoreMigrationFiles.FullName src/Ecocell.Api/Migrations/AppDbContextModelSnapshot.cs
git diff --cached --check
git diff --cached --stat
git diff --cached -- src/Ecocell.Api/Migrations
git commit -m "feat(score): adiciona migration de regra de pontuacao"
```

Expected: commit contém somente a migration, o designer e o snapshot; o diff confirma que nenhuma mudança não relacionada entrou no schema.

---

## Acceptance Checklist

- [ ] Os enums internos e públicos possuem exatamente os nomes e valores aprovados.
- [ ] Os enums públicos serializam como strings.
- [ ] `MaterialScoreRule` nasce aberta e rejeita ID vazio, enum inválido, pontos não positivos e data não UTC.
- [ ] `Close` aceita somente UTC posterior a `ValidFrom`, atualiza `UpdatedAt` e não permite segundo fechamento.
- [ ] Não há setters públicos nem mutador genérico das propriedades da política.
- [ ] `Material` e `Unit` são persistidos como strings com limites 50 e 20.
- [ ] `Points` usa precisão `(10,2)` e check `> 0`.
- [ ] A vigência usa timestamps com fuso e check `ValidTo IS NULL OR ValidTo > ValidFrom`.
- [ ] A FK aponta para `LegalPeople` e impede exclusão da pessoa jurídica referenciada.
- [ ] O índice único filtrado impede duas regras abertas para o mesmo ponto/material.
- [ ] Fechar uma versão permite persistir a próxima sem apagar o histórico.
- [ ] A migration `AddMaterialScoreRules` contém apenas o schema desta feature e possui `Down` correto.
- [ ] `dotnet ef migrations has-pending-model-changes` retorna sucesso.
- [ ] `dotnet test Ecocell.slnx` passa integralmente.
- [ ] Nenhuma alteração preexistente ou fora do escopo foi staged ou commitada.

## Plan Self-Review

* **Cobertura:** contratos, domínio, mapeamento, constraints, migration, snapshot e validação final estão cobertos por Tasks 1–4.
* **TDD:** cada comportamento novo começa por teste RED e recebe a menor implementação GREEN correspondente.
* **Consistência de tipos:** `ElectronicMaterial` e `MaterialScoreUnit` são internos na entidade e espelhados em Shared; `Points` é `decimal`; datas são `DateTime` UTC.
* **Histórico:** somente `Close` encerra uma versão; a próxima versão é uma nova entidade.
* **Concorrência:** a exclusividade da regra aberta é garantida no banco, não apenas em código.
* **Escopo:** não há endpoint, autorização, consulta de jornada/status, UI ou cálculo de descarte.
* **Placeholders:** não há código pendente ou marcador manual; o nome timestampado da migration é resolvido e validado pelo comando de staging.
* **Worktree:** todos os commits usam staging limitado aos arquivos da task.
