# RegisterLegalPerson (US001-PJ) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar o slice `RegisterLegalPerson` — cadastro de Pessoa Jurídica (Ponto de Coleta ou Coletor) por Gestor PF autenticado, com entidade `Address` independente, status inicial `PendingApproval` e notificação por e-mail para PJ e PF gestora.

**Architecture:** VSA (Vertical Slice) — um arquivo por feature em `Features/Person/RegisterLegalPerson.cs` contendo `Command`, `Validator`, `Handler` e `ICarterModule`. `Address` é entidade independente; FK `AddressId` vive em `LegalPerson` (permite reuso futuro em `NaturalPerson`). Notificação desacoplada via evento `LegalPersonRegistered` + `NotificationHandler` (preparado para migração futura a Outbox Pattern).

**Tech Stack:** .NET 10, ASP.NET Core, Carter, Mediator, FluentValidation, EF Core (PostgreSQL prod / SQLite in-memory nos testes), xUnit + Shouldly + Moq + Bogus.

---

## Mapa de Arquivos

| Ação | Arquivo |
|---|---|
| Modificar | `src/Ecocell.Api/Extensions/FluentValidationExtensions.cs` |
| Modificar | `src/Ecocell.Api/Features/Person/RegisterNaturalPerson.cs` |
| Criar | `src/Ecocell.Api/Entities/Address.cs` |
| Criar | `src/Ecocell.Api/Database/TypeConfiguration/AddressTypeConfiguration.cs` |
| Modificar | `src/Ecocell.Api/Database/AppDbContext.cs` |
| Modificar | `src/Ecocell.Api/Entities/LegalPerson.cs` |
| Modificar | `src/Ecocell.Api/Database/TypeConfiguration/LegalPersonTypeConfiguration.cs` |
| Migration | `src/Ecocell.Api/Migrations/` (`AddAddressTable`) |
| Criar | `src/Ecocell.Api/Events/LegalPersonRegistered.cs` |
| Modificar | `src/Ecocell.Api/Services/Email/IEmailSender.cs` |
| Modificar | `src/Ecocell.Api/Services/Email/LoggingEmailSender.cs` |
| Criar | `src/Ecocell.Shared/Requests/RequestRegisterLegalPerson.cs` |
| Criar | `src/Ecocell.Api/Features/Person/RegisterLegalPerson.cs` |
| Criar | `src/Ecocell.Api/Features/Person/SendRegistrationEmailOnLegalPersonRegistered.cs` |
| Criar | `tests/Ecocell.UnitTests/Features/Person/RegisterLegalPersonTests.cs` |
| Modificar | `.claude/TASKS.md` |

---

## Task 1: Extensão `IsValidEmail` + refatorar `RegisterNaturalPerson.Validator`

**Files:**
- Modify: `src/Ecocell.Api/Extensions/FluentValidationExtensions.cs`
- Modify: `src/Ecocell.Api/Features/Person/RegisterNaturalPerson.cs`

Esta extensão elimina duplicação: `RegisterNaturalPerson` e `RegisterLegalPerson` usarão o mesmo `IsValidEmail()`.

- [ ] **Step 1: Adicionar `IsValidEmail` em `FluentValidationExtensions.cs`**

Abrir `src/Ecocell.Api/Extensions/FluentValidationExtensions.cs` e adicionar após `IsValidCnpj`:

```csharp
public static IRuleBuilderOptions<T, string> IsValidEmail<T>(this IRuleBuilder<T, string> ruleBuilder)
{
    return ruleBuilder
        .EmailAddress().WithMessage("E-mail inválido.")
        .MaximumLength(255).WithMessage("E-mail deve conter no máximo 255 caracteres.");
}
```

- [ ] **Step 2: Refatorar `RegisterNaturalPerson.Validator` para usar `IsValidEmail()`**

Em `src/Ecocell.Api/Features/Person/RegisterNaturalPerson.cs`, substituir as duas linhas de validação de e-mail:

```csharp
// remover:
RuleFor(x => x.Email)
    .EmailAddress().WithMessage("E-mail inválido.")
    .MaximumLength(255).WithMessage("E-mail deve conter no máximo 255 caracteres.");

// adicionar:
RuleFor(x => x.Email).IsValidEmail();
```

- [ ] **Step 3: Rodar testes existentes para garantir que a refatoração não quebrou nada**

```bash
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj
```

Esperado: todos os testes passam (especialmente `Handle_ShouldNotPersistInDatabase_WhenEmailIsInvalid` e `Handle_ShouldNotPersistInDatabase_WhenEmailIsTooLong`).

- [ ] **Step 4: Commit**

```bash
git add src/Ecocell.Api/Extensions/FluentValidationExtensions.cs src/Ecocell.Api/Features/Person/RegisterNaturalPerson.cs
git commit -m "refactor: extract IsValidEmail extension for reuse across validators"
```

---

## Task 2: Entidade `Address` + configuração EF Core + `DbSet`

**Files:**
- Create: `src/Ecocell.Api/Entities/Address.cs`
- Create: `src/Ecocell.Api/Database/TypeConfiguration/AddressTypeConfiguration.cs`
- Modify: `src/Ecocell.Api/Database/AppDbContext.cs`

- [ ] **Step 1: Criar `Address.cs`**

```csharp
namespace Ecocell.Api.Entities;

/// <summary>
/// Entidade que representa um endereço físico. Independente — referenciada por FK em
/// <see cref="LegalPerson"/> e, futuramente, em <see cref="NaturalPerson"/>.
/// Coordenadas (<see cref="Latitude"/> e <see cref="Longitude"/>) são preenchidas via
/// geocoding em US003.
/// </summary>
public class Address : BaseEntity
{
    public string Street { get; private set; } = string.Empty;
    public string Number { get; private set; } = string.Empty;
    public string? Complement { get; private set; }
    public string Neighborhood { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public string ZipCode { get; private set; } = string.Empty;
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }

    /// <summary>
    /// Cria um novo endereço com os campos obrigatórios. Lat/Lng ficam nulos até o
    /// geocoding ser executado em US003.
    /// </summary>
    public Address(
        string street,
        string number,
        string neighborhood,
        string city,
        string state,
        string zipCode,
        string? complement = null)
    {
        Street = street;
        Number = number;
        Neighborhood = neighborhood;
        City = city;
        State = state;
        ZipCode = zipCode;
        Complement = complement;
    }
}
```

- [ ] **Step 2: Criar `AddressTypeConfiguration.cs`**

```csharp
using Ecocell.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ecocell.Api.Database.TypeConfiguration;

/// <summary>
/// Configuração EF Core da entidade <see cref="Address"/> — tabela <c>Addresses</c>.
/// </summary>
public class AddressTypeConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("Addresses");

        builder.Property(a => a.Street).HasMaxLength(255).IsRequired();
        builder.Property(a => a.Number).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Complement).HasMaxLength(100);
        builder.Property(a => a.Neighborhood).HasMaxLength(100).IsRequired();
        builder.Property(a => a.City).HasMaxLength(100).IsRequired();
        builder.Property(a => a.State).HasMaxLength(2).IsRequired();
        builder.Property(a => a.ZipCode).HasMaxLength(8).IsRequired();
        builder.Property(a => a.Latitude).HasPrecision(9, 6);
        builder.Property(a => a.Longitude).HasPrecision(9, 6);
    }
}
```

- [ ] **Step 3: Adicionar `DbSet<Address>` em `AppDbContext.cs`**

Abrir `src/Ecocell.Api/Database/AppDbContext.cs` e adicionar após `DbSet<LegalPerson>`:

```csharp
public DbSet<Address> Addresses { get; set; }
```

- [ ] **Step 4: Build para garantir sem erros de compilação**

```bash
dotnet build Ecocell.slnx
```

Esperado: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add src/Ecocell.Api/Entities/Address.cs src/Ecocell.Api/Database/TypeConfiguration/AddressTypeConfiguration.cs src/Ecocell.Api/Database/AppDbContext.cs
git commit -m "feat: add Address entity with EF Core configuration and DbSet"
```

---

## Task 3: Modificar `LegalPerson` — `AddressId`, navegação e status `PendingApproval`

**Files:**
- Modify: `src/Ecocell.Api/Entities/LegalPerson.cs`
- Modify: `src/Ecocell.Api/Database/TypeConfiguration/LegalPersonTypeConfiguration.cs`

- [ ] **Step 1: Atualizar `LegalPerson.cs`**

Substituir o conteúdo completo de `src/Ecocell.Api/Entities/LegalPerson.cs`:

```csharp
using Ecocell.Api.Enums;

namespace Ecocell.Api.Entities;

/// <summary>
/// Entidade que representa uma Pessoa Jurídica cadastrada na plataforma,
/// assumindo o papel de Ponto de Coleta ou Coletor.
/// </summary>
public class LegalPerson : Person
{
    public string LegalName { get; private set; } = string.Empty;
    public string TradeName { get; private set; } = string.Empty;
    public string Cnpj { get; private set; } = string.Empty;
    public string? Cnae { get; private set; }

    public Guid? AddressId { get; private set; }
    public virtual Address? Address { get; private set; }

    public Guid? ResponsiblePersonId { get; private set; }
    public virtual NaturalPerson? ResponsiblePerson { get; private set; }

    /// <summary>
    /// Cria uma nova Pessoa Jurídica vinculada a um Gestor PF e a um endereço.
    /// O status inicial é <see cref="PersonStatus.PendingApproval"/> para os papéis
    /// <see cref="Journey.CollectPoint"/> e <see cref="Journey.Collector"/> — aguarda
    /// aprovação administrativa conforme RN007.
    /// </summary>
    public LegalPerson(
        string legalName,
        string tradeName,
        string cnpj,
        string email,
        Journey journey,
        Guid? addressId = null,
        Guid? responsiblePersonId = null,
        string? cnae = null)
    {
        LegalName = legalName;
        TradeName = tradeName;
        Email = email;
        Cnpj = cnpj;
        AddressId = addressId;
        ResponsiblePersonId = responsiblePersonId;
        Role = Role.User;
        Journey = journey;
        PersonType = PersonType.LegalPerson;
        Cnae = cnae;

        if (journey == Journey.CollectPoint || journey == Journey.Collector)
            PersonStatus = PersonStatus.PendingApproval;
    }
}
```

- [ ] **Step 2: Atualizar `LegalPersonTypeConfiguration.cs` — adicionar relacionamento `Address`**

Abrir `src/Ecocell.Api/Database/TypeConfiguration/LegalPersonTypeConfiguration.cs` e adicionar ao final do método `Configure`, após o relacionamento `ResponsiblePerson`:

```csharp
builder.HasOne(lp => lp.Address)
    .WithMany()
    .HasForeignKey(lp => lp.AddressId)
    .IsRequired(false)
    .OnDelete(DeleteBehavior.SetNull);
```

- [ ] **Step 3: Build**

```bash
dotnet build Ecocell.slnx
```

Esperado: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add src/Ecocell.Api/Entities/LegalPerson.cs src/Ecocell.Api/Database/TypeConfiguration/LegalPersonTypeConfiguration.cs
git commit -m "feat: add AddressId FK to LegalPerson and set PendingApproval as initial status"
```

---

## Task 4: Migration `AddAddressTable`

**Files:**
- Create: migration em `src/Ecocell.Api/Migrations/`

- [ ] **Step 1: Gerar a migration**

```bash
dotnet ef migrations add AddAddressTable --project src/Ecocell.Api --startup-project src/Ecocell.Api
```

Esperado: novo arquivo de migration criado em `src/Ecocell.Api/Migrations/`.

- [ ] **Step 2: Verificar o conteúdo da migration gerada**

Abrir o arquivo de migration e confirmar:
- Criação da tabela `Addresses` com todas as colunas (Street, Number, Complement, Neighborhood, City, State, ZipCode, Latitude, Longitude)
- Adição da coluna `AddressId` (nullable) em `LegalPeople`
- FK de `LegalPeople.AddressId` → `Addresses.Id` com `ON DELETE SET NULL`

- [ ] **Step 3: Commit**

```bash
git add src/Ecocell.Api/Migrations/
git commit -m "feat: add migration AddAddressTable"
```

---

## Task 5: Evento `LegalPersonRegistered` + `IEmailSender` + `LoggingEmailSender`

**Files:**
- Create: `src/Ecocell.Api/Events/LegalPersonRegistered.cs`
- Modify: `src/Ecocell.Api/Services/Email/IEmailSender.cs`
- Modify: `src/Ecocell.Api/Services/Email/LoggingEmailSender.cs`

- [ ] **Step 1: Criar `LegalPersonRegistered.cs`**

```csharp
using Mediator;

namespace Ecocell.Api.Events;

/// <summary>
/// Evento publicado após o cadastro bem-sucedido de uma Pessoa Jurídica.
/// Utilizado para disparar o envio de e-mails de notificação para a PJ e o Gestor PF.
/// </summary>
/// <param name="LegalPersonId">Identificador único da PJ cadastrada.</param>
/// <param name="LegalPersonEmail">E-mail da Pessoa Jurídica.</param>
/// <param name="ResponsiblePersonEmail">E-mail do Gestor PF que realizou o cadastro.</param>
public sealed record LegalPersonRegistered(
    Guid LegalPersonId,
    string LegalPersonEmail,
    string ResponsiblePersonEmail) : INotification;
```

- [ ] **Step 2: Adicionar método em `IEmailSender.cs`**

Abrir `src/Ecocell.Api/Services/Email/IEmailSender.cs` e adicionar após `SendVerificationCodeAsync`:

```csharp
/// <summary>
/// Envia notificação de cadastro recebido para uma Pessoa Jurídica aguardando aprovação.
/// </summary>
/// <param name="email">Destinatário do e-mail.</param>
/// <param name="ct">Token de cancelamento da operação.</param>
Task SendLegalPersonRegistrationNotificationAsync(string email, CancellationToken ct = default);
```

- [ ] **Step 3: Implementar stub em `LoggingEmailSender.cs`**

Adicionar após `SendVerificationCodeAsync`:

```csharp
/// <summary>
/// Registra a notificação de cadastro de PJ no log estruturado (não realiza envio real de e-mail).
/// </summary>
/// <param name="email">Destinatário do e-mail.</param>
/// <param name="ct">Token de cancelamento da operação.</param>
public Task SendLegalPersonRegistrationNotificationAsync(string email, CancellationToken ct = default)
{
    _logger.LogInformation("[PJ_REGISTRATION] Notificação de cadastro enviada para {Email}", email);
    return Task.CompletedTask;
}
```

- [ ] **Step 4: Build**

```bash
dotnet build Ecocell.slnx
```

Esperado: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add src/Ecocell.Api/Events/LegalPersonRegistered.cs src/Ecocell.Api/Services/Email/IEmailSender.cs src/Ecocell.Api/Services/Email/LoggingEmailSender.cs
git commit -m "feat: add LegalPersonRegistered event and registration notification email method"
```

---

## Task 6: DTOs em `Ecocell.Shared`

**Files:**
- Create: `src/Ecocell.Shared/Requests/RequestRegisterLegalPerson.cs`

- [ ] **Step 1: Criar `RequestRegisterLegalPerson.cs`** (contém também `RequestRegisterLegalPersonAddress`)

```csharp
using Ecocell.Shared.Enums;

namespace Ecocell.Shared.Requests;

/// <summary>
/// Requisição HTTP para cadastro de uma nova Pessoa Jurídica (Ponto de Coleta ou Coletor).
/// Enviada pelo Gestor PF autenticado via POST /api/legal-person.
/// </summary>
public record RequestRegisterLegalPerson
{
    public string Cnpj { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string TradeName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Journey Journey { get; set; }
    public RequestRegisterLegalPersonAddress Address { get; set; } = new();
}

/// <summary>
/// Dados de endereço da Pessoa Jurídica para cadastro.
/// Lat/Lng não são informados pelo cliente — preenchidos via geocoding em US003.
/// </summary>
public record RequestRegisterLegalPersonAddress
{
    public string Street { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string? Complement { get; set; }
    public string Neighborhood { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Build**

```bash
dotnet build Ecocell.slnx
```

Esperado: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add src/Ecocell.Shared/Requests/RequestRegisterLegalPerson.cs
git commit -m "feat: add RequestRegisterLegalPerson and RequestRegisterLegalPersonAddress DTOs"
```

---

## Task 7: Testes (Red) — `RegisterLegalPersonTests`

**Files:**
- Create: `tests/Ecocell.UnitTests/Features/Person/RegisterLegalPersonTests.cs`

Escrever todos os testes **antes** da implementação. Eles devem falhar com `does not contain a definition for 'RegisterLegalPerson'`.

- [ ] **Step 1: Criar `RegisterLegalPersonTests.cs`**

```csharp
using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Events;
using Ecocell.Api.Features.Person;
using Ecocell.Api.Shared;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Ecocell.UnitTests.Features.Person;

public class RegisterLegalPersonTests : TestBase
{
    private readonly RegisterLegalPerson.Handler _handler;
    private readonly RegisterLegalPerson.Validator _validator;
    private readonly RegisterLegalPerson.Command _command;
    private readonly Mock<IPublisher> _publisherMock;
    private readonly NaturalPerson _responsiblePerson;

    public RegisterLegalPersonTests()
    {
        _validator = new RegisterLegalPerson.Validator();
        var loggerMock = CreateLoggerMock<RegisterLegalPerson.Handler>();
        _publisherMock = new Mock<IPublisher>();

        _handler = new RegisterLegalPerson.Handler(DbContext, loggerMock.Object, _validator, _publisherMock.Object);

        _responsiblePerson = new NaturalPerson(
            new Faker().Name.FullName(),
            new Faker().Person.Cpf(includeFormatSymbols: false),
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)),
            Role.User,
            new Faker().Internet.Email(),
            Journey.Depositor);

        DbContext.NaturalPeople.Add(_responsiblePerson);
        DbContext.SaveChanges();

        // Ativar a conta do gestor diretamente via SQL (PersonStatus.Active = 1)
        // Confirm() é internal; ExecuteUpdate contorna isso sem violar o isolamento do teste.
        DbContext.People
            .Where(p => p.Id == _responsiblePerson.Id)
            .ExecuteUpdate(s => s.SetProperty(p => p.PersonStatus, PersonStatus.Active));

        _command = new Faker<RegisterLegalPerson.Command>()
            .RuleFor(x => x.Cnpj, f => f.Company.Cnpj(includeFormatSymbols: false))
            .RuleFor(x => x.LegalName, f => f.Company.CompanyName())
            .RuleFor(x => x.TradeName, f => f.Company.CompanyName())
            .RuleFor(x => x.Email, f => f.Internet.Email())
            .RuleFor(x => x.Journey, _ => Journey.CollectPoint)
            .RuleFor(x => x.ResponsiblePersonId, _ => _responsiblePerson.Id)
            .RuleFor(x => x.Address, f => new RegisterLegalPerson.AddressCommand
            {
                Street = f.Address.StreetName(),
                Number = f.Address.BuildingNumber(),
                Neighborhood = "Centro",
                City = f.Address.City(),
                State = "SP",
                ZipCode = "01310100",
                Complement = null
            })
            .Generate();
    }

    [Fact]
    public async Task Handle_ShouldPersistInDatabase_WhenRequestIsValidAsCollectPoint()
    {
        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var lp = await DbContext.LegalPeople.FirstOrDefaultAsync(lp => lp.Cnpj == _command.Cnpj);
        lp.ShouldNotBeNull();
        lp.Journey.ShouldBe(Journey.CollectPoint);
    }

    [Fact]
    public async Task Handle_ShouldPersistInDatabase_WhenRequestIsValidAsCollector()
    {
        // Arrange
        _command.Journey = Journey.Collector;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var lp = await DbContext.LegalPeople.FirstOrDefaultAsync(lp => lp.Cnpj == _command.Cnpj);
        lp.ShouldNotBeNull();
        lp.Journey.ShouldBe(Journey.Collector);
    }

    [Fact]
    public async Task Handle_ShouldPersistAddressInDatabase_WhenRequestIsValid()
    {
        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var lp = await DbContext.LegalPeople
            .Include(lp => lp.Address)
            .FirstOrDefaultAsync(lp => lp.Cnpj == _command.Cnpj);

        lp.ShouldNotBeNull();
        lp.AddressId.ShouldNotBeNull();
        lp.Address.ShouldNotBeNull();
        lp.Address!.City.ShouldBe(_command.Address.City);
        lp.Address.State.ShouldBe(_command.Address.State);
    }

    [Fact]
    public async Task Handle_ShouldSetStatusPendingApproval_WhenRequestIsValid()
    {
        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        var lp = await DbContext.LegalPeople.FirstOrDefaultAsync(lp => lp.Cnpj == _command.Cnpj);
        lp.ShouldNotBeNull();
        lp.PersonStatus.ShouldBe(PersonStatus.PendingApproval);
    }

    [Fact]
    public async Task Handle_ShouldPublishLegalPersonRegistered_WhenSuccess()
    {
        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        _publisherMock.Verify(
            p => p.Publish(
                It.Is<LegalPersonRegistered>(e => e.LegalPersonEmail == _command.Email),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenCnpjAlreadyExists()
    {
        // Arrange
        var address = new Address("Rua A", "1", "Centro", "São Paulo", "SP", "01310100");
        var existing = new LegalPerson(
            "Empresa Existente",
            "Empresa Existente",
            _command.Cnpj,
            "outro@empresa.com",
            Journey.CollectPoint,
            address.Id,
            _responsiblePerson.Id);

        DbContext.Addresses.Add(address);
        DbContext.LegalPeople.Add(existing);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenEmailAlreadyExists()
    {
        // Arrange
        var faker = new Faker();
        string differentCnpj;
        do { differentCnpj = faker.Company.Cnpj(includeFormatSymbols: false); }
        while (differentCnpj == _command.Cnpj);

        var address = new Address("Rua B", "2", "Centro", "São Paulo", "SP", "01310100");
        var existing = new LegalPerson(
            "Outra Empresa",
            "Outra Empresa",
            differentCnpj,
            _command.Email,
            Journey.CollectPoint,
            address.Id,
            _responsiblePerson.Id);

        DbContext.Addresses.Add(address);
        DbContext.LegalPeople.Add(existing);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenJourneyIsDepositor()
    {
        // Arrange — RN003: PJ não pode ter Journey.Depositor
        _command.Journey = Journey.Depositor;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }

    [Theory]
    [InlineData("00000000000000")]
    [InlineData("11111111000191")] // CNPJ com dígitos repetidos inválidos
    public async Task Handle_ShouldReturnValidationError_WhenCnpjIsInvalid(string cnpj)
    {
        // Arrange
        _command.Cnpj = cnpj;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }

    [Theory]
    [InlineData("@com")]
    [InlineData("empresa.com")]
    public async Task Handle_ShouldReturnValidationError_WhenEmailIsInvalid(string email)
    {
        // Arrange
        _command.Email = email;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Handle_ShouldReturnValidationError_WhenLegalNameIsEmpty(string legalName)
    {
        // Arrange
        _command.LegalName = legalName;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }

    [Theory]
    [InlineData("A")]   // 1 char — State deve ter 2
    [InlineData("ABC")] // 3 chars — State deve ter 2
    public async Task Handle_ShouldReturnValidationError_WhenAddressStateIsInvalid(string state)
    {
        // Arrange
        _command.Address.State = state;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }

    [Theory]
    [InlineData("0131010")]   // 7 digits
    [InlineData("013101000")] // 9 digits
    public async Task Handle_ShouldReturnValidationError_WhenAddressZipCodeIsInvalid(string zipCode)
    {
        // Arrange
        _command.Address.ZipCode = zipCode;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ErrorOnValidation);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenResponsiblePersonDoesNotExist()
    {
        // Arrange
        _command.ResponsiblePersonId = Guid.NewGuid(); // ID inexistente

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenResponsiblePersonIsNotActive()
    {
        // Arrange — reverter o status do gestor para AwaitingConfirmation
        DbContext.People
            .Where(p => p.Id == _responsiblePerson.Id)
            .ExecuteUpdate(s => s.SetProperty(p => p.PersonStatus, PersonStatus.AwaitingConfirmation));

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.ForbiddenCodeError);
    }
}
```

- [ ] **Step 2: Rodar os testes para confirmar que falham (Red)**

```bash
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~RegisterLegalPersonTests"
```

Esperado: erro de compilação `The type or namespace name 'RegisterLegalPerson' does not exist`.

---

## Task 8: Implementar `RegisterLegalPerson` slice (Green)

**Files:**
- Create: `src/Ecocell.Api/Features/Person/RegisterLegalPerson.cs`

- [ ] **Step 1: Criar `RegisterLegalPerson.cs`**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Carter;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Ecocell.Api.Enums;
using Ecocell.Api.Events;
using Ecocell.Api.Extensions;
using Ecocell.Api.Shared;
using Ecocell.Shared.Requests;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Ecocell.Api.Features.Person.RegisterLegalPerson;

namespace Ecocell.Api.Features.Person;

/// <summary>
/// Slice responsável pelo cadastro de uma nova Pessoa Jurídica (Ponto de Coleta ou Coletor)
/// por um Gestor PF autenticado. Status inicial: <see cref="PersonStatus.PendingApproval"/> (RN007).
/// </summary>
public static class RegisterLegalPerson
{
    /// <summary>
    /// Dados de endereço internos ao slice — não expostos na borda HTTP.
    /// </summary>
    public record AddressCommand
    {
        public string Street { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string? Complement { get; set; }
        public string Neighborhood { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Comando interno para cadastro de PJ. Inclui <see cref="ResponsiblePersonId"/> extraído
    /// do JWT pelo endpoint — nunca exposto no DTO público.
    /// </summary>
    public record Command : IRequest<Result>
    {
        public string Cnpj { get; set; } = string.Empty;
        public string LegalName { get; set; } = string.Empty;
        public string TradeName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public Journey Journey { get; set; }
        public Guid ResponsiblePersonId { get; set; }
        public AddressCommand Address { get; set; } = new();
    }

    /// <summary>
    /// Valida o comando de cadastro de PJ garantindo CNPJ válido, e-mail válido, campos
    /// obrigatórios de endereço e que a <see cref="Journey"/> seja <see cref="Journey.CollectPoint"/>
    /// ou <see cref="Journey.Collector"/> (RN003).
    /// </summary>
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Cnpj).IsValidCnpj();

            RuleFor(x => x.LegalName)
                .NotEmpty().WithMessage("Razão social é obrigatória.")
                .MaximumLength(255).WithMessage("Razão social deve conter no máximo 255 caracteres.");

            RuleFor(x => x.TradeName)
                .NotEmpty().WithMessage("Nome fantasia é obrigatório.")
                .MaximumLength(255).WithMessage("Nome fantasia deve conter no máximo 255 caracteres.");

            RuleFor(x => x.Email).IsValidEmail();

            RuleFor(x => x.Journey)
                .Must(j => j == Journey.CollectPoint || j == Journey.Collector)
                .WithMessage("Papel da pessoa jurídica deve ser Ponto de Coleta ou Coletor. (RN003)");

            RuleFor(x => x.Address.Street)
                .NotEmpty().WithMessage("Logradouro é obrigatório.")
                .MaximumLength(255).WithMessage("Logradouro deve conter no máximo 255 caracteres.");

            RuleFor(x => x.Address.Number)
                .NotEmpty().WithMessage("Número é obrigatório.")
                .MaximumLength(20).WithMessage("Número deve conter no máximo 20 caracteres.");

            RuleFor(x => x.Address.Complement)
                .MaximumLength(100).WithMessage("Complemento deve conter no máximo 100 caracteres.")
                .When(x => x.Address.Complement is not null);

            RuleFor(x => x.Address.Neighborhood)
                .NotEmpty().WithMessage("Bairro é obrigatório.")
                .MaximumLength(100).WithMessage("Bairro deve conter no máximo 100 caracteres.");

            RuleFor(x => x.Address.City)
                .NotEmpty().WithMessage("Cidade é obrigatória.")
                .MaximumLength(100).WithMessage("Cidade deve conter no máximo 100 caracteres.");

            RuleFor(x => x.Address.State)
                .Length(2).WithMessage("Estado deve conter exatamente 2 caracteres (sigla UF).");

            RuleFor(x => x.Address.ZipCode)
                .Length(8).WithMessage("CEP deve conter exatamente 8 dígitos (sem máscara).");
        }
    }

    /// <summary>
    /// Processa o cadastro da PJ: valida, verifica unicidade de CNPJ e e-mail, confirma
    /// que o Gestor PF existe e está ativo, persiste PJ + Address e publica
    /// <see cref="LegalPersonRegistered"/>.
    /// </summary>
    public sealed class Handler : IRequestHandler<Command, Result>
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<Handler> _logger;
        private readonly IValidator<Command> _validator;
        private readonly IPublisher _publisher;

        public Handler(AppDbContext dbContext, ILogger<Handler> logger, IValidator<Command> validator, IPublisher publisher)
        {
            _dbContext = dbContext;
            _logger = logger;
            _validator = validator;
            _publisher = publisher;
        }

        /// <summary>
        /// Executa o cadastro da Pessoa Jurídica seguindo a sequência: validação de input,
        /// unicidade de CNPJ/e-mail, verificação do Gestor PF e persistência.
        /// </summary>
        /// <param name="request">Comando com os dados da PJ e do endereço.</param>
        /// <param name="cancellationToken">Token de cancelamento da operação.</param>
        /// <returns><see cref="Result"/> indicando sucesso ou o erro encontrado.</returns>
        public async ValueTask<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processando cadastro da pessoa jurídica {@LegalPerson}", request);

            var validationResult = _validator.Validate(request);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                _logger.LogError("Erros de validação ao cadastrar pessoa jurídica {@Erros}", errors);
                return Result.Failure(Error.ErrorOnValidation(errors));
            }

            var cnpjExists = await _dbContext.LegalPeople
                .AnyAsync(lp => lp.Cnpj == request.Cnpj, cancellationToken);
            if (cnpjExists)
            {
                _logger.LogError("CNPJ {Cnpj} já cadastrado", request.Cnpj);
                return Result.Failure(Error.Conflict($"Já existe uma pessoa jurídica cadastrada com o CNPJ {request.Cnpj}."));
            }

            var emailExists = await _dbContext.People
                .AnyAsync(p => p.Email == request.Email, cancellationToken);
            if (emailExists)
            {
                _logger.LogError("E-mail {Email} já cadastrado", request.Email);
                return Result.Failure(Error.Conflict($"Já existe uma pessoa cadastrada com o e-mail {request.Email}."));
            }

            var responsiblePerson = await _dbContext.NaturalPeople
                .FirstOrDefaultAsync(np => np.Id == request.ResponsiblePersonId, cancellationToken);
            if (responsiblePerson is null)
            {
                _logger.LogError("Gestor {ResponsiblePersonId} não encontrado", request.ResponsiblePersonId);
                return Result.Failure(Error.NotFound($"Pessoa física {request.ResponsiblePersonId} não encontrada."));
            }

            if (responsiblePerson.PersonStatus != PersonStatus.Active)
            {
                _logger.LogError("Gestor {ResponsiblePersonId} não está ativo", request.ResponsiblePersonId);
                return Result.Failure(Error.Forbidden());
            }

            var address = new Address(
                request.Address.Street,
                request.Address.Number,
                request.Address.Neighborhood,
                request.Address.City,
                request.Address.State,
                request.Address.ZipCode,
                request.Address.Complement);

            var legalPerson = new LegalPerson(
                request.LegalName,
                request.TradeName,
                request.Cnpj,
                request.Email,
                request.Journey,
                address.Id,
                request.ResponsiblePersonId);

            _dbContext.Addresses.Add(address);
            _dbContext.LegalPeople.Add(legalPerson);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _publisher.Publish(
                new LegalPersonRegistered(legalPerson.Id, legalPerson.Email, responsiblePerson.Email),
                cancellationToken);

            _logger.LogInformation("Pessoa jurídica cadastrada com sucesso {@LegalPerson}", legalPerson);
            return Result.Success();
        }
    }
}

/// <summary>
/// Endpoint Carter para POST /api/legal-person — cadastra nova Pessoa Jurídica.
/// Requer autenticação JWT; extrai <c>ResponsiblePersonId</c> da claim <c>sid</c>.
/// </summary>
public class RegisterLegalPersonEndpoint : ICarterModule
{
    /// <summary>Registra a rota do endpoint no pipeline da aplicação.</summary>
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("api/legal-person", async (
            [FromBody] RequestRegisterLegalPerson request,
            ClaimsPrincipal user,
            ISender sender) =>
        {
            var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sid);
            if (!Guid.TryParse(sub, out var responsiblePersonId))
                return Result.Failure(Error.Unauthorized()).ToProcessResult(StatusCodes.Status201Created);

            var command = new Command
            {
                Cnpj = request.Cnpj,
                LegalName = request.LegalName,
                TradeName = request.TradeName,
                Email = request.Email,
                Journey = (Journey)(int)request.Journey,
                ResponsiblePersonId = responsiblePersonId,
                Address = new AddressCommand
                {
                    Street = request.Address.Street,
                    Number = request.Address.Number,
                    Complement = request.Address.Complement,
                    Neighborhood = request.Address.Neighborhood,
                    City = request.Address.City,
                    State = request.Address.State,
                    ZipCode = request.Address.ZipCode
                }
            };

            var result = await sender.Send(command);
            return result.ToProcessResult(StatusCodes.Status201Created);
        })
        .WithTags("Person")
        .WithName("RegisterLegalPerson")
        .WithSummary("Registra uma nova pessoa jurídica vinculada ao gestor autenticado.")
        .WithDescription("Cria registro de LegalPerson com endereço. Status inicial: PendingApproval (RN007). Requer JWT do Gestor PF.")
        .RequireAuthorization(AuthorizationPolicies.Authenticated)
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }
}
```

- [ ] **Step 2: Rodar os testes — devem passar (Green)**

```bash
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~RegisterLegalPersonTests"
```

Esperado: todos os testes de `RegisterLegalPersonTests` passam.

- [ ] **Step 3: Rodar suite completa — sem regressões**

```bash
dotnet test Ecocell.slnx
```

Esperado: todos os testes passam.

- [ ] **Step 4: Commit**

```bash
git add src/Ecocell.Api/Features/Person/RegisterLegalPerson.cs
git commit -m "feat: implement RegisterLegalPerson slice (US001-PJ)"
```

---

## Task 9: NotificationHandler `SendRegistrationEmailOnLegalPersonRegistered`

**Files:**
- Create: `src/Ecocell.Api/Features/Person/SendRegistrationEmailOnLegalPersonRegistered.cs`

- [ ] **Step 1: Criar `SendRegistrationEmailOnLegalPersonRegistered.cs`**

```csharp
using Ecocell.Api.Events;
using Ecocell.Api.Services.Email;
using Mediator;

namespace Ecocell.Api.Features.Person;

/// <summary>
/// Handler de notificação que reage ao evento <see cref="LegalPersonRegistered"/> enviando
/// e-mails de notificação para a PJ e o Gestor PF. Falhas de infra são logadas e não
/// propagadas — o cadastro já foi efetivado e este handler é fire-and-forget.
/// </summary>
public static class SendRegistrationEmailOnLegalPersonRegistered
{
    /// <summary>
    /// Envia e-mail de "cadastro recebido, aguardando aprovação" para a PJ e o Gestor PF.
    /// </summary>
    public sealed class Handler : INotificationHandler<LegalPersonRegistered>
    {
        private readonly IEmailSender _emailSender;
        private readonly ILogger<Handler> _logger;

        public Handler(IEmailSender emailSender, ILogger<Handler> logger)
        {
            _emailSender = emailSender;
            _logger = logger;
        }

        /// <summary>
        /// Processa a notificação: envia e-mail para o endereço da PJ e para o Gestor PF.
        /// </summary>
        /// <param name="notification">Dados da PJ recém-cadastrada.</param>
        /// <param name="cancellationToken">Token de cancelamento da operação.</param>
        public async ValueTask Handle(LegalPersonRegistered notification, CancellationToken cancellationToken)
        {
            try
            {
                await _emailSender.SendLegalPersonRegistrationNotificationAsync(notification.LegalPersonEmail, cancellationToken);
                await _emailSender.SendLegalPersonRegistrationNotificationAsync(notification.ResponsiblePersonEmail, cancellationToken);

                _logger.LogInformation(
                    "Notificação de cadastro de PJ enviada para {LegalPersonEmail} e {ResponsiblePersonEmail}",
                    notification.LegalPersonEmail,
                    notification.ResponsiblePersonEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Falha ao enviar notificação de cadastro de PJ para {LegalPersonEmail} ou {ResponsiblePersonEmail}",
                    notification.LegalPersonEmail,
                    notification.ResponsiblePersonEmail);
            }
        }
    }
}
```

- [ ] **Step 2: Build + suite completa**

```bash
dotnet build Ecocell.slnx && dotnet test Ecocell.slnx
```

Esperado: `Build succeeded` + todos os testes passam.

- [ ] **Step 3: Commit**

```bash
git add src/Ecocell.Api/Features/Person/SendRegistrationEmailOnLegalPersonRegistered.cs
git commit -m "feat: add notification handler to send registration emails on LegalPersonRegistered"
```

---

## Task 10: Atualizar `TASKS.md` e verificação final

**Files:**
- Modify: `.claude/TASKS.md`

- [ ] **Step 1: Aplicar correções no `TASKS.md`**

Aplicar as seguintes alterações em `.claude/TASKS.md`:

1. **US001-PJ.A — Handler**: trocar `status inicial AwaitingConfirmation` por `PendingApproval`.

2. **US001-PJ.A — adicionar tarefas concluídas** (marcar `[x]`):
   - `[x]` Criar entidade `Address` + `AddressTypeConfiguration` + migration `AddAddressTable`
   - `[x]` Criar extensão `IsValidEmail` em `FluentValidationExtensions` + refatorar `RegisterNaturalPerson.Validator`
   - `[x]` Criar evento `LegalPersonRegistered` + `SendRegistrationEmailOnLegalPersonRegistered`
   - `[x]` Adicionar `SendLegalPersonRegistrationNotificationAsync` em `IEmailSender`
   - `[x]` Criar `RequestRegisterLegalPerson` em `Ecocell.Shared/Requests/`
   - `[x]` Criar slice `Features/Person/RegisterLegalPerson.cs` com `Command`, `Validator`, `Handler` e `RegisterLegalPersonEndpoint`
   - `[x]` Criar testes em `tests/Ecocell.UnitTests/Features/Person/RegisterLegalPersonTests.cs`

3. **US007.A**: adicionar nota após a descrição da máquina de estados:
   > ⚠️ PJ entra diretamente em `PendingApproval` ao ser cadastrada (sem passar por `AwaitingConfirmation`). O fluxo `AwaitingConfirmation → AwaitingApproval` descrito aqui aplica-se apenas à confirmação de conta da PF (US009). Decisão registrada em `docs/superpowers/specs/2026-04-28-register-legal-person-design.md`.

- [ ] **Step 2: Rodar suite completa final**

```bash
dotnet test Ecocell.slnx
```

Esperado: todos os testes passam.

- [ ] **Step 3: Commit**

```bash
git add .claude/TASKS.md
git commit -m "docs: update TASKS.md with US001-PJ completion and status flow correction"
```