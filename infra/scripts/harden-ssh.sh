#!/usr/bin/env bash
set -euo pipefail

if [ "$(id -u)" -ne 0 ]; then
  echo "Execute como root: sudo bash infra/scripts/harden-ssh.sh"
  exit 1
fi

AUTHORIZED_KEYS="/home/deploy/.ssh/authorized_keys"
DROPIN_DIR="/etc/ssh/sshd_config.d"
DROPIN_FILE="$DROPIN_DIR/99-cidademdia-hardening.conf"
BACKUP_FILE="/etc/ssh/sshd_config.cidademdia.bak"

if ! id deploy >/dev/null 2>&1; then
  echo "Usuário deploy não existe. Abortando."
  exit 1
fi

if [ ! -s "$AUTHORIZED_KEYS" ]; then
  echo "Nenhuma chave SSH encontrada para deploy em $AUTHORIZED_KEYS. Abortando."
  exit 1
fi

install -d -m 700 -o deploy -g deploy /home/deploy/.ssh
chown deploy:deploy "$AUTHORIZED_KEYS"
chmod 600 "$AUTHORIZED_KEYS"

if [ ! -f "$BACKUP_FILE" ]; then
  cp -a /etc/ssh/sshd_config "$BACKUP_FILE"
fi

install -d -m 755 "$DROPIN_DIR"
cat > "$DROPIN_FILE" <<'EOF'
PubkeyAuthentication yes
PasswordAuthentication no
KbdInteractiveAuthentication no
PermitRootLogin no
EOF

chmod 600 "$DROPIN_FILE"

if ! sshd -t; then
  echo "Configuração SSH inválida. Removendo drop-in novo."
  rm -f "$DROPIN_FILE"
  exit 1
fi

systemctl reload sshd

echo "==> Configuração efetiva relevante"
sshd -T | grep -E '^(pubkeyauthentication|passwordauthentication|kbdinteractiveauthentication|permitrootlogin) '

echo
echo "Hardening SSH aplicado."
echo "Mantenha esta sessão aberta e teste, em outro terminal, o login do usuário deploy por chave antes de encerrá-la."
