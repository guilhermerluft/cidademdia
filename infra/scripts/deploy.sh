#!/usr/bin/env sh
set -eu

REPO_ROOT="${REPO_ROOT:-/opt/cidademdia}"
ENV_FILE="${ENV_FILE:-$REPO_ROOT/.env}"
COMPOSE_FILE="${COMPOSE_FILE:-$REPO_ROOT/infra/docker-compose.yml}"

if [ ! -f "$ENV_FILE" ]; then
  echo "Arquivo de ambiente não encontrado: $ENV_FILE"
  exit 1
fi

cd "$REPO_ROOT"

echo "==> Build da stack de homologação"
docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" build

echo "==> Subindo stack de homologação"
docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" up -d

echo "==> Smoke test"
./infra/scripts/smoke-test.sh
