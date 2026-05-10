# Regras de Testes de Integração — EcoCell

Estas regras são **normativas** para todo código de teste em `tests/Ecocell.IntegrationTests/`. Para regras de teste de unidade, ver [`unit-tests-rules.md`](unit-tests-rules.md). Para regras de codificação de produção, ver [`coding-rules.md`](coding-rules.md). Para a regra de TDD (ciclo Red → Green → Refactor) e comandos de execução, ver `CLAUDE.md`.

---

## 1. Stack obrigatória

| Ferramenta | Uso |
| --- | --- |
| **xUnit** | Framework de teste (`[Fact]`, `[Theory]`, `[InlineData]`). |
| **Shouldly** | Asserções (`.ShouldBe(HttpStatusCode.Created)`, `.ShouldBeTrue()`, etc.). |
| **Bogus** + `Bogus.Extensions.Brazil` | Geração de dados falsos realistas (`Cpf`, `Cnpj`, nome, e-mail). |
| **Testcontainers.PostgreSql** | Container PostgreSQL real e efêmero por suite. |
| **Testcontainers.Redis** | Container Redis real e efêmero por suite. |
| **Microsoft.AspNetCore.Mvc.Testing** | `WebApplicationFactory<Program>` para rodar a API in-process. |

Não introduza FluentAssertions, NUnit, AutoFixture, NSubstitute, Moq ou outros — o stack acima é fechado. **Mocks são proibidos** no projeto de integração: a stack roda real (HTTP → Handler → DbContext → PostgreSQL real → Redis real). Stubs apenas para serviços externos não-determinísticos (ver §7).

Requer **Docker** em execução na máquina (local e CI).

---

## 2. `IntegrationTestFixture` e `IntegrationTestBase`

### `IntegrationTestFixture` (`ICollectionFixture`)

Sobe os containers **uma vez por suite** (custo de startup pago uma vez, não por classe):

- `PostgreSqlContainer` e `RedisContainer` startam em paralelo no `InitializeAsync`.
- `WebApplicationFactory<Program>` configurada com `UseEnvironment("Testing")`.
- `ConfigureServices` substitui `AppDbContext` (PostgreSQL real do container) e `IConnectionMultiplexer` (Redis real do container).
- Stubs de `IEmailSender` (`CapturingEmailSender`) e `IGeocodingService` (`StubGeocodingService`) registrados como singletons.
- `DisposeAsync` derruba os containers ao fim da suite.

> **Importante:** `appsettings.Testing.json` em `src/Ecocell.Api/` fornece valores placeholder válidos para `Jwt`, `Nominatim`, `Mail`, etc. Sem ele, `AddApi()` falha no startup porque `ConfigureAppConfiguration` da `WebApplicationFactory` não roda antes do `Program.cs` em .NET 10 minimal APIs.

### `IntegrationTestBase` (estendida por toda classe de teste)

```csharp
[Collection(nameof(IntegrationTestCollection))]
public class RegisterNaturalPersonTests : IntegrationTestBase { ... }
```

`IntegrationTestBase` (em `tests/Ecocell.IntegrationTests/IntegrationTestBase.cs`) garante:

- `HttpClient Client` exposto, criado via `Fixture.Factory.CreateClient()`.
- `InitializeAsync` (executa **antes de cada teste** — xUnit cria nova instância por teste):
  - `GetCapturingEmailSender().Clear()` — descarta códigos OTP de testes anteriores.
  - `EnsureDeletedAsync()` + `MigrateAsync()` no `AppDbContext` — banco limpo por teste.
  - Cria novo `HttpClient`.
- `DisposeAsync` descarta o `HttpClient`.
- Helpers de auth, semeação e stubs (ver §6).

**Não** instancie `WebApplicationFactory<Program>` manualmente, **não** monte `HttpClient` paralelo, **não** suba containers locais. Use a fixture.

---

## 3. Localização e nomenclatura

- Espelhe a árvore de slices: cada teste vive em `tests/Ecocell.IntegrationTests/Features/<Agregado>/<Feature>Tests.cs`.
  - Slice: `src/Ecocell.Api/Features/Person/RegisterNaturalPerson.cs`
  - Teste: `tests/Ecocell.IntegrationTests/Features/Person/RegisterNaturalPersonTests.cs`
- Classe de teste: **`<NomeDaFeature>Tests`** (sufixo `Tests` no plural).
- Namespace: `Ecocell.IntegrationTests.Features.<Agregado>`.
- Método de teste: **`<MétodoHttp>_ShouldReturn<StatusCode>_When<Condição>`**, em inglês.
  - ✅ `Post_ShouldReturn201_WhenRequestIsValid`
  - ✅ `Post_ShouldReturn409_WhenCpfAlreadyExists`
  - ✅ `Get_ShouldReturn403_WhenUserIsNotAdmin`
  - ❌ `TestRegister`, `Deve_cadastrar_pessoa`, `Test1`

Nota: a nomenclatura difere dos testes de unidade (`Handle_Should...`) porque o teste de integração observa a **resposta HTTP** (status code), não o retorno do `Handler`.

---

## 4. Setup no construtor

O construtor recebe `IntegrationTestFixture` via DI do xUnit e monta dados compartilhados:

```csharp
public class RegisterNaturalPersonTests : IntegrationTestBase
{
    private readonly RequestRegisterNaturalPerson _request;

    public RegisterNaturalPersonTests(IntegrationTestFixture fixture) : base(fixture)
    {
        var faker = new Faker("pt_BR");
        _request = new RequestRegisterNaturalPerson
        {
            Email = faker.Internet.Email(),
            Cpf = faker.Person.Cpf(includeFormatSymbols: false),
            FullName = faker.Name.FullName(),
            Journey = Journey.Depositor,
            BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
        };
    }
}
```

Regras do setup:

- **Construtor recebe `IntegrationTestFixture`** e passa ao `base(fixture)`.
- **Campos `private readonly`** com prefixo `_` para dados compartilhados (`_request`).
- **Use o DTO público** de `Ecocell.Shared.Requests` (não o `Command` interno) — testes batem no endpoint HTTP, não no Handler.
- **Faker em `pt_BR`** com `Bogus.Extensions.Brazil` para `Cpf`/`Cnpj` — gere sem máscara (`includeFormatSymbols: false`).
- **Não use** primary constructors — declare `private readonly` + construtor explícito.
- **Não capture** `IServiceProvider`, `AppDbContext` ou `IConnectionMultiplexer` no construtor — use scopes via `Fixture.Factory.Services.CreateAsyncScope()` no método de teste.

---

## 5. Estrutura do método (AAA)

Use o padrão **Arrange / Act / Assert** com comentários. Omita seções vazias:

```csharp
[Fact]
public async Task Post_ShouldReturn201_WhenRequestIsValid()
{
    // Act
    var response = await Client.PostAsJsonAsync("api/natural-person", _request);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.Created);
}

[Fact]
public async Task Post_ShouldReturn409_WhenCpfAlreadyExists()
{
    // Arrange — cadastra a primeira vez
    var first = await Client.PostAsJsonAsync("api/natural-person", _request);
    first.StatusCode.ShouldBe(HttpStatusCode.Created);

    // Arrange — segundo request com mesmo CPF, e-mail diferente
    var duplicate = new RequestRegisterNaturalPerson
    {
        Email = new Faker().Internet.Email(),
        Cpf = _request.Cpf,
        FullName = _request.FullName,
        Journey = _request.Journey,
        BirthDate = _request.BirthDate,
    };

    // Act
    var response = await Client.PostAsJsonAsync("api/natural-person", duplicate);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
}
```

Regras do método:

- Se o caminho feliz não precisa de Arrange (o `_request` do construtor já basta), **não escreva** o comentário `// Arrange`.
- **Sempre** assertar o `HttpStatusCode` primeiro — é o contrato HTTP.
- Verifique efeitos colaterais no banco com scope: `await using var scope = Fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();`.
- Métodos de teste são **`async Task`** (nunca `async void`).
- **Um `Should*` por afirmação**. Não combine asserções com `&&`.
- **Não asserte mensagens de erro literais** do `ResponseError` — asserte apenas `StatusCode` e estrutura. Mensagens podem mudar e são responsabilidade do teste de unidade do Validator.
- Para testes que precisam de JWT, use `CreateAuthenticatedClient(jwt)` e descarte o cliente retornado (`using var authClient = ...;`).

---

## 6. Helpers de auth e semeação (`IntegrationTestBase`)

A `IntegrationTestBase` expõe helpers para os fluxos mais comuns. **Use-os** em vez de duplicar lógica:

| Helper | Uso |
| --- | --- |
| `CreateAndLoginNaturalPersonAsync(journey)` | Registra PF via endpoint, confirma OTP, faz login. Retorna `(email, jwt)`. Default journey: `Depositor`. |
| `CreateAdminAndLoginAsync()` | Semeia `NaturalPerson` com `Role.Admin` direto no `DbContext` (sem endpoint público), confirma e faz login. Retorna `(email, jwt)`. |
| `LoginAsync(email)` | Solicita OTP de login, captura do stub e troca por JWT. Retorna o `AccessToken`. |
| `CreateAuthenticatedClient(jwt)` | Cria `HttpClient` com `Authorization: Bearer <jwt>`. Caller é responsável por `Dispose`. |
| `CreatePendingLegalPersonAsync(responsiblePersonId)` | Semeia `LegalPerson` com journey `CollectPoint` em status `PendingApproval` direto no banco. |
| `GetCapturingEmailSender()` | Retorna o stub de e-mail para inspecionar códigos capturados. |

Regras dos helpers:

- **Não bypasse JWT.** O fluxo de auth é parte do contrato — use os helpers acima, que executam o fluxo real (HTTP).
- **Admin** sempre via seed direto no `DbContext` (não há endpoint público). Demais perfis via `CreateAndLoginNaturalPersonAsync`.
- Conflitos de namespace `Journey`: aliases já estão no `IntegrationTestBase` — `ApiEnums = Ecocell.Api.Enums` e `SharedEnums = Ecocell.Shared.Enums`. Em testes, importe `using Ecocell.Shared.Enums;` (DTOs públicos usam o `Shared`).

---

## 7. Stubs (`Stubs/`)

Apenas dois serviços externos têm stub — qualquer outra dependência roda real:

- **`CapturingEmailSender`** (`IEmailSender`) — captura plain-text dos códigos OTP passados via `variables["code"]` em `SendAsync`. Usado para extrair códigos de confirmação e login. **Não** lê do Redis (o store grava o hash, não o código).
- **`StubGeocodingService`** (`IGeocodingService`) — retorna sempre `(-23.5, -46.6)`. Evita chamada HTTP a Nominatim em CI.

Regras:

- **Não crie novos stubs** sem justificativa. Adicionar mock para um serviço de domínio (ex.: `IRepository`, `IValidator`, `IHandler`) viola o objetivo do projeto.
- Stubs novos só são aceitáveis para serviços externos não-determinísticos (HTTP a terceiros, geração de tempo, randomness sem seed).
- Stubs ficam em `tests/Ecocell.IntegrationTests/Stubs/<Nome>.cs`, registrados como singleton em `IntegrationTestFixture.ConfigureServices`.

---

## 8. O que NÃO fazer

- ❌ **Não mocke** `AppDbContext`, `IConnectionMultiplexer`, `IJwtTokenService` ou qualquer dependência interna. A stack de integração é real ponta a ponta.
- ❌ **Não use** SQLite in-memory ou banco compartilhado — o ponto do projeto é PostgreSQL real via Testcontainers.
- ❌ **Não compartilhe estado** entre testes — `InitializeAsync` reseta o banco e limpa o stub de e-mail por teste.
- ❌ **Não dependa da ordem** de execução dos testes (xUnit não garante).
- ❌ **Não asserte logs** — o Logger é silencioso no ambiente `Testing`.
- ❌ **Não bypasse JWT** com `TestAuthenticationHandler` ou similar — use os helpers de auth da base.
- ❌ **Não hardcode** dados realistas (CPF "123.456.789-09", e-mail "joao@teste.com") — use `Faker`.
- ❌ **Não asserte mensagens de erro** literais — asserte apenas `StatusCode` e estrutura do payload.
- ❌ **Não suba containers** dentro do teste — a fixture cuida disso.
- ❌ **Não rode migrations manualmente** dentro do teste — `InitializeAsync` da base faz isso.
- ❌ **Não use** `Task.Delay`, `Thread.Sleep` ou `await Task.Yield()` para "estabilizar" testes — fluxos assíncronos da API são deterministicamente aguardados pela response HTTP.

---

## 9. Cobertura mínima por endpoint

Para considerar um endpoint "verde", o teste de integração deve cobrir, no mínimo:

1. **Caminho feliz** — request válido → status code de sucesso esperado (200/201/202/204).
2. **Falha de autenticação** (quando endpoint exige auth) — sem token → `401`. Token de role insuficiente → `403`.
3. **Conflito de unicidade** (quando aplicável) — duplica via segundo `POST` ou semeia direto no banco antes do teste e espera `409`.
4. **Recurso não encontrado** (quando o endpoint busca antes de operar) — request com id inexistente → `404`.

> **Cobertura de validação de input** é responsabilidade do **teste de unidade** (`Validator` + `Handler`). O teste de integração foca no contrato HTTP ponta a ponta, não em variações de payload inválido. Se quiser cobrir uma combinação específica que o unit test não pega (ex.: serialização de tipo customizado), use `[Theory]` com poucos casos.

### Auth helpers e cenários típicos

| Endpoint exige | Helper a usar | Asserção de erro |
| --- | --- | --- |
| Anônimo | `Client` (já criado no `InitializeAsync`) | n/a |
| Autenticado (qualquer role) | `CreateAndLoginNaturalPersonAsync` + `CreateAuthenticatedClient(jwt)` | sem token → `401` |
| Role `Admin` | `CreateAdminAndLoginAsync` + `CreateAuthenticatedClient(jwt)` | role insuficiente → `403` |

---

## 10. `[Fact]` vs `[Theory]`

- **`[Fact]`** — um cenário fixo (caminho feliz, conflito específico, falha de auth).
- **`[Theory]` + `[InlineData]`** — apenas para variações **HTTP-significativas** (ex.: vários status codes esperados para diferentes inputs). Não use para cobrir variações de validação — isso é trabalho do teste de unidade.