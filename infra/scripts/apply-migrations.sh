#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="${REPO_ROOT:-/opt/cidademdia}"
ENV_FILE="${ENV_FILE:-$REPO_ROOT/.env}"
INFRA_DIR="$REPO_ROOT/infra"
EF_VERSION="${EF_VERSION:-10.0.11}"
MIGRATOR_NAME="cidademdia-migrator-$$"

cleanup() {
  docker rm -f "$MIGRATOR_NAME" >/dev/null 2>&1 || true
}
trap cleanup EXIT

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker não encontrado."
  exit 1
fi

if [ ! -f "$ENV_FILE" ]; then
  echo "Arquivo de ambiente não encontrado: $ENV_FILE"
  exit 1
fi

if ! grep -q '^DATABASE_CONNECTION=' "$ENV_FILE"; then
  echo "DATABASE_CONNECTION ausente em $ENV_FILE"
  exit 1
fi

cd "$INFRA_DIR"

echo "==> Garantindo PostgreSQL ativo"
docker compose --env-file "$ENV_FILE" up -d db

DB_CONTAINER="$(docker compose --env-file "$ENV_FILE" ps -q db)"
if [ -z "$DB_CONTAINER" ]; then
  echo "Container do PostgreSQL não encontrado."
  exit 1
fi

for _ in $(seq 1 30); do
  STATUS="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$DB_CONTAINER")"
  if [ "$STATUS" = "healthy" ] || [ "$STATUS" = "running" ]; then
    break
  fi
  sleep 2
done

STATUS="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$DB_CONTAINER")"
if [ "$STATUS" != "healthy" ] && [ "$STATUS" != "running" ]; then
  echo "PostgreSQL não ficou disponível. Status: $STATUS"
  exit 1
fi

NETWORK="$(docker inspect -f '{{range $name, $_ := .NetworkSettings.Networks}}{{println $name}}{{end}}' "$DB_CONTAINER" | head -n1)"
if [ -z "$NETWORK" ]; then
  echo "Não foi possível determinar a rede Docker do PostgreSQL."
  exit 1
fi

echo "==> Rede privada do PostgreSQL: $NETWORK"
echo "==> Preparando migrator com acesso externo para restore/NuGet"

# A rede backend é internal=true e, por design, não possui saída para a Internet.
# O migrator nasce na bridge padrão para restaurar ferramentas/pacotes. O repositório
# é montado somente-leitura e copiado para /work, evitando bin/obj root-owned no host.
docker run -d \
  --name "$MIGRATOR_NAME" \
  --env-file "$ENV_FILE" \
  -v "$REPO_ROOT:/src:ro" \
  -w /work \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  bash -lc 'mkdir -p /work && cp -a /src/. /work/ && exec sleep infinity' >/dev/null

echo "==> Restaurando ferramentas e dependências"
docker exec "$MIGRATOR_NAME" bash -lc "
  set -euo pipefail
  cd /work
  dotnet tool install --tool-path /tmp/dotnet-tools dotnet-ef --version $EF_VERSION >/dev/null
  dotnet restore CidadeEmDia.sln
  dotnet build CidadeEmDia.sln -c Release --no-restore
"

echo "==> Conectando migrator à rede privada"
docker network connect "$NETWORK" "$MIGRATOR_NAME"

echo "==> Aplicando migrations EF Core"
docker exec "$MIGRATOR_NAME" bash -lc "
  set -euo pipefail
  cd /work
  /tmp/dotnet-tools/dotnet-ef database update \\
    --project apps/api/src/CidadeEmDia.Infrastructure/CidadeEmDia.Infrastructure.csproj \\
    --startup-project apps/api/src/CidadeEmDia.Api/CidadeEmDia.Api.csproj \\
    --configuration Release \\
    --no-build
"

echo "==> Migrations aplicadas"
docker compose --env-file "$ENV_FILE" exec -T db sh -lc '
  psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atc "select \"MigrationId\" from \"__EFMigrationsHistory\" order by \"MigrationId\";"
'
