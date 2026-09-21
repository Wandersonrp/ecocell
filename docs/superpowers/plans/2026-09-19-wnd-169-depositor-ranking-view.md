# WND-169 Depositor Ranking View Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar a migration PostgreSQL e os testes de integração da `v_ranking_depositor`, com rankings históricos Nacional e Municipal exclusivos para Depositantes PF.

**Architecture:** Uma view PostgreSQL comum combina dois conjuntos por `UNION ALL`: o Nacional lê `DepositorTotalScores`, e o Municipal agrega `DepositorScoreTransactions` pela cidade e UF atuais do Ponto de Coleta. A migration também cria um índice funcional para a chave municipal normalizada; não há entidade de escrita, cache, materialized view, trigger ou alteração no job de crédito.

**Tech Stack:** .NET 10, EF Core 10, Npgsql/PostgreSQL 16, migrations com `MigrationBuilder.Sql`, xUnit, Shouldly e Testcontainers.

**Spec:** `docs/superpowers/specs/2026-09-19-wnd-169-depositor-ranking-view-design.md`

## Global Constraints

- Antes de executar, ler `AGENTS.md` e a specification. Os arquivos de regras referenciados pelo `AGENTS.md` não existem no checkout usado para escrever este plano; se estiverem presentes no checkout de execução, lê-los antes de alterar código.
- Confirmar que `HEAD` contém o commit da specification `75b94b6` e que a branch de trabalho deriva de `feature/ranking`.
- Aplicar Red → Green → Refactor: o teste de integração deve falhar antes da migration e passar somente depois do SQL final.
- Manter o escopo em US005.A: sem endpoint, DTO, entidade keyless, `DbSet`, Mobile, paginação, mascaramento de nome ou autorização.
- Incluir somente Depositantes PF ativos, com pontuação maior que zero e histórico acumulado sem reset.
- Usar `lower(btrim("City"))` e `upper(btrim("State"))`; preservar acentos e não habilitar `unaccent`.
- Usar o endereço atual do Ponto de Coleta. Não adicionar snapshot de cidade/UF a `Discard` ou `DepositorScoreTransaction`.
- Pontos sem município válido permanecem no Nacional e não aparecem no Municipal.
- O status atual do Ponto de Coleta não remove créditos; o status atual do depositante controla sua visibilidade.
- Calcular posição com `DENSE_RANK`; empates seguem `1, 1, 2`.
- A view é comum, não materializada. Não criar cache, refresh, tabela, trigger, backfill ou novo pacote.
- Usar RTK nos comandos suportados. Usar `dotnet ef` diretamente para comandos EF, pois o wrapper RTK não suporta esse subcomando neste repositório.
- Docker deve estar disponível para os testes de integração PostgreSQL.
- Preservar `.claude/settings.local.json` e qualquer outra alteração alheia. Fazer staging somente pelos caminhos desta tarefa e executar `git diff --cached --check`.

## Review Focus

- Municípios com o mesmo nome em UFs diferentes devem formar partições diferentes; `São Paulo/SP` nunca pode somar com `São Paulo/RJ`.
- Diferenças apenas de caixa ou espaços externos devem convergir para a mesma chave municipal.
- Endereço ausente ou cidade/UF em branco deve excluir apenas o recorte Municipal, sem reduzir o Nacional.
- Um depositante suspenso depois de pontuar deve desaparecer dos dois recortes imediatamente.
- Um Ponto de Coleta suspenso depois do crédito não deve apagar nem esconder pontos históricos.

---

## File Map

- `tests/Ecocell.IntegrationTests/Features/Ranking/DepositorRankingViewTests.cs`: semeia saldos e créditos diretamente no PostgreSQL e valida objetos, agregações, normalização, elegibilidade e posições da view.
- `src/Ecocell.Api/Migrations/*_AddDepositorRankingView.cs`: migration gerada pelo EF e preenchida com o índice funcional e a view. O prefixo numérico exato é definido pelo EF no momento da execução.
- `src/Ecocell.Api/Migrations/*_AddDepositorRankingView.Designer.cs`: metadados gerados pelo EF; não editar manualmente.
- `src/Ecocell.Api/Migrations/AppDbContextModelSnapshot.cs`: snapshot gerado e revisado; a migration SQL não deve introduzir mudança semântica no modelo EF.

---

### Task 1: Criar e validar a view de ranking de depositantes

**Files:**
- Create: `tests/Ecocell.IntegrationTests/Features/Ranking/DepositorRankingViewTests.cs`
- Create: `src/Ecocell.Api/Migrations/*_AddDepositorRankingView.cs`
- Create: `src/Ecocell.Api/Migrations/*_AddDepositorRankingView.Designer.cs`
- Modify: `src/Ecocell.Api/Migrations/AppDbContextModelSnapshot.cs` somente se o scaffold do EF alterar metadados gerados

**Interfaces:**
- Consumes: tabelas `People`, `NaturalPeople`, `LegalPeople`, `Addresses`, `Discards`, `DepositorScoreTransactions` e `DepositorTotalScores` entregues pela US004.
- Produces: view somente leitura `v_ranking_depositor("Scope", "State", "City", "DepositorId", "FullName", "TotalPoints", "Position")` e índice `IX_Addresses_State_City_Normalized`.
- Leaves for US005.B: entidade keyless, `RankingScope`, filtro, paginação, nome reduzido e `isCurrentUser`.

- [ ] **Step 1: Confirmar branch, escopo e baseline**

Run:

```powershell
rtk git status --short --branch
rtk git merge-base --is-ancestor 75b94b6 HEAD
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --no-restore
```

Expected:

- branch derivada de `feature/ranking`;
- `git merge-base` retorna exit code `0`;
- `.claude/settings.local.json` pode continuar não versionado e não será tocado;
- suíte de integração existente passa antes da alteração.

- [ ] **Step 2: Escrever o teste de integração completo**

Criar `tests/Ecocell.IntegrationTests/Features/Ranking/DepositorRankingViewTests.cs` com esta estrutura e estes cinco cenários:

```csharp
using System.Data;
using Bogus;
using Bogus.Extensions.Brazil;
using Ecocell.Api.Database;
using Ecocell.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ApiEnums = Ecocell.Api.Enums;

namespace Ecocell.IntegrationTests.Features.Ranking;

public sealed class DepositorRankingViewTests(IntegrationTestFixture fixture)
    : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Migration_ShouldCreateViewAndFunctionalIndex()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var viewExists = await ScalarAsync<bool>(db, """
            SELECT EXISTS (
                SELECT 1
                FROM pg_views
                WHERE schemaname = 'public'
                  AND viewname = 'v_ranking_depositor');
            """);
        var indexExists = await ScalarAsync<bool>(db, """
            SELECT EXISTS (
                SELECT 1
                FROM pg_indexes
                WHERE schemaname = 'public'
                  AND indexname = 'IX_Addresses_State_City_Normalized');
            """);

        viewExists.ShouldBeTrue();
        indexExists.ShouldBeTrue();
    }

    [Fact]
    public async Task View_ShouldRankNationalScoresAndExcludeIneligibleDepositors()
    {
        await CreateDepositorAsync("Ana Ativa", 30m, ApiEnums.PersonStatus.Active);
        await CreateDepositorAsync("Bia Ativa", 30m, ApiEnums.PersonStatus.Active);
        await CreateDepositorAsync("Caio Ativo", 10m, ApiEnums.PersonStatus.Active);
        await CreateDepositorAsync("Davi Suspenso", 100m, ApiEnums.PersonStatus.Suspended);
        await CreateDepositorAsync("Eva Pendente", 100m, ApiEnums.PersonStatus.AwaitingConfirmation);
        await CreateDepositorAsync("Fábio Zerado", 0m, ApiEnums.PersonStatus.Active);

        var rows = await QueryRowsAsync("National");

        rows.Select(row => row.FullName)
            .ShouldBe(["Ana Ativa", "Bia Ativa", "Caio Ativo"]);
        rows.Select(row => row.Position).ShouldBe([1L, 1L, 2L]);
        rows.ShouldAllBe(row => row.State is null && row.City is null);
    }

    [Fact]
    public async Task View_ShouldAggregateMunicipalScoresByNormalizedCityAndState()
    {
        var ana = await CreateDepositorAsync(
            "Ana Municipal",
            35m,
            ApiEnums.PersonStatus.Active);
        var bia = await CreateDepositorAsync(
            "Bia Municipal",
            15m,
            ApiEnums.PersonStatus.Active);

        await CreateCreditAsync(ana.Id, 10m, " São Paulo ", " sp ");
        await CreateCreditAsync(ana.Id, 5m, "SÃO PAULO", "SP");
        await CreateCreditAsync(ana.Id, 20m, "São Paulo", "RJ");
        await CreateCreditAsync(bia.Id, 15m, "são paulo", "sp");

        var municipal = await QueryRowsAsync("Municipal");
        var national = await QueryRowsAsync("National");
        var anaSp = municipal.Single(row =>
            row.DepositorId == ana.Id && row.State == "SP" && row.City == "são paulo");
        var biaSp = municipal.Single(row =>
            row.DepositorId == bia.Id && row.State == "SP" && row.City == "são paulo");
        var anaRj = municipal.Single(row =>
            row.DepositorId == ana.Id && row.State == "RJ" && row.City == "são paulo");

        anaSp.TotalPoints.ShouldBe(15m);
        biaSp.TotalPoints.ShouldBe(15m);
        anaSp.Position.ShouldBe(1);
        biaSp.Position.ShouldBe(1);
        anaRj.TotalPoints.ShouldBe(20m);
        anaRj.Position.ShouldBe(1);
        national.Single(row => row.DepositorId == ana.Id).TotalPoints.ShouldBe(35m);
        municipal.Where(row => row.DepositorId == ana.Id)
            .Sum(row => row.TotalPoints)
            .ShouldBe(35m);
    }

    [Fact]
    public async Task View_ShouldKeepNationalScoreAndExcludeMunicipal_WhenLocationIsInvalid()
    {
        var depositor = await CreateDepositorAsync(
            "Ana Sem Município",
            10m,
            ApiEnums.PersonStatus.Active);

        await CreateCreditAsync(
            depositor.Id,
            4m,
            city: null,
            state: null,
            includeAddress: false);
        await CreateCreditAsync(depositor.Id, 6m, " ", " ");

        var municipal = await QueryRowsAsync("Municipal");
        var national = await QueryRowsAsync("National");

        municipal.ShouldNotContain(row => row.DepositorId == depositor.Id);
        national.Single(row => row.DepositorId == depositor.Id)
            .TotalPoints.ShouldBe(10m);
    }

    [Fact]
    public async Task View_ShouldUseCurrentDepositorStatusAndPreserveInactivePointCredits()
    {
        var depositor = await CreateDepositorAsync(
            "Ana Status",
            12m,
            ApiEnums.PersonStatus.Active);
        await CreateCreditAsync(
            depositor.Id,
            12m,
            "Betim",
            "MG",
            suspendCollectorPoint: true);

        var beforeSuspension = await QueryRowsAsync("Municipal");
        beforeSuspension.ShouldContain(row =>
            row.DepositorId == depositor.Id && row.TotalPoints == 12m);

        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.NaturalPeople.SingleAsync(row => row.Id == depositor.Id);
            stored.Block(ApiEnums.Role.Admin);
            await db.SaveChangesAsync();
        }

        (await QueryRowsAsync("Municipal"))
            .ShouldNotContain(row => row.DepositorId == depositor.Id);
        (await QueryRowsAsync("National"))
            .ShouldNotContain(row => row.DepositorId == depositor.Id);
    }

    private async Task<NaturalPerson> CreateDepositorAsync(
        string fullName,
        decimal totalPoints,
        ApiEnums.PersonStatus status)
    {
        var faker = new Faker("pt_BR");
        var person = new NaturalPerson(
            fullName,
            faker.Person.Cpf(includeFormatSymbols: false),
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-25)),
            ApiEnums.Role.User,
            faker.Internet.Email(),
            ApiEnums.Journey.Depositor);

        switch (status)
        {
            case ApiEnums.PersonStatus.Active:
                person.Confirm();
                break;
            case ApiEnums.PersonStatus.Suspended:
                person.Confirm();
                person.Block(ApiEnums.Role.Admin);
                break;
            case ApiEnums.PersonStatus.AwaitingConfirmation:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.People.Add(person);
        if (totalPoints > 0)
            db.DepositorTotalScores.Add(new DepositorTotalScore(person.Id, totalPoints));
        await db.SaveChangesAsync();

        if (totalPoints == 0)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "DepositorTotalScores"
                    ("Id", "DepositorId", "TotalPoints", "CreatedAt", "UpdatedAt")
                VALUES
                    ({Guid.CreateVersion7()}, {person.Id}, {0m}, {DateTime.UtcNow}, NULL);
                """);
        }

        return person;
    }

    private async Task CreateCreditAsync(
        Guid depositorId,
        decimal points,
        string? city,
        string? state,
        bool includeAddress = true,
        bool suspendCollectorPoint = false)
    {
        var faker = new Faker("pt_BR");
        Address? address = null;
        if (includeAddress)
        {
            address = new Address(
                "Rua Teste",
                "1",
                "Centro",
                city ?? string.Empty,
                state ?? string.Empty,
                "01001000");
        }

        var point = new LegalPerson(
            faker.Company.CompanyName(),
            faker.Company.CompanyName(),
            faker.Company.Cnpj(includeFormatSymbols: false),
            faker.Internet.Email(),
            ApiEnums.Journey.CollectPoint,
            addressId: address?.Id);
        point.Approve(ApiEnums.Role.Admin);
        if (suspendCollectorPoint)
            point.Block(ApiEnums.Role.Admin);

        var rule = new MaterialScoreRule(
            point.Id,
            ApiEnums.ElectronicMaterial.Battery,
            points,
            ApiEnums.MaterialScoreUnit.PerUnit,
            DateTime.UtcNow.AddDays(-1));
        var item = new DiscardItem(
            ApiEnums.ElectronicMaterial.Battery,
            1,
            1m,
            rule.Id);
        var discard = new Discard(depositorId, point.Id, [item]);
        discard.Confirm([item]);
        var transaction = new DepositorScoreTransaction(
            discard.Id,
            depositorId,
            points);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (address is not null)
            db.Addresses.Add(address);
        db.People.Add(point);
        db.MaterialScoreRules.Add(rule);
        db.Discards.Add(discard);
        db.DepositorScoreTransactions.Add(transaction);
        await db.SaveChangesAsync();
    }

    private async Task<List<RankingRow>> QueryRowsAsync(string scopeValue)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT "Scope", "State", "City", "DepositorId", "FullName",
                   "TotalPoints", "Position"
            FROM v_ranking_depositor
            WHERE "Scope" = @scope
            ORDER BY "Position", "FullName", "DepositorId";
            """;
        var scopeParameter = command.CreateParameter();
        scopeParameter.ParameterName = "scope";
        scopeParameter.DbType = DbType.String;
        scopeParameter.Value = scopeValue;
        command.Parameters.Add(scopeParameter);

        var rows = new List<RankingRow>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new RankingRow(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetGuid(3),
                reader.GetString(4),
                reader.GetDecimal(5),
                reader.GetInt64(6)));
        }

        return rows;
    }

    private static async Task<T> ScalarAsync<T>(AppDbContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return (T)value!;
    }

    private sealed record RankingRow(
        string Scope,
        string? State,
        string? City,
        Guid DepositorId,
        string FullName,
        decimal TotalPoints,
        long Position);
}
```

- [ ] **Step 3: Executar os testes e confirmar RED**

Run:

```powershell
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~DepositorRankingViewTests"
```

Expected: FAIL. O teste de objetos retorna `false` para view/índice e os testes de consulta falham porque `v_ranking_depositor` ainda não existe. Se falhar por Docker indisponível, iniciar Docker e repetir; isso não conta como RED funcional.

- [ ] **Step 4: Gerar a migration vazia e identificar os arquivos exatos**

Run:

```powershell
dotnet ef migrations add AddDepositorRankingView --project src/Ecocell.Api --startup-project src/Ecocell.Api
Get-ChildItem src/Ecocell.Api/Migrations/*_AddDepositorRankingView*.cs | Select-Object -ExpandProperty FullName
```

Expected:

- um arquivo `*_AddDepositorRankingView.cs`;
- um arquivo `*_AddDepositorRankingView.Designer.cs`;
- snapshot sem alteração semântica, pois a view e o índice funcional são SQL manual.

Não criar entidade, configuração EF ou `DbSet` para forçar diferença no snapshot.

- [ ] **Step 5: Implementar o índice e a view na migration**

No arquivo `*_AddDepositorRankingView.cs` gerado, manter namespace, classe e atributos do scaffold. Substituir somente `Up` e `Down` pelo conteúdo abaixo:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("""
        CREATE INDEX "IX_Addresses_State_City_Normalized"
        ON "Addresses" (
            upper(btrim("State")),
            lower(btrim("City"))
        );
        """);

    migrationBuilder.Sql("""
        CREATE VIEW v_ranking_depositor AS
        WITH national_totals AS (
            SELECT
                'National'::text AS "Scope",
                NULL::text AS "State",
                NULL::text AS "City",
                totals."DepositorId" AS "DepositorId",
                natural_people."FullName" AS "FullName",
                totals."TotalPoints"::numeric(28, 5) AS "TotalPoints"
            FROM "DepositorTotalScores" AS totals
            INNER JOIN "NaturalPeople" AS natural_people
                ON natural_people."PersonId" = totals."DepositorId"
            INNER JOIN "People" AS people
                ON people."PersonId" = totals."DepositorId"
            WHERE people."PersonStatus" = 1
              AND totals."TotalPoints" > 0
        ),
        national_ranked AS (
            SELECT
                "Scope",
                "State",
                "City",
                "DepositorId",
                "FullName",
                "TotalPoints",
                dense_rank() OVER (
                    ORDER BY "TotalPoints" DESC
                ) AS "Position"
            FROM national_totals
        ),
        municipal_totals AS (
            SELECT
                'Municipal'::text AS "Scope",
                upper(btrim(addresses."State")) AS "State",
                lower(btrim(addresses."City")) AS "City",
                transactions."DepositorId" AS "DepositorId",
                natural_people."FullName" AS "FullName",
                sum(transactions."Points")::numeric(28, 5) AS "TotalPoints"
            FROM "DepositorScoreTransactions" AS transactions
            INNER JOIN "Discards" AS discards
                ON discards."Id" = transactions."DiscardId"
            INNER JOIN "LegalPeople" AS collector_points
                ON collector_points."PersonId" = discards."CollectorPointId"
            INNER JOIN "Addresses" AS addresses
                ON addresses."Id" = collector_points."AddressId"
            INNER JOIN "NaturalPeople" AS natural_people
                ON natural_people."PersonId" = transactions."DepositorId"
            INNER JOIN "People" AS people
                ON people."PersonId" = transactions."DepositorId"
            WHERE people."PersonStatus" = 1
              AND nullif(btrim(addresses."City"), '') IS NOT NULL
              AND nullif(btrim(addresses."State"), '') IS NOT NULL
            GROUP BY
                upper(btrim(addresses."State")),
                lower(btrim(addresses."City")),
                transactions."DepositorId",
                natural_people."FullName"
            HAVING sum(transactions."Points") > 0
        ),
        municipal_ranked AS (
            SELECT
                "Scope",
                "State",
                "City",
                "DepositorId",
                "FullName",
                "TotalPoints",
                dense_rank() OVER (
                    PARTITION BY "State", "City"
                    ORDER BY "TotalPoints" DESC
                ) AS "Position"
            FROM municipal_totals
        )
        SELECT
            "Scope", "State", "City", "DepositorId",
            "FullName", "TotalPoints", "Position"
        FROM national_ranked
        UNION ALL
        SELECT
            "Scope", "State", "City", "DepositorId",
            "FullName", "TotalPoints", "Position"
        FROM municipal_ranked;
        """);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("DROP VIEW IF EXISTS v_ranking_depositor;");
    migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Addresses_State_City_Normalized\";");
}
```

Não adicionar filtro por status do Ponto de Coleta. Não recalcular pontos por `DiscardItems`. Não adicionar `ORDER BY` global à view; ordenação de páginas pertence à US005.B.

- [ ] **Step 6: Executar os testes focados e confirmar GREEN**

Run:

```powershell
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~DepositorRankingViewTests"
```

Expected: 5 testes passam. Confirmar no output que não houve skip nem falha de inicialização dos containers.

- [ ] **Step 7: Refatorar somente duplicação comprovada no teste**

Revisar `DepositorRankingViewTests.cs` e manter apenas os helpers privados já definidos:

```text
CreateDepositorAsync
CreateCreditAsync
QueryRowsAsync
ScalarAsync
RankingRow
```

Não mover esses helpers para `IntegrationTestBase`, pois são específicos da view. Não criar builder, fixture adicional, repositório ou helper SQL de produção.

Depois de qualquer ajuste, repetir:

```powershell
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~DepositorRankingViewTests"
```

Expected: PASS.

- [ ] **Step 8: Revisar migration, `Up`, `Down` e snapshot**

Run:

```powershell
rtk git diff -- src/Ecocell.Api/Migrations tests/Ecocell.IntegrationTests/Features/Ranking/DepositorRankingViewTests.cs
dotnet ef migrations list --project src/Ecocell.Api --startup-project src/Ecocell.Api
dotnet ef migrations script 20260917000540_ConfigureDiscardStatusConcurrency AddDepositorRankingView --project src/Ecocell.Api --startup-project src/Ecocell.Api
dotnet ef migrations script AddDepositorRankingView 20260917000540_ConfigureDiscardStatusConcurrency --project src/Ecocell.Api --startup-project src/Ecocell.Api
```

Expected:

- migration nova aparece depois de `20260917000540_ConfigureDiscardStatusConcurrency`;
- script de avanço cria índice antes da view;
- script de retorno remove view antes do índice;
- nenhum `CREATE TABLE`, `ALTER TABLE`, `DROP TABLE`, trigger ou extensão aparece na migration nova;
- snapshot não ganha entidade de ranking nem alteração de domínio.

- [ ] **Step 9: Executar a validação completa obrigatória**

Run:

```powershell
rtk dotnet test Ecocell.slnx
dotnet ef migrations has-pending-model-changes --project src/Ecocell.Api --startup-project src/Ecocell.Api
rtk git diff --check
```

Expected:

- todos os testes unitários e de integração passam;
- EF informa que não há mudanças pendentes no modelo;
- `git diff --check` não produz saída.

- [ ] **Step 10: Fazer commit seletivo**

Primeiro resolver os nomes gerados:

```powershell
$migrationFile = Get-ChildItem src/Ecocell.Api/Migrations/*_AddDepositorRankingView.cs |
    Where-Object Name -NotLike '*.Designer.cs'
$designerFile = Get-ChildItem src/Ecocell.Api/Migrations/*_AddDepositorRankingView.Designer.cs
$migrationFile.FullName
$designerFile.FullName
```

Confirmar que cada variável contém exatamente um arquivo. Depois fazer staging somente destes caminhos:

```powershell
rtk git add -- $migrationFile.FullName $designerFile.FullName src/Ecocell.Api/Migrations/AppDbContextModelSnapshot.cs tests/Ecocell.IntegrationTests/Features/Ranking/DepositorRankingViewTests.cs
rtk git diff --cached --check
rtk git diff --cached --name-status
rtk git commit -m "feat: add depositor ranking view"
```

Expected:

- staging contém somente migration, designer, snapshot se alterado e teste de ranking;
- `.claude/settings.local.json` permanece fora do commit;
- commit criado com todos os testes verdes.

---

## Definition of Done

- `v_ranking_depositor` existe no PostgreSQL e combina Nacional e Municipal por `UNION ALL`.
- Índice `IX_Addresses_State_City_Normalized` existe com as mesmas expressões usadas pela view.
- Nacional usa `DepositorTotalScores`; Municipal usa `DepositorScoreTransactions` e o endereço atual do PC.
- Somente Depositantes PF ativos com pontos positivos aparecem.
- Cidade e UF são normalizadas, acentos são preservados e cidades homônimas de UFs diferentes não se misturam.
- Pontos sem município válido permanecem apenas no Nacional.
- Status do PC não apaga crédito; status do depositante controla visibilidade.
- Empates usam `DENSE_RANK` e produzem `1, 1, 2`.
- Testes focados, solução completa, verificação EF e `git diff --check` passam.
- Commit contém somente os arquivos da US005.A.
