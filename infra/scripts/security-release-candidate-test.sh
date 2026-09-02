#!/usr/bin/env bash
set -Eeuo pipefail

EXPECTED_HEAD="${1:-}"
ROOT="${ROOT:-/opt/cidademdia}"
ENV_FILE="${ENV_FILE:-$ROOT/.env}"
BASE="${BASE:-https://homolog.cidademdia.com.br}"

if [ -z "$EXPECTED_HEAD" ]; then
  echo "Uso: bash infra/scripts/security-release-candidate-test.sh <expected-head>" >&2
  exit 1
fi

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

die() {
  echo
  echo "ERRO: $*" >&2
  exit 1
}

for cmd in git docker curl grep awk stat; do
  command -v "$cmd" >/dev/null 2>&1 || die "comando ausente: $cmd"
done

[ -d "$ROOT/.git" ] || die "repo principal não encontrado em $ROOT"
[ -f "$ENV_FILE" ] || die ".env não encontrado em $ENV_FILE"
[ "$(git -C "$REPO" rev-parse HEAD)" = "$EXPECTED_HEAD" ] || die "worktree fora do HEAD esperado"
[ -z "$(git -C "$ROOT" status --porcelain)" ] || die "main local está suja"

orig_compose() {
  docker compose --env-file "$ENV_FILE" -f "$ROOT/infra/docker-compose.yml" "$@"
}

CURRENT_NGINX_ID="$(orig_compose ps -q nginx)"
CURRENT_API_ID="$(orig_compose ps -q api)"
CURRENT_DB_ID="$(orig_compose ps -q db)"

test -n "$CURRENT_NGINX_ID" || die "nginx atual não encontrado"
test -n "$CURRENT_API_ID" || die "api atual não encontrada"
test -n "$CURRENT_DB_ID" || die "db atual não encontrado"

PROJECT="$(docker inspect "$CURRENT_NGINX_ID" --format '{{ index .Config.Labels "com.docker.compose.project" }}')"
test -n "$PROJECT" || die "compose project não identificado"

compose() {
  docker compose -p "$PROJECT" --env-file "$ENV_FILE" -f "$REPO/infra/docker-compose.yml" "$@"
}

dbq() {
  orig_compose exec -T db sh -lc \
    'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At' \
    <<< "$1"
}

echo "============================================================"
echo "SECURITY — RELEASE CANDIDATE GATE"
echo "============================================================"
echo "head=$EXPECTED_HEAD"

echo
echo "=== 1. APLICAR NGINX DA FEATURE ==="
compose up -d --no-deps --force-recreate nginx
sleep 2

NGINX_ID="$(compose ps -q nginx)"
test -n "$NGINX_ID" || die "nginx não encontrado após recreate"
echo "nginx=running"

echo
echo "=== 2. HEALTH / TLS ==="
HEALTH="$(curl -fsS -o /dev/null -w '%{http_code}' "$BASE/health/ready")"
WEB="$(curl -fsS -o /dev/null -w '%{http_code}' "$BASE/")"
HTTP_RESULT="$(curl -sS -o /dev/null -w '%{http_code}|%{redirect_url}' "${BASE/https:/http:}/")"
HTTP_CODE="${HTTP_RESULT%%|*}"
HTTP_REDIRECT="${HTTP_RESULT#*|}"

echo "health=$HEALTH"
echo "web=$WEB"
echo "http_redirect=$HTTP_CODE"

test "$HEALTH" = "200" || die "health != 200"
test "$WEB" = "200" || die "web != 200"
case "$HTTP_CODE" in 301|302|307|308) ;; *) die "HTTP não redireciona para HTTPS" ;; esac
case "$HTTP_REDIRECT" in https://*) ;; *) die "redirect HTTP não aponta para HTTPS" ;; esac

echo
echo "=== 3. SECURITY HEADERS ==="
HEADERS="$TMP/headers.txt"
curl -fsSI "$BASE/" > "$HEADERS"

header_value() {
  local key="$1"
  awk -v key="$key" 'BEGIN{IGNORECASE=1} $0 ~ "^" key ":" {sub(/^[^:]+:[[:space:]]*/, ""); sub(/\r$/, ""); print; exit}' "$HEADERS"
}

NOSNIFF="$(header_value 'X-Content-Type-Options')"
FRAME="$(header_value 'X-Frame-Options')"
REFERRER="$(header_value 'Referrer-Policy')"
HSTS="$(header_value 'Strict-Transport-Security')"

echo "x_content_type_options=$NOSNIFF"
echo "x_frame_options=$FRAME"
echo "referrer_policy=$REFERRER"
echo "hsts=$HSTS"

test "$NOSNIFF" = "nosniff" || die "X-Content-Type-Options ausente/incorreto"
test "$FRAME" = "DENY" || die "X-Frame-Options ausente/incorreto"
test "$REFERRER" = "strict-origin-when-cross-origin" || die "Referrer-Policy ausente/incorreto"
case "$HSTS" in max-age=31536000*) ;; *) die "HSTS ausente/incorreto" ;; esac

echo
echo "=== 4. CORS ==="
ALLOWED_HEADERS="$TMP/cors-allowed.txt"
DENIED_HEADERS="$TMP/cors-denied.txt"

curl -fsS -D "$ALLOWED_HEADERS" -o /dev/null -H "Origin: $BASE" "$BASE/api/v1/status"
curl -fsS -D "$DENIED_HEADERS" -o /dev/null -H "Origin: https://evil.invalid" "$BASE/api/v1/status"

ALLOWED_ORIGIN="$(awk 'BEGIN{IGNORECASE=1} /^access-control-allow-origin:/ {sub(/^[^:]+:[[:space:]]*/, ""); sub(/\r$/, ""); print; exit}' "$ALLOWED_HEADERS")"
DENIED_ORIGIN="$(awk 'BEGIN{IGNORECASE=1} /^access-control-allow-origin:/ {sub(/^[^:]+:[[:space:]]*/, ""); sub(/\r$/, ""); print; exit}' "$DENIED_HEADERS")"

echo "cors_allowed=$ALLOWED_ORIGIN"
echo "cors_denied_header=${DENIED_ORIGIN:-NONE}"
test "$ALLOWED_ORIGIN" = "$BASE" || die "origem homolog não foi permitida por CORS"
test -z "$DENIED_ORIGIN" || die "origem maliciosa recebeu CORS"

echo
echo "=== 5. DOCKER / DB PRIVATE ==="
DB_PORT="$(docker port "$CURRENT_DB_ID" 5432/tcp 2>/dev/null || true)"
API_PORTS="$(docker port "$CURRENT_API_ID" 2>/dev/null || true)"
NGINX_PORT="$(docker port "$NGINX_ID" 80/tcp 2>/dev/null || true)"
BACKEND_INTERNAL="$(docker network inspect "${PROJECT}_backend" --format '{{.Internal}}')"

echo "db_published=${DB_PORT:-NO}"
echo "api_published=${API_PORTS:-NO}"
echo "nginx_published=$NGINX_PORT"
echo "backend_internal=$BACKEND_INTERNAL"

test -z "$DB_PORT" || die "PostgreSQL possui porta publicada"
test -z "$API_PORTS" || die "API possui porta publicada diretamente"
test "$BACKEND_INTERNAL" = "true" || die "network backend não é internal"
echo "$NGINX_PORT" | grep -Fxq '127.0.0.1:8080' || die "nginx não está restrito a 127.0.0.1:8080"

echo
echo "=== 6. ENV / SECRETS ==="
MODE="$(stat -c '%a' "$ENV_FILE")"
echo "env_mode=$MODE"
case "$MODE" in 600|640) ;; *) die ".env com permissão excessiva: $MODE" ;; esac

if git -C "$ROOT" ls-files --error-unmatch .env >/dev/null 2>&1; then
  die ".env está rastreado pelo Git"
fi

echo "env_tracked=NO"

API_LOG="$TMP/api.log"
orig_compose logs --no-color api > "$API_LOG" 2>&1 || true

check_secret() {
  local label="$1"
  local value="$2"

  if [ -z "$value" ] || [ "${#value}" -lt 8 ]; then
    echo "$label=EMPTY_OR_SHORT_SKIP"
    return 0
  fi

  if git -C "$ROOT" grep -Fq -- "$value"; then
    die "$label encontrado nos arquivos rastreados"
  fi

  if git -C "$ROOT" log --all -S"$value" --format='%H' -- . | grep -q .; then
    die "$label encontrado no histórico Git"
  fi

  if grep -Fq -- "$value" "$API_LOG"; then
    die "$label encontrado nos logs da API"
  fi

  echo "$label=NOT_EXPOSED"
}

JWT_VALUE="$(docker exec "$CURRENT_API_ID" printenv JWT_SIGNING_KEY || true)"
DB_CONN_VALUE="$(docker exec "$CURRENT_API_ID" printenv DATABASE_CONNECTION || true)"
R2_ACCESS_VALUE="$(docker exec "$CURRENT_API_ID" printenv R2_ACCESS_KEY_ID || true)"
R2_SECRET_VALUE="$(docker exec "$CURRENT_API_ID" printenv R2_SECRET_ACCESS_KEY || true)"
SMTP_PASSWORD_VALUE="$(docker exec "$CURRENT_API_ID" printenv SMTP_PASSWORD || true)"
MP_ACCESS_VALUE="$(docker exec "$CURRENT_API_ID" printenv MERCADOPAGO_ACCESS_TOKEN || true)"
MP_WEBHOOK_VALUE="$(docker exec "$CURRENT_API_ID" printenv MERCADOPAGO_WEBHOOK_SECRET || true)"
POSTGRES_PASSWORD_VALUE="$(docker exec "$CURRENT_DB_ID" printenv POSTGRES_PASSWORD || true)"

check_secret JWT_SIGNING_KEY "$JWT_VALUE"
check_secret DATABASE_CONNECTION "$DB_CONN_VALUE"
check_secret R2_ACCESS_KEY_ID "$R2_ACCESS_VALUE"
check_secret R2_SECRET_ACCESS_KEY "$R2_SECRET_VALUE"
check_secret SMTP_PASSWORD "$SMTP_PASSWORD_VALUE"
check_secret MERCADOPAGO_ACCESS_TOKEN "$MP_ACCESS_VALUE"
check_secret MERCADOPAGO_WEBHOOK_SECRET "$MP_WEBHOOK_VALUE"
check_secret POSTGRES_PASSWORD "$POSTGRES_PASSWORD_VALUE"

unset JWT_VALUE DB_CONN_VALUE R2_ACCESS_VALUE R2_SECRET_VALUE SMTP_PASSWORD_VALUE MP_ACCESS_VALUE MP_WEBHOOK_VALUE POSTGRES_PASSWORD_VALUE

echo
echo "=== 7. R2 UNSIGNED S3 API ==="
R2_ACCOUNT="$(docker exec "$CURRENT_API_ID" printenv R2_ACCOUNT_ID || true)"
R2_BUCKET="$(docker exec "$CURRENT_API_ID" printenv R2_BUCKET || true)"
R2_ACCESS_PRESENT="$(docker exec "$CURRENT_API_ID" sh -lc 'test -n "$R2_ACCESS_KEY_ID" && echo YES || echo NO')"
R2_SECRET_PRESENT="$(docker exec "$CURRENT_API_ID" sh -lc 'test -n "$R2_SECRET_ACCESS_KEY" && echo YES || echo NO')"

test -n "$R2_ACCOUNT" || die "R2_ACCOUNT_ID vazio"
test -n "$R2_BUCKET" || die "R2_BUCKET vazio"
test "$R2_ACCESS_PRESENT" = "YES" || die "R2 access key ausente"
test "$R2_SECRET_PRESENT" = "YES" || die "R2 secret ausente"

R2_UNSIGNED_CODE="$(curl -sS -o "$TMP/r2-unsigned-body.txt" -w '%{http_code}' "https://${R2_ACCOUNT}.r2.cloudflarestorage.com/${R2_BUCKET}/" || true)"
echo "r2_unsigned_s3_api=$R2_UNSIGNED_CODE"

case "$R2_UNSIGNED_CODE" in
  400|401|403)
    echo "r2_unsigned_s3_api_rejected=YES"
    ;;
  2??|3??)
    die "R2 S3 API aceitou/redirecionou request sem assinatura: HTTP $R2_UNSIGNED_CODE"
    ;;
  *)
    die "R2 S3 API retornou status não conclusivo sem assinatura: HTTP $R2_UNSIGNED_CODE"
    ;;
esac

unset R2_ACCOUNT R2_BUCKET

echo
echo "=== 8. WEBHOOK INVALID ==="
WEBHOOK_CODE="$(curl -sS -o /dev/null -w '%{http_code}' -X POST -H 'Content-Type: application/json' --data '{}' "$BASE/api/v1/webhooks/mercadopago?data.id=security-rc")"
echo "invalid_webhook=$WEBHOOK_CODE"
test "$WEBHOOK_CODE" = "401" || die "webhook sem assinatura deveria retornar 401"

echo
echo "=== 9. MERCADO PAGO STATE ==="
PROVIDER="$(dbq "
  SELECT
    (SELECT count(*) FROM billing_provider_subscriptions)::text
    || '|' ||
    (SELECT count(*) FROM payments)::text
    || '|' ||
    (SELECT count(*) FROM payment_events)::text;
")"
echo "provider=$PROVIDER"
test "$PROVIDER" = "0|0|0" || die "provider Mercado Pago mudou durante o gate"

echo
echo "=== 10. ESTADO FINAL ==="
FINAL_HEALTH="$(curl -fsS -o /dev/null -w '%{http_code}' "$BASE/health/ready")"
FINAL_WEB="$(curl -fsS -o /dev/null -w '%{http_code}' "$BASE/")"
ROOT_CLEAN="$(test -z "$(git -C "$ROOT" status --porcelain)" && echo YES || echo NO)"

echo "health=$FINAL_HEALTH"
echo "web=$FINAL_WEB"
echo "main_clean=$ROOT_CLEAN"
echo "feature_head=$(git -C "$REPO" rev-parse HEAD)"

test "$FINAL_HEALTH" = "200"
test "$FINAL_WEB" = "200"
test "$ROOT_CLEAN" = "YES"
test "$(git -C "$REPO" rev-parse HEAD)" = "$EXPECTED_HEAD"

echo
echo "============================================================"
echo "SECURITY — RELEASE CANDIDATE: OK"
echo "HEAD: $EXPECTED_HEAD"
echo "TLS/HTTPS: OK"
echo "HTTP REDIRECT: OK"
echo "SECURITY HEADERS: OK"
echo "CORS ALLOWED/DENIED: OK"
echo "DB PRIVATE: OK"
echo "API PRIVATE: OK"
echo "BACKEND NETWORK INTERNAL: OK"
echo "ENV PERMISSIONS: $MODE"
echo "SECRETS GIT/LOGS: NOT EXPOSED"
echo "R2 UNSIGNED S3 API: REJECTED ($R2_UNSIGNED_CODE)"
echo "INVALID WEBHOOK: 401"
echo "MERCADO PAGO PROVIDER: $PROVIDER"
echo "HEALTH: 200"
echo "WEB: 200"
echo "MAIN WORKTREE: CLEAN"
echo "============================================================"
