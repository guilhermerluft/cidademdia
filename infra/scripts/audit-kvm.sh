#!/usr/bin/env bash
set -u

section() {
  printf '\n===== %s =====\n' "$1"
}

run() {
  printf '\n$ %s\n' "$*"
  "$@" 2>&1 || true
}

if [ "$(id -u)" -ne 0 ]; then
  echo "Execute como root para uma auditoria completa: sudo bash infra/scripts/audit-kvm.sh"
fi

section "Identificação"
run hostnamectl
run uname -a
run cat /etc/os-release
run uptime

section "Recursos"
run free -h
run df -hT
run lsblk -f

section "Rede"
run ip -br addr
run ip route
run ss -lntup

section "Firewall e SELinux"
run systemctl is-active firewalld
run firewall-cmd --list-all
run getenforce

section "Serviços web e proxy"
run systemctl status httpd --no-pager
run systemctl status nginx --no-pager
run systemctl status docker --no-pager
run systemctl status podman --no-pager

section "cPanel e serviços relacionados"
run test -d /usr/local/cpanel
run ls -ld /usr/local/cpanel
run systemctl status cpanel --no-pager
run test -f /etc/userdatadomains
run cat /etc/userdatadomains

section "Bancos locais"
run systemctl status mariadb --no-pager
run systemctl status mysqld --no-pager
run systemctl status postgresql --no-pager
run ss -lntp | grep -E ':(3306|5432)\b'

section "E-mail"
run systemctl status exim --no-pager
run systemctl status postfix --no-pager
run systemctl status dovecot --no-pager
run ss -lntp | grep -E ':(25|465|587|110|143|993|995)\b'

section "Usuários e diretórios relevantes"
run awk -F: '$3 >= 1000 {print $1":"$3":"$6":"$7}' /etc/passwd
run ls -la /home
run ls -la /var/www
run ls -la /opt

section "Cron e timers"
run crontab -l
run systemctl list-timers --all --no-pager

section "Docker/Podman existente"
run docker version
run docker compose version
run docker ps -a
run docker volume ls
run podman ps -a

section "Resumo"
echo "Auditoria concluída. Este script é somente leitura e não altera serviços, pacotes, firewall ou arquivos."
