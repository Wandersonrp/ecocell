# Regras de Codificação — EcoCell

Estas regras são **normativas** para todo código de produção do projeto (`src/Ecocell.Api/` e `src/Ecocell.Shared/`). Para regras de teste (TDD, execução, stack), ver `CLAUDE.md`.

---

## 1. Arquitetura — Vertical Slice Architecture (VSA)

A solução (`Ecocell.slnx`) tem dois projetos de produção:

```
src/Ecocell.Api/    ← API (host + features + persistência + entidades)
src/Ecocell.Shared/ ← Contratos HTTP (Requests/Responses/Enums) reutilizáveis por clientes
```

Toda a lógica de uma feature vive em **um único arquivo** dentro de `Ecocell.Api`.

### `src/Ecocell.Api/` — organização por slices

| Pasta | Responsabilidade |
| --- | --- |
| `Features/<Agregado>/<UseCase>.cs` | **Slice completo**: `Command` (request interno), `Validator` (FluentValidation), `Handler` (Mediator) e `Endpoint` (Carter `ICarterModule`). Tudo no mesmo arquivo. |
| `Entities/` | Entidades EF Core (ex.: `Person`, `NaturalPerson`, `LegalPerson`, `BaseEntity`). Setters protegidos/privados, construção via construtor. |
| `Enums/` | Enums de domínio interno (`Role`, `PersonType`, `PersonStatus`, `Journey`, `LegalPersonType`). |
| `Database/AppDbContext.cs` | Único `DbContext` (PostgreSQL via `Npgsql`). Aplica `IEntityTypeConfiguration` por convenção. |
| `Database/TypeConfiguration/` | `IEntityTypeConfiguration<T>` por entidade (TPT/herança via discriminator). |
| `Migrations/` | Migrations EF Core (vivem **dentro** do projeto API, não em Infrastructure). |
| `Configurations/` | Bindings de `appsettings` validados (`DatabaseSettings`, etc.). |
| `Extensions/DependencyInjectionExtensions.cs` | Composição da DI: `AddApi(configuration)` registra Mediator, DbContext, Carter, FluentValidation, Serilog, Settings. |
| `Extensions/ResultExtensions.cs` | `Result.ToProcessResult(...)` converte `Result`/`ResultT<T>` → `IResult` HTTP (mapeia código de erro → status). |
| `Extensions/FluentValidationExtensions.cs` | Regras reutilizáveis (`IsValidCpf`, `IsValidCnpj`). |
| `Middlewares/` | `CorrelationIdMiddleware`, `ExceptionHandlerMiddleware` (fallback 500). |
| `Shared/` | Tipos transversais à API: `Result`, `ResultT<T>`, `Error`, `ErrorCodes`, `Utils/DocumentValidator`. |

### Padrão de uma feature (template)

```csharp
namespace Ecocell.Api.Features.Person;

public static class RegisterNaturalPerson
{
    public record Command : IRequest<Result> { /* propriedades */ }

    public class Validator : AbstractValidator<Command>
    {
        public Validator() { /* regras */ }
    }

    public sealed class Handler(AppDbContext db, ILogger<Handler> log, IValidator<Command> validator)
        : IRequestHandler<Command, Result>
    {
        public async ValueTask<Result> Handle(Command request, CancellationToken ct) { /* ... */ }
    }
}

public class RegisterNaturalPersonEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app) =>
        app.MapPost("api/natural-person", async ([FromBody] RequestRegisterNaturalPerson req, ISender sender) =>
        {
            var result = await sender.Send(/* mapear req → Command */);
            return result.ToProcessResult(StatusCodes.Status201Created);
        });
}
```

### Convenções obrigatórias do slice

- **Um slice = um arquivo** sob `Features/<Agregado>/<NomeDaFeature>.cs` contendo `Command`, `Validator`, `Handler` e o `ICarterModule` correspondente.
- O endpoint recebe o **DTO público** de `Ecocell.Shared.Requests` e mapeia para o `Command` interno; **não exponha o `Command` na borda HTTP**.
- O `Handler` **sempre** dispara o `Validator` antes de tocar o banco e retorna `Result.Failure(Error.ErrorOnValidation(...))` em caso de falha.
- Erros conhecidos usam as factories de `Error` (`Conflict`, `NotFound`, `InvalidCredential`, `ErrorOnValidation`, `Unauthorized`, `Forbidden`). **Não crie nova hierarquia de erro.**
- O endpoint termina com `result.ToProcessResult(<statusCodeDeSucesso>)` — não monte `IResult` manualmente.
- Carter, Mediator (handlers) e FluentValidation (validators) são **descobertos por assembly scan**; basta criar a classe no projeto `Ecocell.Api`.

### `src/Ecocell.Shared/` — contratos HTTP

- `Requests/` (ex.: `RequestRegisterNaturalPerson`) e `Responses/` (ex.: `ResponseError`).
- `Enums/` expostos no contrato (ex.: `Journey`, `PersonType`).
- Sem dependências em `Ecocell.Api`. Esses tipos são consumidos pela API e por **qualquer cliente externo** (apps, integrações). Mantenha-os simples e estáveis.

---

## 2. Summaries em PT-BR

**Toda classe e todo método público** (incluindo `Command`, `Validator`, `Handler`, `ICarterModule`, entidades, configurações de DI, extensões) deve ter um `/// <summary>` em **português do Brasil**, descrevendo objetivo e, quando útil, parâmetros e retorno com `<param>` / `<returns>`. O código é lido por equipe lusófona — clareza em PT-BR é requisito, não estilo.

```csharp
/// <summary>
/// Executa o cadastro de uma nova pessoa física, validando CPF e e-mail únicos
/// antes de persistir o registro.
/// </summary>
/// <param name="request">Comando com os dados do cadastro.</param>
/// <param name="cancellationToken">Token de cancelamento da operação.</param>
/// <returns><see cref="Result"/> indicando sucesso ou o erro encontrado.</returns>
public async ValueTask<Result> Handle(Command request, CancellationToken cancellationToken) { ... }
```

---

## 3. KISS — Keep It Simple, Stupid

VSA já é a abstração: **não** crie camadas extras (services, repositories, mappers genéricos) por cima de um slice. Se o `Handler` precisa do `AppDbContext`, injete o `AppDbContext`. Crie abstração apenas quando houver hoje **dois consumidores reais**. "Talvez precisemos no futuro" **não** é justificativa.

---

## 4. DRY — Don't Repeat Yourself

Antes de criar lógica nova, procure no repositório:

- **Validações reutilizáveis**: `Ecocell.Api/Extensions/FluentValidationExtensions.cs` (`IsValidCpf`, `IsValidCnpj`); `Ecocell.Api/Shared/Utils/DocumentValidator.cs`.
- **Resultado/erro**: `Ecocell.Api/Shared/Result`, `ResultT<T>`, `Error`, `ErrorCodes`. Não crie outra hierarquia.
- **Mapeamento de erro → HTTP**: `ResultExtensions.ToProcessResult(...)`. Não escreva `Results.Problem(...)` ad-hoc no endpoint.
- **DTOs de request/response**: `Ecocell.Shared/Requests` e `Ecocell.Shared/Responses`.

Se a mesma regra aparecer em dois slices (formatação de documento, política de pontuação, cálculo de elegibilidade etc.), extraia:

- regra de domínio puro → método estático em `Ecocell.Api/Shared/` ou na própria entidade;
- regra de validação de input → extensão em `FluentValidationExtensions`;
- contrato HTTP repetido → `Ecocell.Shared`.

**Não compartilhe `Command`/`Handler` entre slices** — duplicar um pequeno pedaço de orquestração entre dois slices é preferível a acoplá-los.

---

## 5. Convenções do repositório

- Idioma: **PT-BR** em summaries, mensagens de erro, commits e documentação. Nomes de identificadores permanecem em inglês.
- `.editorconfig` na raiz define indentação de 4 espaços e `crlf` para arquivos `.cs` — respeite.
- Migrations EF Core vivem em `src/Ecocell.Api/Migrations/` (geradas via `--project src/Ecocell.Api --startup-project src/Ecocell.Api`).
- Logs estruturados via **Serilog** com `CorrelationId` (middleware). Use `_logger.LogInformation("... {@Objeto}", obj)` (destructuring) em vez de interpolação.
- Segredos nunca devem ser comitados. `appsettings.Development.json` e User Secrets ficam fora do controle de versão.
