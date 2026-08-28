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

echo "==> Subindo banco, web e API"
docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" up -d db web api

echo "==> Recriando Nginx para atualizar upstreams Docker"
docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" up -d --force-recreate nginx

echo "==> Aguardando aplicação responder ao smoke test"
ATTEMPT=1
MAX_ATTEMPTS=30

while [ "$ATTEMPT" -le "$MAX_ATTEMPTS" ]; do
  if ./infra/scripts/smoke-test.sh >/dev/null 2>&1; then
    ./infra/scripts/smoke-test.sh
    exit 0
  fi

  echo "Smoke test ainda indisponível ($ATTEMPT/$MAX_ATTEMPTS)"
  ATTEMPT=$((ATTEMPT + 1))
  sleep 2
done

echo "ERRO: stack não ficou saudável após o deploy."
echo "==> Estado dos containers"
docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" ps || true

echo "==> Últimos logs da API"
docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" logs --tail=100 api || true

echo "==> Últimos logs do Nginx"
docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" logs --tail=100 nginx || true
exit 1
