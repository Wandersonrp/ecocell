# Regras de Testes de Unidade — EcoCell

Estas regras são **normativas** para todo código de teste em `tests/Ecocell.UnitTests/`. Para regras de codificação de produção, ver [`coding-rules.md`](coding-rules.md). Para a regra de TDD (ciclo Red → Green → Refactor) e comandos de execução, ver `CLAUDE.md`.

---

## 1. Stack obrigatória

| Ferramenta | Uso |
| --- | --- |
| **xUnit** | Framework de teste (`[Fact]`, `[Theory]`, `[InlineData]`). |
| **Shouldly** | Asserções (`.ShouldBeTrue()`, `.ShouldHaveSingleItem()`, etc.). |
| **Moq** | Mocks de dependências externas (logger, serviços). |
| **Bogus** + `Bogus.Extensions.Brazil` | Geração de dados falsos realistas (`Cpf`, `Cnpj`, nome, e-mail). |
| **Microsoft.EntityFrameworkCore.Sqlite** | Banco de testes via Sqlite **in-memory**. **Nunca** PostgreSQL. |

Não introduza FluentAssertions, NUnit, AutoFixture, NSubstitute ou outros — o stack acima é fechado.

---

## 2. `TestBase` — base de toda classe de teste

Toda classe de teste de unidade que toca o banco **deve** estender `TestBase`:

```csharp
public class RegisterNaturalPersonTests : TestBase { ... }
```

`TestBase` (em `tests/Ecocell.UnitTests/TestBase.cs`) garante:

- `AppDbContext` novo por teste, ligado a um `SqliteConnection("Filename=:memory:")` aberto.
- Schema criado via `EnsureCreated()` (não migrations).
- Helper `CreateLoggerMock<T>()` para padronizar mocks de `ILogger<T>`.
- `IDisposable` que fecha conexão e descarta o contexto após cada teste.

**Não** instancie `AppDbContext` manualmente, **não** monte um `DbContextOptionsBuilder` no teste, **não** abra outra `SqliteConnection` paralela. Use o `DbContext` da base.

---

## 3. Localização e nomenclatura

- Espelhe a árvore de slices: cada teste vive em `tests/Ecocell.UnitTests/Features/<Agregado>/<Feature>Tests.cs`.
  - Slice: `src/Ecocell.Api/Features/Person/RegisterNaturalPerson.cs`
  - Teste: `tests/Ecocell.UnitTests/Features/Person/RegisterNaturalPersonTests.cs`
- Classe de teste: **`<NomeDaFeature>Tests`** (sufixo `Tests` no plural).
- Namespace: `Ecocell.UnitTests.Features.<Agregado>`.
- Método de teste: **`<MétodoSobTeste>_Should<Resultado>_When<Condição>`**, em inglês.
  - ✅ `Handle_ShouldPersistInDatabase_WhenRequestIsValid`
  - ✅ `Handle_ShouldNotPersistInDatabase_WhenEmailIsInvalid`
  - ❌ `TestRegister`, `Deve_cadastrar_pessoa`, `Test1`

---

## 4. Setup no construtor

O construtor da classe monta o SUT (System Under Test) **uma vez por teste** (xUnit cria nova instância por teste). Padrão:

```csharp
public class RegisterNaturalPersonTests : TestBase
{
    private readonly RegisterNaturalPerson.Handler _handler;
    private readonly RegisterNaturalPerson.Validator _validator;
    private readonly RegisterNaturalPerson.Command _command;

    public RegisterNaturalPersonTests()
    {
        _validator = new RegisterNaturalPerson.Validator();
        var loggerMock = CreateLoggerMock<RegisterNaturalPerson.Handler>();

        _handler = new RegisterNaturalPerson.Handler(DbContext, loggerMock.Object, _validator);

        _command = new Faker<RegisterNaturalPerson.Command>()
            .RuleFor(x => x.FullName, f => f.Name.FullName())
            .RuleFor(x => x.Email, f => f.Internet.Email())
            .RuleFor(x => x.Cpf, f => f.Person.Cpf(includeFormatSymbols: false))
            .RuleFor(x => x.Journey, f => f.PickRandom<Journey>())
            .RuleFor(x => x.BirthDate, f => DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-16)))
            .Generate();
    }
}
```

Regras do setup:

- **Campos `private readonly`** com prefixo `_` para SUT, dependências e dados compartilhados (`_handler`, `_validator`, `_command`).
- **Validator real** — nunca mocke `IValidator<TCommand>`. O Validator é parte do slice e o teste cobre a integração `Handler` + `Validator`.
- **Logger mockado** via `CreateLoggerMock<T>()` da `TestBase`.
- **Faker** define um `_command` válido por padrão (caminho feliz). Cada teste de erro **muta** apenas o campo que quer invalidar (`_command.Email = email;`).
- **Use as extensões Brasil** do Bogus para `Cpf`/`Cnpj` — gere sem máscara (`includeFormatSymbols: false`) por padrão, já que o domínio armazena sem formatação.
- Para datas-limite (idade mínima, prazos), use a **fronteira exata** (`AddYears(-16)`) e adicione testes separados para um dia além/aquém da fronteira quando relevante.

---

## 5. Estrutura do método (AAA)

Use o padrão **Arrange / Act / Assert** com comentários. Omita seções vazias:

```csharp
[Fact]
public async Task Handle_ShouldPersistInDatabase_WhenRequestIsValid()
{
    // Act
    var result = await _handler.Handle(_command, CancellationToken.None);

    // Assert
    result.IsSuccess.ShouldBeTrue();

    var exists = await DbContext.NaturalPeople.AnyAsync(np => np.Cpf == _command.Cpf);
    exists.ShouldBeTrue();
}

[Theory]
[InlineData("@com")]
[InlineData("teste.com")]
public async Task Handle_ShouldNotPersistInDatabase_WhenEmailIsInvalid(string email)
{
    // Arrange
    _command.Email = email;

    // Act
    var result = await _handler.Handle(_command, CancellationToken.None);

    // Assert
    result.IsFailure.ShouldBeTrue();
    result.Error.Messages.ShouldHaveSingleItem();
}
```

Regras do método:

- Se o caminho feliz não precisa de Arrange (o `_command` do construtor já basta), **não escreva** o comentário `// Arrange`.
- **Sempre** passe `CancellationToken.None` no teste — não invente um `CancellationTokenSource` a menos que esteja testando cancelamento.
- Métodos de teste são **`async Task`** (nunca `async void`).
- **Um `Should*` por afirmação**. Não combine asserções com `&&` — quebre em linhas.
- Verifique o efeito colateral no banco com `DbContext.<DbSet>.AnyAsync(...)` ou `FirstOrDefaultAsync(...)` direto da `TestBase` — não monte um segundo contexto.

---

## 6. `[Fact]` vs `[Theory]`

- **`[Fact]`** — um cenário fixo (ex.: caminho feliz, conflito específico).
- **`[Theory]` + `[InlineData]`** — mesma asserção, várias entradas inválidas/equivalentes (ex.: vários e-mails malformados, vários CPFs com tamanhos errados).
- Cada slice deve ter, no mínimo:
  - 1 `[Fact]` para o caminho feliz (`...ShouldX_WhenRequestIsValid`).
  - 1 `[Fact]` ou `[Theory]` por **branch de erro** do `Handler` (validação falhou, conflito de unicidade, recurso não encontrado, etc.).

---

## 7. O que NÃO fazer

- ❌ **Não mocke** `AppDbContext`, `DbSet<T>` ou `IQueryable<T>`. Use o Sqlite in-memory da `TestBase`.
- ❌ **Não mocke** o `Validator` do próprio slice — instancie o real.
- ❌ **Não use** PostgreSQL, Testcontainers ou banco compartilhado em testes de unidade.
- ❌ **Não compartilhe estado** entre testes via `static` ou `[Collection]` para reaproveitar dados — cada teste é isolado.
- ❌ **Não dependa da ordem** de execução dos testes (xUnit não garante).
- ❌ **Não asserte logs** (`loggerMock.Verify(...)`) a menos que o log seja parte do contrato da feature; o Logger do `TestBase` é silencioso por design.
- ❌ **Não hardcode** dados realistas (CPF "123.456.789-09", e-mail "joao@teste.com") — use `Faker`. Strings inválidas hardcoded só em `[InlineData]` quando o objetivo é exatamente disparar o erro.
- ❌ **Não duplique** a lógica do `Validator` no teste (ex.: recalcular se um e-mail bate o regex). Teste o **comportamento observável** do `Handler` (`IsFailure`, `Error.Messages`), não a regra interna.

---

## 8. Cobertura mínima por slice

Para considerar uma feature "verde", o teste de unidade deve cobrir:

1. **Caminho feliz** — request válido → `result.IsSuccess` + efeito colateral persistido.
2. **Validação de input** — pelo menos um `[Theory]` por campo com regra não-trivial (formato, faixa, obrigatoriedade).
3. **Conflitos de unicidade** — quando o `Handler` consulta o banco antes de inserir (ex.: CPF duplicado, e-mail duplicado), criar 1 teste por conflito que **pré-popula** o `DbContext` antes de chamar o `Handler`:

```csharp
[Fact]
public async Task Handle_ShouldReturnConflict_WhenCpfAlreadyExists()
{
    // Arrange
    DbContext.People.Add(new NaturalPerson(/* mesmos dados de _command */));
    await DbContext.SaveChangesAsync();

    // Act
    var result = await _handler.Handle(_command, CancellationToken.None);

    // Assert
    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe(ErrorCodes.Conflict);
}
```

4. **Demais branches de erro** do `Handler` (recurso não encontrado, regra de negócio violada, etc.) — um teste por branch.
