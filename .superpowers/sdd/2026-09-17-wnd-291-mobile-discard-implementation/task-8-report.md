# Task 8 — Lista e conferência do Ponto de Coleta

## Status

Implementação concluída com `ConfirmDiscardViewModel`, rota de pendências, CSS scoped e registro transitório na DI. A revisão foi incorporada: `ContextChanged` cancela operações do ponto anterior, invalida lista/detalhe/drafts, recarrega o novo ponto válido e a página redireciona com segurança quando não há contexto PC. O card usa uma superfície `EcoCard` com botão interno semântico.

## Verificações

- `dotnet build src/Ecocell.Mobile/Ecocell.Mobile.csproj -f net10.0-windows10.0.19041.0 --no-restore -p:UseSharedCompilation=false -v minimal` executado após a correção com `TEMP`/`TMP` isolados em `.temp-test`; o runner exibiu `Ecocell.Shared -> ...Ecocell.Shared.dll` e a geração XAML, sem erros de compilação exibidos.
- `git diff --check` dos caminhos alterados da Task 8 passou.
- Busca de hex, estilo inline e componentes Mud crus na tela nova não retornou ocorrências.

## Commit

- `feat: implementa conferência mobile de descartes`

## Limitações

- A história não possui infraestrutura de testes automatizados Mobile; o smoke visual/interativo requer um host Windows com API acessível.
- O runner desta sessão não devolve o resumo/exit code final do projeto MAUI, portanto a evidência de build é a saída de compilação emitida, não uma contagem final de erros/avisos.
