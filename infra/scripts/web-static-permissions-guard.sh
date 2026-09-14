#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DOCKERFILE="$ROOT/apps/web/Dockerfile"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

test -f "$DOCKERFILE" || fail "Dockerfile web ausente"

grep -Fq 'find /usr/share/nginx/html -type d -exec chmod 755 {} +' "$DOCKERFILE" \
  || fail "Dockerfile não normaliza permissões dos diretórios públicos"

grep -Fq 'find /usr/share/nginx/html -type f -exec chmod 644 {} +' "$DOCKERFILE" \
  || fail "Dockerfile não normaliza permissões dos arquivos públicos"

echo "web_static_permissions=OK"
echo "WEB STATIC PERMISSIONS GUARD: OK"
