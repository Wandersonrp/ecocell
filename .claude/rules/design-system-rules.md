# Regras de Design System — EcoCell Mobile (MAUI Blazor)

Estas regras são **normativas** para todo código de UI em `src/Ecocell.Mobile/` (MAUI Blazor Hybrid + MudBlazor). Para regras de codificação backend, ver [`coding-rules.md`](coding-rules.md). Para regras de teste, ver [`unit-tests-rules.md`](unit-tests-rules.md) e [`integration-tests-rules.md`](integration-tests-rules.md).

O objetivo é garantir que **toda tela do app** (iOS/Android) siga a identidade visual do Ecocell — verde floresta profundo sobre branco limpo, sério para parcerias B2B/ESG, leve para o cidadão comum. Nenhuma cor fora da paleta deve aparecer na UI, **inclusive azuis padrão de sistema** (iOS `#007AFF`, Android Material).

---

## 1. Stack obrigatória

| Camada | Ferramenta |
| --- | --- |
| Host | .NET MAUI (`Microsoft.Maui.Controls`) com `BlazorWebView` (`Microsoft.AspNetCore.Components.WebView.Maui`). |
| UI | Razor Components (`.razor`) + CSS scoped (`.razor.css`). |
| Biblioteca de componentes | **MudBlazor 9.x** — usar quando o componente nativo cobrir o caso (Button, TextField, Dialog, Snackbar, NavMenu). Customizar via `MudTheme` + CSS overrides. |
| Tipografia | **Inter** (Variable). Adicionar como `MauiFont` em `Resources/Fonts/` e referenciar via `font-family: 'Inter'` no CSS. **Não usar** `OpenSans` nem `Helvetica/Arial`. |
| Tokens | CSS custom properties em `wwwroot/app.css` (única fonte de verdade) + `MudTheme` (espelho dos mesmos valores) registrado em `MauiProgram`. |

**Não introduzir** outras bibliotecas de UI (Bootstrap nativo, Radzen, Syncfusion, Tailwind). MudBlazor é fechado.

---

## 2. Estrutura de arquivos

```
src/Ecocell.Mobile/
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor          ← layouts operacionais (top-bar + nav-bar)
│   │   ├── AuthLayout.razor          ← onboarding/auth (sem chrome)
│   │   └── *.razor.css
│   ├── Pages/
│   │   └── <Agregado>/<Tela>.razor   ← uma rota por arquivo, espelha slices da API
│   └── Shared/
│       ├── Buttons/EcoPrimaryButton.razor
│       ├── Inputs/EcoTextField.razor
│       ├── Inputs/EcoOtpInput.razor
│       ├── Badges/EcoBadge.razor
│       └── ...
├── Resources/
│   ├── Fonts/Inter-Variable.ttf
│   └── ...
├── Theme/
│   ├── EcocellTheme.cs               ← MudTheme construído a partir dos tokens
│   └── ThemeTokens.cs                ← constantes C# espelhando os tokens CSS
└── wwwroot/
    ├── app.css                       ← :root { --eco-* } + reset + overrides MudBlazor
    └── ...
```

Regras:

- **Uma página = um `.razor` em `Components/Pages/<Agregado>/`**, com namespace correspondente.
- Componentes reutilizáveis ficam em `Components/Shared/<Categoria>/`. Sempre prefixados com `Eco` (ex.: `EcoPrimaryButton`, `EcoBadge`) para diferenciar de MudBlazor (`MudButton`).
- **CSS scoped** (`.razor.css`) é o padrão — só promova classe a `app.css` se a regra precisar atravessar componentes.
- Tokens CSS (`:root { --eco-color-primary: #002700; ... }`) ficam **exclusivamente** em `wwwroot/app.css`. Não duplicar em `.razor.css`.

---

## 3. Tokens de design

Os tokens abaixo são **a fonte de verdade**. Toda cor, fonte, espaçamento e radius aplicado na UI deve referenciar um token — nunca um literal hex/px inline.

### 3.1 Cores

| Token CSS | Hex | Uso |
| --- | --- | --- |
| `--eco-color-primary` | `#002700` | CTA principal, pin de Ponto de Coleta, FAB. |
| `--eco-color-primary-light` | `#335233` | Estados pressed/hover, ghost button. |
| `--eco-color-primary-subtle` | `#546E54` | Ícones secundários, pin de Coletor. |
| `--eco-color-primary-muted` | `#8A9C8A` | Placeholders, elementos desabilitados. |
| `--eco-color-primary-tint` | `#E6E9E6` | Background de cards/chips/badges. |
| `--eco-color-surface` | `#FFFFFF` | Fundo de tela, cards, modais. |
| `--eco-color-surface-alt` | `#EBEBEB` | Seções alternadas, separadores. |
| `--eco-color-text-primary` | `#151815` | Títulos, corpo principal. |
| `--eco-color-text-secondary` | `#333833` | Subtítulos, labels. |
| `--eco-color-text-tertiary` | `#767A76` | Captions, nav inativo. |
| `--eco-color-text-disabled` | `#C0C1C0` | Texto inativo. |
| `--eco-color-text-on-primary` | `#FFFFFF` | Texto sobre primary. |
| `--eco-color-error` | `#950E0E` | Mensagens de erro, alertas críticos. |
| `--eco-color-error-light` | `#F4E7E7` | Background de banners de erro. |
| `--eco-color-error-border` | `#DEB4B4` | Borda de input inválido. |
| `--eco-color-border` | `#C0C1C0` | Bordas padrão. |
| `--eco-color-divider` | `#EBEBEB` | Linhas divisoras. |

### 3.2 Tipografia

Fonte única: **Inter**. Pesos disponíveis: 400, 500, 600, 700. Máximo de **dois pesos por tela**.

| Token | Tamanho | Peso | Line-height | Uso |
| --- | --- | --- | --- | --- |
| `--eco-font-display` | 2rem (32px) | 700 | 1.2 | Splash, onboarding (máx. 2 linhas). |
| `--eco-font-h1` | 1.5rem (24px) | 700 | 1.3 | Título da tela (**1 por view**). |
| `--eco-font-h2` | 1.25rem (20px) | 600 | 1.4 | Seções, dígitos OTP. |
| `--eco-font-h3` | 1rem (16px) | 600 | 1.4 | Título de card. |
| `--eco-font-body-lg` | 1rem (16px) | 400 | 1.6 | Corpo padrão, input preenchido. |
| `--eco-font-body-md` | 0.875rem (14px) | 400 | 1.5 | Corpo secundário, descrições. |
| `--eco-font-label` | 0.875rem (14px) | 500 | 1.4 | Labels de formulário. |
| `--eco-font-caption` | 0.75rem (12px) | 400 | 1.4 | Nav labels, timestamps, expiração de cupom. |
| `--eco-font-button` | 0.875rem (14px) | 600 | 1 | Botão (sentence case obrigatório, `letter-spacing: 0.01em`). |

### 3.3 Espaçamento

| Token | Valor | Uso |
| --- | --- | --- |
| `--eco-space-xs` | 4px | Gap entre ícone e texto. |
| `--eco-space-sm` | 8px | Gap entre boxes OTP. |
| `--eco-space-form-gap` | 12px | **Gap entre campos de formulário** (regra fixa). |
| `--eco-space-md` | 16px | Padding de card, gap entre cards. |
| `--eco-space-lg` | 24px | Padding horizontal de tela. |
| `--eco-space-xl` | 32px | Gap entre seções principais. |
| `--eco-space-xxl` | 48px | Margem superior de splash/onboarding. |

### 3.4 Radius

| Token | Valor | Uso |
| --- | --- | --- |
| `--eco-radius-sm` | 6px | OTP boxes, chips, `badge-coupon`. |
| `--eco-radius-md` | 12px | Cards, bottom sheets. |
| `--eco-radius-lg` | 16px | Modais, topo de bottom sheet. |
| `--eco-radius-full` | 9999px | **Botões primário/secundário/seletor, inputs, FAB, top-bar, avatares, demais badges**. |

### 3.5 Sombras

| Token | Valor |
| --- | --- |
| `--eco-shadow-card` | `0 1px 4px rgba(0,0,0,0.08), 0 0 1px rgba(0,0,0,0.06)` |
| `--eco-shadow-modal` | `0 8px 32px rgba(0,0,0,0.16)` |
| `--eco-shadow-fab` | `0 4px 12px rgba(0,39,0,0.24)` |
| `--eco-shadow-top-bar` | `0 1px 4px rgba(0,0,0,0.06)` |

`--eco-shadow-modal` é **exclusivo** de modais e bottom sheets — **nunca aplicar a card**.

---

## 4. Aplicação dos tokens

- **Em `.razor.css`** — sempre via `var(--eco-*)`. Nunca literal hex/px:
  ```css
  /* ✅ */
  .primary-cta { background: var(--eco-color-primary); border-radius: var(--eco-radius-full); }
  /* ❌ */
  .primary-cta { background: #002700; border-radius: 9999px; }
  ```
- **Em `MudTheme`** — espelhar os mesmos valores em `Theme/EcocellTheme.cs`. Quando o MudBlazor não expor a cor exata via `Palette`, usar `Style="..."` ou override CSS via classe `.mud-button.eco-primary { ... }`.
- **Em C# (raro)** — se precisar de cor em código (ex.: `MapPin` nativo via handler), usar `ThemeTokens.ColorPrimary` (`Color.FromArgb("#002700")`). Não digitar hex em mais de um lugar.

---

## 5. Componentes — convenções obrigatórias

### 5.1 Botões

| Variante | Componente | Quando usar |
| --- | --- | --- |
| Primário | `<EcoPrimaryButton>` | CTA principal da tela. **Um por tela** sempre que possível. |
| Secundário | `<EcoSecondaryButton>` | Acompanha o primário em par (ex.: "Cadastrar" + "Entrar"). |
| Seletor de perfil | `<EcoSelectorButton>` | Escolha de tipo de cadastro (Depositante / Ponto de Coleta / Coletor). Estado padrão outline neutro; selecionado inverte para primary. |
| Ghost | `<EcoGhostButton>` | Retorno ("Voltar para home"). Texto `--eco-color-primary-light`, sem borda, sem underline. |

Regras:

- Border-radius **sempre** `--eco-radius-full`.
- Texto em **sentence case** (não UPPERCASE, não Title Case).
- Estado desabilitado do primário usa `--eco-color-primary-muted` (#8A9C8A), **nunca cinza neutro**.
- `MudButton` cru é **proibido** na UI final — sempre wrap em um `Eco*Button`.

### 5.2 Inputs

`<EcoTextField>` (wrap de `MudTextField`):

- Pill (`--eco-radius-full`), borda 1.5px `--eco-color-border`.
- Foco: borda `--eco-color-primary`, ícone esquerdo muda para `--eco-color-primary`.
- Erro: borda `--eco-color-error-border` (**não** `--eco-color-error` direto).
- Placeholder em `--eco-color-text-tertiary`.
- Gap entre campos consecutivos: `--eco-space-form-gap`.

`<EcoOtpInput>`:

- 6 caixas individuais 48×56px, `--eco-radius-sm` (6px) — **distinção intencional** dos inputs pill: o usuário precisa perceber "modo de código".
- Borda muda por estado: vazia `--eco-color-border` → foco `--eco-color-primary` → preenchida `--eco-color-primary` → erro `--eco-color-error`.
- Dígito centralizado em `--eco-font-h2`.
- Gap entre boxes: `--eco-space-sm` (8px).

### 5.3 Cards

`<EcoCard>`:

- `background: var(--eco-color-surface)`, `border-radius: var(--eco-radius-md)`, `border: 1px solid var(--eco-color-divider)`, `box-shadow: var(--eco-shadow-card)`, `padding: var(--eco-space-md)`.
- **Nunca** aplicar `--eco-shadow-modal`.

### 5.4 Badges

`<EcoBadge Variant="...">` — variantes fixas:

| Variant | Uso | Background | Texto |
| --- | --- | --- | --- |
| `Success` | Coletado, agendado, aprovado | `--eco-color-primary-tint` | `--eco-color-primary` |
| `Error` | Cancelado, vencido, recusado | `--eco-color-error-light` | `--eco-color-error` |
| `Pending` | PJ aguardando aprovação (RN007) | `--eco-color-surface-alt` | `#5C605C` |
| `Seal` | Selo ESG (RN017) | `--eco-color-primary` | `--eco-color-text-on-primary` |
| `Coupon` | Cupom de benefício (US012) | `--eco-color-primary-tint` | `--eco-color-primary` |

Regras:

- `Pending` **não** usa `Error` — não é falha, é espera.
- `Coupon` usa `--eco-radius-sm` (6px); demais usam `--eco-radius-full`.
- `Coupon` **sempre** acompanhado de data de expiração em `--eco-font-caption` (`--eco-color-text-tertiary`) abaixo do badge.
- `Seal` só renderiza para **PJ com meta atingida** — não exibir vazio.

### 5.5 Navegação

`<MainLayout>` (telas operacionais) tem dois chrome elements:

**Top bar** (`<EcoTopBar>`):

- Container pill (`--eco-radius-full`), `background: var(--eco-color-surface)`, sombra `--eco-shadow-top-bar`.
- Layout fixo: `[avatar 36px] ── [logo centralizado] ── [sino]`. Não inverter a ordem.
- Padding: 4px vertical / 16px horizontal.
- **Exclusivo** de telas operacionais — **nunca** em onboarding/auth.

**Nav bar** (`<EcoNavBar>`):

- `background: var(--eco-color-surface)`, altura 52px (3.25rem), `border-top: 1px solid var(--eco-color-divider)`.
- Itens ancorados no rodapé da barra (`justify-content: flex-end`), não centralizados.
- Ativo `--eco-color-primary`, inativo `--eco-color-text-tertiary`.
- **Sempre** exibir label em `--eco-font-caption` abaixo do ícone — nunca ícone sozinho.

`<AuthLayout>` (login, cadastro, OTP, onboarding):

- **Sem** top-bar, **sem** nav-bar.
- Logo centralizado no topo.

### 5.6 Mapa

Pins **nunca** azul. Sempre paleta verde:

- `EcoMapPinCollectorPoint` → `--eco-color-primary` (#002700), 40px, sombra `0 2px 6px rgba(0,39,0,0.30)`.
- `EcoMapPinCollector` → `--eco-color-primary-subtle` (#546E54), 40px, sombra `0 2px 6px rgba(0,0,0,0.20)`.
- Pin selecionado: `transform: scale(1.25)` + sombra `0 4px 12px rgba(0,39,0,0.35)` + abre bottom sheet com card `--eco-radius-lg` no topo.

### 5.7 FAB

`<EcoFab>`:

- Verde `--eco-color-primary`, circular 56px, sombra `--eco-shadow-fab`.
- Ação principal do fluxo: "Registrar descarte" (Depositante) ou "Emitir QR Code" (Ponto de Coleta).
- **Máximo um FAB por tela**.
- **Nunca** em telas de auth, onboarding ou ranking.

### 5.8 Ranking Card

`<EcoRankingCard>` para US005:

- Padrão: `EcoCard` com número de posição em `--eco-font-h1` `--eco-color-primary`.
- Card do **próprio usuário**: background `--eco-color-primary-tint`, borda `1.5px solid --eco-color-primary`.
- **Não renderizar** componente para perfis Admin/Suporte (RN013) — `@if (!IsAdminOrSupport) { ... }`. Ocultar, **não desabilitar**.

---

## 6. Layouts e logo

| Tipo de tela | Layout | Logo | Top-bar | Nav-bar |
| --- | --- | --- | --- | --- |
| Splash / Onboarding / Auth (login, cadastro, OTP) | `AuthLayout` | Centralizado | ❌ | ❌ |
| Operacional interna (mapa, formulários, ranking, listas) | `MainLayout` | Esquerda (dentro da top-bar) | ✅ | ✅ |

Centralização de texto: **apenas** em onboarding e empty states. Nunca em listas, formulários ou cards de dados.

---

## 7. PT-BR e nomenclatura

- Identificadores (classes, parâmetros, métodos) em **inglês**.
- Strings de UI, labels, mensagens, tooltips e summaries em **PT-BR**.
- Summaries (`/// <summary>`) seguem a mesma regra de [`coding-rules.md`](coding-rules.md#2-summaries-em-pt-br): só quando o nome do componente/parâmetro **não** torna o propósito óbvio.

---

## 8. Estados e feedback

- **Loading**: toda ação assíncrona (validação OTP, agendamento, confirmação de descarte, consulta CNPJ, login) **deve** exibir skeleton ou spinner em `--eco-color-primary`.
- **Erro de rede / servidor**: usar `<EcoErrorBanner>` (background `--eco-color-error-light`, borda `--eco-color-error-border`, ícone + texto em `--eco-color-error`).
- **Empty state**: ilustração + título `--eco-font-h2` + descrição `--eco-font-body-md` em `--eco-color-text-secondary` + CTA primário (quando aplicável). Centralizado.
- **Validação de formulário**: erro inline abaixo do campo, em `--eco-font-caption` `--eco-color-error`. Não substituir o placeholder.

---

## 9. Acessibilidade

- Contraste **WCAG AA** mínimo. Pares aprovados: `text-primary` sobre `surface`, `text-on-primary` sobre `primary`, `primary` sobre `primary-tint`.
- **Proibido** usar `primary-muted` (#8A9C8A) ou `primary-tint` (#E6E9E6) como **cor de texto** sobre branco — falham AA.
- Touch target mínimo: **44×44px** (botão, ícone clicável, item de lista).
- Labels de formulário **sempre** visíveis (não confiar só em placeholder).
- Ícones puramente decorativos: `aria-hidden="true"`. Ícones-ação: `aria-label="..."` em PT-BR.

---

## 10. O que NÃO fazer

- ❌ **Não usar** azul de sistema (iOS `#007AFF`, Android Material) em pin, link, ícone, foco. Override obrigatório em todo componente nativo (links, `MudLink`, focus rings).
- ❌ **Não usar** gradientes — paleta sólida e intencional.
- ❌ **Não misturar** red e green no mesmo componente.
- ❌ **Não usar** `--eco-color-primary-muted` ou `--eco-color-primary-tint` como cor de texto.
- ❌ **Não centralizar** texto em listas ou formulários.
- ❌ **Não aplicar** `--eco-shadow-modal` em card.
- ❌ **Não exibir** componentes de ranking para Admin/Suporte (RN013) — ocultar, não desabilitar.
- ❌ **Não usar** `badge-error` para PJ pendente — usar `Pending`.
- ❌ **Não escrever** literal hex/px em `.razor.css` — sempre `var(--eco-*)`.
- ❌ **Não usar** `MudButton` / `MudTextField` / `MudCard` cru na UI final — wrap em `Eco*`.
- ❌ **Não introduzir** Bootstrap (`btn-primary`, `form-control`), Tailwind ou outra lib além de MudBlazor.
- ❌ **Não usar** OpenSans / Helvetica / Arial — fonte única é Inter.
- ❌ **Não criar** segundo FAB na mesma tela.
- ❌ **Não exibir** ícone de nav-bar sem label caption.
- ❌ **Não bypassar** `MudTheme` com cor inline `Style="background:#xxx"` — adicionar token se faltar.

---

## 11. Cobertura mínima ao criar componente novo

Ao adicionar um novo `Eco*` em `Components/Shared/`:

1. Definir tokens necessários em `app.css` (se ainda não existirem). Atualizar `ThemeTokens.cs` em paralelo.
2. Documentar com summary PT-BR **somente** se a regra de uso for não-óbvia (ex.: "FAB exibido apenas em telas operacionais; nunca em auth").
3. Adicionar entrada em `Components/Shared/README` (se existir) com exemplo de uso e variantes.
4. Verificar contraste WCAG AA do par cor-texto/cor-fundo escolhido.
5. Garantir que o componente respeita modo desabilitado e estado de loading quando aplicável.
6. Confirmar que **nenhum literal hex/px** vazou para o CSS — usar grep: `grep -E "#[0-9A-Fa-f]{3,6}|[0-9]+px" Components/Shared/<arquivo>.razor.css`.

---

## 12. Perfis e visibilidade condicional

Três perfis no app: **Depositante (PF)**, **Ponto de Coleta (PJ)**, **Coletor (PJ)**. Cada tela operacional deve:

- Exibir apenas as features do perfil atual (Depositante: mapa + descarte + ranking PF; Ponto de Coleta: QR Code + materiais + cupons + ranking PC; Coletor: coletas + logística + ranking Coletor).
- **Ocultar** (não desabilitar) componentes não aplicáveis ao perfil.
- Ranking **oculto** para Admin/Suporte (RN013).
- Selo ESG (`badge-seal`) só em PJ com meta atingida (RN017).
- Cupons (`badge-coupon`) só visíveis ao Depositante que recebeu, ou ao PC que emitiu (US012).

---

## 13. Componentização e DRY

A UI do EcoCell Mobile **deve ser construída a partir de componentes reutilizáveis e parametrizáveis**. Markup duplicado entre telas viola o princípio de componentização do Blazor e o DRY de [`coding-rules.md`](coding-rules.md#4-dry--dont-repeat-yourself).

### 13.1 Regra de extração (≥2 usos)

A regra é **fixa e mecânica** — sem espaço para julgamento subjetivo:

| Cenário | Ação |
| --- | --- |
| Trecho de markup aparece em **1 tela** | Pode ficar inline na própria `.razor`. |
| Trecho aparece (ou está prestes a aparecer) em **2 ou mais telas** | **Extrair imediatamente** para `Components/Shared/<Categoria>/Eco<Nome>.razor`. |
| Variação só em texto/ícone/cor/handler | Idem — extrair e parametrizar via `[Parameter]`. |
| Variação em estrutura (ex.: tem footer ou não) | Extrair e usar `RenderFragment` (slot). |

> "Parecido o suficiente" inclui: mesmo botão pill com texto/ícone diferente, mesmo card com conteúdo diferente, mesmo banner com mensagem diferente, mesmo modal com payload diferente. Pequenas variações **não** justificam duplicação — justificam parâmetros.

**Antes de criar uma tela nova**, fazer busca por markup similar:

```bash
# busca em razor por padrões repetidos antes de duplicar
grep -rn "class=\"primary-cta\"" src/Ecocell.Mobile/Components/
grep -rn "<MudButton" src/Ecocell.Mobile/Components/Pages/
```

Se houver match → extrair primeiro, usar depois. **Não** copiar/colar com pequenas alterações.

### 13.2 Parametrização — padrão obrigatório

Todo componente reutilizável segue este shape:

```razor
@* Components/Shared/Buttons/EcoPrimaryButton.razor *@
<button type="@Type"
        class="eco-primary-cta @CssClass"
        disabled="@(Disabled || IsLoading)"
        @onclick="OnClick"
        @attributes="AdditionalAttributes">
    @if (IsLoading)
    {
        <EcoSpinner />
    }
    else
    {
        @if (LeadingIcon is not null) { <i class="@LeadingIcon"></i> }
        <span>@Label</span>
        @ChildContent
    }
</button>

@code {
    [Parameter, EditorRequired]
    public string Label { get; set; } = default!;

    [Parameter]
    public string Type { get; set; } = "button";

    [Parameter]
    public string? LeadingIcon { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public bool IsLoading { get; set; }

    [Parameter]
    public EventCallback OnClick { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public string? CssClass { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IDictionary<string, object>? AdditionalAttributes { get; set; }
}
```

Regras:

- **`[Parameter, EditorRequired]`** em parâmetros sem default sensato (`Label`, `OnClick` quando obrigatório).
- **Defaults** explícitos em todo parâmetro opcional (`Disabled = false`, `Type = "button"`).
- **`EventCallback`** para eventos — nunca `Action`/`Func` puro (perde async + StateHasChanged automático).
- **`RenderFragment`** para slots de conteúdo arbitrário (`ChildContent`, `HeaderContent`, `FooterContent`).
- **`AdditionalAttributes`** com `CaptureUnmatchedValues = true` permite passar `data-*`, `aria-*`, `id`, etc. sem precisar declarar cada um.
- **Variantes via enum**, não booleanos múltiplos:
  ```csharp
  // ✅ EcoBadgeVariant.Success | Error | Pending | Seal | Coupon
  [Parameter, EditorRequired]
  public EcoBadgeVariant Variant { get; set; }

  // ❌ tem 5 booleanos mutuamente exclusivos
  [Parameter]
  public bool IsSuccess { get; set; }

  [Parameter]
  public bool IsError { get; set; }
  // ...
  ```

### 13.3 Composição vs herança

- **Compor** componentes pequenos em maiores via `RenderFragment` e child components. **Não herdar** de outro `Eco*` — herança em Blazor cria acoplamento difícil de refatorar.
- Exemplo: `EcoCard` expõe `HeaderContent`, `BodyContent`, `FooterContent`. `EcoRankingCard` **usa** `EcoCard` internamente, **não** herda dele.

### 13.4 Quando NÃO extrair

Extração prematura também é problema. **Não** extrair se:

- O markup aparece em **um único lugar** e não há sinal de reuso iminente.
- O "componente" seria apenas um `<div>` com uma classe (use a classe direto).
- A "variação" exigiria mais de 6 parâmetros para cobrir os casos — sinal de que são **dois componentes diferentes**, não um genérico.

A regra é "≥2 usos extrai", não "todo `<div>` vira componente".

### 13.5 Onde colocar

Hierarquia obrigatória dentro de `Components/Shared/`:

```
Components/Shared/
├── Buttons/      (EcoPrimaryButton, EcoSecondaryButton, EcoSelectorButton, EcoGhostButton, EcoFab)
├── Inputs/       (EcoTextField, EcoOtpInput, EcoSelectField, EcoDatePicker)
├── Feedback/     (EcoErrorBanner, EcoSpinner, EcoSkeleton, EcoEmptyState, EcoToast)
├── Surfaces/     (EcoCard, EcoBottomSheet, EcoModal)
├── Navigation/   (EcoTopBar, EcoNavBar, EcoTabBar)
├── Badges/       (EcoBadge — variantes via enum)
├── Map/          (EcoMapPinCollectorPoint, EcoMapPinCollector, EcoMapBottomSheet)
└── Ranking/      (EcoRankingCard, EcoRankingTabs)
```

- Componente novo **deve** cair em uma dessas pastas. Se nenhuma encaixa, discutir antes de criar pasta nova.
- Cada componente tem `.razor` + `.razor.css` scoped (CSS isolado por componente — ver §2).

### 13.6 Refatoração de duplicação detectada

Se durante uma alteração você identificar markup duplicado **já existente**:

1. Pare a alteração em curso.
2. Extraia o componente compartilhado em `Components/Shared/<Categoria>/` com a parametrização mínima necessária para cobrir os call sites atuais.
3. Substitua **todas** as ocorrências pelo novo componente em um commit separado ("refactor: extrai EcoXxx").
4. Volte para a alteração original sobre a base já refatorada.

**Não** estender o componente novo com parâmetros para casos hipotéticos — só o que os call sites existentes exigem.

### 13.7 O que NÃO fazer (componentização)

- ❌ **Não copiar** `<MudButton>...</MudButton>` com pequenas variações entre páginas — extrair `EcoPrimaryButton`, `EcoSecondaryButton`, etc. e usar.
- ❌ **Não duplicar** estrutura de card, lista item, badge ou banner — extrair com `RenderFragment` slots.
- ❌ **Não usar** `Action`/`Func` em parâmetro de evento — usar `EventCallback`/`EventCallback<T>`.
- ❌ **Não declarar** booleanos mutuamente exclusivos — usar `enum`.
- ❌ **Não receber** `string Style` para repassar CSS — usar `CssClass` + `AdditionalAttributes`.
- ❌ **Não herdar** um `Eco*` de outro `Eco*` — compor.
- ❌ **Não criar** componente "God" com >6 parâmetros para cobrir variações dispares — quebrar em dois.
- ❌ **Não deixar** parâmetro obrigatório sem `[EditorRequired]` — quebra autocompletar e revisão.
- ❌ **Não esquecer** `CaptureUnmatchedValues` em componente que vai aceitar `aria-*`/`data-*`.