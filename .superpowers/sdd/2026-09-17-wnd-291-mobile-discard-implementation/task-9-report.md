# Task 9 — Home e navegação de notificações do Ponto de Coleta

## Status

Implementados o cartão de descartes pendentes com contador acessível, o rationale persistido de notificações e a navegação por toque de notificação revalidada contra a lista autenticada de pontos ativos. A barra inferior existente, o switcher e o FAB de QR foram preservados.

## Testes e verificações

- Busca de literais hexadecimais, estilos inline e componentes Mud crus nas telas verificadas não retornou ocorrências.
- `git diff --check` passou para as alterações da Task 9.
- `dotnet ef migrations has-pending-model-changes --project src/Ecocell.Api --startup-project src/Ecocell.Api` passou: não há mudanças de modelo pendentes.
- `dotnet test Ecocell.slnx` não concluiu verde: 415 testes unitários passaram; os 85 testes de integração falharam ao iniciar por ausência da seção `Nominatim` em `appsettings`.
- A compilação Mobile dos TFMs Windows, Android, iOS e MacCatalyst é bloqueada por `src/Ecocell.Mobile/Components/Pages/Discards/Confirm.razor:35` (`RZ9986`), arquivo fora do escopo da Task 9 e não alterado nesta worktree.

## Concerns

- O smoke interativo de permissão, toque de notificação, troca de contexto e barra inferior requer um host MAUI com API acessível; não foi executado neste ambiente.
- A dependência `SQLitePCLRaw.lib.e_sqlite3` em `Ecocell.UnitTests` emite o warning pré-existente `NU1903` durante a restauração.
