#!/usr/bin/env sh
set -eu
BASE_URL=${1:-http://localhost:8080}
curl -fsS "$BASE_URL/health/live" >/dev/null
curl -fsS "$BASE_URL/health/ready" >/dev/null
curl -fsS "$BASE_URL/api/v1/status" >/dev/null
echo "Smoke test OK: $BASE_URL"
