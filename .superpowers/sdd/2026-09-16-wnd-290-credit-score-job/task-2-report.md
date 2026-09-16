# WND-290 — Task 2: Persistência e migration do processamento de crédito

## Escopo entregue

- Adicionados os `DbSet` de `CreditScoreRequest`, `DepositorScoreTransaction` e `DepositorTotalScore` ao `AppDbContext`.
- Criadas as três configurações EF Core previstas no brief.
- Gerada a migration `AddCreditScoreProcessing` e atualizado o snapshot do modelo.
- Criados testes de metadata para índices, precisão, unicidade, token de concorrência e exclusão restrita.

## Arquivos alterados

- `src/Ecocell.Api/Database/AppDbContext.cs`
- `src/Ecocell.Api/Database/TypeConfiguration/CreditScoreRequestTypeConfiguration.cs`
- `src/Ecocell.Api/Database/TypeConfiguration/DepositorScoreTransactionTypeConfiguration.cs`
- `src/Ecocell.Api/Database/TypeConfiguration/DepositorTotalScoreTypeConfiguration.cs`
- `src/Ecocell.Api/Migrations/20260916201054_AddCreditScoreProcessing.cs`
- `src/Ecocell.Api/Migrations/20260916201054_AddCreditScoreProcessing.Designer.cs`
- `src/Ecocell.Api/Migrations/AppDbContextModelSnapshot.cs`
- `tests/Ecocell.UnitTests/Database/CreditScorePersistenceTests.cs`

## Decisões

- Relacionamentos de crédito usam `DeleteBehavior.Restrict` para não apagar registros de auditoria quando descarte ou depositante forem manipulados.
- `Points` e `TotalPoints` usam precisão `numeric(28,5)`; `TotalPoints` é token de concorrência.
- As tabelas de transação e total possuem checks para pontuação positiva e total não negativo, respectivamente.
- O brief usa duas expressões incompatíveis com as sobrecargas por expression tree do Shouldly nesta solução. O array implícito foi substituído por `new[] { ... }` e o padrão `is not null` por `!= null`, sem alterar a intenção dos testes.

## Red → Green → Refactor

1. RED: criado `CreditScorePersistenceTests` e executado o filtro dos quatro testes. Resultado: 4 falhas esperadas porque as entidades não pertenciam ao modelo EF.
2. GREEN: adicionados DbSets e configurações mínimas previstas. Resultado: 4 testes aprovados.
3. Refactor: nenhuma alteração adicional foi necessária; o código segue o padrão existente de `BaseEntityTypeConfiguration`.

## Comandos e resultados

| Comando | Resultado |
| --- | --- |
| `rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Ecocell.UnitTests.Database.CreditScorePersistenceTests"` | RED confirmado: 0 aprovados, 4 falhos por entidades ausentes do modelo. |
| Mesmo comando após os mapeamentos | GREEN confirmado: 4 aprovados. Há aviso `NU1903` preexistente para `SQLitePCLRaw.lib.e_sqlite3` 2.1.11. |
| `rtk dotnet ef migrations add AddCreditScoreProcessing --project src/Ecocell.Api --startup-project src/Ecocell.Api` | Concluído; migration e designer gerados. |
| `rtk dotnet ef migrations has-pending-model-changes --project src/Ecocell.Api --startup-project src/Ecocell.Api` | Concluído: nenhuma mudança pendente no modelo. |
| `rtk dotnet test Ecocell.slnx` | Disparado, mas a camada de execução não retornou resumo nem código de saída. |
| `dotnet test Ecocell.slnx --logger "console;verbosity=minimal"` | Repetido para capturar saída; retornou apenas `Determinando os projetos a serem restaurados...`, sem resultado conclusivo. |
| `rtk git diff --check` | Sem saída de violações. |

## Preocupações

- Não há evidência conclusiva do resultado da suíte completa devido à saída truncada/inconclusiva da camada de execução; não é possível declarar a suíte verde.
- A execução inicial no sandbox falhou antes dos testes por `UnauthorizedAccessException` ao criar diretório temporário do MSBuild. As execuções fora do sandbox permitiram confirmar RED, GREEN e sincronização da migration.
- Permanece o aviso de dependência `NU1903` para `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, fora do escopo desta tarefa.
