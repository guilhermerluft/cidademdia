#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
PROD_ENV="${CIDADEMDIA_PROD_ENV:-$ROOT/.env.production}"
EXPECTED_HEAD="${1:-}"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

for cmd in git docker grep; do
  command -v "$cmd" >/dev/null 2>&1 || fail "comando ausente: $cmd"
done

test -n "$EXPECTED_HEAD" || fail "informe o HEAD esperado"
test "$(git -C "$ROOT" branch --show-current)" = "main" || fail "repo principal não está na main"
test "$(git -C "$ROOT" rev-parse HEAD)" = "$EXPECTED_HEAD" || fail "main fora do HEAD esperado"
test -z "$(git -C "$ROOT" status --porcelain)" || fail "main local está suja"
test -f "$PROD_ENV" || fail "arquivo de produção ausente: $PROD_ENV"

grep -q '^CIDADEMDIA_HTTP_PORT=8081$' "$PROD_ENV" || fail "produção deve usar CIDADEMDIA_HTTP_PORT=8081"
grep -q '^ASPNETCORE_ENVIRONMENT=Production$' "$PROD_ENV" || fail "ASPNETCORE_ENVIRONMENT de produção inválido"
grep -q '^CORS_ALLOWED_ORIGINS=https://cidademdia.com.br,https://www.cidademdia.com.br$' "$PROD_ENV" || fail "CORS de produção inválido"
grep -q '^PASSWORD_RESET_URL=https://cidademdia.com.br/reset-password$' "$PROD_ENV" || fail "PASSWORD_RESET_URL de produção inválido"
grep -q '^SUBACCOUNT_INVITE_URL=https://cidademdia.com.br/$' "$PROD_ENV" || fail "SUBACCOUNT_INVITE_URL de produção inválido"
grep -q '^MERCADOPAGO_BACK_URL=https://cidademdia.com.br/billing/return$' "$PROD_ENV" || fail "MERCADOPAGO_BACK_URL de produção inválido"

COMPOSE="docker compose -p cidademdia-prod --env-file $PROD_ENV -f $ROOT/infra/docker-compose.yml"
$COMPOSE config >/dev/null

if ss -lnt 2>/dev/null | grep -q '127.0.0.1:8081'; then
  echo "production_port_8081=IN_USE"
else
  echo "production_port_8081=FREE"
fi

echo "production_env=OK"
echo "production_compose=OK"
echo "PRODUCTION DEPLOY PREFLIGHT: OK"
