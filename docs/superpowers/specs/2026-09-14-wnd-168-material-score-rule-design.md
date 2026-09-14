# WND-168 — US011.A — Regra de pontuação por material — Design

**Issue:** [WND-168](https://linear.app/wnd-dev/issue/WND-168/us011-tabela-de-pontuacao-por-material)

**Escopo:** US011.A — Entidade e migration

**Requisito relacionado:** US011 / RF008

**Status:** Aprovado em 2026-09-14

## Objetivo

Criar a fundação relacional e versionada da tabela de pontuação por material de cada Ponto de Coleta. A entrega deve permitir que fluxos posteriores definam novas versões sem apagar o histórico e que descartes armazenem referência à regra vigente no momento da abertura.

## Escopo

US011.A entrega:

- os enums internos e públicos de materiais eletrônicos;
- os enums internos e públicos da unidade de pontuação;
- a entidade `MaterialScoreRule`;
- o mapeamento EF Core;
- o `DbSet` no `AppDbContext`;
- a migration e o snapshot correspondentes;
- testes unitários das invariantes e da persistência.

Não entrega endpoints, DTOs de alteração ou consulta, autorização de gestor, repository, telas Mobile, cálculo de pontos ou crédito de pontuação.

## Catálogo de materiais

Criar `ElectronicMaterial` em `Ecocell.Api.Enums` e `Ecocell.Shared.Enums`, com nomes e valores numéricos idênticos:

```csharp
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

O valor `0` é inválido. Novos materiais exigem evolução explícita dos dois contratos. No contrato público, usar `JsonStringEnumConverter` para representação textual em JSON.

## Unidade de pontuação

Criar `MaterialScoreUnit` em `Ecocell.Api.Enums` e `Ecocell.Shared.Enums`, mantendo paridade de nomes e valores:

```csharp
public enum MaterialScoreUnit
{
    PerUnit = 1,
    PerKilogram = 2,
}
```

`Points` representa a quantidade de pontos concedida por uma unidade ou por `1 kg`, conforme `Unit`. O contrato público também usa representação textual em JSON.

## Modelo de domínio

`MaterialScoreRule` herda de `BaseEntity` e contém:

- `Guid LegalPersonId`;
- navegação `LegalPerson LegalPerson`;
- `ElectronicMaterial Material`;
- `decimal Points`;
- `MaterialScoreUnit Unit`;
- `DateTime ValidFrom`;
- `DateTime? ValidTo`.

O construtor público recebe `legalPersonId`, `material`, `points`, `unit` e `validFrom`. Toda regra nasce aberta, com `ValidTo = null`.

O construtor deve rejeitar:

- `LegalPersonId` vazio;
- material ou unidade fora dos valores definidos;
- `Points <= 0`;
- `ValidFrom` cujo `DateTimeKind` não seja `Utc`.

As propriedades da política são imutáveis depois da criação. A entidade expõe somente `Close(DateTime closedAt)` para encerrar a vigência.

## Vigência e versionamento

O período de vigência é semiaberto: `[ValidFrom, ValidTo)`.

Uma regra está vigente em determinado instante quando:

```text
ValidFrom <= instante AND (ValidTo IS NULL OR ValidTo > instante)
```

`Close` deve:

- aceitar somente horário UTC;
- exigir `closedAt > ValidFrom`;
- falhar se a regra já estiver fechada;
- definir `ValidTo`;
- chamar `MarkAsUpdated()`.

US011.B capturará um único instante de alteração, fechará a regra vigente e inserirá a nova versão dentro da mesma transação. Agendamento de regras futuras fica fora do MVP.

## Persistência

Mapear a entidade para `MaterialScoreRules` com:

- chave e campos de auditoria herdados de `BaseEntityTypeConfiguration<MaterialScoreRule>`;
- FK obrigatória de `LegalPersonId` para `LegalPeople`, usando `DeleteBehavior.Restrict`;
- `Material` convertido para string, obrigatório e limitado a 50 caracteres;
- `Unit` convertido para string, obrigatório e limitado a 20 caracteres;
- `Points` obrigatório como `numeric(10,2)`;
- `ValidFrom` obrigatório como `timestamp with time zone`;
- `ValidTo` opcional como `timestamp with time zone`;
- índice único filtrado por `(LegalPersonId, Material)` quando `ValidTo IS NULL`;
- check constraint `Points > 0`;
- check constraint `ValidTo IS NULL OR ValidTo > ValidFrom`.

Não criar check constraint enumerando materiais ou unidades. Isso evitaria que o catálogo evoluísse sem alteração adicional da estrutura do banco.

O banco garante no máximo uma regra aberta por Ponto de Coleta e material, inclusive sob concorrência. Não será criada exclusion constraint para impedir sobreposição arbitrária entre versões já fechadas; US011.B controlará a sequência de fechamento e inserção.

## Separação de responsabilidades

US011.A conhece apenas a FK para `LegalPerson`. A entidade não consulta banco nem autenticação para validar jornada ou status.

US011.B será responsável por exigir:

- `Journey.CollectPoint`;
- Ponto de Coleta ativo;
- pessoa física autenticada responsável pelo Ponto de Coleta.

Essa separação mantém invariantes locais na entidade e regras dependentes de contexto no slice de aplicação.

## Estratégia de testes

Aplicar TDD Red → Green → Refactor.

Testes da entidade devem provar:

- criação de regra aberta válida;
- rejeição de identificador vazio;
- rejeição de material e unidade inválidos;
- rejeição de pontos zero ou negativos;
- rejeição de `ValidFrom` não UTC;
- fechamento válido e atualização de `UpdatedAt`;
- rejeição de fechamento anterior ou igual ao início;
- rejeição de segundo fechamento.

Testes com SQLite in-memory devem provar:

- persistência e leitura da regra;
- configuração relacional e FK;
- bloqueio de duas regras abertas para o mesmo `LegalPersonId` e material;
- aceitação de nova versão após o fechamento da anterior.

Um teste de contrato deve garantir paridade de nomes e valores entre os enums internos e públicos, pois slices posteriores converterão os contratos entre as duas camadas.

Depois dos testes focados, gerar e revisar a migration `AddMaterialScoreRules`, incluindo `Up`, `Down` e `AppDbContextModelSnapshot`. A verificação final executará a suíte unitária e `dotnet test Ecocell.slnx`; testes de integração exigem Docker disponível.

## Estrutura prevista

- `src/Ecocell.Api/Enums/ElectronicMaterial.cs`
- `src/Ecocell.Api/Enums/MaterialScoreUnit.cs`
- `src/Ecocell.Shared/Enums/ElectronicMaterial.cs`
- `src/Ecocell.Shared/Enums/MaterialScoreUnit.cs`
- `src/Ecocell.Api/Entities/MaterialScoreRule.cs`
- `src/Ecocell.Api/Database/TypeConfiguration/MaterialScoreRuleTypeConfiguration.cs`
- `src/Ecocell.Api/Database/AppDbContext.cs`
- `tests/Ecocell.UnitTests/Entities/MaterialScoreRuleTests.cs`
- migration `AddMaterialScoreRules` e atualização do snapshot.

## Impactos conhecidos

- US011.B deve usar `PerKilogram` e o método `Close` ao versionar regras.
- US011.C consultará regras pela semântica temporal definida neste documento.
- O plano da WND-288 deve substituir a criação direta de regra com `ValidTo` por criação seguida de `Close` nos cenários de teste de regras expiradas.
- WND-288 não deve duplicar `MaterialScoreRule` nem os enums entregues por US011.A.
