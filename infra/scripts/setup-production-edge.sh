#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
TEMPLATE="$ROOT/infra/nginx/host-production.conf.template"
TARGET="/etc/nginx/conf.d/cidademdia-production.conf"
EXPECTED_HEAD="${1:-}"
EMAIL="${2:-}"
DOMAIN="cidademdia.com.br"
WWW="www.cidademdia.com.br"

fail() { echo "ERRO: $*" >&2; exit 1; }

for cmd in git curl nginx certbot dig ip; do
  command -v "$cmd" >/dev/null 2>&1 || fail "comando ausente: $cmd"
done

test "$(id -u)" -eq 0 || fail "execute como root"
test -n "$EXPECTED_HEAD" || fail "informe o HEAD esperado"
test "$(git -C "$ROOT" branch --show-current)" = "main" || fail "repo principal não está na main"
test "$(git -C "$ROOT" rev-parse HEAD)" = "$EXPECTED_HEAD" || fail "main fora do HEAD esperado"
test -z "$(git -C "$ROOT" status --porcelain)" || fail "main local está suja"
test -f "$TEMPLATE" || fail "template de produção ausente"

curl -fsS -H "Host: $DOMAIN" http://127.0.0.1:8081/health/live >/dev/null || fail "stack de produção local não está saudável"
curl -fsS https://homolog.cidademdia.com.br/health/live >/dev/null || fail "homolog indisponível antes do edge"

ORIGIN_IPV4="$(ip -4 -o addr show dev eth0 scope global | awk '{split($4,a,"/"); print a[1]; exit}')"
test -n "$ORIGIN_IPV4" || fail "não foi possível determinar o IPv4 da eth0"

mapfile -t AUTH_NS < <(dig +short NS "$DOMAIN" | sed '/^$/d')
[ "${#AUTH_NS[@]}" -gt 0 ] || fail "não foi possível localizar nameservers autoritativos de $DOMAIN"

check_dns_target() {
  local host="$1"
  local resolver="$2"
  local label="$3"
  local resolved

  resolved="$(dig +short A "$host" @"$resolver" | sed '/^$/d' | sort -u)"
  printf '%s via %s:\n%s\n' "$host" "$label" "$resolved"
  grep -Fxq "$ORIGIN_IPV4" <<<"$resolved" || fail "$host não aponta para $ORIGIN_IPV4 via $label"
}

wait_for_edge_health() {
  local attempts="${1:-15}"
  local attempt

  for attempt in $(seq 1 "$attempts"); do
    if curl -fsS -H "Host: $DOMAIN" http://127.0.0.1/health/live >/dev/null 2>&1; then
      echo "production_http_edge_attempt=$attempt"
      return 0
    fi

    sleep 1
  done

  return 1
}

for host in "$DOMAIN" "$WWW"; do
  for ns in "${AUTH_NS[@]}"; do
    check_dns_target "$host" "$ns" "authoritative:$ns"
  done

  check_dns_target "$host" "1.1.1.1" "cloudflare"
  check_dns_target "$host" "8.8.8.8" "google"
  echo "$host=$ORIGIN_IPV4"
done

echo "production_dns_public=OK"

install -m 0644 "$TEMPLATE" "$TARGET"
nginx -t
systemctl reload nginx

wait_for_edge_health 15 || fail "vhost HTTP de produção não alcança o stack após reload"
echo "production_http_edge=OK"

CERTBOT_ARGS=(--nginx --non-interactive --agree-tos --redirect -d "$DOMAIN" -d "$WWW")
if [ -n "$EMAIL" ]; then
  CERTBOT_ARGS+=(--email "$EMAIL")
fi
certbot "${CERTBOT_ARGS[@]}"

nginx -t
systemctl reload nginx
systemctl enable --now certbot-renew.timer 2>/dev/null || true

CIDADEMDIA_PROD_RESOLVE_IP="$ORIGIN_IPV4" bash "$ROOT/infra/scripts/production-deploy-smoke.sh"
curl -fsS https://homolog.cidademdia.com.br/health/live >/dev/null || fail "homolog indisponível após publicar produção"
echo "homolog_after_edge=OK"

echo "PRODUCTION EDGE SETUP: OK"
echo "URL=https://$DOMAIN"
