#!/usr/bin/env bash
set -Eeuo pipefail

DOMAIN="${CIDADEMDIA_PROD_DOMAIN:-cidademdia.com.br}"
BASE="${CIDADEMDIA_PROD_BASE_URL:-https://$DOMAIN}"
RESOLVE_IP="${CIDADEMDIA_PROD_RESOLVE_IP:-}"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

CURL_ARGS=(-sS -o /dev/null -w '%{http_code}')
if [ -n "$RESOLVE_IP" ]; then
  CURL_ARGS+=(--resolve "$DOMAIN:443:$RESOLVE_IP")
  echo "production_smoke_origin=$RESOLVE_IP"
fi

for path in / /planos /ocorrencias /representantes /health/live; do
  code="$(curl "${CURL_ARGS[@]}" "$BASE$path")"
  test "$code" = "200" || fail "$path != 200 ($code)"
  echo "$path=$code"
done

echo "PRODUCTION DEPLOY SMOKE: OK"
