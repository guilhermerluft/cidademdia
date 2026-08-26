# CidadeEmDia

Nova aplicação CidadeEmDia construída do zero (greenfield).

## Stack

- React + TypeScript + Vite
- ASP.NET Core 10
- PostgreSQL + PostGIS
- EF Core + Npgsql + NetTopologySuite
- Docker Compose
- Nginx

## Estrutura

```text
apps/web                  Frontend React
apps/api/src              API + Application + Domain + Infrastructure
apps/api/tests            Testes unitários e de integração
infra                     Docker, Nginx e scripts operacionais
docs                      Arquitetura, decisões e runbooks
```

## Desenvolvimento local

1. Copie `.env.example` para `.env` e troque os valores sensíveis.
2. Execute `docker compose -f infra/docker-compose.yml up --build`.
3. Web: `http://localhost:8080`.
4. API readiness: `http://localhost:8080/health/ready`.

Não reutilizamos código, banco, componentes ou páginas do sistema legado. A estilização dos dashboards atuais serve apenas como referência visual para os novos dashboards autenticados.
