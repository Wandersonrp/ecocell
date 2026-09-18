# WND-289 — Confirmação e rejeição de descarte — Design

**Issue:** [WND-289](https://linear.app/wnd-dev/issue/WND-289/us004c-confirmarrejeitar-pc)
**Parent:** [WND-166](https://linear.app/wnd-dev/issue/WND-166/us004-descarte-via-qr-code)
**Dependência:** [WND-290](https://linear.app/wnd-dev/issue/WND-290/us004d-job-credito-de-pontuacao)
**Status:** Aprovado em 2026-09-16

## Design aprovado

### Objetivo

Permitir que a pessoa física gestora de um Ponto de Coleta ativo confirme ou rejeite um descarte pendente pertencente ao PC. Na confirmação, o PC informa a composição final recebida e a API persiste atomicamente a decisão, os itens corrigidos e uma solicitação durável de crédito. Na rejeição, a API encerra o descarte sem solicitar crédito.

### Dependência obrigatória

A implementação completa da WND-289 fica bloqueada pela [WND-290](https://linear.app/wnd-dev/issue/WND-290/us004d-job-credito-de-pontuacao). A WND-290 deve entregar, antes da integração dos endpoints:

* a entidade persistida `CreditScoreRequest`;
* índice único por `DiscardId`;
* dispatcher dos pedidos ainda não despachados;
* `CreditScoreJob` idempotente;
* garantia de que o mesmo descarte não gere crédito duplicado.

A WND-289 apenas cria `CreditScoreRequest` na transação de confirmação. Não implementa o cálculo, o dispatcher nem o processamento do crédito.

### Abordagem escolhida

Usar uma outbox específica para crédito. A mesma transação que confirma o descarte e substitui seus itens insere `CreditScoreRequest`. Isso elimina a janela na qual o descarte poderia ser confirmado e a aplicação falhar antes de solicitar a pontuação.

Foram rejeitadas:

* chamada direta à fila após o commit, porque pode perder a solicitação de crédito;
* crédito síncrono no handler, porque mistura as responsabilidades da WND-289 e da WND-290;
* outbox genérica com tipo e payload JSON, porque não existe um segundo consumidor real que justifique a abstração.

### Modelo de domínio

`Discard` ganha duas transições terminais:

* `Confirm(IEnumerable<DiscardItem> finalItems)` aceita somente `Pending`, exige coleção não vazia e materiais únicos, substitui os itens e muda o estado para `Confirmed`;
* `Reject()` aceita somente `Pending` e muda o estado para `Rejected`.

As duas transições chamam `MarkAsUpdated()`. Não serão adicionados `ConfirmedAt`, `RejectedAt` nem motivo de rejeição. Como não existem transições posteriores, `UpdatedAt` registra o instante da decisão.

Qualquer tentativa de confirmar ou rejeitar um descarte `Confirmed` ou `Rejected` resulta em `409 Conflict`. Repetir a mesma operação não é idempotente no contrato HTTP.

### Correção dos itens

A confirmação recebe a lista final completa. O PC pode adicionar, remover ou trocar materiais e corrigir quantidade ou peso. Não haverá cópia paralela de valores declarados e confirmados.

A lista final deve:

* possuir ao menos um item;
* conter cada material no máximo uma vez;
* usar materiais válidos;
* exigir `Quantity > 0`;
* exigir `ApproximateWeightKg > 0` com precisão `decimal(10,3)`.

Cada material final deve possuir uma `MaterialScoreRule` do mesmo PC vigente em `Discard.CreatedAt`:

* `ValidFrom <= Discard.CreatedAt`;
* `ValidTo` ausente ou `ValidTo > Discard.CreatedAt`.

Regra criada após a abertura não torna o material elegível. Regra encerrada depois da abertura continua válida para o descarte. Cada item final grava o ID da versão histórica encontrada.

### Concorrência

Configurar `Discard.Status` como concurrency token do EF Core. Não será criada coluna adicional de versão.

Duas confirmações concorrentes, duas rejeições concorrentes ou uma confirmação concorrente com uma rejeição partem do mesmo valor `Pending`. Uma operação vence; a outra recebe `DbUpdateConcurrencyException`, traduzida para `409 Conflict`.

A confirmação usa uma transação explícita para manter indivisíveis:

* remoção dos itens anteriores;
* inclusão dos itens finais;
* transição para `Confirmed`;
* inclusão de `CreditScoreRequest`.

A implementação pode usar mais de um `SaveChangesAsync` dentro da mesma transação para respeitar o índice único `(DiscardId, Material)` durante trocas de materiais. Nenhum estado parcial pode ser confirmado.

### Contratos HTTP

#### Confirmar

Endpoint autenticado:

`POST /api/v1/discards/{id}/confirm`

Request público em `Ecocell.Shared`:

```json
{
  "items": [
    {
      "material": "CellPhone",
      "quantity": 2,
      "approximateWeightKg": 0.450
    }
  ]
}
```

Criar `RequestConfirmDiscardJson` e o DTO de item correspondente. O `Command` interno permanece no slice.

Sucesso: `204 No Content`.

#### Rejeitar

Endpoint autenticado:

`POST /api/v1/discards/{id}/reject`

Sem body e sem motivo de rejeição.

Sucesso: `204 No Content`.

### Autorização e anti-enumeração

Os endpoints mantêm somente o identificador do descarte na rota. O handler obtém `CollectorPointId` do descarte e reutiliza `ICollectorPointAccessGuard.EnsureResponsibleActiveAsync`.

A ordem obrigatória é:

1. validar o input;
2. localizar o descarte;
3. autorizar o gestor do PC;
4. somente então avaliar o estado do descarte.

Esse ordenamento evita revelar a uma pessoa não autorizada se um descarte de outro PC está pendente, confirmado ou rejeitado.

O contrato permanece:

* `403 Forbidden`: chamador inelegível para operar recursos de PC;
* `404 Not Found`: descarte inexistente, PC de outra jornada ou PC não pertencente ao gestor;
* `409 Conflict`: PC próprio não ativo.

### Fluxo de confirmação

1. Executar o validator antes de consultar o banco.
2. Validar ID, lista não vazia, materiais únicos, quantidade, peso e precisão.
3. Carregar `Discard` com seus itens; retornar `404` quando ausente.
4. Autorizar o gestor por `ICollectorPointAccessGuard`.
5. Exigir `Status = Pending`; outro estado retorna `409`.
6. Buscar em uma consulta as regras históricas de todos os materiais finais.
7. Retornar `409` se qualquer material não possuía regra vigente na abertura.
8. Criar a coleção final de `DiscardItem` com os IDs das regras históricas.
9. Iniciar a transação, substituir os itens e marcar o descarte como `Confirmed`.
10. Inserir um único `CreditScoreRequest` para o descarte.
11. Persistir e confirmar a transação.
12. Traduzir conflito de concorrência para `409`; retornar `204` em caso de sucesso.

### Fluxo de rejeição

1. Executar o validator antes de consultar o banco.
2. Validar o identificador.
3. Carregar o descarte; retornar `404` quando ausente.
4. Autorizar o gestor por `ICollectorPointAccessGuard`.
5. Exigir `Status = Pending`; outro estado retorna `409`.
6. Marcar o descarte como `Rejected`.
7. Persistir sem criar `CreditScoreRequest`.
8. Traduzir conflito de concorrência para `409`; retornar `204` em caso de sucesso.

### Erros HTTP

* `400 Bad Request`: identificador ou body de confirmação inválido, lista vazia, material repetido, quantidade ou peso inválido.
* `401 Unauthorized`: ausência ou invalidade da autenticação.
* `403 Forbidden`: pessoa autenticada inelegível para operar recursos de PC.
* `404 Not Found`: descarte inexistente ou fora do escopo do gestor.
* `409 Conflict`: PC inativo, descarte em estado terminal, material sem regra histórica ou colisão concorrente.

Usar as factories de `Error` e `ResultExtensions.ToProcessResult` existentes. Não criar nova hierarquia de erros.

### Estrutura prevista

* `src/Ecocell.Shared/Requests/Discards/RequestConfirmDiscardJson.cs`;
* `src/Ecocell.Api/Entities/Discard.cs`;
* `src/Ecocell.Api/Database/TypeConfiguration/DiscardTypeConfiguration.cs`;
* `src/Ecocell.Api/Features/Discard/ConfirmDiscard.cs`;
* `src/Ecocell.Api/Features/Discard/RejectDiscard.cs`;
* `tests/Ecocell.UnitTests/Entities/DiscardTests.cs`;
* `tests/Ecocell.UnitTests/Features/Discard/ConfirmDiscardTests.cs`;
* `tests/Ecocell.UnitTests/Features/Discard/RejectDiscardTests.cs`;
* `tests/Ecocell.IntegrationTests/Features/Discard/ConfirmDiscardTests.cs`;
* `tests/Ecocell.IntegrationTests/Features/Discard/RejectDiscardTests.cs`.

Cada slice segue VSA com `Command`, `Validator`, `Handler` e endpoint no mesmo arquivo. Não criar repository, service ou mapper.

### Estratégia de testes

Aplicar TDD Red → Green → Refactor.

Testes de entidade:

* confirmação substitui os itens e muda o estado para `Confirmed`;
* rejeição muda o estado para `Rejected`;
* estados terminais impedem novas transições;
* confirmação recusa lista vazia ou materiais repetidos.

Testes unitários de confirmação:

* persiste status, itens finais e `CreditScoreRequest`;
* permite adicionar, remover e trocar materiais;
* usa regra vigente em `Discard.CreatedAt`;
* ignora regra criada após a abertura;
* retorna `409` para material sem regra histórica;
* propaga `403`, `404` e `409` do guard;
* retorna `409` para descarte terminal.

Testes unitários de rejeição:

* persiste `Rejected`;
* não cria `CreditScoreRequest`;
* propaga erros do guard;
* retorna `409` para descarte terminal.

Testes de integração:

* retornam `401` sem token;
* confirmação válida retorna `204` e persiste estado, itens e outbox;
* rejeição válida retorna `204` sem outbox;
* PC alheio retorna `404`;
* estado terminal retorna `409`;
* troca de materiais funciona com o índice único do PostgreSQL;
* confirmação e rejeição concorrentes produzem um `204` e um `409`.

Verificação final obrigatória: `dotnet test Ecocell.slnx`. Os testes de integração exigem Docker em execução.

### Fora do escopo

* cálculo de pontos, atualização de saldo e ranking;
* implementação do dispatcher e do `CreditScoreJob`;
* listagem de descartes pendentes;
* telas Mobile;
* motivo da rejeição;
* alteração após estado terminal;
* idempotência HTTP;
* outbox genérica;
* novos repositories, services ou mappers.
