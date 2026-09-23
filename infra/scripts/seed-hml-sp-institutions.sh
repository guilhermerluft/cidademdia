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

: "${HML_INSTITUTION_MASTER_PASSWORD:?Defina HML_INSTITUTION_MASTER_PASSWORD para as contas Master institucionais de HML.}"
[ "${#HML_INSTITUTION_MASTER_PASSWORD}" -ge 12 ]   || fail "HML_INSTITUTION_MASTER_PASSWORD deve conter pelo menos 12 caracteres."

command -v python3 >/dev/null 2>&1 || fail "python3 é necessário para gerar o hash das senhas de HML."

COMPOSE=(
  docker compose
  -p infra
  --env-file "$ENV_FILE"
  -f "$COMPOSE_FILE"
)

MASTER_HASH="$(
  python3 - <<'PY'
import base64
import hashlib
import os

password = os.environ["HML_INSTITUTION_MASTER_PASSWORD"].encode("utf-8")
salt = os.urandom(16)
iterations = 210_000
digest = hashlib.pbkdf2_hmac("sha256", password, salt, iterations, dklen=32)
print(
    "pbkdf2-sha256$"
    + str(iterations)
    + "$"
    + base64.b64encode(salt).decode("ascii")
    + "$"
    + base64.b64encode(digest).decode("ascii")
)
PY
)"

[ -n "$MASTER_HASH" ] || fail "Não foi possível gerar o hash das contas institucionais."

echo "==> Garantindo banco de homologação ativo"
"${COMPOSE[@]}" up -d db >/dev/null

echo "==> Cadastrando instituições, jurisdições e contas Master institucionais de São Paulo"
"${COMPOSE[@]}" exec -T -e MASTER_HASH="$MASTER_HASH" db sh -lc '
  set -e
  psql -v ON_ERROR_STOP=1 -v master_hash="$MASTER_HASH" -U "$POSTGRES_USER" -d "$POSTGRES_DB"
' <<'SQL'
BEGIN;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM roles WHERE key = 'MASTER') THEN
        RAISE EXCEPTION 'Role MASTER não encontrada. Inicie a API para executar o seed de identidade antes deste script.';
    END IF;
END
$$;

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
        '7c9ec70d-a59b-40f4-b23c-2f52a07ef001'::uuid,
        'Prefeitura de São Paulo',
        'prefeitura-de-sao-paulo',
        'CITY_HALL',
        'MUNICIPAL',
        NULL,
        NULL,
        'prefeitura.sp.gov.br',
        'Poder Executivo municipal da cidade de São Paulo.',
        NULL,
        NULL,
        'SP',
        'ACTIVE',
        now(),
        now()
    ),
    (
        '7c9ec70d-a59b-40f4-b23c-2f52a07ef002'::uuid,
        'Câmara Municipal de São Paulo',
        'camara-municipal-de-sao-paulo',
        'CITY_COUNCIL',
        'MUNICIPAL',
        NULL,
        NULL,
        'saopaulo.sp.leg.br',
        'Poder Legislativo municipal da cidade de São Paulo.',
        NULL,
        NULL,
        'SP',
        'ACTIVE',
        now(),
        now()
    ),
    (
        '7c9ec70d-a59b-40f4-b23c-2f52a07ef003'::uuid,
        'Governo do Estado de São Paulo',
        'governo-do-estado-de-sao-paulo',
        'PUBLIC_AGENCY',
        'STATE',
        NULL,
        NULL,
        'sp.gov.br',
        'Poder Executivo do Estado de São Paulo.',
        NULL,
        NULL,
        'SP',
        'ACTIVE',
        now(),
        now()
    ),
    (
        '7c9ec70d-a59b-40f4-b23c-2f52a07ef004'::uuid,
        'Assembleia Legislativa do Estado de São Paulo',
        'assembleia-legislativa-do-estado-de-sao-paulo',
        'ASSEMBLY',
        'STATE',
        NULL,
        NULL,
        'al.sp.gov.br',
        'Poder Legislativo do Estado de São Paulo.',
        NULL,
        NULL,
        'SP',
        'ACTIVE',
        now(),
        now()
    ),
    (
        '7c9ec70d-a59b-40f4-b23c-2f52a07ef005'::uuid,
        'SUS São Paulo',
        'sus-sao-paulo',
        'PUBLIC_SERVICE',
        'STATE',
        NULL,
        NULL,
        'saude.sp.gov.br',
        'Destino padrão do Sistema Único de Saúde para o Estado de São Paulo.',
        NULL,
        NULL,
        'SP',
        'ACTIVE',
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
    status = 'ACTIVE',
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
    seed.id,
    institution.id,
    seed.jurisdiction_type,
    NULL,
    'SP',
    seed.custom_area_label,
    now(),
    now()
FROM (
    VALUES
        ('prefeitura-de-sao-paulo', '7c9ec70d-a59b-40f4-b23c-2f52a07ef101'::uuid, 'CUSTOM_AREA', 'Município de São Paulo'),
        ('camara-municipal-de-sao-paulo', '7c9ec70d-a59b-40f4-b23c-2f52a07ef102'::uuid, 'CUSTOM_AREA', 'Município de São Paulo'),
        ('governo-do-estado-de-sao-paulo', '7c9ec70d-a59b-40f4-b23c-2f52a07ef103'::uuid, 'STATE', NULL),
        ('assembleia-legislativa-do-estado-de-sao-paulo', '7c9ec70d-a59b-40f4-b23c-2f52a07ef104'::uuid, 'STATE', NULL),
        ('sus-sao-paulo', '7c9ec70d-a59b-40f4-b23c-2f52a07ef105'::uuid, 'STATE', NULL)
) AS seed(slug, id, jurisdiction_type, custom_area_label)
JOIN institutions institution ON institution.slug = seed.slug
WHERE NOT EXISTS (
    SELECT 1
    FROM institution_jurisdictions existing
    WHERE existing.institution_id = institution.id
      AND existing.jurisdiction_type = seed.jurisdiction_type
      AND existing.state_code = 'SP'
      AND existing.custom_area_label IS NOT DISTINCT FROM seed.custom_area_label
);

INSERT INTO users (
    id,
    email,
    normalized_email,
    password_hash,
    status,
    email_confirmed_at,
    last_login_at,
    created_at,
    updated_at
)
VALUES
    (
        '7c9ec70d-a59b-40f4-b23c-2f52a07ef201'::uuid,
        'prefeitura-sp.master@hml.cidademdia.invalid',
        'PREFEITURA-SP.MASTER@HML.CIDADEMDIA.INVALID',
        :'master_hash',
        'Active',
        now(),
        NULL,
        now(),
        now()
    ),
    (
        '7c9ec70d-a59b-40f4-b23c-2f52a07ef202'::uuid,
        'camara-sp.master@hml.cidademdia.invalid',
        'CAMARA-SP.MASTER@HML.CIDADEMDIA.INVALID',
        :'master_hash',
        'Active',
        now(),
        NULL,
        now(),
        now()
    ),
    (
        '7c9ec70d-a59b-40f4-b23c-2f52a07ef203'::uuid,
        'governo-sp.master@hml.cidademdia.invalid',
        'GOVERNO-SP.MASTER@HML.CIDADEMDIA.INVALID',
        :'master_hash',
        'Active',
        now(),
        NULL,
        now(),
        now()
    ),
    (
        '7c9ec70d-a59b-40f4-b23c-2f52a07ef204'::uuid,
        'alesp.master@hml.cidademdia.invalid',
        'ALESP.MASTER@HML.CIDADEMDIA.INVALID',
        :'master_hash',
        'Active',
        now(),
        NULL,
        now(),
        now()
    ),
    (
        '7c9ec70d-a59b-40f4-b23c-2f52a07ef205'::uuid,
        'sus-sp.master@hml.cidademdia.invalid',
        'SUS-SP.MASTER@HML.CIDADEMDIA.INVALID',
        :'master_hash',
        'Active',
        now(),
        NULL,
        now(),
        now()
    )
ON CONFLICT (normalized_email) DO UPDATE
SET
    password_hash = EXCLUDED.password_hash,
    status = 'Active',
    email_confirmed_at = COALESCE(users.email_confirmed_at, now()),
    updated_at = now();

INSERT INTO user_profiles (
    id,
    user_id,
    display_name,
    document,
    phone,
    avatar_media_id,
    created_at,
    updated_at
)
SELECT
    seed.profile_id,
    user_account.id,
    seed.display_name,
    NULL,
    NULL,
    NULL,
    now(),
    now()
FROM (
    VALUES
        ('PREFEITURA-SP.MASTER@HML.CIDADEMDIA.INVALID', '7c9ec70d-a59b-40f4-b23c-2f52a07ef301'::uuid, 'Prefeitura de São Paulo'),
        ('CAMARA-SP.MASTER@HML.CIDADEMDIA.INVALID', '7c9ec70d-a59b-40f4-b23c-2f52a07ef302'::uuid, 'Câmara Municipal de São Paulo'),
        ('GOVERNO-SP.MASTER@HML.CIDADEMDIA.INVALID', '7c9ec70d-a59b-40f4-b23c-2f52a07ef303'::uuid, 'Governo do Estado de São Paulo'),
        ('ALESP.MASTER@HML.CIDADEMDIA.INVALID', '7c9ec70d-a59b-40f4-b23c-2f52a07ef304'::uuid, 'Assembleia Legislativa do Estado de São Paulo'),
        ('SUS-SP.MASTER@HML.CIDADEMDIA.INVALID', '7c9ec70d-a59b-40f4-b23c-2f52a07ef305'::uuid, 'SUS São Paulo')
) AS seed(normalized_email, profile_id, display_name)
JOIN users user_account ON user_account.normalized_email = seed.normalized_email
ON CONFLICT (user_id) DO UPDATE
SET
    display_name = EXCLUDED.display_name,
    updated_at = now();

DELETE FROM user_roles user_role
USING users user_account, roles role
WHERE user_role.user_id = user_account.id
  AND user_role.role_id = role.id
  AND role.key = 'CITIZEN'
  AND user_account.normalized_email IN (
      'PREFEITURA-SP.MASTER@HML.CIDADEMDIA.INVALID',
      'CAMARA-SP.MASTER@HML.CIDADEMDIA.INVALID',
      'GOVERNO-SP.MASTER@HML.CIDADEMDIA.INVALID',
      'ALESP.MASTER@HML.CIDADEMDIA.INVALID',
      'SUS-SP.MASTER@HML.CIDADEMDIA.INVALID'
  );

INSERT INTO user_roles (user_id, role_id, created_at)
SELECT user_account.id, role.id, now()
FROM users user_account
JOIN roles role ON role.key = 'MASTER'
WHERE user_account.normalized_email IN (
    'PREFEITURA-SP.MASTER@HML.CIDADEMDIA.INVALID',
    'CAMARA-SP.MASTER@HML.CIDADEMDIA.INVALID',
    'GOVERNO-SP.MASTER@HML.CIDADEMDIA.INVALID',
    'ALESP.MASTER@HML.CIDADEMDIA.INVALID',
    'SUS-SP.MASTER@HML.CIDADEMDIA.INVALID'
)
ON CONFLICT (user_id, role_id) DO NOTHING;

INSERT INTO institution_memberships (
    id,
    institution_id,
    user_id,
    representative_id,
    membership_role,
    status,
    joined_at,
    ended_at,
    created_at,
    updated_at
)
SELECT
    seed.membership_id,
    institution.id,
    user_account.id,
    NULL,
    'INSTITUTION_ADMIN',
    'ACTIVE',
    now(),
    NULL,
    now(),
    now()
FROM (
    VALUES
        ('prefeitura-de-sao-paulo', 'PREFEITURA-SP.MASTER@HML.CIDADEMDIA.INVALID', '7c9ec70d-a59b-40f4-b23c-2f52a07ef401'::uuid),
        ('camara-municipal-de-sao-paulo', 'CAMARA-SP.MASTER@HML.CIDADEMDIA.INVALID', '7c9ec70d-a59b-40f4-b23c-2f52a07ef402'::uuid),
        ('governo-do-estado-de-sao-paulo', 'GOVERNO-SP.MASTER@HML.CIDADEMDIA.INVALID', '7c9ec70d-a59b-40f4-b23c-2f52a07ef403'::uuid),
        ('assembleia-legislativa-do-estado-de-sao-paulo', 'ALESP.MASTER@HML.CIDADEMDIA.INVALID', '7c9ec70d-a59b-40f4-b23c-2f52a07ef404'::uuid),
        ('sus-sao-paulo', 'SUS-SP.MASTER@HML.CIDADEMDIA.INVALID', '7c9ec70d-a59b-40f4-b23c-2f52a07ef405'::uuid)
) AS seed(slug, normalized_email, membership_id)
JOIN institutions institution ON institution.slug = seed.slug
JOIN users user_account ON user_account.normalized_email = seed.normalized_email
ON CONFLICT (institution_id, user_id, membership_role) DO UPDATE
SET
    status = 'ACTIVE',
    ended_at = NULL,
    updated_at = now();

COMMIT;
SQL

echo "==> Validando destinos institucionais e Masters"
VALIDATION="$(
  "${COMPOSE[@]}" exec -T db sh -lc '
    psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At
  ' <<'SQL'
WITH candidates AS (
    SELECT
        institution.id AS institution_id,
        institution.name,
        user_account.id AS master_user_id,
        user_account.email
    FROM institutions institution
    JOIN institution_memberships membership
      ON membership.institution_id = institution.id
     AND membership.status = 'ACTIVE'
     AND membership.membership_role = 'INSTITUTION_ADMIN'
    JOIN users user_account
      ON user_account.id = membership.user_id
     AND user_account.status = 'Active'
    JOIN user_roles user_role
      ON user_role.user_id = user_account.id
    JOIN roles role
      ON role.id = user_role.role_id
     AND role.key = 'MASTER'
    WHERE institution.status = 'ACTIVE'
      AND institution.slug IN (
          'prefeitura-de-sao-paulo',
          'camara-municipal-de-sao-paulo',
          'governo-do-estado-de-sao-paulo',
          'assembleia-legislativa-do-estado-de-sao-paulo',
          'sus-sao-paulo'
      )
),
institution_counts AS (
    SELECT institution_id, count(DISTINCT master_user_id) AS masters
    FROM candidates
    GROUP BY institution_id
),
master_counts AS (
    SELECT master_user_id, count(DISTINCT institution_id) AS institutions
    FROM candidates
    GROUP BY master_user_id
)
SELECT 'pairs=' || count(*) FROM candidates
UNION ALL
SELECT 'ambiguous_institutions=' || count(*) FROM institution_counts WHERE masters <> 1
UNION ALL
SELECT 'ambiguous_masters=' || count(*) FROM master_counts WHERE institutions <> 1;
SQL
)"

echo "$VALIDATION"

PAIRS="$(printf '%s\n' "$VALIDATION" | sed -n 's/^pairs=//p')"
AMBIGUOUS_INSTITUTIONS="$(printf '%s\n' "$VALIDATION" | sed -n 's/^ambiguous_institutions=//p')"
AMBIGUOUS_MASTERS="$(printf '%s\n' "$VALIDATION" | sed -n 's/^ambiguous_masters=//p')"

[ "$PAIRS" = "5" ] || fail "Esperados 5 pares instituição/Master; encontrados ${PAIRS:-0}."
[ "$AMBIGUOUS_INSTITUTIONS" = "0" ] || fail "Há instituição com mais de uma Master institucional ativa."
[ "$AMBIGUOUS_MASTERS" = "0" ] || fail "Há Master institucional ativa vinculada a mais de uma instituição."

echo "==> Contas Master institucionais de HML"
"${COMPOSE[@]}" exec -T db sh -lc '
  psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At
' <<'SQL'
select
    institution.name || ' | ' ||
    user_account.email || ' | MASTER | ' ||
    membership.membership_role
from institutions institution
join institution_memberships membership
  on membership.institution_id = institution.id
 and membership.status = 'ACTIVE'
 and membership.membership_role = 'INSTITUTION_ADMIN'
join users user_account
  on user_account.id = membership.user_id
 and user_account.status = 'Active'
join user_roles user_role
  on user_role.user_id = user_account.id
join roles role
  on role.id = user_role.role_id
 and role.key = 'MASTER'
where institution.slug in (
  'prefeitura-de-sao-paulo',
  'camara-municipal-de-sao-paulo',
  'governo-do-estado-de-sao-paulo',
  'assembleia-legislativa-do-estado-de-sao-paulo',
  'sus-sao-paulo'
)
order by institution.name;
SQL

echo "HML_SP_INSTITUTIONS=OK count=5"
echo "HML_SP_INSTITUTIONAL_MASTERS=OK count=5"
