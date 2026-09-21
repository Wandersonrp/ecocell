# WND-168 — US011.B — Definição das regras de pontuação pelo PC — Design

**Issue:** [WND-168](https://linear.app/wnd-dev/issue/WND-168/us011-tabela-de-pontuacao-por-material)

**Escopo:** US011.B — Slice de definição pelo Ponto de Coleta

**Requisitos relacionados:** US011 / RF008 / RN003 / RN005 / RN007

**Dependências:** US011.A concluída; shell de contexto PC da WND-277

**Status:** Aprovado em 2026-09-15

## Objetivo

Permitir que a pessoa física gestora de um Ponto de Coleta ativo substitua a tabela vigente de pontuação por material. A operação deve preservar o histórico, ser idempotente para payloads iguais, rejeitar concorrência sem sobrescrita silenciosa e servir como primeiro consumidor do guard reutilizável de acesso a recursos PC-scoped.

## Contexto existente

US011.A já entrega:

- enums internos e públicos `ElectronicMaterial`;
- enums internos e públicos `MaterialScoreUnit`;
- entidade temporal `MaterialScoreRule`;
- método `MaterialScoreRule.Close(DateTime)`;
- `DbSet<MaterialScoreRule>`;
- mapeamento EF Core, constraints e migration;
- índice único filtrado em `(LegalPersonId, Material)` para regras com `ValidTo IS NULL`.

A validade permanece semiaberta: `[ValidFrom, ValidTo)`. Toda alteração desta subtask deve usar os contratos e as invariantes existentes, sem duplicá-los.

O repositório também possui `GeneratePcQrCode`, que hoje repete as verificações de pessoa gestora, responsabilidade, jornada e status do PC. US011.B extrairá essas verificações para o guard compartilhado e migrará esse slice sem alterar seu contrato HTTP.

## Escopo

US011.B entrega:

- contrato JSON para substituição da tabela vigente;
- endpoint `PUT` PC-scoped;
- validação do payload;
- guard reutilizável de acesso a Ponto de Coleta;
- versionamento diferencial e atômico das regras;
- resposta de concorrência otimista;
- migração restrita de `GeneratePcQrCode` para o guard;
- testes unitários e de integração.

Não entrega:

- consulta pública da tabela, pertencente à US011.C;
- tela, client ou ViewModel Mobile, pertencentes à US011.D;
- cálculo ou crédito de pontos;
- alterações em `MaterialScoreRule` ou nos enums da US011.A;
- repository dedicado;
- migration ou alteração de schema;
- agendamento de regras futuras;
- lock distribuído ou fila de serialização.

## Contrato HTTP

### Rota

```http
PUT /api/collector-points/{collectorPointId:guid}/score-rules
Authorization: Bearer <token da pessoa física gestora>
Content-Type: application/json
```

A rota recebe o identificador do PC porque uma pessoa física pode gerenciar mais de um Ponto de Coleta. O JWT continua representando a PF; não há troca de token nem representação da PJ no claim.

Esta decisão substitui a rota `/api/collector-points/me/score-rules` descrita originalmente na issue.

### Payload

```json
{
  "rules": [
    {
      "material": "Battery",
      "points": 10.50,
      "unit": "PerUnit"
    },
    {
      "material": "Notebook",
      "points": 75.00,
      "unit": "PerUnit"
    }
  ]
}
```

Criar em `Ecocell.Shared.Requests.CollectorPoints`:

```csharp
public sealed record RequestSetMaterialScoreRulesJson
{
    public List<RequestMaterialScoreRuleJson> Rules { get; init; } = [];
}

public sealed record RequestMaterialScoreRuleJson
{
    public ElectronicMaterial Material { get; init; }
    public decimal Points { get; init; }
    public MaterialScoreUnit Unit { get; init; }
}
```

Os enums públicos mantêm sua representação JSON textual. O endpoint converte explicitamente os contratos Shared para os enums internos antes de enviar o comando.

### Resposta

Sucesso retorna `204 No Content`, inclusive quando o payload é idêntico à tabela vigente. Não criar DTO de resposta nesta subtask; a leitura canônica será definida pela US011.C.

## Semântica de substituição completa

`rules` representa a tabela completa que deve permanecer vigente após o `PUT`:

- material sem regra vigente: criar regra;
- material com regra vigente e pontos ou unidade alterados: fechar a regra anterior e criar nova versão;
- material com regra vigente e valores idênticos: não alterar;
- material vigente omitido: fechar a regra e deixar de aceitá-lo.

Reenviar o mesmo payload é idempotente: não cria versão, não fecha regra e não atualiza auditoria.

Lista vazia é inválida. O MVP exige que um PC mantenha ao menos um material aceito; desativar toda a tabela não faz parte desta subtask.

## Validação

O validator do comando deve acumular e retornar erros de conteúdo como `400 Bad Request`:

- `CollectorPointId` diferente de `Guid.Empty`;
- `Rules` obrigatória, com 1 a 7 itens;
- cada material aparece no máximo uma vez;
- material definido em `ElectronicMaterial`;
- unidade definida em `MaterialScoreUnit`;
- `Points > 0`;
- `Points` compatível com `numeric(10,2)`: até 10 dígitos totais, 2 casas decimais e valor máximo `99.999.999,99`.

As regras não dependem de validação no Mobile. Valores de enum não definidos devem ser rejeitados mesmo quando o desserializador conseguir convertê-los para o tipo.

## Guard PC-scoped

Criar `ICollectorPointAccessGuard` e `CollectorPointAccessGuard` em `Ecocell.Api.Services.CollectorPoints`. Interface e implementação podem permanecer no mesmo arquivo; não há motivo para uma abstração adicional.

Contrato conceitual:

```csharp
Task<Result> EnsureResponsibleActiveAsync(
    Guid collectorPointId,
    CancellationToken cancellationToken);
```

O guard usa `ICurrentUserService` e `AppDbContext` para aplicar, nesta ordem:

1. usuário resolvido, `PersonType.NaturalPerson`, `PersonStatus.Active` e `Role.User`; falha retorna `403 Forbidden`;
2. `LegalPerson` com o ID informado, `Journey.CollectPoint` e `ResponsiblePersonId` igual ao usuário atual; ausência retorna `404 Not Found`;
3. PC com `PersonStatus.Active`; outro status retorna `409 Conflict`.

Combinar existência, jornada e responsabilidade na mesma consulta preserva anti-enumeração: um chamador não distingue PC inexistente, PJ de outra jornada ou PC pertencente a outra pessoa.

Registrar a implementação como serviço scoped. Não criar authorization handler, policy dinâmica ou middleware para este caso; o guard explícito mantém o fluxo local e verificável nos slices VSA.

## Slice `SetMaterialScoreRules`

Criar `src/Ecocell.Api/Features/CollectorPoint/SetMaterialScoreRules.cs` com:

- `Command` contendo `CollectorPointId` e a coleção de regras internas;
- item de comando contendo `ElectronicMaterial`, `Points` e `MaterialScoreUnit` internos;
- `Validator` FluentValidation;
- `Handler`;
- módulo Carter do endpoint.

Dependências do handler:

- `AppDbContext`;
- `IValidator<Command>`;
- `ICollectorPointAccessGuard`;
- `TimeProvider`;
- `ILogger<Handler>`.

Registrar `TimeProvider.System` como singleton. Testes podem fornecer um `TimeProvider` controlado sem adicionar pacote ou abstração própria de relógio.

## Fluxo de aplicação

O handler executa:

1. validar o comando;
2. executar o guard e interromper sem escrita em caso de falha;
3. carregar com tracking todas as regras abertas do PC;
4. indexar regras vigentes e solicitadas por material;
5. calcular inclusões, substituições, encerramentos e itens idênticos;
6. retornar sucesso sem escrita quando não houver mudança;
7. capturar uma única vez `TimeProvider.GetUtcNow().UtcDateTime`;
8. garantir que o instante efetivo seja estritamente posterior às regras que serão fechadas;
9. fechar regras omitidas;
10. fechar e substituir regras cujos pontos ou unidade mudaram;
11. criar regras para materiais ainda sem versão vigente;
12. executar um único `SaveChangesAsync`;
13. retornar sucesso sem acessar o banco novamente.

O mesmo instante UTC fecha versões anteriores e inicia as substitutas, preservando a continuidade `[ValidFrom, ValidTo)` sem lacuna temporal.

Como `Close` exige `closedAt > ValidFrom`, o handler deve tratar a resolução do relógio explicitamente:

- se o relógio retornar exatamente o maior `ValidFrom` entre as regras que serão fechadas, avançar o instante efetivo em 1 microssegundo, compatível com a precisão do PostgreSQL;
- se o relógio retornar valor anterior ao maior `ValidFrom`, rejeitar a operação com `409 Conflict` e registrar regressão do relógio;
- nunca alterar `MaterialScoreRule` para aceitar fechamento no mesmo instante.

Um único `SaveChangesAsync` mantém as alterações na transação automática do EF Core. O teste PostgreSQL deve confirmar que o provider ordena fechamento e inserção sem violar o índice no fluxo normal. Não adicionar `BeginTransaction` redundante enquanto esse contrato estiver verde.

## Concorrência

O índice `IX_MaterialScoreRules_LegalPersonId_Material` permanece a barreira final contra duas regras abertas para o mesmo PC e material.

Quando duas operações realmente concorrentes partem do mesmo estado:

- a primeira transação confirmada vence;
- a transação que colidir com o índice sofre rollback integral;
- o handler traduz somente a violação `23505` desse índice para `409 Conflict`;
- outras `DbUpdateException` não são mascaradas como conflito de negócio.

Uma segunda chamada que começar depois do primeiro commit não é perdedora: ela avalia o estado atualizado e pode retornar `204` como no-op ou aplicar um novo diff.

Não haverá retry automático ou política de “última escrita vence”. O cliente poderá consultar o estado atual pela US011.C e reenviar sua intenção.

## Migração de `GeneratePcQrCode`

Alterar somente a autorização interna de `GeneratePcQrCode`:

- substituir verificações duplicadas pelo `ICollectorPointAccessGuard`;
- preservar rota, payload, resposta QR e status HTTP;
- remover dependências e consultas que se tornarem redundantes;
- manter `404` para PC inexistente, jornada errada ou outro responsável;
- manter `409` para PC próprio não ativo;
- manter `403` para chamador inelegível.

Essa migração torna o guard a implementação única do contrato PC-scoped já existente, sem ampliar o escopo funcional do QR.

## Códigos HTTP

- `204 No Content`: alteração concluída ou payload idêntico;
- `400 Bad Request`: ID, coleção ou item inválido;
- `401 Unauthorized`: token ausente ou inválido, tratado pela policy `Authenticated`;
- `403 Forbidden`: pessoa autenticada inelegível;
- `404 Not Found`: PC inexistente, de jornada diferente ou não pertencente à PF;
- `409 Conflict`: PC próprio não ativo ou colisão concorrente no índice de regra aberta;
- `500 Internal Server Error`: falha inesperada.

Usar `Result`, `Error` e `ToProcessResult` existentes. Não criar novo envelope de erro.

## Observabilidade

Usar logs estruturados para:

- falha de elegibilidade do chamador;
- PC não localizado no escopo do gestor;
- PC sem status operacional;
- conclusão do diff com `CollectorPointId` e contagens de regras criadas, substituídas, encerradas e inalteradas;
- conflito concorrente.

Não registrar o payload completo nem dados pessoais do gestor.

## Estratégia de testes

Aplicar TDD Red → Green → Refactor.

### Testes unitários do validator

Provar:

- comando válido;
- ID vazio;
- lista nula ou vazia;
- mais de sete itens;
- material duplicado;
- material e unidade fora dos enums;
- pontos zero, negativos, acima da precisão e com mais de duas casas.

### Testes unitários do guard

Provar:

- acesso permitido para PF `Role.User`, ativa, responsável por PC ativo;
- `403` para usuário ausente, tipo diferente, status não ativo, Admin ou Suporte;
- `404` para ID inexistente, jornada diferente ou outro responsável;
- `409` para PC próprio `PendingApproval`, `Suspended`, `Refused` ou outro status não ativo.

### Testes unitários do handler

Provar:

- criação da tabela inicial;
- alteração fecha versão anterior e cria nova;
- histórico anterior permanece persistido;
- omissão fecha material vigente;
- inclusão, alteração, remoção e no-op podem coexistir no mesmo lote;
- regras idênticas preservam IDs, datas e quantidade de versões;
- todas as mudanças do lote usam o mesmo instante UTC;
- igualdade de relógio avança o instante efetivo em 1 microssegundo;
- regressão do relógio retorna `409` sem escrita;
- falha de validação ou guard não escreve;
- sucesso retorna `Result.Success`.

### Regressão do QR

Atualizar os testes de `GeneratePcQrCode` para o guard e manter os cenários atuais de sucesso, `403`, `404` e `409`.

### Testes de integração

No PostgreSQL real via Testcontainers, provar:

- `401` sem token;
- `204` para criação válida;
- `204` sem nova versão ao reenviar payload idêntico;
- `400` para contrato inválido;
- `403` para chamador inelegível;
- `404` para outro responsável;
- `409` para PC próprio não ativo;
- versionamento diferencial preserva histórico;
- remoção por omissão encerra a regra;
- atualização e inserção funcionam em um único `SaveChangesAsync` com o índice filtrado;
- sob concorrência efetiva, uma transação vence, a colisão retorna `409` e não sobra estado parcial.

O cenário concorrente deve usar sincronização determinística no ambiente de teste; não depender apenas do timing de duas chamadas paralelas.

### Verificação final

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj
dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj
dotnet test Ecocell.slnx
```

Os testes de integração exigem Docker disponível.

## Estrutura prevista

- `src/Ecocell.Shared/Requests/CollectorPoints/RequestSetMaterialScoreRulesJson.cs`
- `src/Ecocell.Api/Services/CollectorPoints/CollectorPointAccessGuard.cs`
- `src/Ecocell.Api/Features/CollectorPoint/SetMaterialScoreRules.cs`
- `src/Ecocell.Api/Features/CollectorPoint/GeneratePcQrCode.cs`
- `src/Ecocell.Api/Extensions/DependencyInjectionExtensions.cs`
- `tests/Ecocell.UnitTests/Services/CollectorPoints/CollectorPointAccessGuardTests.cs`
- `tests/Ecocell.UnitTests/Features/CollectorPoint/SetMaterialScoreRulesTests.cs`
- `tests/Ecocell.UnitTests/Features/CollectorPoint/GeneratePcQrCodeTests.cs`
- `tests/Ecocell.IntegrationTests/Features/CollectorPoint/SetMaterialScoreRulesTests.cs`

Nenhum arquivo de migration ou snapshot deve mudar.

## Critérios de aceite

- somente PF ativa, `Role.User` e responsável por PC ativo altera regras;
- acesso a PC inexistente, de outra jornada ou de outro gestor retorna `404`;
- payload representa a tabela vigente completa;
- material omitido deixa de ser aceito por encerramento, sem exclusão;
- mudanças preservam versões anteriores;
- payload idêntico não cria histórico artificial;
- lote usa um único instante UTC;
- o instante efetivo permanece estritamente monotônico em relação às versões fechadas;
- persistência é atômica;
- conflito concorrente não sobrescreve silenciosamente e retorna `409`;
- sucesso retorna `204`;
- QR usa o mesmo guard sem regressão HTTP;
- testes unitários, integração e solução completa passam;
- nenhuma migration é criada.

## Impactos conhecidos

- US011.C deve ler regras vigentes usando `ValidFrom <= instante` e `ValidTo IS NULL OR ValidTo > instante`.
- US011.D deve enviar sempre a tabela completa e tratar `409` recarregando o estado antes de novo envio.
- WND-288 poderá considerar um material aceito somente quando houver regra vigente para o PC.
- A decisão `404` anti-enumeração substitui o `403` para outro responsável registrado na spec anterior do shell PC; `403` permanece reservado à inelegibilidade do chamador.

## Premissa operacional

O `AGENTS.md` referencia `.Codex/rules/coding-rules.md`, `.Codex/rules/unit-tests-rules.md` e `.Codex/rules/integration-tests-rules.md`, mas esses arquivos não estão presentes neste checkout. O plano deve seguir as regras explícitas do `AGENTS.md` e os padrões observados no código e nos testes existentes. Se os arquivos normativos reaparecerem antes da implementação, devem ser lidos e prevalecer em caso de conflito.
