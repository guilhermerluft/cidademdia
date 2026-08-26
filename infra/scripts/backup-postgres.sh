#!/usr/bin/env sh
set -eu
mkdir -p backups
STAMP=$(date +%Y%m%d-%H%M%S)
docker compose -f infra/docker-compose.yml exec -T db pg_dump -U "${POSTGRES_USER:-cidademdia}" "${POSTGRES_DB:-cidademdia}" | gzip > "backups/cidademdia-$STAMP.sql.gz"
echo "Backup criado em backups/cidademdia-$STAMP.sql.gz"
