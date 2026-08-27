#!/usr/bin/env bash
set -euo pipefail

if [ "$(id -u)" -ne 0 ]; then
  echo "Execute como root: sudo bash infra/scripts/harden-kvm-base.sh"
  exit 1
fi

echo "==> Reduzindo superfície de rede do host"

# O CidadeEmDia não usa NFS/RPC. Desabilitamos serviço e socket para evitar
# que o rpcbind volte a abrir a porta 111 após reboot.
systemctl disable --now rpcbind.socket rpcbind.service 2>/dev/null || true

# Cockpit não faz parte da topologia do projeto. A imagem base do AlmaLinux
# pode deixar a regra liberada no firewalld mesmo sem o serviço em uso.
if firewall-cmd --permanent --query-service=cockpit >/dev/null 2>&1; then
  firewall-cmd --permanent --remove-service=cockpit
fi

# Mantemos SSH/HTTP/HTTPS. dhcpv6-client pode permanecer como regra padrão do
# AlmaLinux para não interferir na configuração de rede do provedor.
firewall-cmd --permanent --add-service=ssh >/dev/null
firewall-cmd --permanent --add-service=http >/dev/null
firewall-cmd --permanent --add-service=https >/dev/null
firewall-cmd --reload >/dev/null

echo "==> Validação"
echo "rpcbind.service: $(systemctl is-active rpcbind.service 2>/dev/null || true)"
echo "rpcbind.socket:  $(systemctl is-active rpcbind.socket 2>/dev/null || true)"
echo
firewall-cmd --list-all
echo
ss -lntup

echo
cat <<'EOF'
Hardening base concluído.

Próximo passo: configurar chave SSH do usuário deploy e validar login antes de
qualquer restrição de autenticação por senha/root.
EOF
