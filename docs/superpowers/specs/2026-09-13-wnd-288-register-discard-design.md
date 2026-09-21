# WND-288 — Abertura de descarte — Design

**Issue:** [WND-288](https://linear.app/wnd-dev/issue/WND-288/us004b-abrir-descarte-depositante)
**Parent:** [WND-166](https://linear.app/wnd-dev/issue/WND-166/us004-descarte-via-qr-code)
**Dependência:** [WND-168](https://linear.app/wnd-dev/issue/WND-168/us011-tabela-de-pontuacao-por-material)
**Status:** Aprovado em 2026-09-13

## Design aprovado

### Objetivo

Permitir que uma pessoa depositante autenticada abra um descarte pendente ao ler o QR Code de um Ponto de Coleta, registrando os itens declarados e congelando as regras de pontuação vigentes. Confirmação, rejeição e crédito de pontos permanecem nas subtasks posteriores da US004.

### Dependência obrigatória

A implementação desta subtask fica bloqueada pela [WND-168](https://linear.app/wnd-dev/issue/WND-168/us011-tabela-de-pontuacao-por-material) **— US011 — Tabela de pontuação por material**, especificamente pelas entregas de entidade/migration e definição das regras pelo PC (US011.A/B). A [WND-288](https://linear.app/wnd-dev/issue/WND-288/us004b-abrir-descarte-depositante) não deve duplicar nem incorporar esse escopo.

### Modelo de domínio

Criar o agregado `Discard`, pois `DiscardHistory` não existe atualmente e o registro de descarte é a entidade transacional principal.

`Discard`:

* herda de `BaseEntity`;
* contém `DepositorId`, `CollectorPointId` e `Status`;
* nasce obrigatoriamente com `Status = Pending`;
* possui uma coleção não vazia de `DiscardItem`;
* somente poderá ser alterado enquanto estiver `Pending`;
* as transições para `Confirmed` e `Rejected` serão implementadas pela [WND-289](https://linear.app/wnd-dev/issue/WND-289/us004c-confirmarrejeitar-pc).

`DiscardItem`:

* contém `DiscardId`, material, `Quantity`, `ApproximateWeightKg` e `MaterialScoreRuleId`;
* exige `Quantity > 0`;
* exige `ApproximateWeightKg > 0`, em quilogramas, com precisão `decimal(10,3)`;
* permite cada material no máximo uma vez por descarte;
* referencia a versão da regra vigente no instante da abertura.

Criar `DiscardStatus` com `Pending`, `Confirmed` e `Rejected`. Os relacionamentos com depositante, PC e regra de pontuação usam exclusão restrita. A persistência do agregado e dos itens ocorre atomicamente em um único `SaveChangesAsync`.

Quando a [WND-289](https://linear.app/wnd-dev/issue/WND-289/us004c-confirmarrejeitar-pc) corrigir material, quantidade ou peso antes da confirmação, ela atualizará os próprios valores do item. Não haverá cópia paralela de valores declarados e confirmados. Se o material for alterado, a regra aplicável deverá ser a versão que estava vigente na data de abertura do descarte.

### Snapshot da pontuação

Cada item grava o `MaterialScoreRuleId` vigente na abertura. Uma alteração posterior da tabela do PC não muda a regra associada ao descarte pendente. Isso preserva o versionamento definido pela US011.

A regra vigente satisfaz:

* `ValidFrom <= openedAt`;
* `ValidTo` ausente ou `ValidTo > openedAt`;
* pertence ao Ponto de Coleta lido;
* corresponde ao material informado.

A ausência de regra vigente significa que o material não é aceito pelo PC.

### Contrato HTTP

Endpoint autenticado:

`POST /api/v1/discards`

Request:

```json
{
  "qrCode": "ecocell://pc/{guid}",
  "items": [
    {
      "material": "CellPhone",
      "quantity": 2,
      "approximateWeightKg": 0.450
    }
  ]
}
```

O endpoint recebe o conteúdo bruto do QR. A API valida exatamente o esquema `ecocell://pc/{guid}`; o cliente não precisa interpretar nem transformar o identificador.

Resposta `201 Created`:

```json
{
  "id": "018f3f2a-7b4c-7c91-8d31-5f7a2b6c9e10",
  "status": "Pending",
  "createdAt": "2026-09-13T12:00:00Z"
}
```

Os DTOs públicos ficam em `Ecocell.Shared`; o `Command` interno permanece dentro do slice.

### Autorização

Pode abrir descarte qualquer `NaturalPerson`:

* autenticada;
* com `PersonStatus.Active`;
* na jornada `Journey.Depositor`.

`Role.User`, `Role.Admin` e `Role.Support` são aceitos. A [WND-288](https://linear.app/wnd-dev/issue/WND-288/us004b-abrir-descarte-depositante) não concede pontos. A RN013 será aplicada no crédito posterior, impedindo que Admin e Support pontuem ou entrem no ranking.

### Fluxo do handler

 1. Executar o validator antes de consultar o banco.
 2. Validar QR, lista não vazia, materiais únicos, quantidade e peso positivos.
 3. Obter e autorizar a pessoa autenticada.
 4. Extrair o identificador do QR.
 5. Localizar a `LegalPerson` correspondente e exigir `Journey.CollectPoint`.
 6. Exigir que o PC esteja ativo.
 7. Buscar, em uma consulta, as regras vigentes para todos os materiais informados.
 8. Rejeitar a operação se qualquer material não tiver regra vigente.
 9. Criar `Discard` e `DiscardItem` com os IDs das regras selecionadas.
10. Persistir e retornar o identificador, status e data de criação.

### Erros HTTP

* `400 Bad Request`: QR malformado, lista vazia, material repetido, quantidade ou peso inválido.
* `401 Unauthorized`: ausência ou invalidade da autenticação.
* `403 Forbidden`: pessoa autenticada não elegível para a jornada de depositante.
* `404 Not Found`: identificador válido, mas PC inexistente ou com jornada diferente.
* `409 Conflict`: PC inativo ou material não aceito pelo PC.

Usar as factories e o mapeamento de erro já existentes; não criar nova hierarquia de erros.

### Estrutura prevista

* `src/Ecocell.Api/Entities/Discard.cs`
* `src/Ecocell.Api/Entities/DiscardItem.cs`
* configurações EF Core correspondentes;
* enum interno `DiscardStatus`;
* `DbSet` no `AppDbContext`;
* migration da estrutura;
* request, response e enums públicos necessários em `Ecocell.Shared`;
* `src/Ecocell.Api/Features/Discard/RegisterDiscard.cs`;
* testes unitários e de integração espelhando o slice.

O slice seguirá VSA: `Command`, `Validator`, `Handler` e endpoint no mesmo arquivo. Não criar repository, service ou mapper adicional.

### Estratégia de testes

Aplicar TDD Red → Green → Refactor.

Testes unitários:

* persiste descarte válido com itens e status `Pending`;
* associa a versão correta da regra;
* mantém o `MaterialScoreRuleId` mesmo após versionamento posterior;
* rejeita QR malformado;
* rejeita lista vazia, material repetido, quantidade ou peso inválido;
* rejeita pessoa inelegível;
* aceita Admin e Support que atendam aos critérios de depositante;
* retorna erro para PC inexistente, jornada incorreta ou status inativo;
* rejeita material sem regra vigente;
* ignora regras futuras ou expiradas.

Testes de integração:

* retorna `401` sem token;
* retorna `201` e persiste agregado completo no fluxo válido;
* cobre `400`, `403`, `404` e `409` nos cenários principais.

Verificação final obrigatória: `dotnet test Ecocell.slnx`. Os testes de integração exigem Docker em execução.

### Fora do escopo

* leitura de câmera e telas Mobile;
* confirmação ou rejeição do descarte;
* crédito de pontos e ranking;
* notificações;
* idempotência do POST;
* implementação parcial da [WND-168](https://linear.app/wnd-dev/issue/WND-168/us011-tabela-de-pontuacao-por-material).
