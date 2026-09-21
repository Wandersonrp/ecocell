# WND-169 — US005.C — Leaderboard Mobile de Depositantes — Design

**Issue:** [WND-169](https://linear.app/wnd-dev/issue/WND-169/us005-rankings)

**Escopo:** US005.C — Mobile

**Requisito relacionado:** US005 / RF011

**Dependências concluídas:** US005.A e US005.B

**Status:** Aprovado em 2026-09-19

## Objetivo

Entregar uma página Mobile dedicada para o ranking histórico de Depositantes PF ativos. A página permite alternar entre os recortes Nacional e Municipal, consultar a posição do usuário autenticado, percorrer a classificação paginada e filtrar o recorte Municipal pelo par cidade + UF.

A entrega consome exclusivamente o endpoint `GET /api/v1/rankings` criado pela US005.B. Não altera a view PostgreSQL, o cálculo de posições nem o contrato HTTP.

## Decisões de produto aprovadas

- Criar uma única página `Components/Pages/Rankings/Leaderboard.razor`, na rota `/ranking`.
- Exibir somente ranking de Depositantes PF no MVP.
- Abrir a página no recorte Nacional.
- Usar alternância Nacional/Municipal na própria página.
- Exibir cidade + UF inline somente quando Municipal estiver selecionado.
- Não criar abas ou categorias para Ponto de Coleta, Coletor, PJ Matriz ou PJ Filial.
- Não usar modal ou bottom sheet como container da Leaderboard.
- Não usar `EcoTopBar` na página de Ranking. O título da página fornece o contexto, e a `EcoNavBar` permanece como navegação principal.
- Não criar pódio, medalhas, gráficos, animações ou FAB no MVP.

## Protótipo normativo

Arquivo: `D:\Design\ecocell.pen`.

Frame aprovado: **Ecocell — Ranking Nacional**.

O frame representa o estado inicial Nacional e define:

- tela operacional 390 × 844;
- título alinhado à esquerda, imediatamente após a área segura do sistema;
- ausência intencional de `EcoTopBar`;
- seletor Nacional/Municipal em formato pill;
- card destacado da posição do usuário quando ele estiver fora da página carregada;
- lista vertical de posições;
- ação secundária **Carregar mais**;
- `EcoNavBar` com **Ranking** ativo.

O estado Municipal usa a mesma hierarquia visual. A única diferença estrutural é a exibição dos filtros cidade + UF e da ação **Buscar ranking** entre o seletor de abrangência e a posição do usuário.

## Escopo

US005.C entrega:

- página Mobile de Leaderboard;
- client Refit para o endpoint de ranking;
- estado e transições da página concentrados em um `RankingViewModel`;
- componente reutilizável `EcoRankingCard`;
- rota `/ranking` no item existente da navegação do Depositante;
- filtros inline de cidade e UF no recorte Municipal;
- paginação incremental por ação explícita;
- estados de carregamento, erro, vazio e retry;
- destaque da posição do usuário autenticado;
- validação visual e smoke manual nos alvos Mobile disponíveis.

US005.C não entrega:

- endpoint, DTO, migration ou alteração na US005.A/US005.B;
- ranking de Ponto de Coleta ou Coletor;
- categorias PF/PJ, Matriz/Filial ou `RankingCategory`;
- obtenção automática de cidade por GPS;
- autocomplete ou catálogo remoto de municípios;
- persistência de filtros entre sessões;
- busca por nome de participante;
- ordenação configurável;
- infinite scroll;
- refresh por gesto;
- cache, repository Mobile ou nova biblioteca de estado;
- pódio, medalhas, selos ou compartilhamento de posição.

## Arquitetura Mobile

### Página

`Leaderboard.razor` é responsável apenas por composição visual, binding e encaminhamento de eventos ao ViewModel.

Hierarquia:

1. área segura do sistema;
2. título **Ranking de depositantes**;
3. descrição curta;
4. seletor Nacional/Municipal;
5. contexto do recorte ou filtros municipais;
6. posição do usuário, quando aplicável;
7. lista da classificação;
8. ação **Carregar mais**, quando `hasMore` for verdadeiro;
9. `EcoNavBar` com `ActiveId="ranking"`.

A página não renderiza `EcoTopBar` nem `EcoBackTopBar`. Ranking é um destino principal da barra inferior, não um subfluxo que exige ação de voltar.

### ViewModel

`RankingViewModel` concentra:

- escopo selecionado;
- cidade e UF informadas;
- itens carregados;
- posição do usuário;
- página atual;
- `hasMore`;
- carregamento inicial;
- aplicação de filtro;
- carregamento incremental;
- erro recuperável;
- invalidação de respostas obsoletas após troca rápida de escopo ou filtro.

Não criar repository, mapper ou service intermediário. O ViewModel usa diretamente `IRankingClient`, seguindo o padrão atual dos ViewModels Mobile.

### Client

Criar `IRankingClient` em `Services/Api/` e registrá-lo no `MauiProgram` com os handlers HTTP já usados pelos demais clients autenticados.

O client recebe `RequestGetRankingJson` como query e retorna `IApiResponse<ResponseRankingJson>`. Não duplicar DTOs no projeto Mobile.

## Fluxo de UX

### Entrada e recorte Nacional

1. O Depositante toca em **Ranking** na barra inferior.
2. A rota `/ranking` abre com Nacional selecionado.
3. A página solicita `scope=National`, `page=1` e `pageSize=20`.
4. Durante a consulta, skeletons preservam a estrutura da lista.
5. A resposta substitui os itens atuais e informa se existe próxima página.

O contexto Nacional exibe o texto **Pontuação acumulada em todo o Brasil**. Cidade e UF não são mostradas.

### Recorte Municipal

1. O usuário seleciona **Municipal**.
2. A página exibe os campos visíveis **Cidade** e **UF**.
3. Cidade é texto livre; UF é uma seleção estática das 27 unidades federativas.
4. A consulta só ocorre após ambos os campos serem válidos e o usuário tocar em **Buscar ranking**.
5. Uma nova busca reinicia a paginação em `1` e substitui a lista anterior.

Não consultar a cada tecla, não solicitar GPS e não adicionar endpoint de municípios. O servidor permanece responsável pela normalização canônica de caixa e espaços.

Ao retornar para Nacional, cidade e UF podem permanecer na memória da página. A API ignora esses campos no recorte Nacional, conforme a US005.B.

### Paginação

- O tamanho de página é `20`.
- **Carregar mais** solicita `page + 1` e acrescenta os novos itens à lista.
- A ação fica desabilitada enquanto a requisição estiver em andamento.
- Falha ao carregar outra página preserva os itens já visíveis e permite retry.
- Quando `hasMore` for falso, a ação não é renderizada.

Não implementar infinite scroll, contador total ou cálculo de total de páginas.

### Posição do usuário

- Quando `currentUser` também estiver em `items`, destacar esse item na posição natural da lista e não renderizar um segundo card **Sua posição**.
- Quando `currentUser` estiver fora dos itens carregados, renderizar o card destacado **Sua posição** antes da classificação.
- Quando `currentUser` for `null`, não renderizar o card destacado.

O card do usuário usa `--eco-color-primary-tint` e borda `--eco-color-primary`. Os demais cards usam a aparência padrão de `EcoCard`.

## Componentização

### Reutilizar

- `MainLayout`;
- `EcoNavBar`;
- `EcoFilterChips` para a alternância Nacional/Municipal;
- `EcoTextField` para cidade;
- `EcoSelectField` para UF;
- `EcoPrimaryButton` para **Buscar ranking**;
- `EcoSecondaryButton` para **Carregar mais**;
- `EcoCard` como superfície interna do card de ranking;
- `EcoSkeleton`;
- `EcoErrorBanner`;
- `EcoEmptyState`.

### Criar

`EcoRankingCard` em `Components/Shared/Ranking/`, composto com `EcoCard`, recebe somente:

- nome reduzido;
- posição;
- pontos;
- indicador de usuário atual.

O componente não recebe categoria, medalha, avatar, cor livre ou callback. Toda linha é informativa, não interativa.

Não criar `EcoRankingTabs`. `EcoFilterChips` já cobre os dois recortes aprovados.

## Navegação e elegibilidade

O item `ranking` existente em `DepositorNav.Items` passa a usar `Href="/ranking"`.

`Profile.razor` deve reutilizar `DepositorNav.Items` em vez de manter uma segunda lista local. Isso evita que a rota de Ranking permaneça inerte em uma tela e ativa em outra.

A navegação de Ranking aparece somente na jornada de Depositante. Admin, Suporte, Ponto de Coleta e Coletor não recebem item desabilitado; o item é ocultado pelo shell aplicável.

O endpoint continua sendo a última barreira de autorização:

- `401` usa o tratamento central de sessão;
- `403` não renderiza dados de ranking e direciona o usuário para a home compatível com sua sessão;
- `400` municipal preserva os filtros e exibe feedback recuperável.

## Estados e feedback

### Carregamento inicial

Exibir skeletons com a altura aproximada dos cards. Não mostrar spinner central sobre tela vazia.

### Erro inicial

Exibir `EcoErrorBanner` com **Tentar novamente**. Manter seletor e filtros acessíveis.

### Erro de paginação

Preservar a lista carregada e apresentar retry próximo à ação de paginação.

### Lista vazia

Nacional:

- título: **Ainda não há posições no ranking**;
- descrição: **Os participantes aparecerão após receberem seus primeiros pontos.**

Municipal:

- título: **Nenhum participante neste município**;
- descrição: **Tente outra cidade ou confira a UF selecionada.**

Ausência de participantes é `200 OK`, não erro.

## Formatação

- Posição: número ordinal em PT-BR, como `1º` e `142º`.
- Pontos: cultura `pt-BR`, separador de milhar e até cinco casas decimais significativas; casas finais iguais a zero são omitidas.
- Nome: usar `reducedName` exatamente como retornado pela API.
- Empates: mostrar a mesma posição recebida, sem renumerar localmente.

O Mobile nunca calcula posição, reduz nome ou reordena os itens.

## Design System e acessibilidade

A implementação segue `.claude/rules/design-system-rules.md`, com a exceção aprovada da ausência de `EcoTopBar` nesta página.

- fundo `--eco-color-surface-alt` e superfícies brancas;
- Inter conforme os tokens: corpo em `400`, labels e ações em `600`, título H1 e número de posição em `700`; não introduzir pesos livres fora desses papéis;
- título H1 alinhado à esquerda;
- espaçamentos, raios, sombras e cores somente por tokens;
- card padrão com raio `--eco-radius-md`;
- card do usuário com contraste aprovado entre primary e primary-tint;
- alvos interativos com no mínimo 44 × 44;
- labels visíveis para Cidade e UF;
- `aria-selected` no seletor de abrangência;
- texto acessível de posição, nome e pontos em cada card;
- navegação inferior com ícone e label;
- sem FAB, gradiente ou cores fora da paleta.

## Concorrência de interface

Trocas rápidas de escopo, cidade ou UF não podem permitir que uma resposta antiga substitua o estado mais recente.

Cada consulta captura uma geração local. Somente a resposta da geração atual pode alterar itens, posição do usuário, página, `hasMore` ou erro. Uma nova busca invalida a geração anterior. A abordagem pode usar `CancellationTokenSource` ou contador de geração; não criar abstração genérica para uma única página.

## Estrutura prevista

```text
src/Ecocell.Mobile/Components/Pages/Rankings/
  Leaderboard.razor
  Leaderboard.razor.css

src/Ecocell.Mobile/Components/Shared/Ranking/
  EcoRankingCard.razor
  EcoRankingCard.razor.css

src/Ecocell.Mobile/Services/Api/
  IRankingClient.cs

src/Ecocell.Mobile/ViewModels/
  RankingViewModel.cs

tests/Ecocell.Mobile.UnitTests/
  Ecocell.Mobile.UnitTests.csproj
  ViewModels/RankingViewModelTests.cs
```

Arquivos existentes previstos para alteração:

- `src/Ecocell.Mobile/MauiProgram.cs`;
- `src/Ecocell.Mobile/Services/Navigation/DepositorNav.cs`;
- `src/Ecocell.Mobile/Components/Pages/Person/Profile.razor`;
- imports globais somente se o namespace novo não estiver coberto.

## Estratégia de testes e validação

### Comportamentos mínimos

- Nacional é o estado inicial;
- Municipal exige cidade + UF;
- nova busca reinicia página e substitui itens;
- carregar mais acrescenta itens e respeita `hasMore`;
- resposta obsoleta não substitui estado atual;
- erro inicial permite retry;
- erro de paginação preserva itens;
- `currentUser` não é duplicado quando já está na lista;
- `currentUser` fora da página aparece em **Sua posição**;
- empates e posições atravessam a paginação sem renumeração.

### Limite atual de automação Mobile

O repositório não possui projeto de testes que referencie o executável MAUI multi-target. Para cumprir a regra obrigatória de TDD sem criar uma camada de aplicação adicional, US005.C adiciona o projeto mínimo `Ecocell.Mobile.UnitTests`.

O projeto:

- usa xUnit, Shouldly e Moq já adotados no repositório;
- referencia `Ecocell.Mobile` pelo alvo `net10.0-windows10.0.19041.0`;
- testa somente `RankingViewModel` e transições de estado, sem bUnit ou automação de UI;
- entra em `Ecocell.slnx`, mas não em `Ecocell.Backend.slnf`;
- não adiciona biblioteca de produção nem dependência ao aplicativo.

O ciclo Red → Green → Refactor começa nesse projeto antes da criação do ViewModel. Renderização, responsividade e integração com chrome nativo permanecem no smoke manual.

### Smoke manual obrigatório

- abrir `/ranking` pela barra inferior;
- confirmar ausência de `EcoTopBar`;
- carregar Nacional com sucesso, vazio e erro;
- alternar para Municipal;
- validar cidade vazia e UF vazia;
- consultar cidades homônimas em UFs diferentes;
- retornar a Nacional preservando os filtros em memória;
- carregar múltiplas páginas;
- provocar falha em **Carregar mais** e confirmar preservação da lista;
- validar usuário dentro e fora da página;
- validar empate `1, 1, 2`;
- validar nomes reduzidos e ausência de identificadores pessoais;
- validar toque, foco, leitor de tela e rolagem em 390 × 844;
- validar Android e Windows; iOS quando houver host compatível.

### Verificação final

```powershell
rtk dotnet test tests/Ecocell.Mobile.UnitTests/Ecocell.Mobile.UnitTests.csproj
rtk dotnet test Ecocell.slnx
rtk proxy dotnet build src/Ecocell.Mobile/Ecocell.Mobile.csproj -f net10.0-windows10.0.19041.0 --no-restore
rtk proxy dotnet build src/Ecocell.Mobile/Ecocell.Mobile.csproj -f net10.0-android -r android-arm64 --no-restore
rtk git diff --check
```

Comparar a tela final com o frame normativo do Pen e revisar tokens, contraste, alvos de toque, estados e ausência de `EcoTopBar`.

## Alternativas rejeitadas

### Leaderboard em bottom sheet

Rejeitada. Ranking é destino principal, possui paginação e exige leitura prolongada. Bottom sheet é adequado apenas para interação auxiliar curta.

### Ranking dentro de Home ou Perfil

Rejeitado. Mistura responsabilidades e deixa o item já existente da barra inferior sem destino próprio.

### Abas por categoria

Rejeitadas no MVP. Ponto de Coleta, Coletor e categorias PJ não possuem dados entregues pela WND-169 atual.

### `EcoTopBar`

Removida por decisão explícita de design. O título identifica a página, enquanto a barra inferior preserva a navegação principal. Não substituir por outra top bar.

### Filtro municipal em modal ou bottom sheet

Rejeitado no MVP. Dois campos inline e uma ação explícita são menores, mais visíveis e suficientes.

### Infinite scroll

Rejeitado. **Carregar mais** é previsível, acessível e usa diretamente `hasMore` sem listener adicional.

### Autocomplete de municípios

Adiado. Exigiria fonte de dados ou endpoint inexistente. Texto livre + UF atende ao contrato atual.

## Critérios de aceite

- Depositante PF ativo acessa `/ranking` pela barra inferior.
- Tela não contém `EcoTopBar` nem top bar substituta.
- Nacional é carregado por padrão.
- Municipal exige cidade + UF e mantém municípios homônimos separados pela UF.
- Página exibe nome reduzido, posição e pontos sem CPF, e-mail ou ID interno.
- Empates preservam a posição calculada pela API.
- Usuário atual recebe destaque sem duplicação visual quando já estiver na página.
- Paginação usa **Carregar mais** e preserva itens diante de falha incremental.
- Loading, erro, vazio e retry seguem os componentes `Eco*`.
- Ranking permanece oculto para perfis inelegíveis.
- Tela segue o frame aprovado, os tokens do EcoCell e os requisitos de acessibilidade.
- Nenhuma alteração é feita na API, view, migration ou cálculo de ranking.

## Fora do escopo

- ranking PJ, Ponto de Coleta ou Coletor;
- categorias, tabs e `RankingCategory`;
- selos ESG e cupons;
- GPS, geocoding ou catálogo de municípios;
- busca de participante;
- ordenação configurável;
- compartilhamento;
- cache offline;
- notificações de mudança de posição;
- top bar de qualquer tipo;
- nova dependência de UI, navegação ou estado.
