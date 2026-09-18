# Task 8 — Lista e conferência do Ponto de Coleta

## Status

Implementação concluída com `ConfirmDiscardViewModel`, rota de pendências, CSS scoped e registro transitório na DI. A lista é ordenada pelo descarte mais antigo, e operações de confirmação/rejeição preservam o contexto de geração e os drafts em falhas recuperáveis.

## Verificações

- `rtk dotnet build src/Ecocell.Mobile/Ecocell.Mobile.csproj -f net10.0-windows10.0.19041.0` executado com `TEMP`/`TMP` isolados em `.temp-test`, após o diretório temporário global bloquear o MSBuild.
- `dotnet build src/Ecocell.Mobile/Ecocell.Mobile.csproj -f net10.0-windows10.0.19041.0 --no-restore -p:UseSharedCompilation=false -v minimal` executado antes e depois do commit com o mesmo isolamento; o runner exibiu a compilação de `Ecocell.Shared` e a geração XAML, sem erros, mas não devolveu o resumo final/exit code do MAUI.
- `git diff --check` nos caminhos da Task 8 passou.
- Busca de hex, estilo inline e componentes Mud crus nas telas novas não retornou ocorrências.

## Commit

- `feat: implementa conferência mobile de descartes`

## Limitações

- A história não possui infraestrutura de testes automatizados Mobile; o smoke visual/interativo requer um host Windows com API acessível.
