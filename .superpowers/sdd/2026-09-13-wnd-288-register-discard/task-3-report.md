# WND-288 — Task 3: Handler e snapshot da regra

## Status

Implementado no branch `feature/descarte-qr-code`. O commit desta tarefa contém somente o slice e seu arquivo de testes; alterações preexistentes no worktree foram preservadas.

## Escopo alterado

- `src/Ecocell.Api/Features/Discard/RegisterDiscard.cs`
  - Adicionado `RegisterDiscard.Handler` com validação, autorização por `NaturalPerson` ativa em `Journey.Depositor`, leitura do QR já existente, consulta do ponto, consulta de regras vigentes, criação de `Discard` pendente e resposta.
  - Usa diretamente `AppDbContext`, um único `SaveChangesAsync` para a abertura e log de sucesso contendo apenas IDs.
- `tests/Ecocell.UnitTests/Features/Discard/RegisterDiscardTests.cs`
  - Adicionada fixture com `TestBase`, `ICurrentUserService` mockado, depositante, ponto e regra.
  - Adicionados casos de caminho feliz, autorização, ponto de coleta, validade de regra, não persistência parcial e snapshot de `MaterialScoreRuleId`.

## Evidência TDD

### RED

Após adicionar somente a fixture e `Handle_ShouldPersistPendingDiscard_WhenRequestIsValid`, foi executado:

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Handle_ShouldPersistPendingDiscard"
```

Resultado observado: falha de compilação esperada `CS0426`: `Handler` não existe no tipo `RegisterDiscard`. O build também reportou `MSG0005` sobre a mensagem sem handler registrado.

### GREEN

Após a implementação mínima do handler, foi executado o mesmo comando:

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Handle_ShouldPersistPendingDiscard"
```

Resumo exato: `Com falha: 0, Aprovado: 1, Ignorado: 0, Total: 1`.

Após incluir os demais casos do brief:

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~RegisterDiscardTests"
```

Resumo exato: `Com falha: 0, Aprovado: 28, Ignorado: 0, Total: 28`.

## Reconciliação aplicada

O construtor atual de `MaterialScoreRule` recebe `validFrom`, mas não `validTo`. Portanto, o helper `AddRule` cria a regra com `validFrom` e, quando recebe `validTo`, chama `rule.Close(validTo.Value)` antes de adicioná-la ao contexto. Nenhuma alteração foi feita no código de produção de WND-168.

## Verificação

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore
```

Resumo exato: `Com falha: 0, Aprovado: 314, Ignorado: 0, Total: 314`.

```powershell
dotnet test Ecocell.slnx --no-restore
```

Uma tentativa retornou apenas o build parcial dos projetos e não devolveu resumo de teste. A repetição com saída/código de saída explícitos falhou antes dos testes por ambiente: `MSB1025` e `UnauthorizedAccessException` ao criar `C:\Users\Wnd\AppData\Local\Temp\MSBuildTemp3hwbeam2.sgs`. Uma repetição elevada foi solicitada, mas não devolveu saída de console nesta sessão; por isso a solução completa não é considerada confirmada neste relatório.

### Atualização posterior à conclusão do Task 3

Conforme solicitado para resolver a pendência, o comando foi executado diretamente sob permissão elevada, sem RTK:

```powershell
dotnet test Ecocell.slnx --no-restore
```

O terminal não repetiu `MSBuildTemp`; ele devolveu somente estes itens de build antes de encerrar a captura: `Ecocell.Shared -> ...net10.0\Ecocell.Shared.dll`, quatro avisos informativos de `MauiXamlInflator=SourceGen` e `Ecocell.Mobile -> ...net10.0-maccatalyst\maccatalyst-x64\Ecocell.Mobile.dll`. Não devolveu resumo unitário, resumo de integração, `TERMINAL_EXIT_CODE` nem erro.

Foi feita uma segunda execução direta elevada com `--logger "console;verbosity=normal"` e o mesmo sufixo PowerShell para imprimir `TERMINAL_EXIT_CODE`; a captura retornou vazia. Não há terminal de aplicativo anexado a esta tarefa para recuperar o código de saída. Portanto, não existe evidência terminal de sucesso da solução completa a anexar: a verificação permanece inconclusiva, embora a suíte unitária completa já registrada tenha passado com `314/314`.

### Atualização de verificação independente

Esta evidência substitui a tentativa inconclusiva acima. O controller executou diretamente, sob permissão elevada:

```powershell
dotnet test Ecocell.slnx --no-restore
```

Código de saída: `0`.

Resumos finais exatos:

- `Ecocell.UnitTests`: `314` aprovados, `0` com falha, `0` ignorados, total `314`.
- `Ecocell.IntegrationTests`: `52` aprovados, `0` com falha, `0` ignorados, total `52`.

O build emitiu apenas os avisos existentes de geração de fonte XAML; não houve falhas de teste. A verificação integral da solução está confirmada.

### Fix round 1 — mapeamento de falha de validação

Foi adicionado somente o teste `Handle_ShouldReturnValidationError_WhenRequestIsInvalid`. Ele envia um QR Code inválido ao handler e prova que o resultado falha com `ErrorCodes.ErrorOnValidation` sem persistir `Discard`.

Verificação focalizada executada sob permissão elevada:

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore --filter "FullyQualifiedName~RegisterDiscardTests"
```

Resumo exato: `Com falha: 0, Aprovado: 29, Ignorado: 0, Total: 29`; `TERMINAL_EXIT_CODE=0`.

Também foram iniciados diretamente sob permissão elevada:

```powershell
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore
dotnet test Ecocell.slnx --no-restore
```

Em ambas as execuções, o adaptador de terminal encerrou a captura após linhas de build, embora os processos `dotnet` tenham finalizado depois; não devolveu resumo final nem código de saída. Não há novas contagens completas verificáveis a registrar para este round além do resultado focalizado acima. A evidência independente anterior da solução continua sendo `314/314` testes unitários e `52/52` testes de integração antes deste teste adicional.

```powershell
git diff --check
```

Resultado: saída sem erros de whitespace. Git emitiu apenas avisos de conversão LF/CRLF e de acesso ao ignore global, sem apontar problema no diff.

## Auto-revisão

- O handler reutiliza `TryGetCollectorPointId`; não introduz outro parser de QR.
- A autorização ignora `Role` e exige somente pessoa física, ativa e depositante; os casos Admin e Support com essa combinação persistem normalmente.
- O ponto é limitado à jornada `CollectPoint`, exige status ativo e usa `AsNoTracking`.
- A regra é filtrada no intervalo `[ValidFrom, ValidTo)`, pelo ponto e pelo material; qualquer material sem regra impede a persistência.
- Cada `DiscardItem` recebe o `MaterialScoreRuleId` vigente. O teste de versionamento prova que o item mantém o ID original depois de fechar/criar versão da regra.
- Não foram adicionados endpoint, confirmação, rejeição, crédito, idempotência, repositório, serviço, mapper ou clock.

## Concernos

- A preocupação anterior sobre a verificação integral de `Ecocell.slnx` foi substituída pela evidência independente posterior: código de saída `0`, `314/314` testes unitários e `52/52` testes de integração aprovados.
- Há alterações preexistentes e não relacionadas no worktree; elas não foram modificadas nem incluídas no staging desta tarefa.
