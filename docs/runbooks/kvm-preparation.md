# Preparação da KVM 1

Objetivo: transformar a KVM em ambiente de homologação reproduzível do CidadeEmDia sem remover serviços existentes antes de confirmar que não são necessários.

## Fase A — Auditoria segura

1. Acessar a KVM como root.
2. Clonar o repositório ou copiar o script de auditoria.
3. Executar:

```bash
sudo bash infra/scripts/audit-kvm.sh | tee /root/cidademdia-kvm-audit.txt
```

4. Revisar principalmente:
   - serviços escutando em 80/443;
   - cPanel/Apache;
   - MariaDB/MySQL/PostgreSQL;
   - serviços de e-mail;
   - diretórios em `/home`, `/var/www` e `/opt`;
   - crons/timers;
   - firewall/SELinux;
   - Docker/Podman existentes.

Não desinstalar cPanel, Apache, banco, e-mail ou alterar firewall antes dessa revisão.

## Fase B — Decisão de topologia

Se a auditoria confirmar que a KVM não hospeda nada necessário:
- preferir ambiente mínimo com Docker Engine + Compose;
- Nginx/edge do CidadeEmDia em 80/443;
- PostgreSQL/PostGIS somente na rede Docker privada;
- aplicação em `/opt/cidademdia`;
- SSH por chave;
- firewall expondo apenas SSH e HTTP/HTTPS conforme necessidade.

Se cPanel/Apache ou e-mail forem necessários, manter a instalação e desenhar proxy/portas sem conflito antes de subir a stack.

## Fase C — Instalação controlada

Somente após a decisão da Fase B:
- atualizar pacotes;
- instalar Docker Engine/Compose pelo repositório oficial adequado ao SO;
- criar usuário de deploy;
- criar `/opt/cidademdia`;
- configurar firewall;
- copiar `.env` fora do Git;
- subir `infra/docker-compose.yml`;
- validar `/health/live` e `/health/ready`;
- confirmar que 5432 não está publicada.

## Critério para concluir o card

- snapshot/backup disponível;
- serviços legados auditados e decisão cPanel/Apache documentada;
- Docker/Compose funcionais;
- staging sobe de forma reproduzível;
- proxy 80/443 definido;
- banco privado;
- HTTPS do subdomínio de homologação funcionando;
- health checks respondendo.
