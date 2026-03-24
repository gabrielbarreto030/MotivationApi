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
Dia 13: Cache para consulta de Goals

Fase 4 – Steps (Dias 14–17)

Dia 14: Criar Step
Dia 15: Listar Steps
Dia 16: Marcar Step como concluído
Dia 17: Calcular progresso do Goal

Fase 5 – Motivation Engine (Dias 18–21)

Dia 18: Adicionar Motivation
Dia 19: Remover Motivation
Dia 20: Serviço que gera mensagem diária
Dia 21: Endpoint para obter motivação diária

Fase 6 – Qualidade & Observabilidade (Dias 22–26)

Dia 22: Logging estruturado com Serilog
Dia 23: Middleware global de erro
Dia 24: Unit Tests Domain
Dia 25: Unit Tests Application
Dia 26: Testes de integração API

Fase 7 – Polimento (Dias 27–30)

Dia 27: Documentação Swagger detalhada
Dia 28: HealthChecks avançados
Dia 29: Melhorias SOLID & Refactors leves
Dia 30: README final + exemplos de uso