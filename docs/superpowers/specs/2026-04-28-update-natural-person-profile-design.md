# Design: Atualização de Perfil da Pessoa Física

**Data:** 2026-04-28  
**Branch:** feature/atualizar-perfil-pessoa-fisica  
**Status:** Aprovado

---

## 1. Contexto

A plataforma EcoCell já possui os slices de cadastro (`RegisterNaturalPerson`) e consulta de perfil (`GetNaturalPersonProfile`) para pessoas físicas. Esta feature adiciona a capacidade de atualizar dados do perfil autenticado.

**Campos bloqueados** (somente via chamado ao suporte — fora de escopo):
- `Email` — usado como credencial de login (RN010)
- `Cpf` — identificador único (RN001)
- `Journey` — mudança de jornada tem impacto de negócio (ex.: `Responsible` → `Depositor` violaria RN003/RN005)
- `BirthDate` — imutável após cadastro

**Campo editável:**
- `FullName` — nome completo, sem impacto em regras de negócio

---

## 2. Contrato HTTP

```
PUT /api/natural-person/me
Authorization: Bearer <JWT>
Content-Type: application/json

Body:
{
  "fullName": "string"
}

Respostas:
200 OK        → ResponseNaturalPersonProfile (perfil atualizado)
400 Bad Request → ResponseError (validação)
401 Unauthorized
404 Not Found
```

- `PersonId` é extraído da claim `Sid` do JWT — não exposto no body.
- Reutiliza `ResponseNaturalPersonProfile` existente (sem novo DTO de resposta).

---

## 3. Arquitetura — Slice VSA

Arquivo: `src/Ecocell.Api/Features/Person/UpdateNaturalPerson.cs`

### 3.1 Command

```csharp
public record Command : IRequest<ResultT<ResponseNaturalPersonProfile>>
{
    public Guid PersonId { get; set; }
    public string FullName { get; set; } = string.Empty;
}
```

### 3.2 Validator

Regras sobre `FullName`:
- `NotEmpty` — obrigatório
- `MinimumLength(3)` — mínimo 3 caracteres
- `MaximumLength(100)` — máximo 100 caracteres

### 3.3 Handler

Fluxo:
1. Valida o `Command` via `IValidator<Command>` → `400` se inválido
2. Busca `NaturalPerson` por `PersonId` → `404` se não encontrada
3. Chama `naturalPerson.Update(fullName)` na entidade
4. `SaveChangesAsync`
5. Retorna projeção `ResponseNaturalPersonProfile` (mesma projeção EF do `GetNaturalPersonProfile`)

### 3.4 Endpoint

```
PUT /api/natural-person/me
RequireAuthorization(AuthorizationPolicies.Authenticated)
PersonId extraído de: user.FindFirstValue(JwtRegisteredClaimNames.Sid)
```

---

## 4. Entidade — NaturalPerson

Novo método interno:

```csharp
internal void Update(string fullName)
{
    FullName = fullName;
    MarkAsUpdated();
}
```

- Modificador `internal` — mutação restrita ao assembly `Ecocell.Api`.

---

## 5. Shared — Novo Request

Arquivo: `src/Ecocell.Shared/Requests/RequestUpdateNaturalPerson.cs`

```csharp
public record RequestUpdateNaturalPerson
{
    public string FullName { get; set; } = string.Empty;
}
```

---

## 6. Testes de Unidade

Arquivo: `tests/Ecocell.UnitTests/Features/Person/UpdateNaturalPersonTests.cs`

| Método | Cenário |
|--------|---------|
| `Handle_ShouldUpdateFullName_WhenRequestIsValid` | Caminho feliz — nome atualizado no banco |
| `Handle_ShouldReturnFailure_WhenFullNameIsEmpty` | `FullName` vazio |
| `Handle_ShouldReturnFailure_WhenFullNameIsTooShort` | `FullName` com menos de 3 chars (`[Theory]`) |
| `Handle_ShouldReturnFailure_WhenFullNameIsTooLong` | `FullName` com mais de 100 chars |
| `Handle_ShouldReturnNotFound_WhenPersonDoesNotExist` | `PersonId` sem registro no banco |

Setup: pré-popula `DbContext` com `NaturalPerson` válida; `_command` usa `PersonId` da entidade inserida.

---

## 7. Decisões e Restrições

| Decisão | Justificativa |
|---------|---------------|
| Apenas `FullName` editável | `Email`/`Cpf`/`Journey`/`BirthDate` bloqueados por regra de negócio ou política de suporte |
| `PUT` (substituição total dos campos editáveis) | Simples; com 1 campo, PATCH não traz ganho real |
| `200` com body | Evita round-trip extra para o cliente atualizar o estado local |
| Método `internal Update` na entidade | Encapsula mutação no domínio; setter privado permanece protegido |
