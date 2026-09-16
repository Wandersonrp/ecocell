# WND-290 — US004.D — Job de crédito de pontuação — Design

**Issue:** [WND-290](https://linear.app/wnd-dev/issue/WND-290/us004d-job-credito-de-pontuacao)  
**Parent:** [WND-166](https://linear.app/wnd-dev/issue/WND-166/us004-descarte-via-qr-code)  
**Bloqueia:** [WND-289](https://linear.app/wnd-dev/issue/WND-289/us004c-confirmarrejeitar-pc)  
**Status:** Aprovado em 2026-09-16

## Objetivo

Processar de forma assíncrona, durável e idempotente a pontuação de descartes confirmados. O processamento cria uma transação imutável de pontuação, atualiza o saldo global do depositante e conclui a solicitação de crédito na mesma transação de banco.

A entrega também fornece a outbox específica consumida pela WND-289. A confirmação do descarte criará um `CreditScoreRequest` atomicamente com a transição para `Confirmed`; a WND-290 será responsável por consumir esse pedido.

## Contexto atual

O repositório já possui:

- `Discard` e `DiscardItem` com referência à versão histórica de `MaterialScoreRule`;
- regras com `Points decimal(10,2)` e unidade `PerUnit` ou `PerKilogram`;
- peso aproximado com precisão `decimal(10,3)`;
- `Role.User`, `Role.Admin` e `Role.Support`;
- PostgreSQL em produção e SQLite in-memory nos testes unitários;
- Testcontainers PostgreSQL nos testes de integração.

O repositório não possui Hangfire, fila externa, scheduler ou entidades de saldo/transação de pontuação. A descrição do épico no Linear que afirma a existência dessas entidades está desatualizada em relação ao código atual.

## Decisões aprovadas

### Execução assíncrona

Usar `BackgroundService` nativo. Não adicionar Hangfire nem fila externa.

O `CreditScoreDispatcher` consulta `CreditScoreRequest` pendentes e chama `CreditScoreJob` no mesmo processo. A persistência do pedido garante recuperação após reinício da API.

### Pontuação

O cálculo usa os itens finais e as regras históricas associadas ao descarte:

- `PerUnit`: `MaterialScoreRule.Points * DiscardItem.Quantity`;
- `PerKilogram`: `MaterialScoreRule.Points * DiscardItem.ApproximateWeightKg`;
- pontuação do descarte: soma dos resultados de todos os itens.

Não há arredondamento durante o crédito. Transação e saldo usam `decimal(28,5)`. As cinco casas preservam o produto de uma regra com duas casas por um peso com três casas; a precisão 28 evita overflow com os limites já aceitos e com a acumulação de vários descartes.

### RN013

Somente `Role.User` recebe pontos. `Role.Admin` e `Role.Support` são processados com sucesso, mas:

- não geram `DepositorScoreTransaction`;
- não criam nem alteram `DepositorTotalScore`;
- têm o `CreditScoreRequest` concluído;
- produzem log informativo.

A elegibilidade usa o `Role` persistido do depositante no momento do processamento. O modelo atual não possui alteração operacional de papel que justifique criar snapshot adicional.

### Saldo

O saldo é global por depositante e agrega créditos de todos os Pontos de Coleta. Consultas futuras específicas por PC podem usar as transações e o relacionamento do descarte, sem duplicar saldos agora.

## Modelo de dados

### `CreditScoreRequest`

Outbox específica de crédito.

- herda `BaseEntity`;
- `Guid DiscardId`, obrigatório;
- `Discard Discard`;
- `DateTime? DispatchedAt`, em UTC;
- FK para `Discard.Id` com exclusão restrita;
- índice único em `DiscardId`;
- índice em `DispatchedAt` para localizar pedidos pendentes.

O nome `DispatchedAt` é mantido por compatibilidade com o contrato aprovado da WND-289. Neste processamento in-process, ele representa o instante em que o pedido terminou com sucesso, com crédito ou com supressão pela RN013. O campo só é preenchido dentro da mesma transação que conclui o processamento.

### `DepositorScoreTransaction`

Ledger imutável de créditos.

- herda `BaseEntity`;
- `Guid DiscardId`, obrigatório;
- `Discard Discard`;
- `Guid DepositorId`, obrigatório;
- `NaturalPerson Depositor`;
- `decimal Points`, obrigatório e positivo, com precisão `decimal(28,5)`;
- FK para `Discard.Id` com exclusão restrita;
- FK para `NaturalPerson.Id` com exclusão restrita;
- índice único em `DiscardId`;
- índice em `DepositorId`;
- check constraint exigindo `Points > 0`.

Não duplicar `CollectorPointId`, materiais ou regras. Essas informações permanecem rastreáveis pelo descarte e por seus itens.

### `DepositorTotalScore`

Saldo materializado global.

- herda `BaseEntity`;
- `Guid DepositorId`, obrigatório;
- `NaturalPerson Depositor`;
- `decimal TotalPoints`, não negativo, com precisão `decimal(28,5)`;
- FK para `NaturalPerson.Id` com exclusão restrita;
- índice único em `DepositorId`;
- `TotalPoints` configurado como concurrency token;
- check constraint exigindo `TotalPoints >= 0`.

A linha é criada no primeiro crédito. Créditos posteriores incrementam o saldo por método da entidade que rejeita valores não positivos.

### `AppDbContext`

Adicionar:

- `DbSet<CreditScoreRequest> CreditScoreRequests`;
- `DbSet<DepositorScoreTransaction> DepositorScoreTransactions`;
- `DbSet<DepositorTotalScore> DepositorTotalScores`.

Uma migration cria as três tabelas, FKs, índices, precisões e constraints. O modelo deve permanecer compatível com PostgreSQL e SQLite.

## Componentes

### `CreditScoreDispatcher`

`BackgroundService` responsável apenas por localizar trabalho pendente e despachá-lo ao job.

- executa um ciclo imediatamente ao iniciar;
- depois aguarda 5 segundos entre ciclos;
- seleciona até 50 pedidos pendentes por ciclo;
- ordena por `CreatedAt` e `Id` para comportamento determinístico;
- processa os pedidos sequencialmente;
- cria um novo escopo de DI por pedido;
- captura falhas por pedido, registra o erro e continua o lote;
- respeita o cancellation token da aplicação.

Intervalo e tamanho do lote permanecem constantes nesta entrega. Torná-los configuráveis só será necessário quando houver requisito operacional real.

O hosted service não é registrado no ambiente `Testing`, evitando corridas com a limpeza de banco da fixture. O job e um ciclo único do dispatcher são testados diretamente.

### `CreditScoreJob`

Serviço scoped que concentra o processamento transacional. Não criar interface, repository ou service adicional.

Entrada mínima: identificador do `CreditScoreRequest`. O job obtém todo o restante do banco.

## Fluxo de processamento

Para cada pedido:

1. iniciar transação explícita;
2. carregar o `CreditScoreRequest` e retornar sem efeito quando ausente ou já concluído;
3. carregar `Discard`, `Depositor`, itens e respectivas regras históricas;
4. exigir `DiscardStatus.Confirmed`;
5. verificar se já existe `DepositorScoreTransaction` para o descarte;
6. se a transação já existir, concluir apenas o pedido, preservando o saldo existente;
7. se o depositante for `Admin` ou `Support`, concluir o pedido sem crédito;
8. calcular a pontuação de cada item e somar o total;
9. criar `DepositorScoreTransaction`;
10. criar ou incrementar `DepositorTotalScore`;
11. preencher `CreditScoreRequest.DispatchedAt` com UTC fornecido por `TimeProvider`;
12. executar `SaveChangesAsync` e confirmar a transação.

Transação de pontuação, saldo e conclusão do pedido são indivisíveis.

## Idempotência e concorrência

### Mesmo descarte

O índice único em `DepositorScoreTransaction.DiscardId` é a garantia persistente de idempotência. Duas execuções concorrentes podem observar o pedido pendente, mas somente uma consegue confirmar a transação de pontuação. A execução perdedora sofre rollback; no ciclo seguinte encontra o pedido concluído e retorna sem efeito.

### Descartes diferentes do mesmo depositante

`DepositorTotalScore.TotalPoints` é concurrency token. Se dois jobs tentarem alterar o mesmo saldo, somente um confirma; o outro sofre rollback e permanece pendente para novo ciclo.

Na criação simultânea do primeiro saldo, o índice único em `DepositorId` cumpre a mesma função. A execução perdedora tenta novamente no próximo ciclo e então incrementa a linha existente.

Não haverá retry imediato dentro do job. O polling com novo escopo fornece retry natural, evita reutilizar `DbContext` inválido após exceção e mantém a implementação pequena.

## Erros e observabilidade

- pedido ausente ou já concluído: retornar sem efeito;
- `Admin` ou `Support`: log informativo e conclusão sem crédito;
- conflito de concorrência ou unicidade: rollback e log de aviso;
- descarte não confirmado ou dados inconsistentes: rollback e log de erro;
- erro inesperado: rollback e log de erro;
- cancelamento da aplicação: encerrar sem registrar erro.

Logs devem ser estruturados e incluir `CreditScoreRequestId` e `DiscardId` quando disponíveis. Uma falha não interrompe os demais pedidos do lote.

Pedidos com falha permanecem pendentes. Não adicionar contador de tentativas, estado de falha, dead-letter, endpoint administrativo ou métricas próprias no MVP. Se pedidos permanentemente inválidos surgirem em operação, essa evidência justificará uma entrega específica de tratamento operacional.

## Estratégia de testes

Aplicar TDD Red → Green → Refactor.

### Testes unitários com SQLite

Cobrir:

- crédito de descarte confirmado;
- cálculo `PerUnit`;
- cálculo `PerKilogram`;
- soma de múltiplos itens;
- preservação de cinco casas decimais;
- `Admin` e `Support` sem transação ou saldo, com pedido concluído;
- pedido já concluído sem efeito;
- descarte não confirmado com rollback e pedido pendente;
- transação já existente sem duplicação de saldo;
- invariantes de `CreditScoreRequest`, `DepositorScoreTransaction` e `DepositorTotalScore`.

A WND-290 não antecipa `Discard.Confirm`, pertencente à WND-289. Enquanto essa transição não existir, os testes semeiam `Confirmed` por `DbContext.Entry(discard).Property(value => value.Status).CurrentValue`, padrão já presente no projeto. Depois da WND-289, o setup pode usar a transição de domínio sem alterar o contrato do job.

### Testes de integração com PostgreSQL

Cobrir:

- índices únicos de pedido por descarte, transação por descarte e saldo por depositante;
- duas execuções concorrentes do mesmo descarte geram um único crédito;
- dois descartes concorrentes do mesmo depositante preservam a soma;
- um ciclo do dispatcher continua após falha de outro pedido;
- migrations aplicadas sem mudanças pendentes no modelo.

O ambiente `Testing` não executa o hosted service automaticamente. Os testes resolvem o job diretamente e invocam explicitamente um ciclo do dispatcher.

Verificação final obrigatória: `dotnet test Ecocell.slnx` com Docker disponível.

## Alternativas consideradas

### Hangfire

Rejeitado. O projeto não possui essa dependência e a entrega não exige dashboard, cron persistente ou recursos que compensem novas tabelas e infraestrutura.

### Ledger sem saldo materializado

Rejeitado. Seria mais simples e eliminaria concorrência no saldo, mas não atende ao requisito explícito de atualizar `DepositorTotalScore`.

### Upsert PostgreSQL específico

Rejeitado. Uma instrução `INSERT ... ON CONFLICT DO UPDATE` resolveria o incremento atomicamente, porém introduziria SQL específico e comportamento diferente nos testes SQLite. Concorrência otimista mantém o modelo portável.

### Crédito síncrono na confirmação

Rejeitado. Mistura WND-289 e WND-290 e pode aumentar latência e acoplamento do endpoint.

### Outbox genérica

Rejeitada. Não existe segundo consumidor real que justifique tipo, payload JSON e infraestrutura genérica.

## Estrutura prevista

- `src/Ecocell.Api/Entities/CreditScoreRequest.cs`;
- `src/Ecocell.Api/Entities/DepositorScoreTransaction.cs`;
- `src/Ecocell.Api/Entities/DepositorTotalScore.cs`;
- configurações EF correspondentes em `Database/TypeConfiguration/`;
- `src/Ecocell.Api/Jobs/CreditScoreJob.cs`;
- `src/Ecocell.Api/Jobs/CreditScoreDispatcher.cs`;
- `src/Ecocell.Api/Database/AppDbContext.cs`;
- `src/Ecocell.Api/Extensions/DependencyInjectionExtensions.cs`;
- migration e snapshot do EF Core;
- testes unitários em `tests/Ecocell.UnitTests/Entities/` e `tests/Ecocell.UnitTests/Jobs/`;
- testes de integração em `tests/Ecocell.IntegrationTests/Jobs/`.

## Critérios de aceite

- `CreditScoreRequest` persistente e único por descarte;
- dispatcher nativo recupera pedidos pendentes após reinício;
- descarte confirmado gera uma única transação de pontuação;
- saldo global recebe exatamente o valor da transação;
- cálculos por unidade e quilograma preservam cinco casas;
- `Admin` e `Support` não pontuam e não entram no saldo;
- processamento repetido não duplica crédito;
- concorrência entre descartes do mesmo depositante não perde atualização;
- falhas deixam o pedido pendente e não bloqueiam o lote;
- WND-289 consegue criar `CreditScoreRequest` na própria transação de confirmação;
- suíte completa passa com PostgreSQL/Redis Testcontainers.

## Fora do escopo

- confirmação ou rejeição de descarte;
- endpoints ou DTOs HTTP;
- ranking;
- cupons, selos ESG ou saldo por Ponto de Coleta;
- alteração ou estorno de crédito;
- edição de transações;
- Mobile;
- Hangfire ou fila externa;
- configuração dinâmica do polling;
- retry imediato, dead-letter ou painel operacional;
- outbox genérica;
- repositories, mappers ou novas camadas arquiteturais.
