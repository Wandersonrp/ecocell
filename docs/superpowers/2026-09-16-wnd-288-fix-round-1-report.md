# WND-288 — Fix round 1 report

Base revisada: `b6ec68e`.

## Achados corrigidos

1. `ApproximateWeightKg` agora aplica `PrecisionScale(10, 3, false)` após manter a regra `> 0`, com a mensagem: `O peso aproximado deve ter até 10 dígitos totais e 3 casas decimais.`
2. Itens nulos são preservados no mapeamento DTO-para-command e rejeitados pelo validator como erro de validação; o endpoint retorna 400 e não persiste o descarte. O comportamento de `Items: null` continua sendo mapeado para coleção vazia.
3. O parser do QR agora valida o texto bruto no formato canônico `ecocell://pc/{guid}`. Apenas o esquema e o host permanecem case-insensitive; caminhos normalizados, texto percent-encoded e GUID em maiúsculas são rejeitados.

## Arquivos alterados

- `src/Ecocell.Api/Features/Discard/RegisterDiscard.cs`
- `tests/Ecocell.UnitTests/Features/Discard/RegisterDiscardTests.cs`
- `tests/Ecocell.IntegrationTests/Features/Discard/RegisterDiscardTests.cs`
- `docs/superpowers/2026-09-16-wnd-288-fix-round-1-report.md`

## Evidência

### RED observado antes da correção

`dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore --filter "FullyQualifiedName~Features.Discard.RegisterDiscardTests"`

- Exit code: `1`
- Total: `37`; aprovados: `31`; falhas: `6`
- Falhas reproduzidas: pesos `0.0001` e `10000000.000` aceitos; QR com `x/../` e GUID em maiúsculas aceitos; item nulo lança `NullReferenceException` no validator e no handler.
- O QR percent-encoded já era rejeitado e permaneceu como regressão coberta.

### GREEN final

| Comando | Resultado |
| --- | --- |
| `dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --no-restore --no-build --filter "FullyQualifiedName~Features.Discard.RegisterDiscardTests"` | Exit `0`; 37/37 aprovados |
| `dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --no-restore --no-build --filter "FullyQualifiedName~Features.Discard.RegisterDiscardTests"` | Exit `0`; 8/8 aprovados |
| `dotnet test Ecocell.slnx --no-restore` | Exit `0`; 323 unitários e 60 integrações aprovados |

## Observação

Os builds de integração exibiram avisos preexistentes `CS0618` para os construtores sem imagem de `PostgreSqlBuilder` e `RedisBuilder`; não foram modificados por esta rodada.
