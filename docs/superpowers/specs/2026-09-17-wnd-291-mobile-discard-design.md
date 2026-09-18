# WND-291 — US004.E — Mobile do fluxo de descarte — Design

**Issue:** [WND-291](https://linear.app/wnd-dev/issue/WND-291/us004e-mobile)

**Parent:** [WND-166](https://linear.app/wnd-dev/issue/WND-166/us004-descarte-via-qr-code)

**Dependências concluídas:** WND-287, WND-288, WND-289 e WND-290

**Status do design:** aprovado em 2026-09-17

## Objetivo

Entregar o fluxo Mobile completo da US004.E para duas jornadas:

- o Depositante lê ou informa o QR Code de um Ponto de Coleta, confere o ponto, declara os itens e abre um descarte pendente;
- o gestor do Ponto de Coleta vê as pendências do contexto ativo, corrige os itens e confirma ou rejeita o descarte.

A entrega também adiciona uma notificação local de novos descartes enquanto o aplicativo estiver em primeiro plano ou for retomado. Não haverá push remoto nem execução contínua em background.

Esta specification substitui `2026-07-03-us004e-tela-scan-descarte-design.md`. A specification anterior foi escrita quando o backend de descartes ainda não existia e restringia a entrega a um scanner simulado. O backend necessário agora está disponível e a WND-291 passa a cobrir o escopo Mobile real.

## Princípios do recorte

- Preservar o contrato de abertura existente: todo descarte nasce com ao menos um item contendo material, quantidade e peso aproximado.
- O Depositante declara os itens após identificar o PC; o gestor pode substituir integralmente essa composição ao confirmar.
- Reutilizar os endpoints de abertura, confirmação e rejeição já existentes.
- Adicionar somente as duas consultas ausentes: preview do QR e lista de pendências do PC.
- Seguir VSA na API, sem repository, mapper ou service genérico.
- Seguir integralmente o Design System Mobile com componentes `Eco*`, tokens e CSS scoped.
- Manter a navegação atual do PC; pendências são um subfluxo da Home, não um sexto item da barra inferior.

## Protótipos normativos

Arquivo: `D:\Design\ecocell.pen`.

As telas aprovadas são:

- `NVBNO` — **Variação A — Preview e itens inline**;
- `jNSlX` — **Opção C1 — Lista de pendências**;
- `J0tvXz` — **Opção C2 — Conferência separada**.

As variações B1, B2 e D permanecem no arquivo apenas como alternativas rejeitadas. Não são fonte normativa para implementação.

## Arquitetura

### Mobile

- `Scan.razor` controla os estados do fluxo do Depositante: introdução, scanner, preview com itens, envio e sucesso.
- `Confirm.razor` controla os estados do PC: lista, conferência, envio, vazio e erro.
- `ScanViewModel` e `ConfirmDiscardViewModel` concentram chamadas HTTP e transições de estado, seguindo o padrão atual do Mobile.
- `IQrScanner` abre uma página MAUI nativa em tela cheia. A implementação usa `CameraBarcodeReaderView` de `ZXing.Net.Maui.Controls`.
- `IDiscardClient` concentra preview, abertura, lista, confirmação e rejeição via Refit.
- `PendingDiscardMonitor` é singleton e mantém o polling somente durante uma sessão elegível em primeiro plano.
- A Home do PC recebe um card **Descartes pendentes**, com contador e acesso a `Confirm.razor`.

Não criar repositório Mobile, cache de domínio, event bus ou abstração genérica de polling.

### API

Criar dois slices VSA independentes em `Features/Discard/`, cada um com query, validator, handler e endpoint Carter no mesmo arquivo:

- preview do Ponto de Coleta a partir do QR;
- listagem das pendências de um Ponto de Coleta.

Os DTOs públicos ficam em `Ecocell.Shared/Responses/Discards/`. O parser canônico `ecocell://pc/{guid}` deve ser compartilhado pelo preview e pelo registro por uma pequena utility de domínio; não criar um serviço ou parser genérico.

## Contratos HTTP

### Preview do QR

Endpoint autenticado:

`GET /api/v1/discards/preview?qrCode={valor}`

Resposta `200 OK`:

```json
{
  "tradeName": "EcoPonto Centro",
  "formattedAddress": "Rua Exemplo, 100 — Centro, Governador Valadares/MG",
  "acceptedMaterials": ["CellPhone", "Battery"]
}
```

Contrato público proposto: `ResponseDiscardPreviewJson`.

Regras:

- validar o QR completo no servidor; o Mobile não extrai nem confia no `Guid`;
- aceitar somente pessoa física ativa na jornada de Depositante;
- localizar um `LegalPerson` da jornada `CollectPoint`;
- retornar somente materiais com `MaterialScoreRule` vigente no instante da consulta;
- ordenar materiais pelo valor do enum para resposta determinística;
- formatar apenas o endereço de exibição necessário, sem CNPJ, e-mail ou responsável;
- retornar `409 Conflict` quando o ponto existir, mas não estiver ativo.

Um PC ativo sem regra vigente retorna `200 OK` com `acceptedMaterials` vazio. O Mobile mostra um estado vazio informando que o ponto não aceita materiais no momento e não exibe o formulário de itens.

O preview é informativo. `POST /api/v1/discards` continua revalidando QR, estado do PC, materiais e regras. Alterações ocorridas entre preview e envio resultam no erro do contrato existente, sem confiar no snapshot do cliente.

### Lista de pendências

Endpoint autenticado:

`GET /api/v1/collector-points/{collectorPointId}/discards/pending`

Resposta `200 OK`:

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "createdAt": "2026-09-17T12:00:00Z",
      "depositorName": "Pessoa Depositante",
      "items": [
        {
          "material": "CellPhone",
          "quantity": 2,
          "approximateWeightKg": 0.450
        }
      ]
    }
  ]
}
```

Contratos públicos propostos:

- `ResponsePendingDiscardListJson`;
- `ResponsePendingDiscardJson`;
- `ResponsePendingDiscardItemJson`.

Regras:

- chamar `ICollectorPointAccessGuard.EnsureResponsibleActiveAsync` antes de consultar ou expor pendências;
- retornar somente descartes `Pending` do PC solicitado;
- ordenar por `CreatedAt` crescente e depois por `Id`;
- incluir somente nome do Depositante e composição declarada;
- não retornar CPF, e-mail ou outros dados pessoais;
- retornar todos os pendentes no MVP, sem paginação.

O mesmo endpoint alimenta lista, contador da Home e monitor. Não criar endpoint separado de contagem.

### Comandos reutilizados

- `POST /api/v1/discards` abre o descarte pendente;
- `POST /api/v1/discards/{id}/confirm` substitui a lista completa de itens e confirma;
- `POST /api/v1/discards/{id}/reject` rejeita sem body ou motivo.

## Autorização e erros

Preservar os contratos já adotados:

- `400 Bad Request`: QR malformado, identificador inválido ou composição inválida;
- `401 Unauthorized`: sessão ausente ou inválida;
- `403 Forbidden`: pessoa autenticada inelegível para a jornada solicitada;
- `404 Not Found`: recurso inexistente ou PC fora do escopo do gestor;
- `409 Conflict`: PC inativo, descarte terminal, regra não vigente ou concorrência.

O `404` da lista, confirmação e rejeição preserva anti-enumeração. Não revelar se um PC alheio possui pendências nem o estado de um descarte fora do escopo.

No Mobile:

- `401` usa o tratamento central de sessão;
- erros de validação aparecem junto ao formulário;
- falha de rede preserva QR, itens ou edição para nova tentativa;
- `409` na confirmação ou rejeição fecha a conferência, recarrega a lista e informa que o descarte já foi processado.

## Fluxo do Depositante

Rota: `/descartes/escanear`.

1. `Scan.razor` apresenta introdução, ação de câmera e alternativa manual.
2. `IQrScanner.ScanAsync` abre a página nativa.
3. A primeira leitura válida encerra o scanner; eventos duplicados são ignorados.
4. O QR lido ou digitado é enviado ao endpoint de preview.
5. A página mostra nome, endereço e materiais aceitos.
6. O Depositante informa materiais únicos, quantidade inteira positiva e peso positivo com até três casas decimais.
7. “Adicionar material” oferece somente materiais aceitos ainda não usados.
8. O `POST /api/v1/discards` envia o QR bruto e os itens.
9. Sucesso exibe a confirmação e limpa o estado transitório.

Trocar ou escanear outro QR limpa preview e itens. Erro de envio mantém o formulário. Cancelar o scanner volta à introdução.

### Scanner por plataforma

- Android e iOS: câmera nativa com `ZXing.Net.Maui.Controls`, inicializada por `.UseBarcodeReader()` e permissões declaradas nos manifests.
- Windows e plataforma sem suporte: entrada manual.
- Permissão negada ou indisponibilidade da câmera: mensagem objetiva e entrada manual, sem bloquear o fluxo.

Não implementar leitura de imagem da galeria, histórico de códigos ou câmera embutida no `BlazorWebView`.

## Fluxo do Ponto de Coleta

Rota: `/ponto-de-coleta/descartes-pendentes`.

1. A Home do PC mostra o card **Descartes pendentes** e o total conhecido.
2. O toque abre `Confirm.razor` no estado lista.
3. A seleção abre o estado de conferência dentro da mesma página, usando os dados já carregados.
4. O gestor pode adicionar, remover ou trocar materiais e corrigir quantidade ou peso.
5. Confirmar chama o endpoint existente e remove o item após `204 No Content`.
6. Rejeitar exige confirmação visual simples e chama o endpoint sem motivo.
7. Voltar da conferência retorna à lista e descarta alterações locais não enviadas.
8. Após uma mutação bem-sucedida, lista e contador são atualizados.

Sem contexto de PC válido, a página não chama a API. Ela direciona o usuário ao fluxo de seleção de contexto. Como a tela é um subfluxo da Home, a barra inferior mantém **Início** ativo e não ganha item adicional.

## Monitor e notificação local

Usar `Plugin.LocalNotification` compatível com .NET 10. A notificação é local; não existe registro de dispositivo ou endpoint de push.

### Elegibilidade e ciclo de vida

O `PendingDiscardMonitor` executa somente quando todas as condições forem verdadeiras:

- usuário autenticado;
- contexto ativo do tipo Ponto de Coleta;
- aplicativo em primeiro plano.

Executar uma consulta imediata ao entrar ou retomar e repetir a cada 30 segundos. Pausar no background, logout ou retorno ao perfil pessoal. Troca de PC cancela o ciclo atual e inicia outro. Toda resposta deve comparar a geração capturada de `ActiveContextService`; respostas antigas são descartadas.

### Baseline e deduplicação

Persistir por PC apenas:

- indicador de baseline concluído;
- conjunto de IDs atualmente conhecidos.

Algoritmo:

1. primeira resposta bem-sucedida sem baseline: salvar todos os IDs e não notificar;
2. respostas posteriores: calcular `currentIds - knownIds`;
3. se houver novos IDs, emitir uma única notificação agregada naquele ciclo;
4. substituir os IDs conhecidos pelo conjunto atual, mantendo a persistência limitada às pendências existentes;
5. falha de rede não altera baseline nem IDs conhecidos.

A notificação não contém nome do Depositante ou composição. O payload contém apenas o identificador do PC necessário para navegação.

### Permissão e navegação

Na primeira entrada elegível no modo PC, a Home mostra um `EcoBottomSheet` explicativo:

- **Ativar notificações** solicita a permissão nativa;
- **Agora não** fecha sem bloquear lista, confirmação ou rejeição;
- uma negativa não provoca solicitações automáticas repetidas.

Ao tocar na notificação, o aplicativo consulta novamente a lista existente de PCs gerenciados. Somente se o PC ainda pertencer ao usuário e estiver ativo ele atualiza `ActiveContextService` e abre a rota de pendências. Caso contrário, ignora o destino obsoleto e permanece no fluxo atual.

## Estado e comportamento das telas

### `Scan.razor`

Estados explícitos:

- introdução;
- abertura do scanner;
- carregamento do preview;
- preview e formulário;
- envio;
- sucesso;
- erro recuperável.

### `Confirm.razor`

Estados explícitos:

- carregamento;
- lista;
- conferência;
- envio;
- vazio;
- erro recuperável.

Não navegar para uma segunda página de conferência. Lista e detalhe são estados da mesma `Confirm.razor`, conforme protótipos C1 e C2.

## Design System e acessibilidade

Todas as telas e alterações na Home devem obedecer a `.claude/rules/design-system-rules.md`:

- componentes finais `Eco*`; páginas não usam componentes MudBlazor crus;
- Inter com somente pesos `400` e `600`;
- cores, espaçamentos, raios e sombras por tokens;
- CSS scoped e sem estilos inline;
- alvos interativos com no mínimo `44 x 44`;
- labels acessíveis para scanner, campos, remoção de itens e ações;
- estados de carregamento, erro, vazio, envio e sucesso;
- textos em português do Brasil;
- layout responsivo para celular e preview Windows.

Os protótipos aprovados já foram ajustados para dois pesos tipográficos e alvos mínimos de toque. A implementação deve reutilizar wrappers existentes antes de criar um novo `Eco*`; componente novo só é justificável com reuso real ou necessidade normativa do Design System.

## Dependências

- `ZXing.Net.Maui.Controls` para o scanner nativo; versão de referência validada no desenho: `0.10.3`.
- `Plugin.LocalNotification` para notificação local; versão de referência compatível com .NET 10: `14.1.1`.
- Refit já existente para `IDiscardClient`.

Fixar versões no projeto. Não adicionar biblioteca de estado, polling, navegação ou permissões.

## Estrutura prevista

```text
src/Ecocell.Api/Features/Discard/
  PreviewDiscardCollectorPoint.cs
  ListPendingDiscards.cs

src/Ecocell.Shared/Responses/Discards/
  ResponseDiscardPreviewJson.cs
  ResponsePendingDiscardListJson.cs
  ResponsePendingDiscardJson.cs
  ResponsePendingDiscardItemJson.cs

src/Ecocell.Mobile/Components/Pages/Discards/
  Scan.razor
  Scan.razor.css
  Confirm.razor
  Confirm.razor.css

src/Ecocell.Mobile/Services/Api/
  IDiscardClient.cs

src/Ecocell.Mobile/Services/Scanner/
  IQrScanner.cs
  MauiQrScanner.cs

src/Ecocell.Mobile/Services/Notifications/
  PendingDiscardMonitor.cs

src/Ecocell.Mobile/ViewModels/
  ScanViewModel.cs
  ConfirmDiscardViewModel.cs
```

A página MAUI nativa do scanner e os registros específicos de plataforma ficam próximos à implementação `MauiQrScanner`, seguindo a organização existente do projeto. Nomes finais podem ser ajustados às convenções encontradas durante o plano, sem alterar as responsabilidades aprovadas.

## Estratégia de testes

Aplicar Red → Green → Refactor na API.

### Unitários

Preview:

- QR válido, vazio, malformado e não canônico;
- perfil ou jornada inelegível;
- PC inexistente e inativo;
- PC ativo sem regra vigente;
- seleção somente de regras vigentes;
- ordenação determinística dos materiais.

Pendências:

- propagação de `403`, `404` e `409` do guard;
- filtro por PC e estado `Pending`;
- exclusão de confirmados e rejeitados;
- ordenação por `CreatedAt` e `Id`;
- lista vazia e mapeamento dos itens.

Usar `TestBase`, SQLite in-memory, xUnit, Shouldly, Moq e Bogus conforme as regras normativas.

### Integração

Cobrir para cada novo endpoint:

- caminho feliz;
- `401` sem token;
- `403` para perfil inelegível;
- `404` para recurso ausente ou fora do escopo;
- `409` para PC inativo;
- payload, filtro e ordenação observáveis via HTTP.

Adicionar regressão do fluxo `preview -> POST /discards -> lista -> confirm/reject`, preservando os contratos já cobertos em WND-288 e WND-289. Usar PostgreSQL e Redis reais por Testcontainers, sem mocks de infraestrutura.

### Mobile

Não criar um novo projeto ou framework de testes de UI nesta história. A ausência de infraestrutura automatizada Mobile fica registrada como limitação consciente para evitar ampliar WND-291 com uma fundação transversal.

Executar roteiro manual reproduzível:

- Android/iOS: câmera autorizada e negada;
- Windows: entrada manual;
- leitura duplicada produz um único resultado;
- QR inválido, PC inativo e falha de rede;
- PC ativo sem materiais aceitos;
- validação e preservação do formulário;
- lista em loading, vazia, carregada e erro;
- confirmação, rejeição e conflito `409`;
- baseline sem notificação, notificação de novo ID e ausência de duplicação;
- troca de PC, background, resume e logout;
- toque na notificação com revalidação de acesso.

### Verificação final

- `dotnet test Ecocell.slnx` com Docker disponível;
- build dos alvos Mobile disponíveis no ambiente;
- smoke manual Android e Windows;
- smoke iOS quando houver host compatível;
- comparação visual com as telas normativas do Pen.dev;
- inspeção de tokens, pesos tipográficos, contraste e alvos de toque.

## Observabilidade e privacidade

- Queries da API usam logs estruturados somente para falhas ou acessos negados relevantes.
- Não registrar o QR completo, nome do Depositante, composição ou endereço em logs Mobile.
- A notificação mostra apenas quantidade agregada e nome do PC quando necessário; não mostra dados do Depositante.
- Falha de polling não interrompe o app, não altera baseline e permanece recuperável no próximo ciclo.

## Alternativas consideradas

### Abrir descarte sem itens

Rejeitada. Romperia o contrato e as invariantes entregues em WND-288. A declaração inline preserva o domínio existente.

### Formulário em etapa separada

Rejeitado. A variação A reduz navegação e mantém preview e declaração no mesmo contexto.

### Edição expandida dentro da lista

Rejeitada. A lista ficaria densa e mais sujeita a erros. Os estados separados C1 e C2 mantêm foco sem criar outra rota.

### Sexto item na barra do PC

Rejeitado. Pendências pertencem à Home e o card oferece entrada suficiente.

### Push remoto

Rejeitado no MVP. Exigiria FCM/APNs, registro de dispositivo e backend de entrega. Polling foreground/resume atende ao requisito local com menor custo.

### Endpoint exclusivo de contador

Rejeitado. Lista, contador e monitor compartilham a mesma consulta; não há volume atual que justifique outro contrato.

### Infraestrutura automatizada de testes Mobile

Adiada. Um novo projeto portátil ou testes específicos de plataforma ampliariam o escopo arquitetural. A API recebe cobertura automatizada e o Mobile usa smoke manual nesta entrega.

## Critérios de aceite

- Depositante lê QR no Android/iOS ou informa código manualmente.
- Preview exibe PC, endereço e materiais vigentes sem expor dados desnecessários.
- Depositante abre descarte válido com ao menos um item.
- PC vê somente suas pendências, em ordem determinística.
- PC corrige a composição e confirma ou rejeita usando os contratos existentes.
- Concorrência ou estado terminal produz feedback `409` e atualização da lista.
- Home do PC mostra card e contador sem alterar a barra inferior.
- Primeiro polling não notifica pendências antigas; novos IDs geram uma notificação agregada por ciclo.
- Polling para no background, logout ou saída do contexto PC.
- Toque em notificação revalida o vínculo antes de trocar contexto.
- Permissão de notificação negada não bloqueia nenhuma operação.
- Telas seguem os protótipos aprovados e todas as regras do Design System.
- Novos endpoints possuem testes unitários e de integração.
- Suíte completa passa no ambiente com Docker e os smokes Mobile previstos são registrados.

## Fora do escopo

- FCM, APNs ou qualquer push remoto;
- polling e notificações em background;
- histórico de descartes;
- paginação;
- motivo de rejeição;
- alteração do cálculo ou job de pontuação;
- nova entidade ou migration;
- novo item de navegação do PC;
- leitura de QR em imagem da galeria;
- testes automatizados de UI Mobile;
- repository, cache distribuído, event bus ou abstração genérica.
