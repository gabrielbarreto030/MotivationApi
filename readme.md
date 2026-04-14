# Motivation API

API REST para sistema de motivação pessoal, construída com .NET 8 seguindo os princípios de DDD (Domain-Driven Design) e SOLID.

## Tecnologias

- **.NET 8** – Framework principal
- **DDD** – Domain-Driven Design (Domain, Application, Infrastructure, API)
- **EF Core InMemory** – Persistência em memória
- **JWT Bearer** – Autenticação via tokens
- **Serilog** – Logging estruturado (console + arquivo)
- **IMemoryCache** – Cache de consultas rápidas
- **Swagger/OpenAPI** – Documentação interativa
- **HealthChecks** – Monitoramento de saúde da aplicação
- **xUnit** – Testes unitários e de integração

---

# Backlog (30 dias)

Fase 1 – Fundamentos (Dias 1–5)

Dia 1: Scaffold solução + projetos (DDD structure) - OK
Dia 2: Configuração básica API + Swagger + HealthCheck - OK
Dia 3: Domain entities (User, Goal, Step, Motivation) - OK
Dia 4: EF Core InMemory + DbContext - OK
Dia 5: Repository pattern básico - OK

Fase 2 – Autenticação (Dias 6–8)

Dia 6: Registro de usuário - OK
Dia 7: Login + geração JWT - OK
Dia 8: Proteção de endpoints com Bearer - OK

Fase 3 – Goals (Dias 9–13)

Dia 9: Criar Goal - OK
Dia 10: Listar Goals por usuário - OK
Dia 11: Atualizar Goal - OK
Dia 12: Deletar Goal - OK
Dia 13: Cache para consulta de Goals - OK

Fase 4 – Steps (Dias 14–17)

Dia 14: Criar Step - OK
Dia 15: Listar Steps - OK
Dia 16: Marcar Step como concluído - OK
Dia 17: Calcular progresso do Goal - OK

Fase 5 – Motivation Engine (Dias 18–21)

Dia 18: Adicionar Motivation - OK
Dia 19: Remover Motivation - OK
Dia 20: Serviço que gera mensagem diária - OK
Dia 21: Endpoint para obter motivação diária - OK

Fase 6 – Qualidade & Observabilidade (Dias 22–26)

Dia 22: Logging estruturado com Serilog - OK
Dia 23: Middleware global de erro - OK
Dia 24: Unit Tests Domain - OK
Dia 25: Unit Tests Application - OK
Dia 26: Testes de integração API - OK

Fase 7 – Polimento (Dias 27–30)

Dia 27: Documentação Swagger detalhada - OK
Dia 28: HealthChecks avançados - OK
Dia 29: Melhorias SOLID & Refactors leves - OK
Dia 30: README final + exemplos de uso - OK

Fase 8 – Consolidação (Dias 31+)

Dia 31: Refatoração de AuthService e GoalService (redução de dependências desnecessárias) - OK
Dia 32: Paginação nas listagens de Goals e Steps - OK
Dia 33: Filtros avançados nas listagens (Goals por status, Steps por isCompleted) - OK

---

## Estrutura do Projeto

```
DOTNET/
├── src/
│   ├── Motivation.Api/                  # Camada de apresentação (Controllers, Middleware)
│   │   ├── Controllers/
│   │   │   ├── UsersController.cs
│   │   │   ├── GoalsController.cs
│   │   │   ├── StepsController.cs
│   │   │   ├── MotivationsController.cs
│   │   │   └── DailyMessageController.cs
│   │   ├── Middleware/
│   │   │   └── GlobalExceptionMiddleware.cs
│   │   ├── HealthChecks/
│   │   │   └── HealthCheckResponseWriter.cs
│   │   ├── Models/
│   │   │   ├── LoginRequestDto.cs
│   │   │   └── RegisterRequestDto.cs
│   │   ├── Services/
│   │   │   └── CurrentUserService.cs
│   │   └── Program.cs
│   │
│   ├── Motivation.Application/          # Camada de aplicação (Use Cases, DTOs, Interfaces)
│   │   ├── DTOs/
│   │   ├── Interfaces/
│   │   ├── Services/
│   │   └── Exceptions/
│   │
│   ├── Motivation.Domain/               # Camada de domínio (Entidades, Regras de Negócio)
│   │   ├── Entities/
│   │   │   ├── User.cs
│   │   │   ├── Goal.cs
│   │   │   ├── Step.cs
│   │   │   ├── Motivation.cs
│   │   │   └── GoalStatus.cs
│   │   └── Interfaces/                  # Contratos de repositório
│   │
│   └── Motivation.Infrastructure/       # Camada de infraestrutura (EF Core, JWT, Cache)
│       ├── Db/
│       │   └── AppDbContext.cs
│       ├── Repositories/
│       ├── Services/
│       │   └── JwtService.cs
│       └── HealthChecks/
│
└── tests/
    └── Motivation.UnitTests/            # Testes unitários e de integração
```

---

## Como Executar

### Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Rodando a API

```bash
# Clonar o repositório
git clone <url-do-repositorio>
cd DOTNET

# Restaurar dependências
dotnet restore

# Executar a API
dotnet run --project src/Motivation.Api

# A API estará disponível em:
# http://localhost:5000
# https://localhost:5001
# Swagger UI: http://localhost:5000/swagger
```

### Rodando os Testes

```bash
dotnet test
```

---

## Configuração

`src/Motivation.Api/appsettings.json`:

```json
{
  "Jwt": {
    "Key": "super_secret_key_12345_32bytes_min",
    "Issuer": "motivation",
    "Audience": "motivation",
    "ExpiresMinutes": "120"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": { "path": "logs/motivation-.log", "rollingInterval": "Day" }
      }
    ]
  }
}
```

---

## Endpoints da API

### Autenticação

| Método | Rota              | Auth | Descrição                    |
|--------|-------------------|------|------------------------------|
| POST   | `/users/register` | Não  | Registrar novo usuário       |
| POST   | `/users/login`    | Não  | Login e obter token JWT      |
| GET    | `/users/profile`  | Sim  | Perfil do usuário autenticado |

### Goals (Metas)

| Método | Rota                    | Auth | Descrição                         |
|--------|-------------------------|------|-----------------------------------|
| POST   | `/goals`                | Sim  | Criar nova meta                   |
| GET    | `/goals`                | Sim  | Listar metas do usuário           |
| PUT    | `/goals/{id}`           | Sim  | Atualizar meta                    |
| DELETE | `/goals/{id}`           | Sim  | Deletar meta                      |
| GET    | `/goals/{id}/progress`  | Sim  | Calcular progresso da meta        |

### Steps (Passos)

| Método | Rota                                      | Auth | Descrição                    |
|--------|-------------------------------------------|------|------------------------------|
| POST   | `/goals/{goalId}/steps`                   | Sim  | Criar passo para uma meta    |
| GET    | `/goals/{goalId}/steps`                   | Sim  | Listar passos de uma meta    |
| PUT    | `/goals/{goalId}/steps/{stepId}/complete` | Sim  | Marcar passo como concluído  |

### Motivations (Frases Motivacionais)

| Método | Rota                                           | Auth | Descrição                         |
|--------|------------------------------------------------|------|-----------------------------------|
| POST   | `/goals/{goalId}/motivations`                  | Sim  | Adicionar frase motivacional      |
| DELETE | `/goals/{goalId}/motivations/{motivationId}`   | Sim  | Remover frase motivacional        |

### Mensagem Diária

| Método | Rota             | Auth | Descrição                                  |
|--------|------------------|------|--------------------------------------------|
| GET    | `/daily-message` | Sim  | Obter mensagem motivacional do dia         |

### Health Checks

| Método | Rota            | Auth | Descrição                              |
|--------|-----------------|------|----------------------------------------|
| GET    | `/health`       | Não  | Status completo (DB + Cache)           |
| GET    | `/health/live`  | Não  | Liveness probe                         |
| GET    | `/health/ready` | Não  | Readiness probe (DB + Cache)           |

---

## Exemplos de Uso

### 1. Registrar Usuário

```bash
curl -X POST http://localhost:5000/users/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "joao@exemplo.com",
    "password": "Senha@123"
  }'
```

**Resposta 201 Created:**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "joao@exemplo.com"
}
```

---

### 2. Fazer Login

```bash
curl -X POST http://localhost:5000/users/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "joao@exemplo.com",
    "password": "Senha@123"
  }'
```

**Resposta 200 OK:**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "joao@exemplo.com",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIzZmE4NWY2NC01NzE3LTQ1NjItYjNmYy0yYzk2M2Y2NmFmYTYiLCJlbWFpbCI6ImpvYW9AZXhlbXBsby5jb20iLCJleHAiOjE3MTMxNjgwMDB9.abc123"
}
```

> **Guarde o token!** Use-o no header `Authorization: Bearer {token}` em todas as demais requisições.

---

### 3. Criar uma Meta (Goal)

```bash
curl -X POST http://localhost:5000/goals \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Aprender .NET 8",
    "description": "Dominar C# e ASP.NET Core com DDD"
  }'
```

**Resposta 201 Created:**
```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "title": "Aprender .NET 8",
  "description": "Dominar C# e ASP.NET Core com DDD",
  "status": "Pending",
  "createdAt": "2024-04-10T14:30:00Z"
}
```

---

### 4. Listar Metas

```bash
curl http://localhost:5000/goals \
  -H "Authorization: Bearer {token}"
```

**Resposta 200 OK:**
```json
[
  {
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "title": "Aprender .NET 8",
    "description": "Dominar C# e ASP.NET Core com DDD",
    "status": "Pending",
    "createdAt": "2024-04-10T14:30:00Z"
  }
]
```

---

### 5. Atualizar uma Meta

```bash
curl -X PUT http://localhost:5000/goals/a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Aprender .NET 8 e Azure",
    "status": "InProgress"
  }'
```

**Status disponíveis:** `Pending`, `InProgress`, `Completed`, `Cancelled`

**Resposta 200 OK:**
```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "title": "Aprender .NET 8 e Azure",
  "description": "Dominar C# e ASP.NET Core com DDD",
  "status": "InProgress",
  "createdAt": "2024-04-10T14:30:00Z"
}
```

---

### 6. Adicionar Passos (Steps) à Meta

```bash
curl -X POST http://localhost:5000/goals/a1b2c3d4-e5f6-7890-abcd-ef1234567890/steps \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Completar curso de C# básico"
  }'
```

**Resposta 201 Created:**
```json
{
  "id": "b2c3d4e5-f6a7-8901-bcde-f01234567891",
  "goalId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "title": "Completar curso de C# básico",
  "isCompleted": false,
  "completedAt": null
}
```

---

### 7. Marcar Passo como Concluído

```bash
curl -X PUT http://localhost:5000/goals/a1b2c3d4-e5f6-7890-abcd-ef1234567890/steps/b2c3d4e5-f6a7-8901-bcde-f01234567891/complete \
  -H "Authorization: Bearer {token}"
```

**Resposta 200 OK:**
```json
{
  "id": "b2c3d4e5-f6a7-8901-bcde-f01234567891",
  "goalId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "title": "Completar curso de C# básico",
  "isCompleted": true,
  "completedAt": "2024-04-10T15:00:00Z"
}
```

---

### 8. Ver Progresso da Meta

```bash
curl http://localhost:5000/goals/a1b2c3d4-e5f6-7890-abcd-ef1234567890/progress \
  -H "Authorization: Bearer {token}"
```

**Resposta 200 OK:**
```json
{
  "goalId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "totalSteps": 3,
  "completedSteps": 1,
  "progressPercentage": 33.33
}
```

---

### 9. Adicionar Frase Motivacional

```bash
curl -X POST http://localhost:5000/goals/a1b2c3d4-e5f6-7890-abcd-ef1234567890/motivations \
  -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{
    "text": "Consistência é a chave para a maestria!"
  }'
```

**Resposta 201 Created:**
```json
{
  "id": "c3d4e5f6-a7b8-9012-cdef-012345678902",
  "goalId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "text": "Consistência é a chave para a maestria!"
}
```

---

### 10. Obter Mensagem Motivacional do Dia

```bash
curl http://localhost:5000/daily-message \
  -H "Authorization: Bearer {token}"
```

**Resposta 200 OK:**
```json
{
  "message": "Consistência é a chave para a maestria!",
  "date": "2024-04-10"
}
```

> A mensagem rotaciona diariamente entre as frases cadastradas nas metas do usuário. Se não houver frases, retorna: *"Keep going! Every step forward is progress."*

---

### 11. Deletar Frase Motivacional

```bash
curl -X DELETE http://localhost:5000/goals/a1b2c3d4-e5f6-7890-abcd-ef1234567890/motivations/c3d4e5f6-a7b8-9012-cdef-012345678902 \
  -H "Authorization: Bearer {token}"
```

**Resposta:** `204 No Content`

---

### 12. Deletar Meta

```bash
curl -X DELETE http://localhost:5000/goals/a1b2c3d4-e5f6-7890-abcd-ef1234567890 \
  -H "Authorization: Bearer {token}"
```

**Resposta:** `204 No Content`

---

### 13. Verificar Saúde da API

```bash
# Status completo com detalhes
curl http://localhost:5000/health

# Liveness probe
curl http://localhost:5000/health/live

# Readiness probe
curl http://localhost:5000/health/ready
```

**Resposta (health):**
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0123456",
  "entries": {
    "database": {
      "status": "Healthy",
      "duration": "00:00:00.0050000"
    },
    "memory-cache": {
      "status": "Healthy",
      "duration": "00:00:00.0001000"
    }
  }
}
```

---

## Códigos de Erro

| Código | Descrição                                              |
|--------|--------------------------------------------------------|
| 400    | Bad Request – dados inválidos ou ausentes              |
| 401    | Unauthorized – token ausente, inválido ou expirado     |
| 403    | Forbidden – recurso pertence a outro usuário           |
| 404    | Not Found – recurso não encontrado                     |
| 409    | Conflict – email já cadastrado ou passo já concluído   |
| 500    | Internal Server Error – erro inesperado no servidor    |

**Formato padrão de erro:**
```json
{
  "error": "Descrição do problema"
}
```

---

## Fluxo Completo de Uso

```
1. POST /users/register   →  Cria conta
2. POST /users/login       →  Obtém JWT token
3. POST /goals             →  Cria uma meta
4. POST /goals/{id}/steps  →  Adiciona passos à meta (repita para vários passos)
5. PUT  /goals/{id}/steps/{stepId}/complete  →  Conclui um passo
6. GET  /goals/{id}/progress  →  Verifica progresso (%)
7. POST /goals/{id}/motivations  →  Adiciona frases motivacionais
8. GET  /daily-message         →  Recebe mensagem motivacional do dia
9. PUT  /goals/{id}            →  Atualiza status da meta quando concluir
```

---

## Arquitetura

```
┌─────────────────────────────────────────────────────┐
│                   Motivation.Api                     │
│   Controllers → Middleware → Services → DTOs        │
└────────────────────────┬────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────┐
│               Motivation.Application                 │
│   Services → Interfaces → DTOs → Exceptions         │
└────────────────────────┬────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────┐
│                 Motivation.Domain                    │
│        Entities → Interfaces (Repositories)         │
└────────────────────────┬────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────┐
│             Motivation.Infrastructure                │
│   EF Core → Repositories → JwtService → Cache      │
└─────────────────────────────────────────────────────┘
```

### Princípios SOLID aplicados

- **S** – Single Responsibility: cada serviço tem uma única responsabilidade (`AuthService`, `GoalService`, `MotivationService`, etc.)
- **O** – Open/Closed: extensível via interfaces, fechado para modificação direta
- **L** – Liskov Substitution: implementações substituem interfaces sem quebrar o contrato
- **I** – Interface Segregation: interfaces específicas por domínio (`IGoalService`, `IStepService`, etc.)
- **D** – Dependency Inversion: dependência via abstrações (interfaces injetadas via DI)

---

## Decisões Técnicas

| Decisão | Motivo |
|---------|--------|
| EF Core InMemory | Simplicidade para desenvolvimento sem banco de dados externo |
| IMemoryCache para Goals | Reduz leituras repetidas ao banco em consultas frequentes |
| JWT com validade de 120 min | Equilíbrio entre segurança e usabilidade |
| Serilog com arquivo rolling diário | Rastreabilidade de logs sem crescimento ilimitado |
| GlobalExceptionMiddleware | Centralização do tratamento de erros com responses padronizados |
| GoalStatus como enum | Garante valores válidos no domínio sem magic strings |
| DailyMessage com rotação por dia do ano | Variedade sem complexidade de agendamento |
