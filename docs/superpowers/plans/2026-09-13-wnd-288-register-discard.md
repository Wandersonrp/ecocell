# WND-288 — Abertura de descarte Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Implementar a abertura de um descarte pendente por uma pessoa depositante autenticada, usando o QR Code de um Ponto de Coleta ativo e congelando a regra de pontuação vigente para cada material.

**Architecture:** Adicionar o agregado relacional Discard + DiscardItem ao AppDbContext. Um único slice VSA recebe o QR bruto, valida depositante e PC, resolve as regras vigentes entregues pela [WND-168](https://linear.app/wnd-dev/issue/WND-168/us011-tabela-de-pontuacao-por-material), persiste o agregado atomicamente e retorna 201 Created.

**Tech Stack:** .NET 10, Carter, Mediator, FluentValidation, EF Core 10, PostgreSQL, xUnit, Shouldly, Moq, Bogus, SQLite in-memory e Testcontainers.

**Spec:** `docs/superpowers/specs/2026-09-13-wnd-288-register-discard-design.md`

## Global Constraints

* [WND-288](https://linear.app/wnd-dev/issue/WND-288/us004b-abrir-descarte-depositante) permanece bloqueada pela [WND-168](https://linear.app/wnd-dev/issue/WND-168/us011-tabela-de-pontuacao-por-material). Não iniciar RED enquanto [WND-168](https://linear.app/wnd-dev/issue/WND-168/us011-tabela-de-pontuacao-por-material).A/B não estiver integrada.
* [WND-168](https://linear.app/wnd-dev/issue/WND-168/us011-tabela-de-pontuacao-por-material) deve fornecer MaterialScoreRule com Id, LegalPersonId, Material, Points, Unit, ValidFrom e ValidTo; enums internos/públicos ElectronicMaterial e MaterialScoreUnit; e DbSet<MaterialScoreRule>.
* Se a implementação real da [WND-168](https://linear.app/wnd-dev/issue/WND-168/us011-tabela-de-pontuacao-por-material) usar nomes diferentes, reconciliar este plano antes de editar código. Não duplicar tipos.
* Cada descarte exige ao menos um item; cada material aparece no máximo uma vez.
* Quantity é inteiro maior que zero. ApproximateWeightKg é decimal maior que zero em kg, persistido como decimal(10,3).
* Regra vigente: ValidFrom <= openedAt e ValidTo nulo ou ValidTo > openedAt.
* NaturalPerson ativa com Journey.Depositor pode abrir descarte, inclusive Role.Admin ou `Role.Support`.
* Usar AppDbContext diretamente. Não criar repository, service, mapper, clock abstraction ou nova hierarquia de erros.
* Command, Validator, Handler e endpoint ficam em RegisterDiscard.cs.
* Aplicar TDD Red → Green → Refactor.
* Preservar alterações não relacionadas; git add sempre com caminhos explícitos.
* Fora do escopo: Mobile, confirmação, rejeição, crédito, notificações e idempotência.
* Verificação final: dotnet test Ecocell.slnx. Integração exige Docker.

---

## File Map

### Criar

* src/Ecocell.Api/Enums/DiscardStatus.cs
* src/Ecocell.Api/Entities/Discard.cs
* src/Ecocell.Api/Entities/DiscardItem.cs
* src/Ecocell.Api/Database/TypeConfiguration/DiscardTypeConfiguration.cs
* src/Ecocell.Api/Database/TypeConfiguration/DiscardItemTypeConfiguration.cs
* src/Ecocell.Shared/Enums/DiscardStatus.cs
* src/Ecocell.Shared/Requests/Discards/RequestRegisterDiscardJson.cs
* src/Ecocell.Shared/Responses/ResponseRegisterDiscardJson.cs
* src/Ecocell.Api/Features/Discard/RegisterDiscard.cs
* tests/Ecocell.UnitTests/Entities/DiscardTests.cs
* tests/Ecocell.UnitTests/Features/Discard/RegisterDiscardTests.cs
* tests/Ecocell.IntegrationTests/Features/Discard/RegisterDiscardTests.cs
* migration AddDiscards gerada pelo EF Core

### Modificar

* src/Ecocell.Api/Database/AppDbContext.cs
* src/Ecocell.Api/Migrations/AppDbContextModelSnapshot.cs

---

### Task 0: Gate da dependência e baseline

**Files:**

* Read: artefatos MaterialScoreRule/ElectronicMaterial/MaterialScoreUnit da [WND-168](https://linear.app/wnd-dev/issue/WND-168/us011-tabela-de-pontuacao-por-material)
* Read: src/Ecocell.Api/Database/AppDbContext.cs

**Interfaces:**

- Consumes: [WND-168](https://linear.app/wnd-dev/issue/WND-168/us011-tabela-de-pontuacao-por-material).A/B integrada.
- Produces: autorização fail-closed para iniciar a [WND-288](https://linear.app/wnd-dev/issue/WND-288/us004b-abrir-descarte-depositante).
- [ ] **Step 1: Confirmar os artefatos da** [WND-168](https://linear.app/wnd-dev/issue/WND-168/us011-tabela-de-pontuacao-por-material)

```powershell
rg -n "class MaterialScoreRule|enum ElectronicMaterial|enum MaterialScoreUnit|DbSet<MaterialScoreRule>" src tests
```

Expected: entidade, enums interno/público e DbSet existentes. Se algum estiver ausente, parar e manter [WND-288](https://linear.app/wnd-dev/issue/WND-288/us004b-abrir-descarte-depositante) bloqueada.

- [ ] **Step 2: Confirmar versionamento**

Inspecionar handler e testes da [WND-168](https://linear.app/wnd-dev/issue/WND-168/us011-tabela-de-pontuacao-por-material).B. Deve ser impossível manter duas regras vigentes para o mesmo LegalPersonId + Material; a versão anterior recebe ValidTo antes da nova ser criada.

Expected: teste automatizado da [WND-168](https://linear.app/wnd-dev/issue/WND-168/us011-tabela-de-pontuacao-por-material) prova a troca de versão sem apagar histórico. Se ausente, corrigir na [WND-168](https://linear.app/wnd-dev/issue/WND-168/us011-tabela-de-pontuacao-por-material), não nesta task.

- [ ] **Step 3: Inventariar worktree**

```powershell
git status --short
git branch --show-current
```

Expected: branch conhecida e alterações preexistentes registradas. Não limpar o worktree.

- [ ] **Step 4: Executar baseline unitário**

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore
```

Expected: PASS. Falha preexistente interrompe execução.

---

### Task 1: Agregado relacional e migration

**Files:**

* Create: src/Ecocell.Api/Enums/DiscardStatus.cs
* Create: src/Ecocell.Api/Entities/Discard.cs
* Create: src/Ecocell.Api/Entities/DiscardItem.cs
* Create: src/Ecocell.Api/Database/TypeConfiguration/DiscardTypeConfiguration.cs
* Create: src/Ecocell.Api/Database/TypeConfiguration/DiscardItemTypeConfiguration.cs
* Create: tests/Ecocell.UnitTests/Entities/DiscardTests.cs
* Modify: src/Ecocell.Api/Database/AppDbContext.cs
* Generate: migration AddDiscards e model snapshot

**Interfaces:**

- Consumes: MaterialScoreRule e ElectronicMaterial da [WND-168](https://linear.app/wnd-dev/issue/WND-168/us011-tabela-de-pontuacao-por-material).
- Produces:
  * Discard(Guid depositorId, Guid collectorPointId, IEnumerable<DiscardItem> items)
  * DiscardItem(ElectronicMaterial material, int quantity, decimal approximateWeightKg, Guid materialScoreRuleId)
  * AppDbContext.Discards
  * AppDbContext.DiscardItems
- [ ] **Step 1: Escrever RED das invariantes**

Criar tests/Ecocell.UnitTests/Entities/DiscardTests.cs:

```csharp
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Shouldly;

namespace Ecocell.UnitTests.Entities;

public class DiscardTests
{
    private static ElectronicMaterial Material =>
        Enum.GetValues<ElectronicMaterial>()
            .First(value => Convert.ToInt32(value) > 0);

    [Fact]
    public void Constructor_ShouldCreatePendingDiscard_WhenItemsAreValid()
    {
        var item = new DiscardItem(Material, 2, 0.450m, Guid.NewGuid());

        var discard = new Discard(Guid.NewGuid(), Guid.NewGuid(), [item]);

        discard.Status.ShouldBe(DiscardStatus.Pending);
        discard.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenItemsAreEmpty()
    {
        Should.Throw<ArgumentException>(
            () => new Discard(Guid.NewGuid(), Guid.NewGuid(), []));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenMaterialIsDuplicated()
    {
        var first = new DiscardItem(Material, 1, 0.200m, Guid.NewGuid());
        var second = new DiscardItem(Material, 2, 0.400m, Guid.NewGuid());

        Should.Throw<ArgumentException>(
            () => new Discard(Guid.NewGuid(), Guid.NewGuid(), [first, second]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ShouldThrow_WhenQuantityIsNotPositive(int quantity)
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => new DiscardItem(Material, quantity, 0.100m, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenWeightIsNotPositive()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => new DiscardItem(Material, 1, 0m, Guid.NewGuid()));
    }
}
```

- [ ] **Step 2: Executar RED**

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~DiscardTests"
```

Expected: FAIL de compilação porque os tipos ainda não existem.

- [ ] **Step 3: Criar enum e entidades mínimas**

src/Ecocell.Api/Enums/DiscardStatus.cs:

```csharp
namespace Ecocell.Api.Enums;

public enum DiscardStatus
{
    Pending = 1,
    Confirmed = 2,
    Rejected = 3,
}
```

src/Ecocell.Api/Entities/DiscardItem.cs:

```csharp
using Ecocell.Api.Enums;

namespace Ecocell.Api.Entities;

public sealed class DiscardItem : BaseEntity
{
    private DiscardItem() { }

    public DiscardItem(
        ElectronicMaterial material,
        int quantity,
        decimal approximateWeightKg,
        Guid materialScoreRuleId)
    {
        if (Convert.ToInt32(material) <= 0)
            throw new ArgumentOutOfRangeException(nameof(material));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        if (approximateWeightKg <= 0)
            throw new ArgumentOutOfRangeException(nameof(approximateWeightKg));
        if (materialScoreRuleId == Guid.Empty)
            throw new ArgumentException("A regra de pontuação é obrigatória.", nameof(materialScoreRuleId));

        Material = material;
        Quantity = quantity;
        ApproximateWeightKg = approximateWeightKg;
        MaterialScoreRuleId = materialScoreRuleId;
    }

    public Guid DiscardId { get; private set; }
    public Discard Discard { get; private set; } = null!;
    public ElectronicMaterial Material { get; private set; }
    public int Quantity { get; private set; }
    public decimal ApproximateWeightKg { get; private set; }
    public Guid MaterialScoreRuleId { get; private set; }
    public MaterialScoreRule MaterialScoreRule { get; private set; } = null!;
}
```

src/Ecocell.Api/Entities/Discard.cs:

```csharp
using Ecocell.Api.Enums;

namespace Ecocell.Api.Entities;

public sealed class Discard : BaseEntity
{
    private readonly List<DiscardItem> _items = [];

    private Discard() { }

    public Discard(
        Guid depositorId,
        Guid collectorPointId,
        IEnumerable<DiscardItem> items)
    {
        if (depositorId == Guid.Empty)
            throw new ArgumentException("O depositante é obrigatório.", nameof(depositorId));
        if (collectorPointId == Guid.Empty)
            throw new ArgumentException("O Ponto de Coleta é obrigatório.", nameof(collectorPointId));

        var itemList = items?.ToList()
            ?? throw new ArgumentNullException(nameof(items));

        if (itemList.Count == 0)
            throw new ArgumentException("O descarte deve possuir ao menos um item.", nameof(items));
        if (itemList.Select(item => item.Material).Distinct().Count() != itemList.Count)
            throw new ArgumentException("Cada material pode aparecer somente uma vez.", nameof(items));

        DepositorId = depositorId;
        CollectorPointId = collectorPointId;
        Status = DiscardStatus.Pending;
        _items.AddRange(itemList);
    }

    public Guid DepositorId { get; private set; }
    public NaturalPerson Depositor { get; private set; } = null!;
    public Guid CollectorPointId { get; private set; }
    public LegalPerson CollectorPoint { get; private set; } = null!;
    public DiscardStatus Status { get; private set; }
    public IReadOnlyCollection<DiscardItem> Items => _items;
}
```

Não adicionar Confirm, Reject ou UpdateItem; pertencem à [WND-289](https://linear.app/wnd-dev/issue/WND-289/us004c-confirmarrejeitar-pc).

- [ ] **Step 4: Configurar EF Core**

DiscardTypeConfiguration deve:

```csharp
public override void Configure(EntityTypeBuilder<Discard> builder)
{
    base.Configure(builder);
    builder.ToTable("Discards");

    builder.Property(value => value.Status)
        .HasConversion<string>()
        .HasMaxLength(20)
        .IsRequired();

    builder.HasOne(value => value.Depositor)
        .WithMany()
        .HasForeignKey(value => value.DepositorId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne(value => value.CollectorPoint)
        .WithMany()
        .HasForeignKey(value => value.CollectorPointId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasMany(value => value.Items)
        .WithOne(value => value.Discard)
        .HasForeignKey(value => value.DiscardId)
        .OnDelete(DeleteBehavior.Cascade);

    builder.Navigation(value => value.Items)
        .HasField("_items")
        .UsePropertyAccessMode(PropertyAccessMode.Field);
}
```

DiscardItemTypeConfiguration deve:

```csharp
public override void Configure(EntityTypeBuilder<DiscardItem> builder)
{
    base.Configure(builder);
    builder.ToTable("DiscardItems");

    builder.Property(value => value.Material)
        .HasConversion<string>()
        .HasMaxLength(50)
        .IsRequired();

    builder.Property(value => value.Quantity).IsRequired();
    builder.Property(value => value.ApproximateWeightKg)
        .HasPrecision(10, 3)
        .IsRequired();

    builder.HasIndex(value => new { value.DiscardId, value.Material })
        .IsUnique();

    builder.HasOne(value => value.MaterialScoreRule)
        .WithMany()
        .HasForeignKey(value => value.MaterialScoreRuleId)
        .OnDelete(DeleteBehavior.Restrict);
}
```

Ambas as configurações herdam BaseEntityTypeConfiguration<T>. Adicionar ao AppDbContext:

```csharp
public DbSet<Discard> Discards { get; set; }
public DbSet<DiscardItem> DiscardItems { get; set; }
```

- [ ] **Step 5: Executar GREEN**

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~DiscardTests"
```

Expected: PASS.

- [ ] **Step 6: Gerar e revisar migration**

```powershell
dotnet ef migrations add AddDiscards --project src/Ecocell.Api --startup-project src/Ecocell.Api
```

Expected: tabelas Discards e DiscardItems; FKs restritas para depositante, PC e regra; cascade somente de Discard para itens; índice único DiscardId + Material; peso numeric(10,3).

- [ ] **Step 7: Verificar e commitar**

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore
git diff --check
git add -- src/Ecocell.Api/Enums/DiscardStatus.cs src/Ecocell.Api/Entities/Discard.cs src/Ecocell.Api/Entities/DiscardItem.cs src/Ecocell.Api/Database/TypeConfiguration/DiscardTypeConfiguration.cs src/Ecocell.Api/Database/TypeConfiguration/DiscardItemTypeConfiguration.cs src/Ecocell.Api/Database/AppDbContext.cs 'src/Ecocell.Api/Migrations/*_AddDiscards.cs' 'src/Ecocell.Api/Migrations/*_AddDiscards.Designer.cs' src/Ecocell.Api/Migrations/AppDbContextModelSnapshot.cs tests/Ecocell.UnitTests/Entities/DiscardTests.cs
git commit -m "feat(discard): adiciona agregado de descarte"
```

Expected: testes PASS, git diff --check sem saída e commit seletivo.

---

### Task 2: Contratos e Validator

**Files:**

* Create: src/Ecocell.Shared/Enums/DiscardStatus.cs
* Create: src/Ecocell.Shared/Requests/Discards/RequestRegisterDiscardJson.cs
* Create: src/Ecocell.Shared/Responses/ResponseRegisterDiscardJson.cs
* Create: src/Ecocell.Api/Features/Discard/RegisterDiscard.cs
* Create: tests/Ecocell.UnitTests/Features/Discard/RegisterDiscardTests.cs

**Interfaces:**

- Consumes: ElectronicMaterial público/interno.
- Produces: request, response, Command, ItemCommand, Validator e parser do QR.
- [ ] **Step 1: Escrever RED do Validator**

Criar RegisterDiscardTests com um comando válido e estes casos:

```csharp
private static RegisterDiscard.Command ValidCommand(Guid? pointId = null) =>
    new()
    {
        QrCode = $"ecocell://pc/{pointId ?? Guid.NewGuid():D}",
        Items =
        [
            new RegisterDiscard.ItemCommand
            {
                Material = Material,
                Quantity = 1,
                ApproximateWeightKg = 0.250m,
            },
        ],
    };

[Fact]
public async Task Validate_ShouldSucceed_WhenCommandIsValid()
{
    var result = await new RegisterDiscard.Validator()
        .ValidateAsync(ValidCommand(), CancellationToken.None);

    result.IsValid.ShouldBeTrue();
}

[Theory]
[InlineData("")]
[InlineData("not-a-qr")]
[InlineData("https://pc/018f3f2a-7b4c-7c91-8d31-5f7a2b6c9e10")]
[InlineData("ecocell://collector/018f3f2a-7b4c-7c91-8d31-5f7a2b6c9e10")]
[InlineData("ecocell://pc/not-a-guid")]
public async Task Validate_ShouldFail_WhenQrCodeIsInvalid(string qrCode)
{
    var result = await new RegisterDiscard.Validator()
        .ValidateAsync(ValidCommand() with { QrCode = qrCode }, CancellationToken.None);

    result.IsValid.ShouldBeFalse();
}
```

Adicionar os casos restantes:

```csharp
[Fact]
public async Task Validate_ShouldFail_WhenItemsAreEmpty()
{
    var result = await new RegisterDiscard.Validator()
        .ValidateAsync(ValidCommand() with { Items = [] }, CancellationToken.None);

    result.IsValid.ShouldBeFalse();
}

[Fact]
public async Task Validate_ShouldFail_WhenMaterialIsDuplicated()
{
    var item = ValidCommand().Items[0];
    var result = await new RegisterDiscard.Validator()
        .ValidateAsync(ValidCommand() with { Items = [item, item] }, CancellationToken.None);

    result.IsValid.ShouldBeFalse();
}

[Theory]
[InlineData(0)]
[InlineData(-1)]
public async Task Validate_ShouldFail_WhenQuantityIsNotPositive(int quantity)
{
    var command = ValidCommand();
    command.Items[0].Quantity = quantity;

    var result = await new RegisterDiscard.Validator()
        .ValidateAsync(command, CancellationToken.None);

    result.IsValid.ShouldBeFalse();
}

[Theory]
[InlineData("0")]
[InlineData("-0.001")]
public async Task Validate_ShouldFail_WhenWeightIsNotPositive(string weight)
{
    var command = ValidCommand();
    command.Items[0].ApproximateWeightKg = decimal.Parse(
        weight,
        System.Globalization.CultureInfo.InvariantCulture);

    var result = await new RegisterDiscard.Validator()
        .ValidateAsync(command, CancellationToken.None);

    result.IsValid.ShouldBeFalse();
}

[Fact]
public async Task Validate_ShouldFail_WhenMaterialIsUndefined()
{
    var command = ValidCommand();
    var invalidItem = command.Items[0] with { Material = (ElectronicMaterial)0 };
    var invalidCommand = command with { Items = [invalidItem] };

    var result = await new RegisterDiscard.Validator()
        .ValidateAsync(invalidCommand, CancellationToken.None);

    result.IsValid.ShouldBeFalse();
}
```

- [ ] **Step 2: Executar RED**

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~RegisterDiscardTests"
```

Expected: FAIL de compilação porque o slice ainda não existe.

- [ ] **Step 3: Criar contratos públicos**

```csharp
// Ecocell.Shared.Enums
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiscardStatus
{
    Pending = 1,
    Confirmed = 2,
    Rejected = 3,
}

// Ecocell.Shared.Requests.Discards
public sealed record RequestRegisterDiscardJson
{
    public string QrCode { get; init; } = string.Empty;
    public IReadOnlyList<RequestRegisterDiscardItemJson> Items { get; init; } = [];
}

public sealed record RequestRegisterDiscardItemJson
{
    public ElectronicMaterial Material { get; init; }
    public int Quantity { get; init; }
    public decimal ApproximateWeightKg { get; init; }
}

// Ecocell.Shared.Responses
public sealed record ResponseRegisterDiscardJson
{
    public Guid Id { get; init; }
    public DiscardStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
}
```

- [ ] **Step 4: Criar Command e Validator no slice**

```csharp
public sealed record ItemCommand
{
    public ElectronicMaterial Material { get; init; }
    public int Quantity { get; set; }
    public decimal ApproximateWeightKg { get; set; }
}

public sealed record Command : IRequest<ResultT<ResponseRegisterDiscardJson>>
{
    public string QrCode { get; init; } = string.Empty;
    public IReadOnlyList<ItemCommand> Items { get; init; } = [];
}

public sealed class Validator : AbstractValidator<Command>
{
    public Validator()
    {
        RuleFor(value => value.QrCode)
            .NotEmpty()
            .Must(value => TryGetCollectorPointId(value, out _))
            .WithMessage("O QR Code do Ponto de Coleta é inválido.");

        RuleFor(value => value.Items)
            .NotEmpty()
            .WithMessage("Informe ao menos um item.");

        RuleFor(value => value.Items)
            .Must(items => items.Select(item => item.Material).Distinct().Count() == items.Count)
            .WithMessage("Cada material pode aparecer somente uma vez.");

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
                .WithMessage("O peso aproximado deve ser maior que zero.");
        });
    }
}

internal static bool TryGetCollectorPointId(string qrCode, out Guid id)
{
    id = Guid.Empty;

    if (!Uri.TryCreate(qrCode, UriKind.Absolute, out var uri)
        || !uri.Scheme.Equals("ecocell", StringComparison.OrdinalIgnoreCase)
        || !uri.Host.Equals("pc", StringComparison.OrdinalIgnoreCase)
        || uri.Port != -1
        || uri.UserInfo.Length != 0
        || uri.Query.Length != 0
        || uri.Fragment.Length != 0)
    {
        return false;
    }

    var path = uri.AbsolutePath;
    return path.Length == 37
        && path[0] == '/'
        && Guid.TryParseExact(path[1..], "D", out id);
}
```

- [ ] **Step 5: Executar GREEN e commitar**

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~RegisterDiscardTests"
git diff --check
git add -- src/Ecocell.Shared/Enums/DiscardStatus.cs src/Ecocell.Shared/Requests/Discards/RequestRegisterDiscardJson.cs src/Ecocell.Shared/Responses/ResponseRegisterDiscardJson.cs src/Ecocell.Api/Features/Discard/RegisterDiscard.cs tests/Ecocell.UnitTests/Features/Discard/RegisterDiscardTests.cs
git commit -m "feat(discard): define contrato de abertura"
```

Expected: PASS e commit seletivo.

---

### Task 3: Handler e snapshot da regra

**Files:**

* Modify: src/Ecocell.Api/Features/Discard/RegisterDiscard.cs
* Modify: tests/Ecocell.UnitTests/Features/Discard/RegisterDiscardTests.cs

**Interfaces:**

- Consumes: ICurrentUserService, LegalPeople, MaterialScoreRules e agregado Discard.
- Produces: Handler.Handle(Command, CancellationToken) retornando ResultT<ResponseRegisterDiscardJson>.
- [ ] **Step 1: Montar fixture unitário**

Adicionar os campos e helpers ao teste:

```csharp
private readonly Mock<ICurrentUserService> _currentUserMock = new();
private readonly RegisterDiscard.Validator _validator = new();
private readonly Guid _depositorId;

public RegisterDiscardTests()
{
    _depositorId = AddDepositor();
    SetCurrentUser(
        _depositorId,
        Role.User,
        PersonStatus.Active,
        PersonType.NaturalPerson,
        Journey.Depositor);
}

private RegisterDiscard.Handler CreateHandler() =>
    new(
        DbContext,
        CreateLoggerMock<RegisterDiscard.Handler>().Object,
        _validator,
        _currentUserMock.Object);

private Guid AddDepositor(
    Role role = Role.User,
    Journey journey = Journey.Depositor)
{
    var faker = new Bogus.Faker("pt_BR");
    var person = new NaturalPerson(
        faker.Name.FullName(),
        faker.Person.Cpf(includeFormatSymbols: false),
        DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
        role,
        faker.Internet.Email(),
        journey);

    DbContext.NaturalPeople.Add(person);
    DbContext.SaveChanges();
    return person.Id;
}

private void SetCurrentUser(
    Guid id,
    Role role,
    PersonStatus status,
    PersonType type,
    Journey journey)
{
    _currentUserMock
        .Setup(value => value.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(new CurrentUserDto
        {
            Id = id,
            Role = role,
            PersonStatus = status,
            PersonType = type,
            Journey = journey,
        });
}

private LegalPerson AddCollectorPoint(
    PersonStatus status = PersonStatus.Active,
    Journey journey = Journey.CollectPoint)
{
    var faker = new Bogus.Faker("pt_BR");
    var point = new LegalPerson(
        faker.Company.CompanyName(),
        faker.Company.CompanyName(),
        faker.Company.Cnpj(includeFormatSymbols: false),
        faker.Internet.Email(),
        journey,
        responsiblePersonId: _depositorId);

    DbContext.LegalPeople.Add(point);

    if (status == PersonStatus.Active)
        point.Approve(Role.Admin);

    DbContext.SaveChanges();
    return point;
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
        validFrom,
        validTo);

    DbContext.MaterialScoreRules.Add(rule);
    DbContext.SaveChanges();
    return rule;
}
```

Adicionar usings de Bogus.Extensions.Brazil, Ecocell.Api.Entities, Ecocell.Api.Services.CurrentUser, Microsoft.EntityFrameworkCore e Moq.

- [ ] **Step 2: Escrever RED do caminho feliz**

```csharp
[Fact]
public async Task Handle_ShouldPersistPendingDiscard_WhenRequestIsValid()
{
    var point = AddCollectorPoint();
    var rule = AddRule(point.Id, Material, DateTime.UtcNow.AddDays(-1));

    var result = await CreateHandler()
        .Handle(ValidCommand(point.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Status.ShouldBe(Ecocell.Shared.Enums.DiscardStatus.Pending);

    var discard = await DbContext.Discards
        .Include(value => value.Items)
        .SingleAsync();

    discard.DepositorId.ShouldBe(_depositorId);
    discard.CollectorPointId.ShouldBe(point.Id);
    discard.Status.ShouldBe(DiscardStatus.Pending);
    discard.Items.Single().MaterialScoreRuleId.ShouldBe(rule.Id);
}
```

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Handle_ShouldPersistPendingDiscard"
```

Expected: FAIL porque Handler ainda não existe.

- [ ] **Step 3: Implementar Handler**

Sequência exata:

```csharp
var validation = await _validator.ValidateAsync(request, ct);
if (!validation.IsValid)
{
    return ResultT<ResponseRegisterDiscardJson>.Failure(
        Error.ErrorOnValidation(
            validation.Errors.Select(value => value.ErrorMessage).ToList()));
}

var currentUser = await _currentUserService.GetCurrentUserAsync(ct);
if (currentUser is null
    || currentUser.PersonType != PersonType.NaturalPerson
    || currentUser.PersonStatus != PersonStatus.Active
    || currentUser.Journey != Journey.Depositor)
{
    return ResultT<ResponseRegisterDiscardJson>.Failure(Error.Forbidden());
}

TryGetCollectorPointId(request.QrCode, out var collectorPointId);

var collectorPoint = await _dbContext.LegalPeople
    .AsNoTracking()
    .FirstOrDefaultAsync(
        value => value.Id == collectorPointId
            && value.Journey == Journey.CollectPoint,
        ct);

if (collectorPoint is null)
{
    return ResultT<ResponseRegisterDiscardJson>.Failure(
        Error.NotFound("Ponto de Coleta não encontrado."));
}

if (collectorPoint.PersonStatus != PersonStatus.Active)
{
    return ResultT<ResponseRegisterDiscardJson>.Failure(
        Error.Conflict("Ponto de Coleta não está ativo."));
}

var openedAt = DateTime.UtcNow;
var materials = request.Items.Select(value => value.Material).ToArray();

var rules = await _dbContext.MaterialScoreRules
    .AsNoTracking()
    .Where(value => value.LegalPersonId == collectorPoint.Id
        && materials.Contains(value.Material)
        && value.ValidFrom <= openedAt
        && (value.ValidTo == null || value.ValidTo > openedAt))
    .ToListAsync(ct);

var rulesByMaterial = rules.ToDictionary(value => value.Material);
var unsupported = materials
    .Where(value => !rulesByMaterial.ContainsKey(value))
    .Distinct()
    .Order()
    .ToArray();

if (unsupported.Length > 0)
{
    return ResultT<ResponseRegisterDiscardJson>.Failure(
        Error.Conflict(
            $"Materiais não aceitos pelo Ponto de Coleta: {string.Join(", ", unsupported)}."));
}

var items = request.Items
    .Select(value => new DiscardItem(
        value.Material,
        value.Quantity,
        value.ApproximateWeightKg,
        rulesByMaterial[value.Material].Id))
    .ToArray();

var discard = new Ecocell.Api.Entities.Discard(
    currentUser.Id,
    collectorPoint.Id,
    items);

_dbContext.Discards.Add(discard);
await _dbContext.SaveChangesAsync(ct);

return ResultT<ResponseRegisterDiscardJson>.Success(
    new ResponseRegisterDiscardJson
    {
        Id = discard.Id,
        Status = (Ecocell.Shared.Enums.DiscardStatus)(int)discard.Status,
        CreatedAt = discard.CreatedAt,
    });
```

Injetar AppDbContext, ILogger<Handler>, IValidator<Command> e ICurrentUserService. Registrar somente IDs no log de sucesso; não registrar request completo.

- [ ] **Step 4: Executar GREEN**

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Handle_ShouldPersistPendingDiscard"
```

Expected: PASS.

- [ ] **Step 5: Cobrir autorização**

```csharp
[Theory]
[InlineData(PersonStatus.Suspended, PersonType.NaturalPerson, Journey.Depositor)]
[InlineData(PersonStatus.Active, PersonType.LegalPerson, Journey.Depositor)]
[InlineData(PersonStatus.Active, PersonType.NaturalPerson, Journey.None)]
public async Task Handle_ShouldReturnForbidden_WhenDepositorIsIneligible(
    PersonStatus status,
    PersonType type,
    Journey journey)
{
    var point = AddCollectorPoint();
    AddRule(point.Id, Material, DateTime.UtcNow.AddDays(-1));
    SetCurrentUser(_depositorId, Role.User, status, type, journey);

    var result = await CreateHandler()
        .Handle(ValidCommand(point.Id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    (await DbContext.Discards.CountAsync()).ShouldBe(0);
}

[Theory]
[InlineData(Role.Admin)]
[InlineData(Role.Support)]
public async Task Handle_ShouldPersistDiscard_WhenPrivilegedUserIsDepositor(Role role)
{
    var point = AddCollectorPoint();
    AddRule(point.Id, Material, DateTime.UtcNow.AddDays(-1));
    SetCurrentUser(
        _depositorId,
        role,
        PersonStatus.Active,
        PersonType.NaturalPerson,
        Journey.Depositor);

    var result = await CreateHandler()
        .Handle(ValidCommand(point.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    (await DbContext.Discards.CountAsync()).ShouldBe(1);
}

[Fact]
public async Task Handle_ShouldReturnForbidden_WhenCurrentUserIsNull()
{
    var point = AddCollectorPoint();
    AddRule(point.Id, Material, DateTime.UtcNow.AddDays(-1));
    _currentUserMock
        .Setup(value => value.GetCurrentUserAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync((CurrentUserDto?)null);

    var result = await CreateHandler()
        .Handle(ValidCommand(point.Id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    (await DbContext.Discards.CountAsync()).ShouldBe(0);
}
```

Run:

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~RegisterDiscardTests"
```

Expected: PASS.

- [ ] **Step 6: Cobrir PC e regras**

```csharp
[Fact]
public async Task Handle_ShouldReturnNotFound_WhenCollectorPointDoesNotExist()
{
    var result = await CreateHandler()
        .Handle(ValidCommand(Guid.NewGuid()), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe(ErrorCodes.NotFound);
}

[Fact]
public async Task Handle_ShouldReturnNotFound_WhenLegalPersonIsNotCollectorPoint()
{
    var collector = AddCollectorPoint(journey: Journey.Collector);

    var result = await CreateHandler()
        .Handle(ValidCommand(collector.Id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe(ErrorCodes.NotFound);
}

[Fact]
public async Task Handle_ShouldReturnConflict_WhenCollectorPointIsInactive()
{
    var point = AddCollectorPoint(PersonStatus.PendingApproval);

    var result = await CreateHandler()
        .Handle(ValidCommand(point.Id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe(ErrorCodes.Conflict);
}

[Fact]
public async Task Handle_ShouldReturnConflict_WhenMaterialRuleIsExpired()
{
    var point = AddCollectorPoint();
    AddRule(
        point.Id,
        Material,
        DateTime.UtcNow.AddDays(-2),
        DateTime.UtcNow.AddDays(-1));

    var result = await CreateHandler()
        .Handle(ValidCommand(point.Id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    (await DbContext.Discards.CountAsync()).ShouldBe(0);
}

[Fact]
public async Task Handle_ShouldReturnConflict_WhenMaterialRuleIsFuture()
{
    var point = AddCollectorPoint();
    AddRule(point.Id, Material, DateTime.UtcNow.AddDays(1));

    var result = await CreateHandler()
        .Handle(ValidCommand(point.Id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    (await DbContext.Discards.CountAsync()).ShouldBe(0);
}

[Fact]
public async Task Handle_ShouldNotPersistPartially_WhenOneMaterialIsUnsupported()
{
    var point = AddCollectorPoint();
    var materials = Enum.GetValues<ElectronicMaterial>()
        .Where(value => Convert.ToInt32(value) > 0)
        .Take(2)
        .ToArray();
    materials.Length.ShouldBe(2);
    AddRule(point.Id, materials[0], DateTime.UtcNow.AddDays(-1));

    var command = ValidCommand(point.Id) with
    {
        Items =
        [
            new RegisterDiscard.ItemCommand
            {
                Material = materials[0],
                Quantity = 1,
                ApproximateWeightKg = 0.200m,
            },
            new RegisterDiscard.ItemCommand
            {
                Material = materials[1],
                Quantity = 1,
                ApproximateWeightKg = 0.300m,
            },
        ],
    };

    var result = await CreateHandler().Handle(command, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    (await DbContext.Discards.CountAsync()).ShouldBe(0);
}
```

Run:

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~RegisterDiscardTests"
```

Expected: PASS.

- [ ] **Step 7: Provar snapshot**

```csharp
[Fact]
public async Task Handle_ShouldKeepOriginalRuleId_WhenRuleIsVersionedLater()
{
    var point = AddCollectorPoint();
    var original = AddRule(point.Id, Material, DateTime.UtcNow.AddDays(-1));

    var result = await CreateHandler()
        .Handle(ValidCommand(point.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    DbContext.Entry(original).Property(value => value.ValidTo).CurrentValue = DateTime.UtcNow;
    AddRule(point.Id, Material, DateTime.UtcNow);
    await DbContext.SaveChangesAsync();

    var item = await DbContext.DiscardItems.SingleAsync();
    item.MaterialScoreRuleId.ShouldBe(original.Id);
}
```

- [ ] **Step 8: Rodar slice, suíte unitária e commitar**

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~RegisterDiscardTests"
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore
git diff --check
git add -- src/Ecocell.Api/Features/Discard/RegisterDiscard.cs tests/Ecocell.UnitTests/Features/Discard/RegisterDiscardTests.cs
git commit -m "feat(discard): implementa abertura de descarte"
```

Expected: PASS, diff limpo e commit seletivo.

---

### Task 4: Endpoint e integração

**Files:**

* Modify: src/Ecocell.Api/Features/Discard/RegisterDiscard.cs
* Create: tests/Ecocell.IntegrationTests/Features/Discard/RegisterDiscardTests.cs

**Interfaces:**

- Consumes: RequestRegisterDiscardJson e RegisterDiscard.Command.
- Produces: POST /api/v1/discards.
- [ ] **Step 1: Escrever RED do endpoint**

Criar tests/Ecocell.IntegrationTests/Features/Discard/RegisterDiscardTests.cs:

```csharp
using System.Net;
using System.Net.Http.Json;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Shared.Requests.Discards;
using Ecocell.Shared.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ApiEnums = Ecocell.Api.Enums;
using SharedEnums = Ecocell.Shared.Enums;

namespace Ecocell.IntegrationTests.Features.Discard;

[Collection(nameof(IntegrationTestCollection))]
public class RegisterDiscardTests : IntegrationTestBase
{
    private static SharedEnums.ElectronicMaterial Material =>
        Enum.GetValues<SharedEnums.ElectronicMaterial>()
            .First(value => Convert.ToInt32(value) > 0);

    public RegisterDiscardTests(IntegrationTestFixture fixture) : base(fixture) { }

    private static RequestRegisterDiscardJson ValidRequest(Guid pointId) =>
        new()
        {
            QrCode = $"ecocell://pc/{pointId:D}",
            Items =
            [
                new RequestRegisterDiscardItemJson
                {
                    Material = Material,
                    Quantity = 2,
                    ApproximateWeightKg = 0.450m,
                },
            ],
        };

    private async Task AddRuleAsync(Guid pointId)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.MaterialScoreRules.Add(
            new MaterialScoreRule(
                pointId,
                (ApiEnums.ElectronicMaterial)(int)Material,
                10m,
                ApiEnums.MaterialScoreUnit.PerUnit,
                DateTime.UtcNow.AddDays(-1)));
        await db.SaveChangesAsync();
    }

    private async Task<int> CountDiscardsAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Discards.CountAsync();
    }

    [Fact]
    public async Task Post_ShouldReturn401_WhenTokenIsMissing()
    {
        var response = await Client.PostAsJsonAsync(
            "api/v1/discards",
            ValidRequest(Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_ShouldReturn201AndPersistDiscard_WhenRequestIsValid()
    {
        var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
        var depositorId = await GetPersonIdByEmailAsync(email);
        var point = await CreateActiveCollectorPointAsync(depositorId);
        await AddRuleAsync(point.Id);
        using var authClient = CreateAuthenticatedClient(jwt);

        var response = await authClient.PostAsJsonAsync(
            "api/v1/discards",
            ValidRequest(point.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ResponseRegisterDiscardJson>();
        body.ShouldNotBeNull();
        body!.Status.ShouldBe(SharedEnums.DiscardStatus.Pending);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var discard = await db.Discards.Include(value => value.Items).SingleAsync();

        discard.Id.ShouldBe(body.Id);
        discard.DepositorId.ShouldBe(depositorId);
        discard.CollectorPointId.ShouldBe(point.Id);
        discard.Items.ShouldHaveSingleItem();
    }
}
```

Run:

```powershell
dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~Features.Discard.RegisterDiscardTests"
```

Expected: RED com 404 na rota autenticada. Se Docker não iniciar, corrigir o ambiente e repetir; não tratar falha de inicialização do Testcontainers como RED funcional.

- [ ] **Step 2: Implementar endpoint**

```csharp
public sealed class RegisterDiscardEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("api/v1/discards", async (
            RequestRegisterDiscardJson request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new RegisterDiscard.Command
            {
                QrCode = request.QrCode,
                Items = (request.Items ?? [])
                    .Select(item => new RegisterDiscard.ItemCommand
                    {
                        Material = (ElectronicMaterial)(int)item.Material,
                        Quantity = item.Quantity,
                        ApproximateWeightKg = item.ApproximateWeightKg,
                    })
                    .ToArray(),
            };

            var result = await sender.Send(command, ct);
            return result.ToProcessResult(StatusCodes.Status201Created);
        })
        .WithTags("Discard")
        .WithName("RegisterDiscard")
        .WithSummary("Abre um descarte pendente a partir do QR Code de um Ponto de Coleta.")
        .RequireAuthorization(AuthorizationPolicies.Authenticated)
        .Produces<ResponseRegisterDiscardJson>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }
}
```

- [ ] **Step 3: Executar GREEN inicial**

```powershell
dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~Features.Discard.RegisterDiscardTests"
```

Expected: 401 e 201 PASS.

- [ ] **Step 4: Adicionar matriz HTTP**

```csharp
[Fact]
public async Task Post_ShouldReturn400_WhenQrCodeIsInvalid()
{
    var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
    using var authClient = CreateAuthenticatedClient(jwt);
    var request = ValidRequest(Guid.NewGuid()) with { QrCode = "invalid" };

    var response = await authClient.PostAsJsonAsync("api/v1/discards", request);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await CountDiscardsAsync()).ShouldBe(0);
}

[Fact]
public async Task Post_ShouldReturn403_WhenPersonIsNotDepositor()
{
    var (_, jwt) = await CreateAdminAndLoginAsync();
    using var authClient = CreateAuthenticatedClient(jwt);

    var response = await authClient.PostAsJsonAsync(
        "api/v1/discards",
        ValidRequest(Guid.NewGuid()));

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    (await CountDiscardsAsync()).ShouldBe(0);
}

[Fact]
public async Task Post_ShouldReturn404_WhenCollectorPointDoesNotExist()
{
    var (_, jwt) = await CreateAndLoginNaturalPersonAsync();
    using var authClient = CreateAuthenticatedClient(jwt);

    var response = await authClient.PostAsJsonAsync(
        "api/v1/discards",
        ValidRequest(Guid.NewGuid()));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    (await CountDiscardsAsync()).ShouldBe(0);
}

[Fact]
public async Task Post_ShouldReturn409_WhenCollectorPointIsInactive()
{
    var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
    var depositorId = await GetPersonIdByEmailAsync(email);
    var point = await CreatePendingLegalPersonAsync(depositorId);
    using var authClient = CreateAuthenticatedClient(jwt);

    var response = await authClient.PostAsJsonAsync(
        "api/v1/discards",
        ValidRequest(point.Id));

    response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    (await CountDiscardsAsync()).ShouldBe(0);
}

[Fact]
public async Task Post_ShouldReturn409_WhenMaterialIsNotAccepted()
{
    var (email, jwt) = await CreateAndLoginNaturalPersonAsync();
    var depositorId = await GetPersonIdByEmailAsync(email);
    var point = await CreateActiveCollectorPointAsync(depositorId);
    using var authClient = CreateAuthenticatedClient(jwt);

    var response = await authClient.PostAsJsonAsync(
        "api/v1/discards",
        ValidRequest(point.Id));

    response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    (await CountDiscardsAsync()).ShouldBe(0);
}
```

Run:

```powershell
dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~Features.Discard.RegisterDiscardTests"
```

Expected: sete testes PASS.

- [ ] **Step 5: Rodar integração e suíte completa**

```powershell
dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~Features.Discard.RegisterDiscardTests"
dotnet test Ecocell.slnx
```

Expected: todos PASS, incluindo resumo final da suíte completa.

- [ ] **Step 6: Verificar escopo e commitar**

```powershell
git diff --check
git status --short
git diff -- src/Ecocell.Api src/Ecocell.Shared tests/Ecocell.UnitTests tests/Ecocell.IntegrationTests
git add -- src/Ecocell.Api/Features/Discard/RegisterDiscard.cs tests/Ecocell.IntegrationTests/Features/Discard/RegisterDiscardTests.cs
git commit -m "test(discard): cobre endpoint de abertura"
```

Expected: nenhuma saída em git diff --check; somente arquivos [WND-288](https://linear.app/wnd-dev/issue/WND-288/us004b-abrir-descarte-depositante) no staging.

---

## Final Acceptance Checklist

- [ ] [WND-168](https://linear.app/wnd-dev/issue/WND-168/us011-tabela-de-pontuacao-por-material).A/B integrada; nenhum tipo duplicado.
- [ ] POST /api/v1/discards exige autenticação.
- [ ] QR aceito somente como ecocell://pc/{guid}.
- [ ] NaturalPerson ativa + Journey.Depositor autorizada independentemente de Role.
- [ ] PC existe, tem Journey.CollectPoint e está Active.
- [ ] Itens têm material único, quantidade positiva e peso positivo.
- [ ] Cada item aponta para regra vigente em openedAt.
- [ ] Versionamento posterior não altera MaterialScoreRuleId salvo.
- [ ] Agregado nasce Pending e persiste atomicamente.
- [ ] Resposta 201 contém id, Pending e createdAt.
- [ ] Erros 400, 401, 403, 404 e 409 cobertos.
- [ ] Nenhum item fora do escopo foi implementado.
- [ ] dotnet test Ecocell.slnx finalizou com PASS.
- [ ] git diff --check não produziu saída.
- [ ] Commits contêm somente arquivos da [WND-288](https://linear.app/wnd-dev/issue/WND-288/us004b-abrir-descarte-depositante).

## Plan Self-Review

* Spec coverage: Tasks 1–4 cobrem domínio, snapshot, contrato, autorização, fluxo, erros, testes e exclusões.
* Placeholder scan: somente os nomes timestampados gerados automaticamente pelo EF Core variam; todos os comportamentos e resultados esperados estão definidos.
* Type consistency: Discard, DiscardItem, DiscardStatus, RequestRegisterDiscardJson, ResponseRegisterDiscardJson e RegisterDiscard.Command mantêm nomes e tipos entre tasks.
* Scope: um agregado, um slice e um endpoint; [WND-168](https://linear.app/wnd-dev/issue/WND-168/us011-tabela-de-pontuacao-por-material) permanece dependência externa e fail-closed.
