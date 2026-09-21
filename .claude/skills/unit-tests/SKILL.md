---
name: unit-tests
description: Gera testes de unidade para um slice/feature do EcoCell seguindo TDD (Red→Green→Refactor), stack xUnit+Shouldly+Moq+Bogus e as regras normativas de `.claude/rules/unit-tests-rules.md`. Use quando o usuário pedir para escrever, criar ou adicionar testes a uma feature específica.
---

# Workflow: escrever testes de unidade

> Regras normativas (stack, proibições, nomenclatura) estão em `.claude/rules/unit-tests-rules.md` e têm precedência sobre qualquer decisão pontual. Este arquivo define **como executar** o processo.

---

## 1. Identificar o alvo

Leia o slice de produção antes de escrever qualquer linha de teste:

- Arquivo: `src/Ecocell.Api/Features/<Agregado>/<Feature>.cs`
- Mapeie: propriedades do `Command`, regras do `Validator` (campos, formatos, faixas), branches do `Handler` (validação falhou, conflitos de unicidade, not found, sucesso).

---

## 2. Planejar os casos de teste

Liste os testes antes de codificar. Cobertura mínima obrigatória:

| Prioridade | Tipo | Cenário |
|---|---|---|
| 1 | `[Fact]` | Caminho feliz — request válido → `IsSuccess` + efeito persistido no banco |
| 2 | `[Theory]` | Um por campo com regra não-trivial (formato, faixa, obrigatoriedade) |
| 3 | `[Fact]` | Um por conflito de unicidade (CPF duplicado, e-mail duplicado, etc.) |
| 4 | `[Fact]` | Um por branch de erro adicional do `Handler` (not found, regra de negócio, etc.) |

---

## 3. Ciclo TDD — Red → Green → Refactor

Para **cada** caso de teste, na ordem:

1. **Red** — escreva o teste e execute `dotnet test`. O teste deve falhar.
2. **Green** — implemente o mínimo necessário no código de produção para o teste passar.
3. **Refactor** — limpe o código sem quebrar os testes verdes.

Nunca pule para Green sem ver o Red primeiro.

---

## 4. Criar o arquivo de teste

Caminho obrigatório (espelha a árvore de slices):

```
tests/Ecocell.UnitTests/Features/<Agregado>/<Feature>Tests.cs
```

Template completo:

```csharp
namespace Ecocell.UnitTests.Features.<Agregado>;

public class <Feature>Tests : TestBase
{
    private readonly <Feature>.Handler _handler;
    private readonly <Feature>.Validator _validator;
    private readonly <Feature>.Command _command;

    public <Feature>Tests()
    {
        _validator = new <Feature>.Validator();
        var loggerMock = CreateLoggerMock<<Feature>.Handler>();

        _handler = new <Feature>.Handler(DbContext, loggerMock.Object, _validator);

        _command = new Faker<<Feature>.Command>()
            .RuleFor(x => x.Campo, f => /* dado válido */)
            // demais campos com Bogus — use f.Person.Cpf(includeFormatSymbols: false) para CPF
            .Generate();
    }

    // ── Caminho feliz ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ShouldPersistInDatabase_WhenRequestIsValid()
    {
        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var exists = await DbContext.<DbSet>.AnyAsync(x => x.Campo == _command.Campo);
        exists.ShouldBeTrue();
    }

    // ── Validação de input ───────────────────────────────────────────────────

    [Theory]
    [InlineData("valor-inválido-1")]
    [InlineData("valor-inválido-2")]
    public async Task Handle_ShouldReturnFailure_WhenCampoIsInvalid(string valor)
    {
        // Arrange
        _command.Campo = valor;

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Messages.ShouldHaveSingleItem();
    }

    // ── Conflito de unicidade ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenCampoUnicoAlreadyExists()
    {
        // Arrange
        DbContext.<DbSet>.Add(new <Entidade>(/* mesmos dados de _command */));
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(_command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ErrorCodes.Conflict);
    }
}
```

---

## 5. Checklist antes de finalizar

Confirme cada item antes de declarar a tarefa concluída:

- [ ] Classe estende `TestBase` — sem `AppDbContext` ou `SqliteConnection` manual
- [ ] `Validator` real instanciado no construtor — nunca mockado
- [ ] Logger criado via `CreateLoggerMock<T>()` da `TestBase`
- [ ] `_command` gerado com `Bogus.Faker<T>`; CPF/CNPJ com `Bogus.Extensions.Brazil` sem máscara
- [ ] Strings inválidas hardcoded apenas em `[InlineData]`, nunca em `Faker`
- [ ] Todos os métodos são `async Task` com `CancellationToken.None`
- [ ] Asserções com `Shouldly` — uma por linha, sem `&&`
- [ ] Nomes no padrão `Handle_Should<Resultado>_When<Condição>` (inglês)
- [ ] Efeitos no banco verificados via `DbContext.<DbSet>.AnyAsync/FirstOrDefaultAsync`
- [ ] Todos os branches do `Handler` cobertos (validação, unicidade, demais erros)

---

## 6. Executar e confirmar

```bash
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj
```

A tarefa só está concluída quando **todos** os testes passarem.
Para rodar um teste isolado durante o ciclo Red→Green:

```bash
dotnet test --filter "FullyQualifiedName~<Feature>Tests.<NomeDoTeste>"
```
