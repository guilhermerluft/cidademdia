#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"

grep -q 'CIDADEMDIA_HTTP_PORT' "$ROOT/infra/docker-compose.yml"
grep -q '127.0.0.1:8081' "$ROOT/infra/nginx/host-production.conf.template"
! grep -q '^map \$http_upgrade \$connection_upgrade' "$ROOT/infra/nginx/host-production.conf.template"
grep -q 'docker compose -p "\$PROJECT"' "$ROOT/infra/scripts/deploy-production-stack.sh"
grep -q 'production-deploy-preflight.sh' "$ROOT/infra/scripts/deploy-production-stack.sh"
grep -q 'homolog_before=OK' "$ROOT/infra/scripts/deploy-production-stack.sh"
grep -q 'homolog_after=OK' "$ROOT/infra/scripts/deploy-production-stack.sh"
grep -q 'production-deploy-smoke.sh' "$ROOT/infra/scripts/setup-production-edge.sh"
grep -q 'homolog_after_edge=OK' "$ROOT/infra/scripts/setup-production-edge.sh"
grep -q 'dig +short NS' "$ROOT/infra/scripts/setup-production-edge.sh"
grep -q 'authoritative:' "$ROOT/infra/scripts/setup-production-edge.sh"
grep -q '1.1.1.1' "$ROOT/infra/scripts/setup-production-edge.sh"
grep -q '8.8.8.8' "$ROOT/infra/scripts/setup-production-edge.sh"
grep -q 'production_dns_public=OK' "$ROOT/infra/scripts/setup-production-edge.sh"
! grep -q 'getent ahostsv4' "$ROOT/infra/scripts/setup-production-edge.sh"

echo "PRODUCTION DEPLOY GUARD: OK"
