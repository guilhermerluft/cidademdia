#!/usr/bin/env bash
set -euo pipefail

if [ "$(id -u)" -ne 0 ]; then
  echo "Execute como root: sudo bash infra/scripts/prepare-kvm-almalinux.sh"
  exit 1
fi

if ! grep -qi '^ID="\?almalinux"\?$' /etc/os-release; then
  echo "Este script foi preparado para AlmaLinux. Abortando para evitar aplicar passos em outro SO."
  exit 1
fi

echo "==> Atualizando sistema e instalando pré-requisitos"
dnf -y update
dnf -y install dnf-plugins-core git curl ca-certificates firewalld

echo "==> Instalando Docker Engine e Compose plugin"
if ! dnf repolist --all | grep -q 'docker-ce-stable'; then
  dnf config-manager --add-repo https://download.docker.com/linux/centos/docker-ce.repo
fi

dnf -y install docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
systemctl enable --now docker
systemctl enable --now firewalld

echo "==> Criando usuário e diretórios operacionais"
if ! id deploy >/dev/null 2>&1; then
  useradd --create-home --shell /bin/bash deploy
fi
usermod -aG docker deploy
mkdir -p /opt/cidademdia /var/backups/cidademdia
chown -R deploy:deploy /opt/cidademdia /var/backups/cidademdia
chmod 0755 /opt/cidademdia
chmod 0750 /var/backups/cidademdia

echo "==> Aplicando firewall mínimo"
firewall-cmd --permanent --add-service=ssh
firewall-cmd --permanent --add-service=http
firewall-cmd --permanent --add-service=https
firewall-cmd --reload

echo "==> Validações"
docker --version
docker compose version
systemctl is-active docker
systemctl is-active firewalld
firewall-cmd --list-all
ss -lntup

echo
cat <<'EOF'
Preparação base concluída.

Próximos passos manuais:
1. Adicionar uma chave SSH válida ao usuário deploy antes de restringir login root/senha.
2. Clonar o repositório em /opt/cidademdia usando credencial/deploy key apropriada.
3. Criar o arquivo .env fora do Git.
4. Subir a stack com Docker Compose.
5. Confirmar que PostgreSQL não publica 5432 no host.
6. Validar /health/live e /health/ready.
EOF
