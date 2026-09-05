#!/usr/bin/env bash
set -Eeuo pipefail

BASE="${CIDADEMDIA_PROD_BASE_URL:-https://cidademdia.com.br}"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

for path in / /planos /ocorrencias /representantes /health/; do
  code="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE$path")"
  test "$code" = "200" || fail "$path != 200 ($code)"
  echo "$path=$code"
done

echo "PRODUCTION DEPLOY SMOKE: OK"
