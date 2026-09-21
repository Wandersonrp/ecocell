---
name: sonar-babysit
description: >
  Verifica e corrige falhas do SonarCloud/SonarQube no GitHub Actions. Use quando o usuário
  disser "verifique o Sonar", "limpe o PR", "resolva o quality gate", "o Sonar falhou",
  "CI quebrado", "fix sonar issues", ou qualquer menção a quality gate, coverage, code smells,
  vulnerabilidades ou bugs apontados pelo Sonar. Executa o ciclo completo: diagnóstico via GH
  CLI → análise de log → correção técnica → validação local → push.
disable-model-invocation: true
allowed-tools: Bash(gh *) Bash(git *) Bash(dotnet *) Bash(cat *) Bash(find *) Read Write
argument-hint: "[branch]"
---

# Sonar Babysitting — Quality Gate Remediation

Ciclo completo de detecção e correção de falhas do SonarCloud/SonarQube no GitHub Actions.

## Fluxo de execução

Execute sempre nesta ordem:

1. **Monitorar** — identificar o run com falha no branch atual (ou `$ARGUMENTS` se informado)
2. **Diagnosticar** — extrair a causa raiz do log da step do Sonar
3. **Corrigir** — aplicar a correção técnica no código-fonte
4. **Validar** — rodar `dotnet test` localmente antes de qualquer push
5. **Push** — commitar com mensagem padronizada e subir

## Fase 1 — Monitorar

```bash
BRANCH=${ARGUMENTS:-$(git rev-parse --abbrev-ref HEAD)}
gh run list --branch "$BRANCH" --limit 5
```

- `failure` → avançar para Fase 2
- `in_progress` → aguardar e re-checar
- `success` → informar que o pipeline está verde e encerrar

Obter o log do run com falha:

```bash
RUN_ID=$(gh run list --branch "$BRANCH" --limit 1 --json databaseId --jq '.[0].databaseId')
gh run view "$RUN_ID" --log-failed
```

## Fase 2 — Diagnosticar

Localizar a step do Sonar no log. Nomes comuns:
- `SonarCloud Analysis` / `SonarQube Analysis`
- `Run SonarCloud action`
- `dotnet-sonarscanner end`

Identificar o tipo de falha — consulte [diagnostics.md](diagnostics.md) para a tabela completa
de padrões de log e como interpretar cada um.

## Fase 3 — Corrigir

Abrir os arquivos citados no log e aplicar a correção. Dependendo do tipo:

- **Bugs / Vulnerabilidades** → ver regras e correções em [fixes.md](fixes.md#bugs-e-vulnerabilidades)
- **Cobertura insuficiente** → gerar testes com os templates em [fixes.md](fixes.md#cobertura)
- **Code smells** → ver refatorações em [fixes.md](fixes.md#code-smells)

## Fase 4 — Validar

Nunca fazer push sem rodar os testes localmente:

```bash
dotnet test --verbosity normal
```

Se houver falhas: corrigir a regressão antes de avançar. Apenas continuar com **0 falhas**.

## Fase 5 — Push

```bash
git add -A
git commit -m "fix: resolve sonar quality gate issues"
git push origin HEAD
```

Usar mensagens de commit específicas por tipo — ver tabela em [fixes.md](fixes.md#mensagens-de-commit).

Após o push, monitorar o novo run:

```bash
gh run list --branch "$BRANCH" --limit 1
```
