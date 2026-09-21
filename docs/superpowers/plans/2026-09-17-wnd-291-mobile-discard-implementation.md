# WND-291 Mobile Discard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar leitura e digitação de QR pelo Depositante, abertura do descarte, conferência pelo Ponto de Coleta e notificação local de novas pendências.

**Architecture:** Dois novos slices VSA fornecem preview do QR e lista de pendências, enquanto os comandos existentes continuam responsáveis por abrir, confirmar e rejeitar. O MAUI Blazor usa um único `IDiscardClient`, ViewModels transitórios, scanner MAUI nativo e um monitor singleton limitado ao foreground; Razor permanece responsável pela composição visual e navegação.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, Carter, Mediator, FluentValidation, EF Core, PostgreSQL, SQLite in-memory, xUnit, Shouldly, Moq, Bogus, Testcontainers, .NET MAUI Blazor Hybrid, MudBlazor 9, Refit 10.1.6, ZXing.Net.Maui.Controls 0.10.3 e Plugin.LocalNotification 14.1.1.

**Spec:** `docs/superpowers/specs/2026-09-17-wnd-291-mobile-discard-design.md`

## Global Constraints

- Antes de executar, ler a specification e `.claude/rules/coding-rules.md`, `.claude/rules/unit-tests-rules.md`, `.claude/rules/integration-tests-rules.md` e `.claude/rules/design-system-rules.md`.
- A worktree atual contém alterações alheias. Iniciar a execução com `superpowers:using-git-worktrees`, a partir do commit `2347071` ou de descendente confirmado, sem transportar arquivos não versionados.
- Aplicar Red → Green → Refactor em toda alteração de API. Não escrever produção antes do teste correspondente falhar.
- Cada slice da API fica em um arquivo com query, validator, handler e endpoint; DTO público nunca é o command interno.
- Não criar repository, mapper, service genérico, cache, event bus, endpoint de contador ou migration.
- Preservar `404` anti-enumeração para PC fora do escopo e `409` para inatividade, terminalidade e concorrência.
- Scanner de câmera somente Android/iOS; Windows e indisponibilidade de câmera usam entrada manual.
- Polling somente foreground/resume: imediato e depois a cada 30 segundos; sem push remoto e sem background polling.
- Não registrar QR completo, nome do Depositante, composição ou endereço; notificações não expõem dados do Depositante.
- Todas as telas seguem `D:\Design\ecocell.pen`, frames `NVBNO`, `jNSlX` e `J0tvXz`.
- Inspecionar o arquivo `.pen` somente pelo MCP do Pen.dev; nunca ler ou editar o arquivo bruto.
- UI final usa componentes `Eco*`, CSS scoped, tokens, textos pt-BR, Inter 400/600 e alvos de toque de pelo menos 44 × 44.
- A specification aprovada não autoriza criar infraestrutura de testes automatizados Mobile; validar Mobile por build e smoke manual reproduzível.
- Usar RTK nos comandos suportados. Verificação final obrigatória: `rtk dotnet test Ecocell.slnx` com Docker disponível.
- Cada commit usa staging por caminho e deve passar `git diff --cached --check`; nunca incluir mudanças alheias.

---

## File Map

### Shared e API

- `src/Ecocell.Shared/Utils/CollectorPointQrCode.cs`: parser canônico compartilhado por API e scanner.
- `src/Ecocell.Shared/Responses/Discards/ResponseDiscardPreviewJson.cs`: preview público do PC.
- `src/Ecocell.Shared/Responses/Discards/ResponsePendingDiscardListJson.cs`: envelope da lista.
- `src/Ecocell.Shared/Responses/Discards/ResponsePendingDiscardJson.cs`: cabeçalho da pendência.
- `src/Ecocell.Shared/Responses/Discards/ResponsePendingDiscardItemJson.cs`: composição declarada.
- `src/Ecocell.Api/Features/Discard/RegisterDiscard.cs`: troca o parser local pela utility compartilhada.
- `src/Ecocell.Api/Features/Discard/PreviewDiscardCollectorPoint.cs`: valida QR e retorna PC, endereço e materiais vigentes.
- `src/Ecocell.Api/Features/Discard/ListPendingDiscards.cs`: autoriza o gestor e retorna somente pendências do PC.
- `tests/Ecocell.UnitTests/Shared/Utils/CollectorPointQrCodeTests.cs`: contrato canônico do parser.
- `tests/Ecocell.UnitTests/Features/Discard/PreviewDiscardCollectorPointTests.cs`: branches do preview.
- `tests/Ecocell.UnitTests/Features/Discard/ListPendingDiscardsTests.cs`: branches da lista.
- `tests/Ecocell.IntegrationTests/Features/Discard/PreviewDiscardCollectorPointTests.cs`: contrato HTTP do preview.
- `tests/Ecocell.IntegrationTests/Features/Discard/ListPendingDiscardsTests.cs`: contrato HTTP da lista.

### Mobile

- `src/Ecocell.Mobile/Ecocell.Mobile.csproj`: pacotes ZXing e notificação.
- `src/Ecocell.Mobile/MauiProgram.cs`: inicialização dos plugins, Refit e DI.
- `src/Ecocell.Mobile/App.xaml.cs`: sinais de foreground/background para o monitor.
- `src/Ecocell.Mobile/Platforms/Android/AndroidManifest.xml`: permissão de câmera.
- `src/Ecocell.Mobile/Platforms/iOS/Info.plist`: justificativa de câmera.
- `src/Ecocell.Mobile/Services/Api/IDiscardClient.cs`: cinco operações HTTP do fluxo.
- `src/Ecocell.Mobile/Services/Scanner/IQrScanner.cs`: contrato e resultado do scanner.
- `src/Ecocell.Mobile/Services/Scanner/MauiQrScanner.cs`: modal nativo, permissão e resultado único.
- `src/Ecocell.Mobile/Services/Scanner/QrScannerPage.xaml`: câmera nativa em tela cheia.
- `src/Ecocell.Mobile/Services/Scanner/QrScannerPage.xaml.cs`: detecção, cancelamento e deduplicação.
- `src/Ecocell.Mobile/Services/Notifications/PendingDiscardMonitor.cs`: polling, baseline, contador e notificação.
- `src/Ecocell.Mobile/Models/Discards/DiscardItemDraft.cs`: edição e validação local reutilizada.
- `src/Ecocell.Mobile/Models/Discards/ElectronicMaterialDisplay.cs`: labels pt-BR dos materiais.
- `src/Ecocell.Mobile/ViewModels/ScanViewModel.cs`: preview, itens e abertura.
- `src/Ecocell.Mobile/ViewModels/ConfirmDiscardViewModel.cs`: lista, conferência, confirmação e rejeição.
- `src/Ecocell.Mobile/Components/Pages/Discards/Scan.razor(.css)`: fluxo do Depositante.
- `src/Ecocell.Mobile/Components/Pages/Discards/Confirm.razor(.css)`: lista e conferência do PC.
- `src/Ecocell.Mobile/Components/Shared/Buttons/EcoIconButton.razor(.css)`: ação de ícone acessível reutilizada.
- `src/Ecocell.Mobile/Services/Navigation/DepositorNav.cs`: itens compartilhados de navegação do Depositante.
- `src/Ecocell.Mobile/Components/Pages/Map/Nearby.razor`: usa a navegação compartilhada e a nova rota.
- `src/Ecocell.Mobile/Components/Pages/CollectorPoints/CollectorPointHome.razor(.css)`: card, contador e rationale.
- `src/Ecocell.Mobile/Components/Routes.razor`: revalidação e navegação após toque na notificação.
- `src/Ecocell.Mobile/Components/_Imports.razor`: imports dos novos contratos e modelos.

---

### Task 1: Compartilhar o parser canônico do QR

**Files:**
- Create: `src/Ecocell.Shared/Utils/CollectorPointQrCode.cs`
- Modify: `src/Ecocell.Api/Features/Discard/RegisterDiscard.cs`
- Create: `tests/Ecocell.UnitTests/Shared/Utils/CollectorPointQrCodeTests.cs`
- Test: `tests/Ecocell.UnitTests/Features/Discard/RegisterDiscardTests.cs`

**Interfaces:**
- Consumes: QR no formato `ecocell://pc/{guid-D-em-minúsculas}`.
- Produces: `CollectorPointQrCode.TryParse(string? value, out Guid collectorPointId)` para API e Mobile.

- [ ] **Step 1: Executar a regressão existente antes do refactor**

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~RegisterDiscardTests"
```

Expected: PASS.

- [ ] **Step 2: Escrever os testes do parser e confirmar RED**

Criar o arquivo de teste com estes casos concretos:

```csharp
using Ecocell.Shared.Utils;
using Shouldly;

namespace Ecocell.UnitTests.Shared.Utils;

public sealed class CollectorPointQrCodeTests
{
    private const string Id = "018f3f2a-7b4c-7c91-8d31-5f7a2b6c9e10";

    [Fact]
    public void TryParse_ShouldReturnId_WhenValueIsCanonical()
    {
        var parsed = CollectorPointQrCode.TryParse($"ecocell://pc/{Id}", out var result);

        parsed.ShouldBeTrue();
        result.ShouldBe(Guid.Parse(Id));
    }

    [Fact]
    public void TryParse_ShouldAllowDifferentSchemeAndHostCasing()
    {
        CollectorPointQrCode.TryParse($"EcOcElL://PC/{Id}", out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-qr")]
    [InlineData("ecocell://pc/018F3F2A-7B4C-7C91-8D31-5F7A2B6C9E10")]
    [InlineData("ecocell://pc/x/../018f3f2a-7b4c-7c91-8d31-5f7a2b6c9e10")]
    public void TryParse_ShouldReturnFalse_WhenValueIsNotCanonical(string? value)
    {
        CollectorPointQrCode.TryParse(value, out var result).ShouldBeFalse();
        result.ShouldBe(Guid.Empty);
    }
}
```

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~CollectorPointQrCodeTests"
```

Expected: FAIL porque `CollectorPointQrCode` ainda não existe.

- [ ] **Step 3: Implementar a utility mínima**

```csharp
namespace Ecocell.Shared.Utils;

public static class CollectorPointQrCode
{
    private const string Prefix = "ecocell://pc/";

    public static bool TryParse(string? value, out Guid collectorPointId)
    {
        collectorPointId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || !value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rawId = value[Prefix.Length..];
        return rawId.Length == 36
            && Guid.TryParseExact(rawId, "D", out collectorPointId)
            && rawId == collectorPointId.ToString("D");
    }
}
```

Em `RegisterDiscard`, importar `Ecocell.Shared.Utils`, substituir todas as chamadas a `TryGetCollectorPointId` por `CollectorPointQrCode.TryParse` e remover o método local.

- [ ] **Step 4: Rodar parser e regressão do registro**

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~CollectorPointQrCodeTests|FullyQualifiedName~RegisterDiscardTests"
```

Expected: PASS.

- [ ] **Step 5: Commit seletivo**

```powershell
git add -- src/Ecocell.Shared/Utils/CollectorPointQrCode.cs src/Ecocell.Api/Features/Discard/RegisterDiscard.cs tests/Ecocell.UnitTests/Shared/Utils/CollectorPointQrCodeTests.cs
git diff --cached --check
git commit -m "refactor: compartilha parser de QR do ponto de coleta"
```

---

### Task 2: Implementar o preview do Ponto de Coleta

**Files:**
- Create: `src/Ecocell.Shared/Responses/Discards/ResponseDiscardPreviewJson.cs`
- Create: `src/Ecocell.Api/Features/Discard/PreviewDiscardCollectorPoint.cs`
- Create: `tests/Ecocell.UnitTests/Features/Discard/PreviewDiscardCollectorPointTests.cs`
- Create: `tests/Ecocell.IntegrationTests/Features/Discard/PreviewDiscardCollectorPointTests.cs`
- Modify: `tests/Ecocell.IntegrationTests/IntegrationTestBase.cs`

**Interfaces:**
- Consumes: `CollectorPointQrCode.TryParse`, `ICurrentUserService`, `MaterialScoreRule` e `Address`.
- Produces: `GET /api/v1/discards/preview?qrCode={value}` e `ResponseDiscardPreviewJson`.

- [ ] **Step 1: Criar o DTO público**

```csharp
using Ecocell.Shared.Enums;

namespace Ecocell.Shared.Responses.Discards;

public sealed record ResponseDiscardPreviewJson
{
    public string TradeName { get; init; } = string.Empty;
    public string FormattedAddress { get; init; } = string.Empty;
    public IReadOnlyList<ElectronicMaterial> AcceptedMaterials { get; init; } = [];
}
```

- [ ] **Step 2: Escrever os unit tests e confirmar RED**

O arquivo deve construir handler real com SQLite e mock somente de `ICurrentUserService`. Implementar estes testes:

| Teste | Arrange | Assert |
|---|---|---|
| `Validate_ShouldFail_WhenQrCodeIsInvalid` | `QrCode = "invalid"` | validator inválido |
| `Handle_ShouldReturnForbidden_WhenDepositorIsIneligible` | usuário suspenso, tipo PJ ou jornada `None` | `ForbiddenCodeError` |
| `Handle_ShouldReturnNotFound_WhenCollectorPointDoesNotExist` | QR canônico com ID ausente | `NotFound` |
| `Handle_ShouldReturnConflict_WhenCollectorPointIsInactive` | PC `PendingApproval` | `Conflict` |
| `Handle_ShouldReturnEmptyMaterials_WhenNoRuleIsActive` | PC ativo, sem regra | sucesso e lista vazia |
| `Handle_ShouldReturnOnlyActiveMaterialsInEnumOrder` | regra expirada, futura e duas vigentes inseridas fora de ordem | somente vigentes, ordenadas |
| `Handle_ShouldFormatAddressWithoutPersonalData` | endereço completo com complemento | nome e endereço; DTO não possui CNPJ/e-mail |

O teste feliz deve usar esta asserção de contrato:

```csharp
var result = await CreateHandler().Handle(
    new PreviewDiscardCollectorPoint.Query($"ecocell://pc/{point.Id:D}"),
    CancellationToken.None);

result.IsSuccess.ShouldBeTrue();
result.Value.TradeName.ShouldBe(point.TradeName);
result.Value.AcceptedMaterials.ShouldBe([
    Ecocell.Shared.Enums.ElectronicMaterial.Battery,
    Ecocell.Shared.Enums.ElectronicMaterial.Notebook,
]);
result.Value.FormattedAddress.ShouldContain(address.Street);
```

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~PreviewDiscardCollectorPointTests"
```

Expected: FAIL porque query e handler não existem.

- [ ] **Step 3: Implementar o slice mínimo**

Usar estes tipos e fluxo exatos:

```csharp
public static class PreviewDiscardCollectorPoint
{
    public sealed record Query(string QrCode)
        : IRequest<ResultT<ResponseDiscardPreviewJson>>;

    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator() => RuleFor(value => value.QrCode)
            .NotEmpty()
            .Must(value => CollectorPointQrCode.TryParse(value, out _))
            .WithMessage("O QR Code do Ponto de Coleta é inválido.");
    }
}
```

No handler:

1. executar `ValidateAsync` antes do banco;
2. exigir `NaturalPerson`, `Active` e jornada `Depositor`;
3. extrair o ID com `CollectorPointQrCode.TryParse`;
4. carregar `LegalPerson` com `Address` usando `AsNoTracking` e jornada `CollectPoint`;
5. retornar `404` quando ausente e `409` quando inativo;
6. capturar um único `now = DateTime.UtcNow`;
7. consultar regras com `ValidFrom <= now && (ValidTo == null || ValidTo > now)`;
8. aplicar `Distinct`, ordenar pelo enum e mapear ao enum de `Ecocell.Shared`;
9. retornar endereço no formato `Street, Number[, Complement] — Neighborhood, City/State`.

Injetar `ILogger<Handler>`. Registrar warning estruturado para chamador inelegível, PC ausente ou inativo usando somente `CollectorPointId`; não registrar QR, endereço ou dados do Depositante. Não registrar log de sucesso para a consulta.

Usar um helper privado determinístico:

```csharp
private static string FormatAddress(Address? address)
{
    if (address is null)
        return string.Empty;

    var complement = string.IsNullOrWhiteSpace(address.Complement)
        ? string.Empty
        : $", {address.Complement}";

    return $"{address.Street}, {address.Number}{complement} — "
        + $"{address.Neighborhood}, {address.City}/{address.State}";
}
```

O endpoint deve ser:

```csharp
app.MapGet(
    "api/v1/discards/preview",
    async ([FromQuery] string qrCode, ISender sender, CancellationToken ct) =>
    {
        var result = await sender.Send(new PreviewDiscardCollectorPoint.Query(qrCode), ct);
        return result.ToProcessResult(StatusCodes.Status200OK);
    })
    .WithTags("Discard")
    .WithName("PreviewDiscardCollectorPoint")
    .WithSummary("Exibe o Ponto de Coleta e os materiais aceitos a partir do QR Code.")
    .RequireAuthorization(AuthorizationPolicies.Authenticated)
    .Produces<ResponseDiscardPreviewJson>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status401Unauthorized)
    .Produces(StatusCodes.Status403Forbidden)
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status409Conflict);
```

- [ ] **Step 4: Rodar os unit tests até GREEN**

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~PreviewDiscardCollectorPointTests|FullyQualifiedName~RegisterDiscardTests"
```

Expected: PASS.

- [ ] **Step 5: Tornar o helper de PC ativo compatível com preview**

Em `CreateActiveCollectorPointAsync`, criar `Address`, passar `addressId: address.Id` ao `LegalPerson` e adicionar ambos ao contexto antes do `SaveChangesAsync`. Não alterar assinatura nem status do helper.

```csharp
var address = new Address(
    faker.Address.StreetName(),
    faker.Address.BuildingNumber(),
    faker.Address.SecondaryAddress(),
    faker.Address.City(),
    faker.Address.StateAbbr(),
    faker.Address.ZipCode("########"));

var legalPerson = new LegalPerson(
    legalName: faker.Company.CompanyName(),
    tradeName: faker.Company.CompanyName(),
    cnpj: faker.Company.Cnpj(includeFormatSymbols: false),
    email: faker.Internet.Email(),
    journey: ApiEnums.Journey.CollectPoint,
    addressId: address.Id,
    responsiblePersonId: responsiblePersonId);
```

- [ ] **Step 6: Escrever integration tests e confirmar RED**

Cobrir exatamente:

- `Get_ShouldReturn401_WhenTokenIsMissing`;
- `Get_ShouldReturn400_WhenQrCodeIsInvalid`;
- `Get_ShouldReturn403_WhenPersonIsNotDepositor`;
- `Get_ShouldReturn404_WhenCollectorPointDoesNotExist`;
- `Get_ShouldReturn409_WhenCollectorPointIsInactive`;
- `Get_ShouldReturn200WithEmptyMaterials_WhenNoRuleIsActive`;
- `Get_ShouldReturn200WithPointAndActiveMaterials_WhenRequestIsValid`.

O caminho feliz desserializa `ResponseDiscardPreviewJson` e verifica nome, endereço e lista de materiais. Não asserir texto literal de erro.

```powershell
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~PreviewDiscardCollectorPointTests"
```

Expected: o primeiro run antes de fechar endpoint/seed deve falhar; após a implementação completa, PASS com Docker ativo.

- [ ] **Step 7: Commit seletivo**

```powershell
git add -- src/Ecocell.Shared/Responses/Discards/ResponseDiscardPreviewJson.cs src/Ecocell.Api/Features/Discard/PreviewDiscardCollectorPoint.cs tests/Ecocell.UnitTests/Features/Discard/PreviewDiscardCollectorPointTests.cs tests/Ecocell.IntegrationTests/Features/Discard/PreviewDiscardCollectorPointTests.cs tests/Ecocell.IntegrationTests/IntegrationTestBase.cs
git diff --cached --check
git commit -m "feat: adiciona preview de descarte por QR"
```

---

### Task 3: Implementar a lista de descartes pendentes

**Files:**
- Create: `src/Ecocell.Shared/Responses/Discards/ResponsePendingDiscardListJson.cs`
- Create: `src/Ecocell.Shared/Responses/Discards/ResponsePendingDiscardJson.cs`
- Create: `src/Ecocell.Shared/Responses/Discards/ResponsePendingDiscardItemJson.cs`
- Create: `src/Ecocell.Api/Features/Discard/ListPendingDiscards.cs`
- Create: `tests/Ecocell.UnitTests/Features/Discard/ListPendingDiscardsTests.cs`
- Create: `tests/Ecocell.IntegrationTests/Features/Discard/ListPendingDiscardsTests.cs`

**Interfaces:**
- Consumes: `ICollectorPointAccessGuard.EnsureResponsibleActiveAsync(Guid, CancellationToken)`.
- Produces: `GET /api/v1/collector-points/{collectorPointId}/discards/pending` e os três DTOs de pendência.

- [ ] **Step 1: Criar os DTOs públicos**

```csharp
namespace Ecocell.Shared.Responses.Discards;

public sealed record ResponsePendingDiscardListJson
{
    public IReadOnlyList<ResponsePendingDiscardJson> Items { get; init; } = [];
}

public sealed record ResponsePendingDiscardJson
{
    public Guid Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public string DepositorName { get; init; } = string.Empty;
    public IReadOnlyList<ResponsePendingDiscardItemJson> Items { get; init; } = [];
}

public sealed record ResponsePendingDiscardItemJson
{
    public Ecocell.Shared.Enums.ElectronicMaterial Material { get; init; }
    public int Quantity { get; init; }
    public decimal ApproximateWeightKg { get; init; }
}
```

Manter um tipo público por arquivo, mesmo que o bloco acima mostre o contrato em conjunto.

- [ ] **Step 2: Escrever unit tests e confirmar RED**

Construir o handler com validator real e `Mock<ICollectorPointAccessGuard>`. Cobrir:

| Teste | Resultado |
|---|---|
| `Validate_ShouldFail_WhenCollectorPointIdIsEmpty` | validação falha |
| `Handle_ShouldReturnValidationError_WhenCollectorPointIdIsEmpty` | `ErrorOnValidation` e guard não chamado |
| `Handle_ShouldPropagateForbidden_WhenCallerIsIneligible` | `ForbiddenCodeError` |
| `Handle_ShouldPropagateNotFound_WhenPointIsOutsideScope` | `NotFound` |
| `Handle_ShouldPropagateConflict_WhenPointIsInactive` | `Conflict` |
| `Handle_ShouldReturnEmptyList_WhenNoDiscardIsPending` | sucesso com `Items` vazio |
| `Handle_ShouldReturnOnlyPendingDiscardsFromRequestedPoint` | exclui outro PC, confirmado e rejeitado |
| `Handle_ShouldOrderByCreatedAtThenId` | ordem determinística |
| `Handle_ShouldMapDepositorNameAndDeclaredItems` | nome, material, quantidade e peso corretos |

Asserção central:

```csharp
var result = await _handler.Handle(
    new ListPendingDiscards.Query(point.Id),
    CancellationToken.None);

result.IsSuccess.ShouldBeTrue();
result.Value.Items.Select(value => value.Id)
    .ShouldBe([older.Id, sameTimeLowerId, sameTimeHigherId]);
result.Value.Items.ShouldAllBe(value => value.Id != terminal.Id);
```

Run:

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~ListPendingDiscardsTests"
```

Expected: FAIL porque o slice não existe.

- [ ] **Step 3: Implementar o slice mínimo**

```csharp
public static class ListPendingDiscards
{
    public sealed record Query(Guid CollectorPointId)
        : IRequest<ResultT<ResponsePendingDiscardListJson>>;

    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator() => RuleFor(value => value.CollectorPointId)
            .NotEmpty()
            .WithMessage("O identificador do Ponto de Coleta é obrigatório.");
    }
}
```

O handler deve:

1. validar;
2. chamar o guard e propagar seu erro;
3. consultar `Discards` com `AsNoTracking`, `Depositor` e `Items`;
4. filtrar `CollectorPointId` e `DiscardStatus.Pending`;
5. ordenar por `CreatedAt` e `Id` no banco;
6. mapear após o materialize, ordenando itens por `Material`;
7. retornar `ResponsePendingDiscardListJson` mesmo quando vazio.

O endpoint deve usar `ToProcessResult(StatusCodes.Status200OK)`, autorização autenticada e declarar `200/400/401/403/404/409`.

- [ ] **Step 4: Rodar unit tests até GREEN**

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~ListPendingDiscardsTests"
```

Expected: PASS.

- [ ] **Step 5: Escrever integration tests e confirmar RED**

Cobrir:

- `Get_ShouldReturn401_WhenTokenIsMissing`;
- `Get_ShouldReturn400_WhenCollectorPointIdIsEmpty`;
- `Get_ShouldReturn403_WhenCallerIsIneligible`;
- `Get_ShouldReturn404_WhenPointBelongsToAnotherResponsible`;
- `Get_ShouldReturn409_WhenPointIsInactive`;
- `Get_ShouldReturn200WithEmptyItems_WhenThereAreNoPendingDiscards`;
- `Get_ShouldReturn200WithOnlyOwnPendingDiscardsInOrder`;
- `Get_ShouldNotExposeCpfOrEmail` verificando que o JSON contém `depositorName` e não contém as propriedades `cpf` ou `email`.

Use os helpers reais de autenticação e `CreatePendingDiscardAsync`. Para terminalidade, carregar a entidade e chamar `Confirm` ou `Reject` antes do GET.

```powershell
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~ListPendingDiscardsTests"
```

Expected: RED antes do endpoint completo; depois PASS com Docker.

- [ ] **Step 6: Executar regressão do agregado Discard**

```powershell
rtk dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj --filter "FullyQualifiedName~Features.Discard"
rtk dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj --filter "FullyQualifiedName~Features.Discard"
```

Expected: PASS.

- [ ] **Step 7: Commit seletivo**

```powershell
git add -- src/Ecocell.Shared/Responses/Discards/ResponsePendingDiscardListJson.cs src/Ecocell.Shared/Responses/Discards/ResponsePendingDiscardJson.cs src/Ecocell.Shared/Responses/Discards/ResponsePendingDiscardItemJson.cs src/Ecocell.Api/Features/Discard/ListPendingDiscards.cs tests/Ecocell.UnitTests/Features/Discard/ListPendingDiscardsTests.cs tests/Ecocell.IntegrationTests/Features/Discard/ListPendingDiscardsTests.cs
git diff --cached --check
git commit -m "feat: lista descartes pendentes do ponto de coleta"
```

---

### Task 4: Preparar contratos e dependências Mobile

**Files:**
- Modify: `src/Ecocell.Mobile/Ecocell.Mobile.csproj`
- Modify: `src/Ecocell.Mobile/MauiProgram.cs`
- Modify: `src/Ecocell.Mobile/Components/_Imports.razor`
- Create: `src/Ecocell.Mobile/Services/Api/IDiscardClient.cs`
- Create: `src/Ecocell.Mobile/Models/Discards/DiscardItemDraft.cs`
- Create: `src/Ecocell.Mobile/Models/Discards/ElectronicMaterialDisplay.cs`

**Interfaces:**
- Consumes: DTOs dos Tasks 2 e 3 e requests já existentes.
- Produces: `IDiscardClient`, draft validável e labels pt-BR usados pelos dois ViewModels.

- [ ] **Step 1: Adicionar somente os dois pacotes aprovados**

```xml
<PackageReference Include="Plugin.LocalNotification" Version="14.1.1" />
<PackageReference Include="ZXing.Net.Maui.Controls" Version="0.10.3" />
```

Não alterar versões de MudBlazor, Refit, QRCoder ou MAUI.

- [ ] **Step 2: Inicializar os plugins no builder**

Adicionar imports de `Plugin.LocalNotification` e `ZXing.Net.Maui.Controls`. Encadear antes de `ConfigureFonts`:

```csharp
builder
    .UseMauiApp<App>()
    .UseBarcodeReader()
    .UseLocalNotification()
    .ConfigureFonts(fonts =>
    {
        fonts.AddFont("Inter-VariableFont.ttf", "Inter");
    });
```

Os modelos de notificação usam `Plugin.LocalNotification.Core.Models`.

- [ ] **Step 3: Criar o cliente Refit e registrá-lo**

```csharp
public interface IDiscardClient
{
    [Get("/api/v1/discards/preview")]
    Task<IApiResponse<ResponseDiscardPreviewJson>> PreviewAsync(
        [Query] string qrCode,
        CancellationToken ct = default);

    [Post("/api/v1/discards")]
    Task<IApiResponse<ResponseRegisterDiscardJson>> RegisterAsync(
        [Body] RequestRegisterDiscardJson request,
        CancellationToken ct = default);

    [Get("/api/v1/collector-points/{collectorPointId}/discards/pending")]
    Task<IApiResponse<ResponsePendingDiscardListJson>> ListPendingAsync(
        Guid collectorPointId,
        CancellationToken ct = default);

    [Post("/api/v1/discards/{discardId}/confirm")]
    Task<IApiResponse> ConfirmAsync(
        Guid discardId,
        [Body] RequestConfirmDiscardJson request,
        CancellationToken ct = default);

    [Post("/api/v1/discards/{discardId}/reject")]
    Task<IApiResponse> RejectAsync(Guid discardId, CancellationToken ct = default);
}
```

Registrar com `TransientRetryHandler` externo e `AuthTokenHandler` interno, igual aos clientes autenticados existentes.

- [ ] **Step 4: Implementar labels e draft reutilizável**

`ElectronicMaterialDisplay.Label` deve mapear todos os sete valores:

```csharp
public static string Label(ElectronicMaterial material) => material switch
{
    ElectronicMaterial.Battery => "Pilhas e baterias",
    ElectronicMaterial.CellPhone => "Celulares",
    ElectronicMaterial.PortableGameConsole => "Consoles portáteis",
    ElectronicMaterial.Headphones => "Fones de ouvido",
    ElectronicMaterial.Printer => "Impressoras",
    ElectronicMaterial.Notebook => "Notebooks",
    ElectronicMaterial.SmallAppliance => "Pequenos eletrodomésticos",
    _ => material.ToString(),
};
```

`DiscardItemDraft` deve manter `ElectronicMaterial? Material`, `string QuantityText`, `string WeightText` e mensagens por campo. `TryGetValues` deve:

- recusar material ausente ou duplicado;
- aceitar apenas `int > 0`;
- normalizar vírgula para ponto;
- aceitar `decimal > 0`, no máximo três casas e no máximo `9999999.999m`;
- produzir `ElectronicMaterial`, `int` e `decimal` tipados.

Assinatura:

```csharp
public bool TryGetValues(
    bool materialIsDuplicated,
    out ElectronicMaterial material,
    out int quantity,
    out decimal approximateWeightKg);
```

- [ ] **Step 5: Atualizar imports Razor**

Adicionar:

```razor
@using Ecocell.Mobile.Models.Discards
@using Ecocell.Shared.Requests.Discards
@using Ecocell.Shared.Responses.Discards
```

- [ ] **Step 6: Restaurar e compilar Windows**

```powershell
rtk dotnet restore src/Ecocell.Mobile/Ecocell.Mobile.csproj
rtk dotnet build src/Ecocell.Mobile/Ecocell.Mobile.csproj -f net10.0-windows10.0.19041.0 --no-restore
```

Expected: PASS sem novos warnings do código criado.

- [ ] **Step 7: Commit seletivo**

```powershell
git add -- src/Ecocell.Mobile/Ecocell.Mobile.csproj src/Ecocell.Mobile/MauiProgram.cs src/Ecocell.Mobile/Components/_Imports.razor src/Ecocell.Mobile/Services/Api/IDiscardClient.cs src/Ecocell.Mobile/Models/Discards/DiscardItemDraft.cs src/Ecocell.Mobile/Models/Discards/ElectronicMaterialDisplay.cs
git diff --cached --check
git commit -m "feat: prepara contratos mobile de descartes"
```

---

### Task 5: Implementar o scanner MAUI nativo

**Files:**
- Create: `src/Ecocell.Mobile/Services/Scanner/IQrScanner.cs`
- Create: `src/Ecocell.Mobile/Services/Scanner/MauiQrScanner.cs`
- Create: `src/Ecocell.Mobile/Services/Scanner/QrScannerPage.xaml`
- Create: `src/Ecocell.Mobile/Services/Scanner/QrScannerPage.xaml.cs`
- Modify: `src/Ecocell.Mobile/MauiProgram.cs`
- Modify: `src/Ecocell.Mobile/Platforms/Android/AndroidManifest.xml`
- Modify: `src/Ecocell.Mobile/Platforms/iOS/Info.plist`

**Interfaces:**
- Consumes: `CollectorPointQrCode.TryParse` e `ZXing.Net.Maui.Controls.CameraBarcodeReaderView`.
- Produces: `IQrScanner.ScanAsync(CancellationToken)` com resultado detectado, cancelado, sem suporte ou permissão negada.

- [ ] **Step 1: Definir o contrato sem exceções para estados esperados**

```csharp
namespace Ecocell.Mobile.Services.Scanner;

public enum QrScanStatus
{
    Detected,
    Cancelled,
    Unsupported,
    PermissionDenied,
}

public sealed record QrScanResult(QrScanStatus Status, string? Value = null);

public interface IQrScanner
{
    Task<QrScanResult> ScanAsync(CancellationToken ct = default);
}
```

- [ ] **Step 2: Criar a página nativa de câmera**

O XAML deve conter um `CameraBarcodeReaderView` ocupando a tela, overlay escuro, instrução e botão “Fechar”. Configurar:

```xml
xmlns:zxing="clr-namespace:ZXing.Net.Maui.Controls;assembly=ZXing.Net.MAUI.Controls"

<zxing:CameraBarcodeReaderView
    x:Name="BarcodeView"
    CameraLocation="Rear"
    IsDetecting="True"
    BarcodesDetected="OnBarcodesDetected" />
```

No code-behind, definir opções para QR e concluir apenas uma vez:

```csharp
BarcodeView.Options = new BarcodeReaderOptions
{
    Formats = BarcodeFormats.TwoDimensional,
    AutoRotate = true,
    Multiple = false,
};

private int _completed;

private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs args)
{
    var value = args.Results
        .Select(result => result.Value)
        .FirstOrDefault(candidate => CollectorPointQrCode.TryParse(candidate, out _));

    if (value is null || Interlocked.Exchange(ref _completed, 1) != 0)
        return;

    MainThread.BeginInvokeOnMainThread(() => Complete(new(QrScanStatus.Detected, value)));
}
```

`Complete` desliga `IsDetecting`, fecha o modal e resolve o `TaskCompletionSource<QrScanResult>`. O botão fechar retorna `Cancelled`. `OnDisappearing` retorna `Cancelled` somente se ainda não concluído.

- [ ] **Step 3: Implementar o serviço modal**

`MauiQrScanner.ScanAsync` deve:

1. retornar `Unsupported` quando `BarcodeScanning.IsSupported` for falso;
2. solicitar `Permissions.Camera`;
3. retornar `PermissionDenied` quando o status não for `Granted`;
4. obter `Application.Current.Windows.FirstOrDefault()?.Page` na main thread;
5. criar `QrScannerPage`, executar `PushModalAsync` e aguardar seu resultado;
6. respeitar cancelamento fechando o modal e retornando `Cancelled`.

Registrar:

```csharp
builder.Services.AddSingleton<IQrScanner, MauiQrScanner>();
```

- [ ] **Step 4: Declarar permissões**

Android, dentro de `<manifest>`:

```xml
<uses-permission android:name="android.permission.CAMERA" />
```

iOS, dentro de `<dict>`:

```xml
<key>NSCameraUsageDescription</key>
<string>A EcoCell usa a câmera para ler o QR Code do Ponto de Coleta.</string>
```

- [ ] **Step 5: Compilar os alvos disponíveis**

```powershell
rtk dotnet build src/Ecocell.Mobile/Ecocell.Mobile.csproj -f net10.0-windows10.0.19041.0
rtk dotnet build src/Ecocell.Mobile/Ecocell.Mobile.csproj -f net10.0-android
```

Expected: PASS. Windows compila, mas `BarcodeScanning.IsSupported` conduz ao fallback manual em runtime.

- [ ] **Step 6: Commit seletivo**

```powershell
git add -- src/Ecocell.Mobile/Services/Scanner src/Ecocell.Mobile/MauiProgram.cs src/Ecocell.Mobile/Platforms/Android/AndroidManifest.xml src/Ecocell.Mobile/Platforms/iOS/Info.plist
git diff --cached --check
git commit -m "feat: adiciona scanner nativo de QR"
```

---

### Task 6: Implementar monitor e notificação de pendências

**Files:**
- Create: `src/Ecocell.Mobile/Services/Notifications/PendingDiscardMonitor.cs`
- Modify: `src/Ecocell.Mobile/MauiProgram.cs`
- Modify: `src/Ecocell.Mobile/App.xaml.cs`
- Modify: `src/Ecocell.Mobile/Services/Auth/AuthStateService.cs`

**Interfaces:**
- Consumes: `IDiscardClient.ListPendingAsync`, `AuthStateService`, `ActiveContextService`, `Preferences` e `LocalNotificationCenter`.
- Produces: contador observado pela Home, refresh após mutações, rationale, permissão e destino pendente de notificação.

- [ ] **Step 1: Criar a API pública do monitor**

```csharp
public sealed class PendingDiscardMonitor : IAsyncDisposable
{
    public event Action? Changed;
    public event Action? NotificationTargetAvailable;

    public int PendingCount { get; private set; }
    public bool IsLoading { get; private set; }
    public bool ShouldExplainNotifications { get; }

    public void SetForeground(bool isForeground);
    public Task RefreshAsync(CancellationToken ct = default);
    public Task<bool> EnableNotificationsAsync();
    public void DismissNotificationPrompt();
    public bool TryTakeNotificationTarget(out Guid collectorPointId);
    public ValueTask DisposeAsync();
}
```

`ShouldExplainNotifications` lê `Preferences.Default.Get("ecocell.notifications.explained", false)`.

- [ ] **Step 2: Implementar elegibilidade e loop cancelável**

O construtor assina `AuthStateChanged`, `ContextChanged` e `LocalNotificationCenter.Current.NotificationActionTapped`. `Restart` cancela o CTS anterior e inicia o loop somente quando:

```csharp
private bool IsEligible =>
    _isForeground
    && _authState.IsAuthenticated
    && _context.Current is { IsCollectPoint: true, CollectPointId: not null };
```

O loop executa `RefreshAsync` antes de criar `PeriodicTimer(TimeSpan.FromSeconds(30))`. Cada iteração captura `collectorPointId` e `generation`; se a geração mudar antes da resposta, descarta o resultado.

- [ ] **Step 3: Implementar baseline persistido e notificação agregada**

Usar chaves por PC:

```csharp
private static string BaselineKey(Guid id) => $"ecocell.pending.{id:D}.baseline";
private static string KnownIdsKey(Guid id) => $"ecocell.pending.{id:D}.ids";
```

Após resposta `200`:

```csharp
var currentIds = response.Content.Items.Select(value => value.Id).ToHashSet();
var hasBaseline = Preferences.Default.Get(BaselineKey(pointId), false);
var knownIds = ReadKnownIds(pointId);
var newCount = hasBaseline ? currentIds.Except(knownIds).Count() : 0;

PendingCount = currentIds.Count;
WriteKnownIds(pointId, currentIds);
Preferences.Default.Set(BaselineKey(pointId), true);
Changed?.Invoke();
```

Somente quando `newCount > 0`, enviar uma `NotificationRequest`:

```csharp
var request = new NotificationRequest
{
    NotificationId = BitConverter.ToInt32(pointId.ToByteArray(), 0) & int.MaxValue,
    Title = "Novos descartes pendentes",
    Description = newCount == 1
        ? "Há 1 novo descarte aguardando conferência."
        : $"Há {newCount} novos descartes aguardando conferência.",
    ReturningData = pointId.ToString("D"),
};

await LocalNotificationCenter.Current.Show(request);
```

Falha HTTP ou exceção não altera baseline, IDs ou contador. Persistir IDs como JSON e substituir pelo conjunto atual para limitar crescimento.

- [ ] **Step 4: Implementar permissão e toque**

`EnableNotificationsAsync` marca a explicação como vista e chama:

```csharp
return await LocalNotificationCenter.Current.RequestNotificationPermission();
```

`DismissNotificationPrompt` apenas marca a explicação como vista. O handler de toque valida `Guid.TryParseExact(args.Request.ReturningData, "D", out var id)`, armazena um único `_notificationTarget` e dispara `NotificationTargetAvailable`. No construtor, também inspecionar `LocalNotificationCenter.LaunchNotificationDetails` para cold start.

```csharp
var launchDetails = LocalNotificationCenter.LaunchNotificationDetails;
if (launchDetails?.DidNotificationLaunchApp == true)
    CaptureNotificationTarget(launchDetails.Request?.ReturningData);
```

- [ ] **Step 5: Integrar ciclo de vida MAUI**

Registrar o monitor singleton. Injetá-lo em `App` e conectar a janela:

```csharp
var window = new Window(new MainPage()) { Title = "Ecocell.Mobile" };
window.Activated += (_, _) => _pendingMonitor.SetForeground(true);
window.Resumed += (_, _) => _pendingMonitor.SetForeground(true);
window.Deactivated += (_, _) => _pendingMonitor.SetForeground(false);
window.Stopped += (_, _) => _pendingMonitor.SetForeground(false);
return window;
```

Em `AuthStateService.InitializeAsync`, após carregar tokens, disparar `AuthStateChanged?.Invoke()` para que monitor e roteamento reajam à sessão persistida.

- [ ] **Step 6: Executar smoke técnico controlado**

Com API local e PC ativo:

1. entrar no contexto PC com pendências existentes: contador atualiza, nenhuma notificação;
2. abrir novo descarte após baseline: uma notificação agregada;
3. aguardar dois ciclos sem novos IDs: nenhuma duplicação;
4. desligar rede: contador/baseline permanecem;
5. trocar PC durante request: resposta antiga não altera o novo contexto;
6. colocar app em background por mais de 30 segundos: não há polling até resume.

- [ ] **Step 7: Compilar e commit seletivo**

```powershell
rtk dotnet build src/Ecocell.Mobile/Ecocell.Mobile.csproj -f net10.0-windows10.0.19041.0
git add -- src/Ecocell.Mobile/Services/Notifications/PendingDiscardMonitor.cs src/Ecocell.Mobile/MauiProgram.cs src/Ecocell.Mobile/App.xaml.cs src/Ecocell.Mobile/Services/Auth/AuthStateService.cs
git diff --cached --check
git commit -m "feat: monitora novos descartes pendentes"
```

---

### Task 7: Implementar o fluxo de scan do Depositante

**Files:**
- Create: `src/Ecocell.Mobile/ViewModels/ScanViewModel.cs`
- Create: `src/Ecocell.Mobile/Components/Pages/Discards/Scan.razor`
- Create: `src/Ecocell.Mobile/Components/Pages/Discards/Scan.razor.css`
- Create: `src/Ecocell.Mobile/Components/Shared/Buttons/EcoIconButton.razor`
- Create: `src/Ecocell.Mobile/Components/Shared/Buttons/EcoIconButton.razor.css`
- Create: `src/Ecocell.Mobile/Services/Navigation/DepositorNav.cs`
- Modify: `src/Ecocell.Mobile/Components/Pages/Map/Nearby.razor`
- Modify: `src/Ecocell.Mobile/MauiProgram.cs`

**Interfaces:**
- Consumes: `IQrScanner`, `IDiscardClient`, `DiscardItemDraft`, `ElectronicMaterialDisplay` e `ResponseDiscardPreviewJson`.
- Produces: rota `/descartes/escanear` e fluxo completo de abertura.

- [ ] **Step 1: Criar o botão de ícone `Eco*`**

API:

```razor
<button type="button"
        class="eco-icon-btn @(Destructive ? "eco-icon-btn--destructive" : "") @CssClass"
        aria-label="@AriaLabel"
        disabled="@Disabled"
        @onclick="OnClick">
    <MudIcon Icon="@Icon" aria-hidden="true" />
</button>
```

Parâmetros: `Icon`, `AriaLabel`, `OnClick`, `Disabled`, `Destructive`, `CssClass`. CSS usa somente tokens e `min-width/min-height: 2.75rem`.

- [ ] **Step 2: Centralizar a navegação do Depositante**

Criar `DepositorNav.Items` com IDs `inicio`, `descartar`, `ranking`, `perfil`. Fixar `Href` de Descartar em `/descartes/escanear`. Substituir a lista inline de `Nearby.razor` por `DepositorNav.Items`.

- [ ] **Step 3: Implementar o ViewModel**

Estados:

```csharp
public enum ScanState
{
    Intro,
    LoadingPreview,
    Ready,
    NoMaterials,
    Submitting,
    Success,
    Error,
}
```

Propriedades: `State`, `QrCode`, `Preview`, `Items`, `ErrorMessage`. Métodos:

```csharp
Task LoadPreviewAsync(string qrCode, CancellationToken ct = default);
void AddItem();
void RemoveItem(DiscardItemDraft item);
Task<bool> SubmitAsync(CancellationToken ct = default);
void Reset();
```

`LoadPreviewAsync` preserva o QR digitado, limpa preview e itens, chama `PreviewAsync`, cria um primeiro draft quando há materiais e usa `NoMaterials` quando a lista vier vazia. `AddItem` só adiciona se houver material aceito não utilizado.

`SubmitAsync` valida todos os drafts, detecta duplicados e monta:

```csharp
var request = new RequestRegisterDiscardJson
{
    QrCode = QrCode,
    Items = values.Select(value => new RequestRegisterDiscardItemJson
    {
        Material = value.Material,
        Quantity = value.Quantity,
        ApproximateWeightKg = value.ApproximateWeightKg,
    }).ToArray(),
};
```

Sucesso `201` muda para `Success`; qualquer falha preserva itens. Mensagens: `400` formulário/QR inválido, `404` ponto não encontrado, `409` ponto/material indisponível e fallback genérico de conexão.

- [ ] **Step 4: Construir `Scan.razor` conforme frame `NVBNO`**

A página deve:

- declarar `@page "/descartes/escanear"` e `@layout MainLayout`;
- renderizar `EcoTopBar`, conteúdo rolável e `EcoNavBar` com `ActiveId="descartar"`;
- oferecer “Escanear QR Code” e `EcoTextField` para código manual;
- chamar `IQrScanner.ScanAsync`; em `Detected`, carregar preview; em `Unsupported` ou `PermissionDenied`, focar a entrada manual e mostrar mensagem;
- em `Ready`, mostrar `EcoCard` com nome/endereço e os drafts inline;
- usar `EcoSelectField`, `EcoTextField`, `EcoIconButton`, `EcoPrimaryButton`, `EcoGhostButton`, `EcoSpinner`, `EcoErrorBanner` e `EcoEmptyState`;
- impedir submit durante `Submitting` ou com zero itens;
- no sucesso, mostrar confirmação e ação “Concluir” que chama `Reset`.

Não usar `MudTextField`, `MudSelect`, `MudButton`, estilos inline ou literal hexadecimal.

- [ ] **Step 5: Aplicar CSS scoped e tokens**

Usar esta estrutura de seletores:

```css
.eco-scan { display: flex; flex-direction: column; height: 100%; background: var(--eco-color-surface-alt); }
.eco-scan__body { flex: 1; overflow-y: auto; padding: 0 var(--eco-space-lg) var(--eco-space-lg); }
.eco-scan__items { display: flex; flex-direction: column; gap: var(--eco-space-md); }
.eco-scan__actions { display: flex; flex-direction: column; gap: var(--eco-space-sm); }
.eco-scan__navbar { flex-shrink: 0; }
```

Títulos usam tamanho `--eco-font-h1-size`, mas peso `--eco-font-h2-weight` para manter somente 400/600. Botões e remoção mantêm 44 × 44.

- [ ] **Step 6: Registrar ViewModel, compilar e executar smoke**

Registrar `ScanViewModel` como transient. Rodar:

```powershell
rtk dotnet build src/Ecocell.Mobile/Ecocell.Mobile.csproj -f net10.0-windows10.0.19041.0
```

Smoke Windows:

1. Descartar abre `/descartes/escanear`;
2. câmera cai para código manual;
3. QR inválido mostra erro;
4. QR válido mostra PC e materiais;
5. PC sem regras mostra vazio;
6. duplicidade, quantidade zero e peso com quatro casas bloqueiam envio;
7. erro HTTP preserva o formulário;
8. `201` mostra sucesso e “Concluir” limpa tudo.

- [ ] **Step 7: Commit seletivo**

```powershell
git add -- src/Ecocell.Mobile/ViewModels/ScanViewModel.cs src/Ecocell.Mobile/Components/Pages/Discards/Scan.razor src/Ecocell.Mobile/Components/Pages/Discards/Scan.razor.css src/Ecocell.Mobile/Components/Shared/Buttons/EcoIconButton.razor src/Ecocell.Mobile/Components/Shared/Buttons/EcoIconButton.razor.css src/Ecocell.Mobile/Services/Navigation/DepositorNav.cs src/Ecocell.Mobile/Components/Pages/Map/Nearby.razor src/Ecocell.Mobile/MauiProgram.cs
git diff --cached --check
git commit -m "feat: implementa abertura mobile de descarte"
```

---

### Task 8: Implementar lista e conferência do Ponto de Coleta

**Files:**
- Create: `src/Ecocell.Mobile/ViewModels/ConfirmDiscardViewModel.cs`
- Create: `src/Ecocell.Mobile/Components/Pages/Discards/Confirm.razor`
- Create: `src/Ecocell.Mobile/Components/Pages/Discards/Confirm.razor.css`
- Modify: `src/Ecocell.Mobile/MauiProgram.cs`

**Interfaces:**
- Consumes: `IDiscardClient`, `ActiveContextService`, `PendingDiscardMonitor` e drafts compartilhados.
- Produces: rota `/ponto-de-coleta/descartes-pendentes` com estados C1/C2.

- [ ] **Step 1: Implementar o ViewModel com proteção de geração**

Estados:

```csharp
public enum ConfirmState
{
    Loading,
    List,
    Detail,
    Submitting,
    Empty,
    Error,
}
```

Propriedades: `State`, `Items`, `Selected`, `DraftItems`, `ErrorMessage`, `FeedbackMessage`. Métodos:

```csharp
Task LoadAsync(CancellationToken ct = default);
void Open(ResponsePendingDiscardJson discard);
void BackToList();
void AddItem();
void RemoveItem(DiscardItemDraft item);
Task ConfirmAsync(CancellationToken ct = default);
Task RejectAsync(CancellationToken ct = default);
```

`LoadAsync` captura `CollectorPointId` e `Generation`; não chama API sem PC válido; descarta resposta obsoleta. `Open` clona os itens recebidos para drafts. `BackToList` limpa seleção/drafts sem request.

`ConfirmAsync` monta `RequestConfirmDiscardJson`; `RejectAsync` não envia body. Em `204`, remover o item local, limpar detalhe, definir `Empty` ou `List` e chamar `_monitor.RefreshAsync`. Em `409`, definir `FeedbackMessage = "Este descarte já foi processado."` e recarregar. Outras falhas preservam drafts e retornam a `Detail` com erro.

- [ ] **Step 2: Construir `Confirm.razor` conforme C1 e C2**

A página deve:

- declarar `@page "/ponto-de-coleta/descartes-pendentes"`;
- redirecionar para `/meus-pontos-de-coleta` sem contexto PC;
- manter `EcoNavBar` com `ActiveId="pc-inicio"`;
- em lista, renderizar cards ordenados com nome, data/hora e resumo dos itens;
- em detalhe, mostrar drafts editáveis e ações “Confirmar descarte” e “Rejeitar”;
- usar `EcoBottomSheet` para confirmação da rejeição, sem campo de motivo;
- voltar do detalhe descartando somente alterações locais;
- mostrar `EcoSpinner`, `EcoEmptyState` e `EcoErrorBanner` nos estados correspondentes;
- anunciar `FeedbackMessage` com `role="status"`.

Não adicionar item ao `NavItemsResolver`.

- [ ] **Step 3: Aplicar CSS scoped e registrar ViewModel**

Usar tokens, Inter 400/600 e layout:

```css
.eco-confirm { display: flex; flex-direction: column; height: 100%; background: var(--eco-color-surface-alt); }
.eco-confirm__body { flex: 1; min-height: 0; overflow-y: auto; padding: var(--eco-space-lg); }
.eco-confirm__list { display: flex; flex-direction: column; gap: var(--eco-space-md); }
.eco-confirm__actions { display: flex; flex-direction: column; gap: var(--eco-space-sm); }
.eco-confirm__navbar { flex-shrink: 0; }
```

Registrar `ConfirmDiscardViewModel` como transient.

- [ ] **Step 4: Compilar e executar smoke**

```powershell
rtk dotnet build src/Ecocell.Mobile/Ecocell.Mobile.csproj -f net10.0-windows10.0.19041.0
```

Smoke:

1. sem contexto PC redireciona sem request;
2. loading, vazio, erro e retry aparecem corretamente;
3. lista está em ordem mais antiga primeiro;
4. detalhe replica os valores declarados;
5. voltar descarta edição local;
6. confirmar envia lista completa e remove após `204`;
7. rejeitar abre sheet e envia sem body;
8. `409` fecha detalhe, recarrega e informa processamento concorrente;
9. troca de PC durante request não mistura dados.

- [ ] **Step 5: Commit seletivo**

```powershell
git add -- src/Ecocell.Mobile/ViewModels/ConfirmDiscardViewModel.cs src/Ecocell.Mobile/Components/Pages/Discards/Confirm.razor src/Ecocell.Mobile/Components/Pages/Discards/Confirm.razor.css src/Ecocell.Mobile/MauiProgram.cs
git diff --cached --check
git commit -m "feat: implementa conferência mobile de descartes"
```

---

### Task 9: Integrar Home, permissão e navegação da notificação

**Files:**
- Modify: `src/Ecocell.Mobile/Components/Pages/CollectorPoints/CollectorPointHome.razor`
- Modify: `src/Ecocell.Mobile/Components/Pages/CollectorPoints/CollectorPointHome.razor.css`
- Modify: `src/Ecocell.Mobile/Components/Routes.razor`

**Interfaces:**
- Consumes: `PendingDiscardMonitor`, `ICollectorPointClient`, `ActiveContextService` e `NavigationManager`.
- Produces: card/contador, rationale de primeira entrada e navegação segura após toque.

- [ ] **Step 1: Adicionar card e rationale à Home**

Injetar o monitor, assinar `Changed` em `OnInitialized` e remover a assinatura em `Dispose`. No corpo, adicionar `EcoCard` interativo:

```razor
<EcoCard CssClass="eco-pc-home__pending" OnClick="OpenPending">
    <div>
        <h2>Descartes pendentes</h2>
        <p>Confira os materiais recebidos antes de confirmar.</p>
    </div>
    <span class="eco-pc-home__pending-count" aria-label="@PendingCountLabel">
        @(Monitor.IsLoading ? "—" : Monitor.PendingCount)
    </span>
</EcoCard>
```

`OpenPending` navega para `/ponto-de-coleta/descartes-pendentes`.

Na primeira entrada elegível, abrir `EcoBottomSheet` com texto explicativo, `EcoPrimaryButton Label="Ativar notificações"` e `EcoGhostButton Label="Agora não"`. Ambas marcam a explicação como vista; somente a primeira chama `EnableNotificationsAsync`. Negativa fecha o sheet e não bloqueia o card.

- [ ] **Step 2: Aplicar CSS do card sem alterar a nav**

Usar grid/flex, tokens e contador com mínimo de 44 × 44. Substituir o texto provisório atual da Home por conteúdo operacional. Preservar switcher, FAB de QR e os cinco itens existentes.

- [ ] **Step 3: Revalidar toque na raiz Blazor**

Em `Routes.razor`, injetar `PendingDiscardMonitor`, `ICollectorPointClient` e `ActiveContextService`. Assinar `NotificationTargetAvailable` e, no callback, usar `InvokeAsync(OpenNotificationTargetAsync)`.

Fluxo exato:

```csharp
private async Task OpenNotificationTargetAsync()
{
    if (!AuthState.IsAuthenticated
        || !Monitor.TryTakeNotificationTarget(out var collectorPointId))
    {
        return;
    }

    var response = await CollectorPointClient.ListMineAsync();
    var point = response.IsSuccessStatusCode
        ? response.Content?.Items.FirstOrDefault(value =>
            value.Id == collectorPointId && value.Status == PersonStatus.Active)
        : null;

    if (point is null)
        return;

    Context.EnterCollectPoint(point.Id, point.TradeName);
    Navigation.NavigateTo("/ponto-de-coleta/descartes-pendentes");
}
```

Em `OnAfterRenderAsync(firstRender)`, tentar consumir destino de cold start depois da primeira renderização. Se a sessão ainda estiver inicializando, o `AuthStateChanged` já assinado tenta novamente. Não confiar apenas no ID do payload.

Atualizar o callback já existente de autenticação para manter logout e também retomar destino válido:

```csharp
private void OnAuthStateChanged()
{
    if (!AuthState.IsAuthenticated)
    {
        InvokeAsync(() => Navigation.NavigateTo("/", replace: true));
        return;
    }

    _ = InvokeAsync(OpenNotificationTargetAsync);
}
```

- [ ] **Step 4: Executar smoke de navegação e permissão**

1. primeira entrada PC abre rationale;
2. “Agora não” não reaparece automaticamente e lista continua acessível;
3. permissão negada não bloqueia card;
4. toque em notificação de PC ainda gerido troca contexto e abre pendências;
5. toque em payload inválido é ignorado;
6. toque em PC removido/inativo não troca contexto;
7. card atualiza após confirmação e rejeição;
8. barra inferior continua com cinco itens e “Início” ativo.

- [ ] **Step 5: Compilar e commit seletivo**

```powershell
rtk dotnet build src/Ecocell.Mobile/Ecocell.Mobile.csproj -f net10.0-windows10.0.19041.0
git add -- src/Ecocell.Mobile/Components/Pages/CollectorPoints/CollectorPointHome.razor src/Ecocell.Mobile/Components/Pages/CollectorPoints/CollectorPointHome.razor.css src/Ecocell.Mobile/Components/Routes.razor
git diff --cached --check
git commit -m "feat: integra pendências à home do ponto de coleta"
```

---

## Final Verification

- [ ] **Step 1: Rodar testes completos com Docker ativo**

```powershell
rtk dotnet test Ecocell.slnx
```

Expected: todas as suítes passam. Se o runner não puder compilar workloads Mobile, rodar também `rtk dotnet test Ecocell.Backend.slnf` e registrar separadamente a limitação; não declarar a solução completa verde sem o comando normativo.

- [ ] **Step 2: Confirmar ausência de migration pendente**

```powershell
rtk dotnet ef migrations has-pending-model-changes --project src/Ecocell.Api --startup-project src/Ecocell.Api
```

Expected: nenhuma mudança de modelo, pois WND-291 não altera entidades ou configuração EF.

- [ ] **Step 3: Compilar Mobile**

```powershell
rtk dotnet build src/Ecocell.Mobile/Ecocell.Mobile.csproj -f net10.0-windows10.0.19041.0
rtk dotnet build src/Ecocell.Mobile/Ecocell.Mobile.csproj -f net10.0-android
```

Em host macOS compatível, executar também os TFMs iOS e MacCatalyst.

- [ ] **Step 4: Executar smoke ponta a ponta**

1. gerar QR no PC;
2. Depositante escanear no Android ou digitar no Windows;
3. conferir preview e abrir descarte com dois materiais;
4. PC receber somente uma notificação após baseline;
5. abrir pendências pelo card e pela notificação;
6. corrigir item e confirmar; verificar remoção da lista;
7. abrir segundo descarte e rejeitar; verificar remoção;
8. simular duas operações no mesmo descarte; verificar um `204` e feedback de `409`;
9. negar permissões de câmera/notificação; confirmar fallback e continuidade;
10. trocar PC, background/resume e logout; confirmar isolamento e pausa.

- [ ] **Step 5: Fazer QA visual contra Pen.dev e Design System**

Abrir `D:\Design\ecocell.pen` pelo MCP do Pen.dev. Comparar `Scan` com `NVBNO`, lista com `jNSlX` e detalhe com `J0tvXz`. Conferir 400/600, tokens, contraste, scroll, safe areas, 44 × 44, loading, erro, vazio, envio e sucesso. Executar busca:

```powershell
rtk rg -n "#[0-9A-Fa-f]{3,8}|style=|<Mud(Button|TextField|Select|Card)" src/Ecocell.Mobile/Components/Pages/Discards src/Ecocell.Mobile/Components/Pages/CollectorPoints/CollectorPointHome.razor src/Ecocell.Mobile/Components/Pages/CollectorPoints/CollectorPointHome.razor.css
```

Expected: nenhum literal hexadecimal, estilo inline ou componente Mud cru nas telas novas.

- [ ] **Step 6: Revisar diff final**

```powershell
git status --short
git diff --check
git log --oneline --max-count=10
```

Expected: somente arquivos da WND-291 alterados no branch isolado; nenhum segredo, artefato de build ou arquivo da worktree original.
