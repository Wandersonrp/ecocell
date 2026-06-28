
## 1. Visão Geral (Overview)

O **EcoCell** é uma plataforma mobile focada em sustentabilidade e economia circular. O projeto visa solucionar o problema do descarte incorreto de lixo eletrônico, conectando geradores (pessoas e empresas) a locais de recepção e empresas de coleta especializada. O diferencial da plataforma é a **gamificação**, que incentiva o descarte correto através de um sistema de ranqueamento e benefícios.

### 1.1 Perfis de Usuário (Personas)

- **Pessoa Física (PF):** Usuário individual que se cadastra diretamente na plataforma. Assume o papel de **Depositante** (gerador de resíduo) e, opcionalmente, pode ser **Gestor** de uma Pessoa Jurídica — nesse caso cadastra a PJ após o login, mantendo o papel de Depositante.
- **Pessoa Jurídica (PJ):** Entidade cadastrada por um Gestor PF autenticado. Assume exclusivamente o papel de **Ponto de Coleta** (hub intermediário) ou **Coletor** (empresa de reciclagem/logística). Não possui acesso autônomo à plataforma — todas as ações são realizadas pelo Gestor PF vinculado.

## 2. Fluxos Principais e Gamificação

### 2.1 Ciclo de Pontuação

O sistema utiliza gamificação para manter o ecossistema ativo:

1. **Descarte:** O Depositante entrega o material e o receptor (Ponto ou Coletor) valida a transação no app.
2. **Validação:** A pontuação só é creditada após o "double-check" (ambas as partes confirmam a ação).
3. **Benefícios:** Pontos acumulados podem gerar cupons em estabelecimentos parceiros (Pontos de Coleta).

### 2.2 Estrutura de Ranqueamento

Os rankings são segregados para garantir competitividade justa:

- **Depositantes:** Separados entre PF e PJ.
- **Pontos de Coleta:** * **Nacional:** Focado em grandes redes (Soma de Matriz + Filiais).
  - **Municipal:** Focado em unidades locais (Matriz e Filiais competem individualmente).

## 3. Requisitos Funcionais (RF)

| **ID**      | **Descrição (User Story)**                                                                                                              | **Prioridade** |
| ----------- | --------------------------------------------------------------------------------------------------------------------------------------- | -------------- |
| **[RF001]** | Como usuário, quero me cadastrar no Ecocell como Pessoa Física (tornando-me Depositante) ou, após logado, cadastrar uma Pessoa Jurídica (Ponto de Coleta ou Coletor) vinculada à minha conta. | Essencial      |
| **[RF002]** | Como usuário pessoa jurídica, quero me cadastrar informando o CNPJ e e-mail para que o sistema recupere meus dados automaticamente.     | Desejável      |
| **[RF003]** | Como sistema, devo enviar um e-mail de confirmação para cada nova Pessoa Jurídica cadastrada.                                           | Essencial      |
| **[RF004]** | Como usuário, quero realizar login via código de verificação enviado ao e-mail cadastrado.                                              | Essencial      |
| **[RF005]** | Como DEPOSITANTE, quero visualizar um mapa com os PONTOS DE COLETA e COLETORES próximos à minha localização.                            | Essencial      |
| **[RF006]** | Como PONTO DE COLETA ou COLETOR, quero visualizar os outros atores da rede na minha cidade para coordenar entregas e coletas.           | Importante     |
| **[RF007]** | Como DEPOSITANTE, quero registrar um descarte através da leitura de QR Code no estabelecimento receptor.                                | Essencial      |
| **[RF008]** | Como PONTO DE COLETA, quero definir a quantidade de pontos que cada tipo de material descartado irá gerar para o depositante.           | Importante     |
| **[RF009]** | Como PONTO DE COLETA, quero emitir cupons de benefícios para depositantes que atingirem metas de pontos no meu estabelecimento.         | Desejável      |
| **[RF010]** | Como COLETOR, quero poder oferecer e gerenciar a opção de "Coleta a Domicílio" para depositantes.                                       | Importante     |
| **[RF011]** | Como usuário, quero visualizar os três tipos de ranqueamento (Depositante, Ponto de Coleta e Coletor) com filtros Municipal e Nacional. | Essencial      |
| **[RF012]** | Como gestor de PONTO DE COLETA (Matriz), quero vincular minhas unidades filiais ao meu perfil para centralizar a pontuação nacional.    | Importante     |
| **[RF013]** | Como ADMINISTRADOR, quero uma interface para validar, aprovar ou rejeitar novos cadastros de Pessoas Jurídicas (Pontos e Coletadores).  | Crítico        |
| **[RF014]** | Como PONTO DE COLETA, quero registrar a saída de materiais para um COLETOR, informando tipo, peso e quantidade.                         | Essencial      |

## 4. User Stories


### Epic 1: Gestão de Identidade e Acesso

#### US001 – Cadastro de Pessoa Física e Pessoa Jurídica

**Como** novo usuário, **quero** me cadastrar como Pessoa Física para acessar o Ecocell como Depositante e, se necessário, cadastrar uma Pessoa Jurídica (Ponto de Coleta ou Coletor) vinculada à minha conta após o login.

- **Critérios de Aceite:**

    - O fluxo de registro inicial é exclusivo para Pessoa Física (CPF + e-mail obrigatórios; unicidade validada — RN001).

    - Após autenticado, um PF Gestor pode cadastrar uma PJ (CNPJ + e-mail; validação de unicidade — RN001) e atribuir o papel de Ponto de Coleta ou Coletor (RN003).

    - Uma PJ só existe vinculada a um Gestor PF; não possui credenciais próprias de login.

    - A conta PF e a conta PJ são entidades separadas — papéis não se misturam (RN005).

- **Tasks Técnicas:**

    - [x] Slice `RegisterNaturalPerson` (PF → Depositante).

    - [ ] Slice `RegisterLegalPerson` (PJ → PC ou Coletor), acessível apenas por PF autenticado.

    - [ ] Telas de cadastro PF e cadastro PJ no Mobile (Blazor Hybrid).

#### **US002 – Login Passwordless (OTP)**

> **Como** usuário cadastrado, **quero** entrar no app usando apenas meu documento e um código enviado por e-mail, **para que** eu não precise memorizar mais uma senha.

- **Critérios de Aceite:**
    
    - Envio de código de 6 dígitos (expira em 5 min).
        
    - Bloqueio de conta após 3 tentativas inválidas.
        
- **Tasks Técnicas:**
    
    - [ ] Configurar `MailKit` ou `SendGrid` no Backend .NET 10.
        
    - [ ] Implementar lógica de geração e validação de Token (OTP) no Redis.
        
    - [ ] Criar interceptor no MAUI para gerenciar o Secure Cookie/Token.

#### **US008 – Auto-preenchimento PJ via CNPJ**

> **Como** novo usuário Pessoa Jurídica, **quero** que o sistema preencha automaticamente os dados da empresa a partir do CNPJ informado, **para que** meu cadastro seja mais rápido e menos suscetível a erros.

- **Critérios de Aceite:**

    - Ao informar CNPJ válido, o sistema consulta API externa e preenche razão social, nome fantasia, CNAE principal e endereço.

    - Situação cadastral diferente de "Ativa" exibe aviso e só permite seguir sob aprovação administrativa.

    - Falha ou timeout na API não deve bloquear o cadastro — permitir preenchimento manual.

- **Tasks Técnicas:**

    - [ ] Criar `ICnpjLookupService` na `Application` e implementação usando **BrasilAPI** na `Infrastructure`.

    - [ ] Expor endpoint `GET /api/v1/cnpj/{document}` na `EcoCell.Api` (com cache curto em memória).

    - [ ] Integrar consulta no formulário de cadastro PJ do Mobile via Refit client.

#### **US009 – Confirmação de conta por e-mail**

> **Como** usuário recém-cadastrado, **quero** receber um e-mail com código de verificação, **para que** eu confirme meu cadastro e ative minha conta no EcoCell.

- **Critérios de Aceite:**

    - Após o cadastro, o sistema envia e-mail com código de 6 dígitos válido por 10 minutos.

    - Ao confirmar o código, o status da conta muda de `AwaitingConfirmation` para `Active`.

    - Código expirado permite reenvio; tentativas inválidas são limitadas (3 por código).

- **Tasks Técnicas:**

    - [ ] Configurar `MailKit` (ou `SendGrid`) e mover credenciais para `appsettings`/User Secrets.

    - [ ] Implementar `VerificationCodeJobs` (Hangfire) de expiração e reenvio — remover `TODO` em `RegisterDepositorUseCase.cs`.

    - [ ] Criar `ConfirmAccountUseCase` com endpoint `POST /api/v1/account/confirm`.

    - [ ] Criar tela de confirmação de código no Mobile (6 inputs com máscara).

### Epic 2: Operação de Descarte e Logística

#### **US003 – Localização de Pontos (Mapa)**

> **Como** Depositante, **quero** visualizar um mapa com os Pontos de Coleta próximos, **para que** eu saiba onde levar meu lixo eletrônico.

- **Critérios de Aceite:**
    
    - Exibir Pins diferenciados para Pontos de Coleta e Coletores.
        
    - Permitir busca por outras cidades.
        
- **Tasks Técnicas:**
    
    - [ ] Integrar Google Maps SDK ou Leaflet no Blazor Hybrid.
        
    - [ ] Criar Endpoint de busca espacial (PostGIS ou lógica de latitude/longitude).
        

#### **US004 – Registro de Descarte via QR Code**

> **Como** Depositante, **quero** ler o QR Code de um Ponto de Coleta, **para que** o registro do meu descarte seja rápido e seguro.

- **Critérios de Aceite:**
    
    - O app deve abrir a câmera e ler o código.
        
    - O receptor (Ponto) deve confirmar o peso e tipo de material para validar.
        
- **Tasks Técnicas:**
    
    - [ ] Integrar biblioteca `ZXing.Net.Mobile` no MAUI.
        
    - [ ] Criar Service de transação de descarte (Update de saldo de pontos).

#### **US010 – Visualização da rede (PC e Coletor)**

> **Como** Ponto de Coleta ou Coletor, **quero** visualizar os demais atores da minha cidade no mapa, **para que** eu possa coordenar entregas, retiradas e parcerias.

- **Critérios de Aceite:**

    - Mapa exibe pins categorizados por perfil (PC, Coletor) com filtro de cidade.

    - Ao tocar em um pin, exibe ficha com razão social, endereço e dados de contato.

    - Somente atores com cadastro **ativo** (aprovado pelo admin) aparecem na rede.

- **Tasks Técnicas:**

    - [ ] Reutilizar o endpoint de busca espacial de US003 com filtro por tipo (`IsCollectorPoint`/`IsCollector`).

    - [ ] Adicionar rota de mapa no Mobile para perfis PC/Coletor reutilizando o componente criado em US003.

#### **US013 – Coleta a domicílio**

> **Como** Coletor, **quero** disponibilizar e gerenciar solicitações de coleta no endereço do depositante, **para que** grandes volumes não dependam de deslocamento do gerador.

- **Critérios de Aceite:**

    - Coletor habilita o serviço no perfil e define área/bairros atendidos e volume mínimo.

    - Depositante visualiza Coletores com serviço ativo e solicita coleta informando data e itens.

    - Coletor aceita ou recusa; ambas as partes confirmam a retirada (double-check).

- **Tasks Técnicas:**

    - [ ] Criar entidade `HomePickupRequest` (status, data, volume, endereço) em `Domain`.

    - [ ] Criar `RequestHomePickupUseCase`, `AcceptHomePickupUseCase` e `ConfirmHomePickupUseCase`.

    - [ ] Criar telas de solicitação (Depositante) e fila de atendimento (Coletor) no Mobile.

#### **US014 – Saída de materiais para Coletor**

> **Como** Ponto de Coleta, **quero** registrar a saída de materiais entregues a um Coletor, **para que** o estoque e os relatórios da rede fiquem consistentes.

- **Critérios de Aceite:**

    - PC escolhe Coletor destino, tipo, peso/quantidade de material e data da retirada.

    - Coletor valida o recebimento no app (double-check) para efetivar o movimento.

    - Histórico fica disponível para ambas as partes em "Movimentações".

- **Tasks Técnicas:**

    - [ ] Criar entidade `MaterialDispatch` em `Domain` (PC origem, Coletor destino, itens, status).

    - [ ] Criar `RegisterMaterialDispatchUseCase` e `ConfirmMaterialDispatchUseCase`.

    - [ ] Criar telas de registro (PC) e confirmação (Coletor) no Mobile.

### Epic 3: Gamificação e Engajamento

#### **US005 – Visualização de Rankings**

> **Como** usuário engajado, **quero** ver minha posição no ranking municipal e nacional, **para que** eu possa competir e ganhar selos de sustentabilidade.

- **Critérios de Aceite:**
    
    - Ranking separado por categorias (PF, PJ Matriz, PJ Filial).
        
    - Filtro por cidade.
        
- **Tasks Técnicas:**
    
    - [ ] Criar View/Procedure no PostgreSQL para cálculo de ranking (Performance).
        
    - [ ] Desenvolver tela de Leaderboard com Blazor.
        

#### **US006 – Gestão de Matriz e Filiais**

> **Como** gestor de uma rede (Matriz), **quero** vincular as filiais ao meu CNPJ, **para que** nossa pontuação seja somada no ranking nacional.

- **Critérios de Aceite:**
    
    - Apenas a Matriz pode solicitar o vínculo.
        
    - Relatórios devem mostrar a contribuição individual de cada filial.
        
- **Tasks Técnicas:**
    
    - [ ] Implementar auto-relacionamento na tabela `Empresas`.
        
    - [ ] Criar lógica de agregação de pontos (Sum) no Backend.

#### **US011 – Tabela de pontuação por material**

> **Como** Ponto de Coleta, **quero** cadastrar quantos pontos cada tipo de material descartado vale, **para que** a pontuação dos depositantes reflita a política da minha unidade.

- **Critérios de Aceite:**

    - PC lista os materiais aceitos (enum `EletronicMaterials`) e define pontos por unidade/kg.

    - Valores são versionados (histórico) — alterações não afetam descartes já registrados.

    - Depositante vê a tabela vigente na ficha do PC no mapa.

- **Tasks Técnicas:**

    - [ ] Criar entidade `MaterialScoreRule` (PC, material, pontos, valid from/to) em `Domain`.

    - [ ] Criar `IMaterialScoreRuleRepository` e migration EF Core.

    - [ ] Criar `SetMaterialScoreRulesUseCase` e `GetCurrentMaterialScoreRulesUseCase`.

    - [ ] Criar tela de gestão da tabela no Mobile (perfil PC).

#### **US012 – Emissão de cupons de benefícios**

> **Como** Ponto de Coleta, **quero** emitir cupons para depositantes que atingirem metas de pontos, **para que** eu fidelize quem descarta na minha unidade.

- **Critérios de Aceite:**

    - PC configura meta (pontos acumulados no estabelecimento) e descrição do benefício.

    - Depositante ao atingir a meta recebe cupom com código único e validade.

    - Cupom pode ser marcado como "utilizado" pelo próprio PC (não reutilizável).

- **Tasks Técnicas:**

    - [ ] Criar entidades `CouponRule` e `IssuedCoupon` em `Domain`.

    - [ ] Criar `IssueCouponOnScoreJob` (Hangfire) disparado após confirmação de descarte.

    - [ ] Criar telas "Meus cupons" (Depositante) e "Gerir cupons" (PC) no Mobile.

### Epic 4: Administração e Governança

#### **US007 – Aprovação de Parceiros**

> **Como** Administrador do EcoCell, **quero** revisar o cadastro de novas empresas (Pontos/Coletores), **para que** eu garanta a segurança da rede.

- **Critérios de Aceite:**
    
    - Listagem de cadastros pendentes.
        
    - Botão de Aprovar/Reprovar com campo de motivo.
        
- **Tasks Técnicas:**
    
    - [ ] Criar Dashboard administrativo.
        
    - [ ] Implementar State Machine para o status do usuário (Pendente, Ativo, Bloqueado).


## 4. Materiais Aceitos para Descarte

A plataforma foca em duas categorias principais:

1. **Pilhas e Baterias.**
    
2. **Eletrônicos:** Smartphones, videogames portáteis, periféricos (fones), impressoras, notebooks e pequenos eletrodomésticos.

## 5. Regras de Negócio

## 1. Gestão de Perfis e Identidade

| **ID**      | **Regra de Negócio**           | **Descrição Detalhada**                                                                                                                   |
| ----------- | ------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------- |
| **[RN001]** | **Unicidade de Identificador** | Não deve ser permitido o cadastro de múltiplos usuários com o mesmo CPF (Pessoa Física) ou CNPJ (Pessoa Jurídica).                        |
| **[RN002]** | **Validação de PJ**            | Todo cadastro de Pessoa Jurídica deve passar por validação de status "Ativo" na Receita Federal via API.                                  |
| **[RN003]** | **Restrição de Perfil (PJ)**   | Os papéis de **Ponto de Coleta** e **Coletor** são exclusivos de Pessoas Jurídicas. Uma PF não pode assumir esses papéis diretamente.     |
| **[RN004]** | **Acúmulo de Papéis PJ**       | *(Standby — fora do escopo do MVP)* Uma PJ Coletor poderá atuar simultaneamente como Ponto de Coleta, mas o inverso não será permitido.   |
| **[RN005]** | **Separação PF/PJ**            | A conta PF (Depositante/Gestor) e a conta PJ (PC/Coletor) são entidades distintas. O Gestor PF acessa e opera a PJ após autenticação, mas os papéis não se misturam na mesma entidade. |
| **[RN006]** | **Hierarquia Corporativa**     | Unidades filiais podem se cadastrar individualmente, mas devem possuir a opção de associação a um CNPJ Matriz para consolidação de dados. |

## 2. Processos de Cadastro e Acesso

| **ID**      | **Regra de Negócio**           | **Descrição Detalhada**                                                                                                                |
| ----------- | ------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------- |
| **[RN007]** | **Aprovação de Parceiros**     | Cadastros de **Pontos de Coleta** e **Coletores** permanecem em estado "Pendente" até aprovação manual por um Administrador.           |
| **[RN008]** | **Notificação de Pendência**   | Em caso de reprovação do cadastro, o sistema deve disparar e-mail automático solicitando as correções necessárias ao solicitante.      |
| **[RN009]** | **Geolocalização Obrigatória** | Pontos de Coleta e Coletores devem obrigatoriamente validar um endereço geocodificado para exibição no mapa de busca.                  |
| **[RN010]** | **Autenticação Segura**        | O login deve ser realizado via documento (CPF/CNPJ) ou e-mail, utilizando autenticação por código de verificação temporário (MFA/OTP). |

## 3. Operação de Descarte e Gamificação

|**ID**|**Regra de Negócio**|**Descrição Detalhada**|
|---|---|---|
|**[RN011]**|**Registro de Descarte**|Todo descarte deve registrar obrigatoriamente: tipo de material, quantidade e peso aproximado para fins estatísticos e de pontuação.|
|**[RN012]**|**Fluxos de Destinação**|Depositantes e Pontos de Coleta podem realizar o descarte direto (entrega ou coleta agendada) com um usuário do tipo Coletor.|
|**[RN013]**|**Elegibilidade de Pontos**|Usuários com roles de **Administrador** ou **Suporte** podem realizar descartes, porém estes não geram pontos nem contam para ranqueamentos.|
|**[RN014]**|**Segregação de Ranking**|O ranqueamento de Depositantes deve ser processado em categorias distintas para Pessoa Física e Pessoa Jurídica.|
|**[RN015]**|**Cálculo de Ranking PJ**|No ranking **Nacional**, a pontuação da Matriz é a soma de todas as suas filiais. No ranking **Municipal**, filiais e matrizes competem individualmente.|
|**[RN016]**|**Benefícios de Terceiros**|Pontos de Coleta têm autonomia para definir e gerenciar cupons/benefícios próprios para depositantes que utilizarem seus postos.|
|**[RN017]**|**Certificação ESG**|O sistema deve atribuir "Selos de Meio Ambiente" para Pessoas Jurídicas que atingirem metas de volume de descarte ou coleta.|