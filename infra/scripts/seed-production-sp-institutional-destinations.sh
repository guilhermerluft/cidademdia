#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
PROD_ENV="${CIDADEMDIA_PROD_ENV:-$ROOT/.env.production}"
COMPOSE_FILE="$ROOT/infra/docker-compose.yml"
PROJECT="${CIDADEMDIA_PROD_PROJECT:-cidademdia-prod}"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

for cmd in docker grep python3; do
  command -v "$cmd" >/dev/null 2>&1 || fail "comando ausente: $cmd"
done

test -f "$PROD_ENV" || fail "arquivo de produção ausente: $PROD_ENV"
grep -q '^ASPNETCORE_ENVIRONMENT=Production$' "$PROD_ENV"   || fail "este script só pode usar ambiente Production"

COMPOSE=(
  docker compose
  -p "$PROJECT"
  --env-file "$PROD_ENV"
  -f "$COMPOSE_FILE"
)

"${COMPOSE[@]}" up -d db >/dev/null

db_scalar() {
  local sql="$1"
  "${COMPOSE[@]}" exec -T db sh -lc '
    psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At
  ' <<<"$sql" | tr -d '[:space:]'
}

echo "==> Validando role MASTER"
MASTER_ROLE_COUNT="$(db_scalar "select count(*) from roles where key = 'MASTER';")"
test "$MASTER_ROLE_COUNT" = "1" || fail "role MASTER ausente ou duplicada"

echo "==> Garantindo instituições oficiais de São Paulo"
"${COMPOSE[@]}" exec -T db sh -lc '
  psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"
' <<'SQL'
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
        '8c9ec70d-a59b-40f4-b23c-2f52a07ef001'::uuid,
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
        '9c9ec70d-a59b-40f4-b23c-2f52a07ef002'::uuid,
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
        '9c9ec70d-a59b-40f4-b23c-2f52a07ef003'::uuid,
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
        '9c9ec70d-a59b-40f4-b23c-2f52a07ef004'::uuid,
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
        ('prefeitura-de-sao-paulo', '9c9ec70d-a59b-40f4-b23c-2f52a07ef101'::uuid, 'CUSTOM_AREA', 'Município de São Paulo'),
        ('camara-municipal-de-sao-paulo', '9c9ec70d-a59b-40f4-b23c-2f52a07ef102'::uuid, 'CUSTOM_AREA', 'Município de São Paulo'),
        ('governo-do-estado-de-sao-paulo', '9c9ec70d-a59b-40f4-b23c-2f52a07ef103'::uuid, 'STATE', NULL),
        ('assembleia-legislativa-do-estado-de-sao-paulo', '9c9ec70d-a59b-40f4-b23c-2f52a07ef104'::uuid, 'STATE', NULL)
) AS seed(slug, id, jurisdiction_type, custom_area_label)
JOIN institutions institution
  ON institution.slug = seed.slug
WHERE NOT EXISTS (
    SELECT 1
    FROM institution_jurisdictions existing
    WHERE existing.institution_id = institution.id
      AND existing.jurisdiction_type = seed.jurisdiction_type
      AND existing.state_code = 'SP'
      AND existing.custom_area_label IS NOT DISTINCT FROM seed.custom_area_label
);

COMMIT;
SQL

active_master_count() {
  local display_name="$1"
  local sql

  sql="$(cat <<'SQL'
select count(distinct user_account.id)
from users user_account
join user_profiles profile
  on profile.user_id = user_account.id
join user_roles user_role
  on user_role.user_id = user_account.id
join roles role
  on role.id = user_role.role_id
 and role.key = 'MASTER'
where user_account.status = 'Active'
  and profile.display_name = :'display_name';
SQL
)"

  printf '%s\n' "$sql" |
    "${COMPOSE[@]}" exec -T \
      -e QUERY_DISPLAY_NAME="$display_name" \
      db sh -lc '
        psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At \
          -v display_name="$QUERY_DISPLAY_NAME"
      ' |
    tr -d '[:space:]'
}

hash_password() {
  local password="$1"

  PASSWORD_TO_HASH="$password" python3 - <<'PY'
import base64
import hashlib
import os

password = os.environ["PASSWORD_TO_HASH"].encode("utf-8")
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
}

read_password() {
  local variable_name="$1"
  local label="$2"
  local value="${!variable_name:-}"

  if [ -z "$value" ]; then
    read -r -s -p "Senha inicial da conta institucional $label: " value
    echo
  fi

  [ "${#value}" -ge 12 ] || fail "senha de $label deve conter pelo menos 12 caracteres"
  printf '%s' "$value"
}

ensure_internal_master_if_missing() {
  local display_name="$1"
  local email="$2"
  local user_id="$3"
  local profile_id="$4"
  local password_variable="$5"
  local count

  count="$(active_master_count "$display_name")"

  case "$count" in
    0)
      ;;
    1)
      echo "existing_master=$display_name"
      return 0
      ;;
    *)
      fail "há $count Masters ativas com display_name '$display_name'"
      ;;
  esac

  local password
  local password_hash
  local normalized_email

  password="$(read_password "$password_variable" "$display_name")"
  password_hash="$(hash_password "$password")"
  normalized_email="$(printf '%s' "$email" | tr '[:lower:]' '[:upper:]')"

  "${COMPOSE[@]}" exec -T \
    -e MASTER_HASH="$password_hash" \
    db sh -lc '
      psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
        -v user_id="$1" \
        -v profile_id="$2" \
        -v email="$3" \
        -v normalized_email="$4" \
        -v display_name="$5" \
        -v master_hash="$MASTER_HASH"
    ' sh "$user_id" "$profile_id" "$email" "$normalized_email" "$display_name" <<'SQL'
BEGIN;

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
VALUES (
    :'user_id'::uuid,
    :'email',
    :'normalized_email',
    :'master_hash',
    'Active',
    now(),
    NULL,
    now(),
    now()
)
ON CONFLICT (normalized_email) DO UPDATE
SET
    status = 'Active',
    password_hash = EXCLUDED.password_hash,
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
    :'profile_id'::uuid,
    user_account.id,
    :'display_name',
    NULL,
    NULL,
    NULL,
    now(),
    now()
FROM users user_account
WHERE user_account.normalized_email = :'normalized_email'
ON CONFLICT (user_id) DO UPDATE
SET
    display_name = EXCLUDED.display_name,
    updated_at = now();

DELETE FROM user_roles user_role
USING users user_account, roles role
WHERE user_role.user_id = user_account.id
  AND user_role.role_id = role.id
  AND role.key = 'CITIZEN'
  AND user_account.normalized_email = :'normalized_email';

INSERT INTO user_roles (user_id, role_id, created_at)
SELECT user_account.id, role.id, now()
FROM users user_account
JOIN roles role
  ON role.key = 'MASTER'
WHERE user_account.normalized_email = :'normalized_email'
ON CONFLICT (user_id, role_id) DO NOTHING;

COMMIT;
SQL

  count="$(active_master_count "$display_name")"
  test "$count" = "1" || fail "não foi possível provisionar uma Master única para $display_name"
  echo "provisioned_master=$display_name"
}

echo "==> Validando Master real já existente da Prefeitura"
PREFEITURA_COUNT="$(active_master_count "Prefeitura de São Paulo")"
test "$PREFEITURA_COUNT" = "1"   || fail "esperada exatamente 1 Master ativa chamada Prefeitura de São Paulo; encontradas $PREFEITURA_COUNT"

echo "==> Garantindo Masters institucionais dos demais destinos"
ensure_internal_master_if_missing \
  "Câmara Municipal de São Paulo" \
  "camara-sp.master@cidademdia.com.br" \
  "9c9ec70d-a59b-40f4-b23c-2f52a07ef202" \
  "9c9ec70d-a59b-40f4-b23c-2f52a07ef302" \
  "PROD_CAMARA_MASTER_PASSWORD"

ensure_internal_master_if_missing \
  "Governo do Estado de São Paulo" \
  "governo-sp.master@cidademdia.com.br" \
  "9c9ec70d-a59b-40f4-b23c-2f52a07ef203" \
  "9c9ec70d-a59b-40f4-b23c-2f52a07ef303" \
  "PROD_GOVERNO_MASTER_PASSWORD"

ensure_internal_master_if_missing \
  "Assembleia Legislativa do Estado de São Paulo" \
  "alesp.master@cidademdia.com.br" \
  "9c9ec70d-a59b-40f4-b23c-2f52a07ef204" \
  "9c9ec70d-a59b-40f4-b23c-2f52a07ef304" \
  "PROD_ALESP_MASTER_PASSWORD"

ensure_membership() {
  local slug="$1"
  local display_name="$2"
  local membership_id="$3"

  local master_id
  local institution_id
  local other_institutions
  local other_masters

  local master_sql
  master_sql="$(cat <<'SQL'
select distinct user_account.id
from users user_account
join user_profiles profile
  on profile.user_id = user_account.id
join user_roles user_role
  on user_role.user_id = user_account.id
join roles role
  on role.id = user_role.role_id
 and role.key = 'MASTER'
where user_account.status = 'Active'
  and profile.display_name = :'display_name';
SQL
)"

  master_id="$(
    printf '%s\n' "$master_sql" |
      "${COMPOSE[@]}" exec -T \
        -e QUERY_DISPLAY_NAME="$display_name" \
        db sh -lc '
          psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At \
            -v display_name="$QUERY_DISPLAY_NAME"
        ' |
      tr -d '[:space:]'
  )"

  test -n "$master_id" || fail "Master não encontrada para $display_name"

  local institution_sql
  institution_sql="$(cat <<'SQL'
select id
from institutions
where slug = :'slug'
  and status = 'ACTIVE';
SQL
)"

  institution_id="$(
    printf '%s\n' "$institution_sql" |
      "${COMPOSE[@]}" exec -T \
        -e QUERY_SLUG="$slug" \
        db sh -lc '
          psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At \
            -v slug="$QUERY_SLUG"
        ' |
      tr -d '[:space:]'
  )"

  test -n "$institution_id" || fail "instituição ativa não encontrada: $slug"

  local other_institutions_sql
  other_institutions_sql="$(cat <<'SQL'
select count(distinct membership.institution_id)
from institution_memberships membership
join institutions institution
  on institution.id = membership.institution_id
 and institution.status = 'ACTIVE'
where membership.user_id = :'master_id'::uuid
  and membership.status = 'ACTIVE'
  and membership.membership_role = 'INSTITUTION_ADMIN'
  and membership.institution_id <> :'institution_id'::uuid;
SQL
)"

  other_institutions="$(
    printf '%s\n' "$other_institutions_sql" |
      "${COMPOSE[@]}" exec -T \
        -e QUERY_MASTER_ID="$master_id" \
        -e QUERY_INSTITUTION_ID="$institution_id" \
        db sh -lc '
          psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At \
            -v master_id="$QUERY_MASTER_ID" \
            -v institution_id="$QUERY_INSTITUTION_ID"
        ' |
      tr -d '[:space:]'
  )"

  test "$other_institutions" = "0"     || fail "$display_name já administra outra instituição ativa"

  local other_masters_sql
  other_masters_sql="$(cat <<'SQL'
select count(distinct membership.user_id)
from institution_memberships membership
join users user_account
  on user_account.id = membership.user_id
 and user_account.status = 'Active'
join user_roles user_role
  on user_role.user_id = user_account.id
join roles role
  on role.id = user_role.role_id
 and role.key = 'MASTER'
where membership.institution_id = :'institution_id'::uuid
  and membership.status = 'ACTIVE'
  and membership.membership_role = 'INSTITUTION_ADMIN'
  and membership.user_id <> :'master_id'::uuid;
SQL
)"

  other_masters="$(
    printf '%s\n' "$other_masters_sql" |
      "${COMPOSE[@]}" exec -T \
        -e QUERY_MASTER_ID="$master_id" \
        -e QUERY_INSTITUTION_ID="$institution_id" \
        db sh -lc '
          psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At \
            -v master_id="$QUERY_MASTER_ID" \
            -v institution_id="$QUERY_INSTITUTION_ID"
        ' |
      tr -d '[:space:]'
  )"

  test "$other_masters" = "0"     || fail "$display_name possui outra Master institucional ativa"

  "${COMPOSE[@]}" exec -T db sh -lc '
    psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
      -v membership_id="$1" \
      -v institution_id="$2" \
      -v master_id="$3"
  ' sh "$membership_id" "$institution_id" "$master_id" <<'SQL'
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
VALUES (
    :'membership_id'::uuid,
    :'institution_id'::uuid,
    :'master_id'::uuid,
    NULL,
    'INSTITUTION_ADMIN',
    'ACTIVE',
    now(),
    NULL,
    now(),
    now()
)
ON CONFLICT (institution_id, user_id, membership_role) DO UPDATE
SET
    status = 'ACTIVE',
    ended_at = NULL,
    updated_at = now();
SQL
}

echo "==> Garantindo vínculos institucionais 1:1"
ensure_membership \
  "prefeitura-de-sao-paulo" \
  "Prefeitura de São Paulo" \
  "8c9ec70d-a59b-40f4-b23c-2f52a07ef401"

ensure_membership \
  "camara-municipal-de-sao-paulo" \
  "Câmara Municipal de São Paulo" \
  "9c9ec70d-a59b-40f4-b23c-2f52a07ef402"

ensure_membership \
  "governo-do-estado-de-sao-paulo" \
  "Governo do Estado de São Paulo" \
  "9c9ec70d-a59b-40f4-b23c-2f52a07ef403"

ensure_membership \
  "assembleia-legislativa-do-estado-de-sao-paulo" \
  "Assembleia Legislativa do Estado de São Paulo" \
  "9c9ec70d-a59b-40f4-b23c-2f52a07ef404"

echo "==> Validando quatro destinos institucionais elegíveis"
VALIDATION="$(
  "${COMPOSE[@]}" exec -T db sh -lc '
    psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At
  ' <<'SQL'
WITH candidates AS (
    SELECT DISTINCT
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
          'assembleia-legislativa-do-estado-de-sao-paulo'
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

test "$PAIRS" = "4" || fail "esperados 4 pares instituição/Master; encontrados ${PAIRS:-0}"
test "$AMBIGUOUS_INSTITUTIONS" = "0" || fail "há instituição com múltiplas Masters elegíveis"
test "$AMBIGUOUS_MASTERS" = "0" || fail "há Master elegível vinculada a múltiplas instituições"

"${COMPOSE[@]}" exec -T db sh -lc '
  psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At
' <<'SQL'
SELECT
    institution.name || ' | ' ||
    user_account.email || ' | MASTER | ' ||
    membership.membership_role
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
WHERE institution.slug IN (
    'prefeitura-de-sao-paulo',
    'camara-municipal-de-sao-paulo',
    'governo-do-estado-de-sao-paulo',
    'assembleia-legislativa-do-estado-de-sao-paulo'
)
ORDER BY institution.name;
SQL

echo "PRODUCTION_SP_INSTITUTIONS=OK count=4"
echo "PRODUCTION_SP_INSTITUTIONAL_MASTERS=OK count=4"
echo "PRODUCTION INSTITUTIONAL DESTINATIONS SEED: OK"
