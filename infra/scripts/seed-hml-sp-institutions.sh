#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="${REPO_ROOT:-/opt/cidademdia}"
ENV_FILE="${ENV_FILE:-$ROOT/.env}"
COMPOSE_FILE="${COMPOSE_FILE:-$ROOT/infra/docker-compose.yml}"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

[ -f "$ENV_FILE" ] || fail "Arquivo de ambiente não encontrado: $ENV_FILE"

case "$ENV_FILE" in
  *.production|*.prod)
    fail "Este seed é exclusivo de homologação e não pode usar ambiente de produção."
    ;;
esac

COMPOSE=(
  docker compose
  -p infra
  --env-file "$ENV_FILE"
  -f "$COMPOSE_FILE"
)

echo "==> Garantindo banco de homologação ativo"
"${COMPOSE[@]}" up -d db >/dev/null

echo "==> Cadastrando instituições de São Paulo (idempotente)"
"${COMPOSE[@]}" exec -T db sh -lc '
  set -e

  psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" <<'"'"'SQL'"'"'
BEGIN;

INSERT INTO institutions (
    id,
    name,
    slug,
    type,
    scope_level,
    cnpj,
    official_email,
    official_domain,
    description,
    logo_media_id,
    city_id,
    state_code,
    status,
    created_at,
    updated_at
)
VALUES
    (
        '''7c9ec70d-a59b-40f4-b23c-2f52a07ef001''',
        '''Prefeitura de São Paulo''',
        '''prefeitura-de-sao-paulo''',
        '''CITY_HALL''',
        '''MUNICIPAL''',
        NULL,
        NULL,
        '''prefeitura.sp.gov.br''',
        '''Poder Executivo municipal da cidade de São Paulo.''',
        NULL,
        NULL,
        '''SP''',
        '''ACTIVE''',
        now(),
        now()
    ),
    (
        '''7c9ec70d-a59b-40f4-b23c-2f52a07ef002''',
        '''Câmara Municipal de São Paulo''',
        '''camara-municipal-de-sao-paulo''',
        '''CITY_COUNCIL''',
        '''MUNICIPAL''',
        NULL,
        NULL,
        '''saopaulo.sp.leg.br''',
        '''Poder Legislativo municipal da cidade de São Paulo.''',
        NULL,
        NULL,
        '''SP''',
        '''ACTIVE''',
        now(),
        now()
    ),
    (
        '''7c9ec70d-a59b-40f4-b23c-2f52a07ef003''',
        '''Governo do Estado de São Paulo''',
        '''governo-do-estado-de-sao-paulo''',
        '''PUBLIC_AGENCY''',
        '''STATE''',
        NULL,
        NULL,
        '''sp.gov.br''',
        '''Poder Executivo do Estado de São Paulo.''',
        NULL,
        NULL,
        '''SP''',
        '''ACTIVE''',
        now(),
        now()
    ),
    (
        '''7c9ec70d-a59b-40f4-b23c-2f52a07ef004''',
        '''Assembleia Legislativa do Estado de São Paulo''',
        '''assembleia-legislativa-do-estado-de-sao-paulo''',
        '''ASSEMBLY''',
        '''STATE''',
        NULL,
        NULL,
        '''al.sp.gov.br''',
        '''Poder Legislativo do Estado de São Paulo.''',
        NULL,
        NULL,
        '''SP''',
        '''ACTIVE''',
        now(),
        now()
    )
ON CONFLICT (slug) DO UPDATE
SET
    name = EXCLUDED.name,
    type = EXCLUDED.type,
    scope_level = EXCLUDED.scope_level,
    official_domain = EXCLUDED.official_domain,
    description = EXCLUDED.description,
    state_code = EXCLUDED.state_code,
    status = '''ACTIVE''',
    updated_at = now();

INSERT INTO institution_jurisdictions (
    id,
    institution_id,
    jurisdiction_type,
    city_id,
    state_code,
    custom_area_label,
    created_at,
    updated_at
)
SELECT
    '''7c9ec70d-a59b-40f4-b23c-2f52a07ef101'''::uuid,
    i.id,
    '''CUSTOM_AREA''',
    NULL,
    '''SP''',
    '''Município de São Paulo''',
    now(),
    now()
FROM institutions i
WHERE i.slug = '''prefeitura-de-sao-paulo'''
  AND NOT EXISTS (
      SELECT 1
      FROM institution_jurisdictions j
      WHERE j.institution_id = i.id
        AND j.jurisdiction_type = '''CUSTOM_AREA'''
        AND j.custom_area_label = '''Município de São Paulo'''
  );

INSERT INTO institution_jurisdictions (
    id,
    institution_id,
    jurisdiction_type,
    city_id,
    state_code,
    custom_area_label,
    created_at,
    updated_at
)
SELECT
    '''7c9ec70d-a59b-40f4-b23c-2f52a07ef102'''::uuid,
    i.id,
    '''CUSTOM_AREA''',
    NULL,
    '''SP''',
    '''Município de São Paulo''',
    now(),
    now()
FROM institutions i
WHERE i.slug = '''camara-municipal-de-sao-paulo'''
  AND NOT EXISTS (
      SELECT 1
      FROM institution_jurisdictions j
      WHERE j.institution_id = i.id
        AND j.jurisdiction_type = '''CUSTOM_AREA'''
        AND j.custom_area_label = '''Município de São Paulo'''
  );

INSERT INTO institution_jurisdictions (
    id,
    institution_id,
    jurisdiction_type,
    city_id,
    state_code,
    custom_area_label,
    created_at,
    updated_at
)
SELECT
    '''7c9ec70d-a59b-40f4-b23c-2f52a07ef103'''::uuid,
    i.id,
    '''STATE''',
    NULL,
    '''SP''',
    NULL,
    now(),
    now()
FROM institutions i
WHERE i.slug = '''governo-do-estado-de-sao-paulo'''
  AND NOT EXISTS (
      SELECT 1
      FROM institution_jurisdictions j
      WHERE j.institution_id = i.id
        AND j.jurisdiction_type = '''STATE'''
        AND j.state_code = '''SP'''
  );

INSERT INTO institution_jurisdictions (
    id,
    institution_id,
    jurisdiction_type,
    city_id,
    state_code,
    custom_area_label,
    created_at,
    updated_at
)
SELECT
    '''7c9ec70d-a59b-40f4-b23c-2f52a07ef104'''::uuid,
    i.id,
    '''STATE''',
    NULL,
    '''SP''',
    NULL,
    now(),
    now()
FROM institutions i
WHERE i.slug = '''assembleia-legislativa-do-estado-de-sao-paulo'''
  AND NOT EXISTS (
      SELECT 1
      FROM institution_jurisdictions j
      WHERE j.institution_id = i.id
        AND j.jurisdiction_type = '''STATE'''
        AND j.state_code = '''SP'''
  );

COMMIT;
SQL
'

echo "==> Instituições ativas cadastradas"
"${COMPOSE[@]}" exec -T db sh -lc '
  psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atc "
    select
      name || ''' | ''' ||
      type || ''' | ''' ||
      scope_level || ''' | ''' ||
      coalesce(official_domain, '''''') || ''' | ''' ||
      status
    from institutions
    where slug in (
      '''prefeitura-de-sao-paulo''',
      '''camara-municipal-de-sao-paulo''',
      '''governo-do-estado-de-sao-paulo''',
      '''assembleia-legislativa-do-estado-de-sao-paulo'''
    )
    order by scope_level, name;
  "
'

COUNT="$(
  "${COMPOSE[@]}" exec -T db sh -lc '
    psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atc "
      select count(*)
      from institutions
      where status = '''ACTIVE'''
        and slug in (
          '''prefeitura-de-sao-paulo''',
          '''camara-municipal-de-sao-paulo''',
          '''governo-do-estado-de-sao-paulo''',
          '''assembleia-legislativa-do-estado-de-sao-paulo'''
        );
    "
  ' | tr -d '\r'
)"

[ "$COUNT" = "4" ] || fail "Esperadas 4 instituições ativas; encontradas $COUNT."

echo "HML_SP_INSTITUTIONS=OK count=$COUNT"
