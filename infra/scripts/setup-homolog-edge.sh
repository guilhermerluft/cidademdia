#!/usr/bin/env bash
set -euo pipefail

DOMAIN="${1:-homolog.cidademdia.com.br}"
EMAIL="${2:-}"
REPO_ROOT="/opt/cidademdia"
TEMPLATE="$REPO_ROOT/infra/nginx/host-homolog.conf.template"
TARGET="/etc/nginx/conf.d/cidademdia-homolog.conf"

if [ "$(id -u)" -ne 0 ]; then
  echo "Execute como root: bash infra/scripts/setup-homolog-edge.sh <dominio> <email>"
  exit 1
fi

if [ -z "$EMAIL" ]; then
  echo "Uso: bash infra/scripts/setup-homolog-edge.sh <dominio> <email-certbot>"
  exit 1
fi

if [ ! -f "$TEMPLATE" ]; then
  echo "Template não encontrado: $TEMPLATE"
  exit 1
fi

ORIGIN_IPV4="$(ip -4 -o addr show dev eth0 scope global | awk '{split($4,a,"/"); print a[1]; exit}')"
if [ -z "$ORIGIN_IPV4" ]; then
  echo "Não foi possível determinar o IPv4 público da interface eth0."
  exit 1
fi

if ! getent ahostsv4 "$DOMAIN" >/dev/null 2>&1; then
  echo "DNS de $DOMAIN ainda não resolve."
  echo "Crie no provedor DNS um registro A para $DOMAIN apontando para $ORIGIN_IPV4."
  exit 1
fi

if ! getent ahostsv4 "$DOMAIN" | awk '{print $1}' | sort -u | grep -Fxq "$ORIGIN_IPV4"; then
  echo "$DOMAIN não resolve diretamente para $ORIGIN_IPV4."
  echo "Confirme o registro A no provedor DNS antes de emitir o certificado."
  exit 1
fi

echo "==> Instalando Nginx e Certbot"
dnf -y install epel-release >/dev/null
dnf -y install nginx certbot python3-certbot-nginx >/dev/null

echo "==> Configurando edge HTTP para $DOMAIN"
sed "s/__DOMAIN__/$DOMAIN/g" "$TEMPLATE" > "$TARGET"
nginx -t
systemctl enable --now nginx

echo "==> Validando origem HTTP"
curl -fsS -H "Host: $DOMAIN" http://127.0.0.1/health/live >/dev/null

echo "==> Emitindo certificado Let's Encrypt"
certbot --nginx \
  --non-interactive \
  --agree-tos \
  --email "$EMAIL" \
  --domain "$DOMAIN" \
  --redirect

nginx -t
systemctl reload nginx
systemctl enable --now certbot-renew.timer 2>/dev/null || true

echo "==> Validação final"
curl -fsS "https://$DOMAIN/health/live"
echo

echo "Homologação publicada com TLS em https://$DOMAIN"
echo "Mantenha o registro A do domínio apontando diretamente para esta KVM enquanto este ambiente estiver ativo."
