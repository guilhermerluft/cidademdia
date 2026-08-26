# Ambiente local / homologação

## Pré-requisitos

- Docker Engine
- Docker Compose v2

## Subir

```bash
cp .env.example .env
docker compose -f infra/docker-compose.yml up --build -d
```

## Validar

```bash
curl -fsS http://localhost:8080/health/live
curl -fsS http://localhost:8080/health/ready
curl -fsS http://localhost:8080/api/v1/status
```

## Derrubar

```bash
docker compose -f infra/docker-compose.yml down
```
