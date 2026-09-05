#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
PROD_ENV="${CIDADEMDIA_PROD_ENV:-$ROOT/.env.production}"
PROJECT="${CIDADEMDIA_PROD_PROJECT:-cidademdia-prod}"
EXPECTED_HEAD="${1:-}"
EF_VERSION="${EF_VERSION:-10.0.11}"
MIGRATOR_NAME="cidademdia-prod-migrator-$$"

fail() { echo "ERRO: $*" >&2; exit 1; }
cleanup() { docker rm -f "$MIGRATOR_NAME" >/dev/null 2>&1 || true; }
trap cleanup EXIT

for cmd in git docker curl; do
  command -v "$cmd" >/dev/null 2>&1 || fail "comando ausente: $cmd"
done

test -n "$EXPECTED_HEAD" || fail "informe o HEAD esperado"
bash "$ROOT/infra/scripts/production-deploy-preflight.sh" "$EXPECTED_HEAD"

COMPOSE=(docker compose -p "$PROJECT" --env-file "$PROD_ENV" -f "$ROOT/infra/docker-compose.yml")

curl -fsS https://homolog.cidademdia.com.br/health/live >/dev/null || fail "homolog indisponível antes do deploy"
echo "homolog_before=OK"

"${COMPOSE[@]}" up -d db
DB_CONTAINER="$("${COMPOSE[@]}" ps -q db)"
test -n "$DB_CONTAINER" || fail "container PostgreSQL de produção não encontrado"

for _ in $(seq 1 40); do
  status="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$DB_CONTAINER")"
  if [ "$status" = "healthy" ]; then break; fi
  sleep 2
done
status="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$DB_CONTAINER")"
test "$status" = "healthy" || fail "PostgreSQL de produção não ficou healthy: $status"
echo "production_db=HEALTHY"

NETWORK="$(docker inspect -f '{{range $name, $_ := .NetworkSettings.Networks}}{{println $name}}{{end}}' "$DB_CONTAINER" | head -n1)"
test -n "$NETWORK" || fail "rede do PostgreSQL de produção não encontrada"

docker run -d \
  --name "$MIGRATOR_NAME" \
  --env-file "$PROD_ENV" \
  -v "$ROOT:/src:ro" \
  -w /work \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  bash -lc 'mkdir -p /work && cp -a /src/. /work/ && exec sleep infinity' >/dev/null

docker exec "$MIGRATOR_NAME" bash -lc "
  set -euo pipefail
  cd /work
  dotnet tool install --tool-path /tmp/dotnet-tools dotnet-ef --version $EF_VERSION >/dev/null
  dotnet restore CidadeEmDia.sln
  dotnet build CidadeEmDia.sln -c Release --no-restore
"

docker network connect "$NETWORK" "$MIGRATOR_NAME"
docker exec "$MIGRATOR_NAME" bash -lc "
  set -euo pipefail
  cd /work
  /tmp/dotnet-tools/dotnet-ef database update \
    --project apps/api/src/CidadeEmDia.Infrastructure/CidadeEmDia.Infrastructure.csproj \
    --startup-project apps/api/src/CidadeEmDia.Api/CidadeEmDia.Api.csproj \
    --configuration Release \
    --no-build
"
echo "production_migrations=OK"

"${COMPOSE[@]}" up -d --build api web nginx

for _ in $(seq 1 60); do
  if curl -fsS -H 'Host: cidademdia.com.br' http://127.0.0.1:8081/health/live >/dev/null 2>&1; then
    break
  fi
  sleep 2
done
curl -fsS -H 'Host: cidademdia.com.br' http://127.0.0.1:8081/health/live >/dev/null || fail "produção local em 8081 não respondeu"
curl -fsS -H 'Host: cidademdia.com.br' http://127.0.0.1:8081/ >/dev/null || fail "home local de produção não respondeu"
echo "production_local_8081=OK"

curl -fsS https://homolog.cidademdia.com.br/health/live >/dev/null || fail "homolog indisponível após subir produção"
echo "homolog_after=OK"

"${COMPOSE[@]}" ps

echo "PRODUCTION STACK DEPLOY: OK"
echo "PROJECT=$PROJECT"
echo "HEAD=$EXPECTED_HEAD"
echo "LOCAL_URL=http://127.0.0.1:8081"
