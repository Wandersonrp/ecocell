# WND-169 — US005.A — View de ranking de depositantes — Design

**Issue:** [WND-169](https://linear.app/wnd-dev/issue/WND-169/us005-rankings)

**Escopo:** US005.A — View de agregação

**Requisito relacionado:** US005 / RF011

**Status:** Aprovado em 2026-09-19

## Objetivo

Criar a fundação PostgreSQL somente leitura para o ranking histórico de Depositantes PF. A entrega deve disponibilizar, em uma única view comum, os recortes Nacional e Municipal com posições estáveis e sem introduzir cache, atualização programada ou novas fontes de pontuação.

## Decisão de produto para o MVP

Pontos e rankings ficam restritos a Depositantes PF. Pontos de Coleta e Coletores não recebem pontuação nem aparecem em rankings no MVP.

Esta decisão remove da WND-169:

- ranking de Pontos de Coleta;
- ranking de Coletores;
- consolidação Matriz + Filiais;
- `RankingCategory`;
- abas de categoria na Leaderboard.

US005.A depende das entregas da US004 que já fornecem `Discard`, `DepositorScoreTransaction` e `DepositorTotalScore`.

## Escopo

US005.A entrega:

- migration PostgreSQL com a view `v_ranking_depositor`;
- recortes Nacional e Municipal na mesma view, combinados por `UNION ALL`;
- posição calculada com `DENSE_RANK`;
- índice funcional para a chave municipal normalizada;
- testes de integração da migration e da semântica da view.

US005.A não entrega:

- endpoint, request, response ou paginação;
- entidade keyless e mapeamento da view no `AppDbContext`;
- mascaramento de nome ou destaque do usuário autenticado;
- página Mobile;
- tabela, trigger, job, cache, materialized view ou rotina de refresh;
- snapshot de cidade e UF no descarte ou na transação;
- alteração no job de crédito da US004.D;
- ranking periódico, expiração ou reset de pontuação.

O mapeamento de leitura e o contrato HTTP pertencem à US005.B. A apresentação Mobile pertence à US005.C.

## Fontes de dados

### Nacional

O recorte Nacional usa `DepositorTotalScores` como saldo materializado oficial. A consulta relaciona o saldo a `NaturalPeople` e `People` para obter o nome e validar o status atual do depositante.

Entram somente linhas com:

- `People.PersonStatus = Active`;
- `DepositorTotalScores.TotalPoints > 0`.

O total nacional é histórico e acumulado, sem janela temporal.

### Municipal

O recorte Municipal usa somente créditos persistidos em `DepositorScoreTransactions`. A cadeia relacional é:

```text
DepositorScoreTransactions
  -> Discards
  -> LegalPeople (Ponto de Coleta)
  -> Addresses
```

As transações são agrupadas por depositante e município. Como a transação só existe após o crédito, a view não recalcula pontos a partir dos itens do descarte e não duplica a regra RN013.

Um mesmo depositante pode ocupar posições diferentes em municípios distintos.

## Identidade municipal

O município é identificado por duas chaves derivadas do endereço atual do Ponto de Coleta:

```sql
lower(btrim("City"))
upper(btrim("State"))
```

A normalização ignora caixa e espaços externos, mas preserva acentos. O MVP não adiciona código IBGE nem depende da extensão PostgreSQL `unaccent`.

O endereço atual do Ponto de Coleta é a fonte da classificação municipal. Se o endereço textual do estabelecimento for alterado no futuro, os créditos históricos serão reatribuídos ao novo município. Um snapshot de localização só deverá ser introduzido quando houver fluxo de mudança de endereço ou requisito explícito de auditoria geográfica histórica.

Créditos associados a um Ponto de Coleta sem endereço, cidade ou UF válida:

- não aparecem no recorte Municipal;
- permanecem no recorte Nacional.

O status atual do Ponto de Coleta não remove créditos históricos. Somente o status atual do depositante controla sua visibilidade.

## Contrato interno da view

`v_ranking_depositor` expõe:

| Coluna | Tipo lógico | Regra |
| --- | --- | --- |
| `Scope` | texto | `National` ou `Municipal` |
| `State` | texto anulável | chave de UF normalizada; nula no Nacional |
| `City` | texto anulável | chave de cidade normalizada; nula no Nacional |
| `DepositorId` | UUID | uso interno; não integra o contrato público |
| `FullName` | texto | uso interno; será reduzido pela US005.B |
| `TotalPoints` | decimal | soma histórica no recorte correspondente |
| `Position` | inteiro de 64 bits | resultado de `DENSE_RANK` |

As duas consultas devem projetar os mesmos tipos antes do `UNION ALL`. Os valores textuais de `Scope` devem corresponder aos nomes do futuro enum público `RankingScope`.

## Posicionamento

O Nacional calcula:

```sql
dense_rank() over (order by "TotalPoints" desc)
```

O Municipal calcula:

```sql
dense_rank() over (
  partition by "State", "City"
  order by "TotalPoints" desc
)
```

Empates compartilham posição e a sequência não possui lacunas: `1, 1, 2`. Ordenação determinística para apresentação e paginação será responsabilidade da US005.B, usando `Position` e critérios secundários sem alterar a posição calculada.

## Privacidade e fronteira pública

A view mantém `DepositorId` e `FullName` porque a US005.B precisa identificar o usuário autenticado e produzir o nome reduzido. Esses campos são internos ao banco e à API.

O contrato público da US005.B deverá expor somente:

- nome reduzido, como `Ana S.`;
- posição;
- pontos;
- `isCurrentUser`.

CPF, e-mail e identificador interno do depositante não serão expostos.

## Migration e índice

A migration deve usar `MigrationBuilder.Sql`.

No `Up`:

1. criar o índice funcional composto `IX_Addresses_State_City_Normalized` sobre `upper(btrim("State"))` e `lower(btrim("City"))`;
2. criar `v_ranking_depositor` como view PostgreSQL comum;
3. falhar se as tabelas ou colunas obrigatórias da US004 não existirem, sem criar uma estrutura parcial alternativa.

No `Down`:

1. remover `v_ranking_depositor`;
2. remover `IX_Addresses_State_City_Normalized`.

O índice único existente em `DepositorTotalScores.DepositorId` deve ser reutilizado. Os índices já existentes nas FKs de transações e descartes também permanecem. Não criar índice adicional de baixa seletividade para `PersonStatus`.

A migration não altera o modelo de domínio nem adiciona `DbSet`; o snapshot do EF Core permanece semanticamente inalterado.

## Consistência esperada

Para dados em que todos os Pontos de Coleta possuem município válido, a soma dos totais municipais de um depositante deve corresponder ao seu total nacional.

Essa igualdade não é uma garantia global quando houver endereço municipal ausente ou inválido, pois esses créditos continuam no Nacional e são excluídos do Municipal.

## Estratégia de testes

Aplicar TDD Red → Green → Refactor.

Os testes de US005.A devem ser de integração com PostgreSQL/Testcontainers. SQLite não valida a sintaxe da view, as funções PostgreSQL nem o índice funcional.

O primeiro teste deve falhar antes da migration por ausência de `v_ranking_depositor`. A cobertura mínima deve provar:

- criação da view e do índice funcional após a migration;
- uso de `DepositorTotalScores` no Nacional;
- agregação municipal por cidade e UF do Ponto de Coleta;
- normalização de caixa e espaços;
- participação do mesmo depositante em municípios diferentes;
- exclusão de depositantes não ativos;
- exclusão municipal, mas preservação nacional, quando o PC não possui município válido;
- preservação de créditos quando o PC deixa de estar ativo;
- posições `1, 1, 2` em empate;
- equivalência entre soma municipal e total nacional para dados consistentes.

Testes de endpoint, paginação, mascaramento de nome e autorização ficam para US005.B. Testes Mobile ficam para US005.C.

## Validação final

Executar:

```bash
dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj
dotnet test Ecocell.slnx
dotnet ef migrations has-pending-model-changes --project src/Ecocell.Api --startup-project src/Ecocell.Api
git diff --check
```

Docker deve estar disponível para os testes de integração.

## Estrutura prevista

- migration `AddDepositorRankingView` em `src/Ecocell.Api/Migrations/`;
- designer gerado da migration;
- `AppDbContextModelSnapshot.cs` revisado, sem mudança semântica do modelo;
- `tests/Ecocell.IntegrationTests/Features/Ranking/DepositorRankingViewTests.cs`.

## Impactos nas próximas etapas

US005.B deve:

- criar `RankingScope { Municipal, National }`;
- mapear a view como leitura keyless;
- exigir cidade e UF somente no recorte Municipal;
- normalizar o filtro com as mesmas regras da view;
- preservar `Position` durante a paginação;
- reduzir o nome e calcular `isCurrentUser` sem expor `DepositorId`.

US005.C deve oferecer uma única Leaderboard de Depositantes PF, com alternância Municipal/Nacional e seleção de cidade + UF, sem abas de categoria.
