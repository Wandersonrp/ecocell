<div align="center">

![EcoCell Banner](/public/ecocell_banner.png)

<br/>

**Ecossistema de logística reversa para descarte inteligente de resíduos eletrônicos.**

<br/>

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)
![EF Core](https://img.shields.io/badge/EF_Core-10.0-512BD4?style=flat-square)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791?style=flat-square&logo=postgresql&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-7-DC382D?style=flat-square&logo=redis&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)

</div>

---

## Sobre

O **EcoCell** é um aplicativo mobile que conecta pessoas, empresas e organizações para o descarte sustentável de resíduos eletrônicos. O projeto integra a **Atividade Extensionista do Bacharelado em Engenharia de Software da Uninter** e está alinhado às seguintes ODS da ONU:

- 🏙️ **ODS 11** — Cidades e comunidades sustentáveis
- 🌡️ **ODS 13** — Ação contra a mudança global do clima
- 🌿 **ODS 15** — Vida terrestre

### Atores

| Ator | Tipo | Papel |
|---|---|---|
| **Depositante** | Pessoa Física | Descarta resíduos eletrônicos em Pontos de Coleta ou via coleta a domicílio |
| **Ponto de Coleta** | Pessoa Jurídica | Recebe resíduos dos Depositantes e os repassa para Coletores |
| **Coletor** | Pessoa Jurídica | Coleta os materiais dos Pontos de Coleta para destinação final |

### Gamificação

Cada ação de descarte ou coleta gera **pontos** para os participantes, exibidos em três modalidades de ranking:

- **Ranking Depositante** — pontuação individual por descarte
- **Ranking Ponto de Coleta** — modalidade **Nacional** (Matriz + Filiais) e **Municipal**
- **Ranking Coletor** — pontuação por volume coletado

---

## Arquitetura

O backend segue **Vertical Slice Architecture (VSA)**: cada caso de uso é um _slice_ autossuficiente contendo `Command/Query`, `Validator`, `Handler` e `Endpoint` em um único arquivo.

```
src/
├── Ecocell.Api/          # Host + Features + Persistência + Entidades
│   ├── Features/         # Slices por agregado (Account, Person, Admin…)
│   ├── Entities/         # Entidades EF Core (TPT/herança)
│   ├── Services/         # Email, Auth, VerificationCodes, CurrentUser
│   ├── Database/         # AppDbContext + TypeConfigurations
│   ├── Migrations/       # Migrations EF Core
│   ├── Configurations/   # Settings bindings (JWT, Mail, Redis, DB)
│   ├── Extensions/       # DI, Result, FluentValidation
│   └── Middlewares/      # CorrelationId, ExceptionHandler
└── Ecocell.Shared/       # Contratos HTTP (Requests / Responses / Enums)

tests/
├── Ecocell.UnitTests/         # xUnit + Shouldly + Moq + Bogus + SQLite in-memory
└── Ecocell.IntegrationTests/  # xUnit + Testcontainers (PostgreSQL + Redis) + WebApplicationFactory
```

---

## Stack

### API

| Tecnologia | Versão | Uso |
|---|---|---|
| .NET | 10.0 | Runtime |
| ASP.NET Core | 10.0 | Host HTTP |
| Entity Framework Core | 10.0 | ORM |
| Npgsql EF Core Provider | 10.0 | PostgreSQL |
| Carter | 10.0 | Endpoints mínimos |
| Mediator (Source Generator) | 3.0 | CQRS in-process |
| FluentValidation | 12.1 | Validação de entrada |
| MailKit | 4.16 | Envio de e-mail (SMTP) |
| StackExchange.Redis | 2.12 | Armazenamento de códigos OTP |
| JWT Bearer | 10.0 | Autenticação stateless |
| Serilog | 4.3 | Logs estruturados |
| Scalar | 2.14 | Documentação interativa da API |

### Testes de Unidade

| Tecnologia | Versão | Uso |
|---|---|---|
| xUnit | 2.9 | Framework de testes |
| Shouldly | 4.3 | Asserções fluentes |
| Moq | 4.20 | Mocks |
| Bogus + Bogus.Extensions.Brazil | 35.6 | Geração de dados falsos (CPF, CNPJ) |
| EF Core SQLite In-Memory | 10.0 | Banco de testes isolado |

### Testes de Integração

| Tecnologia | Versão | Uso |
|---|---|---|
| xUnit | 2.9 | Framework de testes |
| Shouldly | 4.3 | Asserções fluentes |
| Bogus + Bogus.Extensions.Brazil | 35.6 | Geração de dados falsos (CPF, CNPJ) |
| Testcontainers.PostgreSql | 4.4 | Container PostgreSQL efêmero por suite |
| Testcontainers.Redis | 4.4 | Container Redis efêmero por suite |
| Microsoft.AspNetCore.Mvc.Testing | 10.0 | `WebApplicationFactory<Program>` in-process |

---

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PostgreSQL 16+](https://www.postgresql.org/)
- [Redis 7+](https://redis.io/)
- [Docker](https://www.docker.com/) — necessário para os testes de integração (Testcontainers sobe PostgreSQL e Redis automaticamente)

---

## Configuração

### 1. Clone

```bash
git clone https://github.com/Wandersonrp/ecocell.git
cd ecocell
```

### 2. User Secrets

Copie `src/Ecocell.Api/appsettings.Example.json` como referência e configure via **User Secrets** (nunca commite credenciais):

```bash
cd src/Ecocell.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=ecocell;Username=<user>;Password=<senha>"
dotnet user-secrets set "Redis:ConnectionString" "localhost:6379"
dotnet user-secrets set "Jwt:SigningKey" "<chave-secreta-minimo-32-chars>"
dotnet user-secrets set "Mail:Host" "smtp.exemplo.com"
dotnet user-secrets set "Mail:Username" "noreply@exemplo.com"
dotnet user-secrets set "Mail:Password" "<senha-smtp>"
```

Estrutura completa de configuração em [`src/Ecocell.Api/appsettings.Example.json`](src/Ecocell.Api/appsettings.Example.json).

### 3. Migrations

```bash
dotnet ef database update --project src/Ecocell.Api --startup-project src/Ecocell.Api
```

### 4. Executar

```bash
dotnet run --project src/Ecocell.Api/Ecocell.Api.csproj
```

| Perfil | URL |
|---|---|
| HTTPS | https://localhost:7284 |
| HTTP | http://localhost:5207 |

---

## Documentação da API

Em ambiente de desenvolvimento, a documentação interativa está disponível via **Scalar**:

```
https://localhost:7284/scalar/v1
```

O esquema OpenAPI fica em `/openapi/v1.json`.

---

## Testes

```bash
# Todos os testes (unidade + integração)
dotnet test Ecocell.slnx

# Apenas testes de unidade (sem Docker, SQLite in-memory)
dotnet test tests/Ecocell.UnitTests/Ecocell.UnitTests.csproj

# Apenas testes de integração (requer Docker)
dotnet test tests/Ecocell.IntegrationTests/Ecocell.IntegrationTests.csproj

# Filtro por nome
dotnet test --filter "FullyQualifiedName~RegisterNaturalPersonTests"
```

Os **testes de unidade** usam SQLite in-memory — nenhuma infraestrutura externa necessária.

Os **testes de integração** requerem Docker. O Testcontainers sobe automaticamente containers de PostgreSQL e Redis por suite, sem configuração manual.

---

## Comandos úteis

| Objetivo | Comando |
|---|---|
| Restaurar dependências | `dotnet restore Ecocell.slnx` |
| Build da solução | `dotnet build Ecocell.slnx` |
| Rodar a API | `dotnet run --project src/Ecocell.Api/Ecocell.Api.csproj` |
| Rodar todos os testes | `dotnet test Ecocell.slnx` |
| Nova migration | `dotnet ef migrations add <Nome> --project src/Ecocell.Api --startup-project src/Ecocell.Api` |
| Aplicar migrations | `dotnet ef database update --project src/Ecocell.Api --startup-project src/Ecocell.Api` |

---

## Licença

Distribuído sob a licença **MIT**. Veja [`LICENSE`](LICENSE) para detalhes.