# Motivation API

API para sistema de motivação pessoal.

Tecnologias

.NET 8

DDD

JWT

EF Core

Serilog

MemoryCache

xUnit

Swagger

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
Dia 30: README final + exemplos de uso