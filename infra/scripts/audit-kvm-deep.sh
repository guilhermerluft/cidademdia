#!/usr/bin/env bash
set -u

section() {
  printf '\n===== %s =====\n' "$1"
}

run() {
  printf '\n$ %s\n' "$*"
  bash -lc "$*" 2>&1 || true
}

if [ "$(id -u)" -ne 0 ]; then
  echo "Execute como root: sudo bash infra/scripts/audit-kvm-deep.sh"
fi

section "Contas cPanel"
run "ls -la /var/cpanel/users 2>/dev/null || true"
run "find /var/cpanel/users -maxdepth 1 -type f -printf '%f\n' 2>/dev/null | sort"
run "/usr/local/cpanel/bin/whmapi1 listaccts 2>/dev/null | sed -n '1,220p' || true"
run "cat /etc/trueuserdomains 2>/dev/null || true"
run "cat /etc/userdomains 2>/dev/null || true"

section "Domínios e VirtualHosts"
run "cat /etc/localdomains 2>/dev/null || true"
run "cat /etc/remotedomains 2>/dev/null || true"
run "apachectl -S 2>&1 || httpd -S 2>&1 || true"
run "find /var/cpanel/userdata -maxdepth 2 -type f -printf '%p\n' 2>/dev/null | sort | head -200"

section "Bancos MariaDB"
run "mysql -NBe 'SHOW DATABASES;' 2>/dev/null || true"
run "mysql -NBe \"SELECT User,Host FROM mysql.user ORDER BY User,Host;\" 2>/dev/null || true"
run "du -sh /var/lib/mysql 2>/dev/null || true"

section "E-mail e filas"
run "exim -bpc 2>/dev/null || true"
run "find /home -maxdepth 4 -type d \( -name mail -o -name etc \) -printf '%p\n' 2>/dev/null | sort"
run "find /etc/valiases -maxdepth 1 -type f -printf '%f\n' 2>/dev/null | sort || true"
run "find /etc/vfilters -maxdepth 1 -type f -printf '%f\n' 2>/dev/null | sort || true"

section "Dados de usuários fora do sistema"
run "find /home -mindepth 1 -maxdepth 2 -printf '%M %u:%g %s %p\n' 2>/dev/null | sort"
run "du -sh /home/* 2>/dev/null || true"
run "find /var/www -mindepth 1 -maxdepth 3 -type f -printf '%s %p\n' 2>/dev/null | sort -nr | head -100"

section "Firewall alternativo e regras atuais"
run "command -v csf >/dev/null && csf -l || true"
run "systemctl status lfd --no-pager 2>/dev/null || true"
run "nft list ruleset 2>/dev/null | sed -n '1,260p' || true"
run "iptables -S 2>/dev/null || true"
run "ip6tables -S 2>/dev/null || true"

section "Pacotes/serviços cPanel relevantes"
run "rpm -qa | grep -Ei 'cpanel|ea-apache|exim|dovecot|mariadb|powerdns|cloudlinux' | sort | head -250"
run "systemctl list-unit-files --type=service --no-pager | grep -Ei 'cpanel|httpd|exim|dovecot|mariadb|named|pdns|lfd' || true"

section "Resumo"
echo "Auditoria profunda concluída. Nenhuma alteração foi realizada."
