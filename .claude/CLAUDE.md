# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Regras do projeto

- **Codificação** (arquitetura VSA, padrão de slice, summaries em PT-BR, KISS, DRY e convenções do repositório): [`.claude/rules/coding-rules.md`](rules/coding-rules.md). Consulte antes de criar ou alterar qualquer código de produção.
- **Testes de unidade** (stack, `TestBase`, nomenclatura, setup com Bogus, padrão AAA, cobertura mínima por slice): [`.claude/rules/unit-tests-rules.md`](rules/unit-tests-rules.md). Consulte antes de escrever ou alterar qualquer teste de unidade.
- **Testes de integração** (Testcontainers, `IntegrationTestFixture`/`IntegrationTestBase`, helpers de auth, stubs, cobertura mínima por endpoint): [`.claude/rules/integration-tests-rules.md`](rules/integration-tests-rules.md). Consulte antes de escrever ou alterar qualquer teste de integração.

Esses arquivos têm precedência sobre decisões pontuais.

---

## Documento de Requisitos do Produto (PRD)

O PRD oficial do EcoCell está na raiz do repositório: [`PRD EcoCell – Ecossistema de Logística Reversa.md`](../PRD%20EcoCell%20%E2%80%93%20Ecossistema%20de%20Log%C3%ADstica%20Reversa.md).

Consulte o PRD antes de iniciar qualquer nova funcionalidade para entender as personas (Depositante, Ponto de Coleta, Coletor), os requisitos funcionais (RF001–RF014), as user stories (US001–US014) e as **regras de negócio (RN001–RN017, seção 5)**. As regras de negócio são **normativas** e têm precedência sobre decisões de implementação — verifique RN003–RN005 (restrições de perfil), RN009 (geolocalização obrigatória), RN010 (login por documento ou e-mail), RN013 (Admin/Suporte não pontuam), RN015 (cálculo de ranking Matriz/Filial) e RN017 (Selos ESG) antes de definir escopo.

---

## Regra de TDD (obrigatória)

Toda **nova funcionalidade**, **correção de bug** e **refatoração** deve seguir o ciclo **Red → Green → Refactor**:

1. **Red** — escreva primeiro o teste que reproduz o comportamento desejado (ou o bug) e veja-o falhar.
2. **Green** — implemente o mínimo necessário para o teste passar.
3. **Refactor** — melhore o código mantendo os testes verdes.

Para bugs: o teste deve reproduzir o defeito antes de qualquer alteração no código de produção. Para refatorações: garanta cobertura existente antes de alterar; se não houver, escreva-a primeiro.

### Execução de testes após cada alteração

Ao final de **qualquer** criação ou alteração de código, execute a suíte de testes e só considere a tarefa concluída se todos passarem:

```bash
dotnet test Ecocell.slnx
```

Para rodar apenas o projeto de testes:

```bash
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj
```

Para rodar um único teste por nome (xUnit):

```bash
dotnet test --filter "FullyQualifiedName~RegisterNaturalPersonTests.Handle_ShouldPersistInDatabase_WhenRequestIsValid"
```

---

## Comandos comuns

| Objetivo | Comando |
| --- | --- |
| Restaurar dependências | `dotnet restore Ecocell.slnx` |
| Build da solução | `dotnet build Ecocell.slnx` |
| Rodar a API | `dotnet run --project src/Ecocell.Api/Ecocell.Api.csproj` |
| Rodar todos os testes | `dotnet test Ecocell.slnx` |
| Nova migration EF Core | `dotnet ef migrations add <Nome> --project src/Ecocell.Api --startup-project src/Ecocell.Api` |
| Aplicar migrations | `dotnet ef database update --project src/Ecocell.Api --startup-project src/Ecocell.Api` |

> A solução usa **.NET 10** (`net10.0`). Garanta que o SDK correto está instalado.
> A API expõe Scalar em `/scalar/v1` (perfil `https`: `https://localhost:7284`; perfil `http`: `http://localhost:5207`).

---

## Stack de testes (`tests/Ecocell.UnitTests/`)

- **xUnit + Shouldly + Moq + Bogus**. Banco de testes via **Sqlite in-memory** (não PostgreSQL).
- `TestBase` cria um `AppDbContext` por teste com `Sqlite` `:memory:`, expõe `DbContext` e `CreateLoggerMock<T>()`. **Estenda `TestBase`** em vez de instanciar contexto manualmente.
- Estrutura espelha as features: `tests/Ecocell.UnitTests/Features/<Agregado>/<Feature>Tests.cs`.
- Use `Bogus.Faker<TCommand>` (com `Bogus.Extensions.Brazil` para `Cpf`/`Cnpj`) para gerar dados — não hardcode strings inválidas a menos que o teste exija.
- Asserções com `Shouldly` (`result.IsSuccess.ShouldBeTrue()`, `result.Error.Messages.ShouldHaveSingleItem()`).

## Stack de testes (`tests/Ecocell.IntegrationTests/`)

- **xUnit + Shouldly + Bogus + Testcontainers + WebApplicationFactory**. PostgreSQL e Redis reais via Testcontainers — sem mocks de infraestrutura.
- `IntegrationTestFixture` sobe containers uma vez por suite (`ICollectionFixture`). `IntegrationTestBase` reseta banco por teste (`EnsureDeletedAsync + MigrateAsync`). **Estenda `IntegrationTestBase`**.
- Estrutura espelha as features: `tests/Ecocell.IntegrationTests/Features/<Agregado>/<Feature>Tests.cs`.
- Auth via fluxo real (HTTP): `CreateAndLoginNaturalPersonAsync()` e `CreateAdminAndLoginAsync()` em `IntegrationTestBase`.
- `CapturingEmailSender` (stub de `IEmailSender`) captura códigos OTP para uso nos testes. `StubGeocodingService` retorna coordenadas fixas (-23.5, -46.6).
- Requer **Docker** em execução.
