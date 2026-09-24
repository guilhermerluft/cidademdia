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

: "${HML_INSTITUTION_MASTER_PASSWORD:?Defina HML_INSTITUTION_MASTER_PASSWORD para os fallbacks institucionais de HML.}"
[ "${#HML_INSTITUTION_MASTER_PASSWORD}" -ge 12 ] \
  || fail "HML_INSTITUTION_MASTER_PASSWORD deve conter pelo menos 12 caracteres."

command -v python3 >/dev/null 2>&1 || fail "python3 é necessário para gerar o hash."

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

[ -n "$MASTER_HASH" ] || fail "Não foi possível gerar o hash."

"${COMPOSE[@]}" up -d db >/dev/null

echo "==> Cadastrando fallbacks globais do CIDADEMDIA em HML"
"${COMPOSE[@]}" exec -T -e MASTER_HASH="$MASTER_HASH" db sh -lc '
  set -e
  psql -v ON_ERROR_STOP=1 -v master_hash="$MASTER_HASH" -U "$POSTGRES_USER" -d "$POSTGRES_DB"
' <<'SQL'
BEGIN;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM roles WHERE key = 'MASTER') THEN
        RAISE EXCEPTION 'Role MASTER não encontrada.';
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
        '6d9ec70d-a59b-40f4-b23c-2f52a07ef001'::uuid,
        'Prefeitura',
        'cidademdia-fallback-prefeitura',
        'CITY_HALL',
        'MUNICIPAL',
        NULL, NULL, NULL,
        'Fallback padrão do CIDADEMDIA para Prefeitura quando não houver Master local elegível.',
        NULL, NULL, NULL, 'ACTIVE', now(), now()
    ),
    (
        '6d9ec70d-a59b-40f4-b23c-2f52a07ef002'::uuid,
        'Câmara Municipal',
        'cidademdia-fallback-camara-municipal',
        'CITY_COUNCIL',
        'MUNICIPAL',
        NULL, NULL, NULL,
        'Fallback padrão do CIDADEMDIA para Câmara Municipal quando não houver Master local elegível.',
        NULL, NULL, NULL, 'ACTIVE', now(), now()
    ),
    (
        '6d9ec70d-a59b-40f4-b23c-2f52a07ef003'::uuid,
        'Governo do Estado',
        'cidademdia-fallback-governo-estado',
        'PUBLIC_AGENCY',
        'STATE',
        NULL, NULL, NULL,
        'Fallback padrão do CIDADEMDIA para Governo do Estado quando não houver Master local elegível.',
        NULL, NULL, NULL, 'ACTIVE', now(), now()
    ),
    (
        '6d9ec70d-a59b-40f4-b23c-2f52a07ef004'::uuid,
        'Assembleia Legislativa',
        'cidademdia-fallback-assembleia-legislativa',
        'ASSEMBLY',
        'STATE',
        NULL, NULL, NULL,
        'Fallback padrão do CIDADEMDIA para Assembleia Legislativa quando não houver Master local elegível.',
        NULL, NULL, NULL, 'ACTIVE', now(), now()
    ),
    (
        '6d9ec70d-a59b-40f4-b23c-2f52a07ef005'::uuid,
        'SUS',
        'cidademdia-fallback-sus',
        'PUBLIC_SERVICE',
        'FEDERAL',
        NULL, NULL, NULL,
        'Fallback padrão do CIDADEMDIA para SUS quando não houver Master local elegível.',
        NULL, NULL, NULL, 'ACTIVE', now(), now()
    ),
    (
        '6d9ec70d-a59b-40f4-b23c-2f52a07ef006'::uuid,
        'Câmara dos Deputados',
        'cidademdia-fallback-camara-deputados',
        'OTHER',
        'FEDERAL',
        NULL, NULL, NULL,
        'Fallback padrão do CIDADEMDIA para Câmara dos Deputados quando não houver Master local elegível.',
        NULL, NULL, NULL, 'ACTIVE', now(), now()
    ),
    (
        '6d9ec70d-a59b-40f4-b23c-2f52a07ef007'::uuid,
        'Senado Federal',
        'cidademdia-fallback-senado-federal',
        'OTHER',
        'FEDERAL',
        NULL, NULL, NULL,
        'Fallback padrão do CIDADEMDIA para Senado Federal quando não houver Master local elegível.',
        NULL, NULL, NULL, 'ACTIVE', now(), now()
    )
ON CONFLICT (slug) DO UPDATE
SET
    name = EXCLUDED.name,
    type = EXCLUDED.type,
    scope_level = EXCLUDED.scope_level,
    description = EXCLUDED.description,
    state_code = NULL,
    status = 'ACTIVE',
    updated_at = now();

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
        '6d9ec70d-a59b-40f4-b23c-2f52a07ef201'::uuid,
        'fallback-prefeitura.master@hml.cidademdia.invalid',
        'FALLBACK-PREFEITURA.MASTER@HML.CIDADEMDIA.INVALID',
        :'master_hash', 'Active', now(), NULL, now(), now()
    ),
    (
        '6d9ec70d-a59b-40f4-b23c-2f52a07ef202'::uuid,
        'fallback-camara.master@hml.cidademdia.invalid',
        'FALLBACK-CAMARA.MASTER@HML.CIDADEMDIA.INVALID',
        :'master_hash', 'Active', now(), NULL, now(), now()
    ),
    (
        '6d9ec70d-a59b-40f4-b23c-2f52a07ef203'::uuid,
        'fallback-governo.master@hml.cidademdia.invalid',
        'FALLBACK-GOVERNO.MASTER@HML.CIDADEMDIA.INVALID',
        :'master_hash', 'Active', now(), NULL, now(), now()
    ),
    (
        '6d9ec70d-a59b-40f4-b23c-2f52a07ef204'::uuid,
        'fallback-assembleia.master@hml.cidademdia.invalid',
        'FALLBACK-ASSEMBLEIA.MASTER@HML.CIDADEMDIA.INVALID',
        :'master_hash', 'Active', now(), NULL, now(), now()
    ),
    (
        '6d9ec70d-a59b-40f4-b23c-2f52a07ef205'::uuid,
        'fallback-sus.master@hml.cidademdia.invalid',
        'FALLBACK-SUS.MASTER@HML.CIDADEMDIA.INVALID',
        :'master_hash', 'Active', now(), NULL, now(), now()
    ),
    (
        '6d9ec70d-a59b-40f4-b23c-2f52a07ef206'::uuid,
        'fallback-camara-deputados.master@hml.cidademdia.invalid',
        'FALLBACK-CAMARA-DEPUTADOS.MASTER@HML.CIDADEMDIA.INVALID',
        :'master_hash', 'Active', now(), NULL, now(), now()
    ),
    (
        '6d9ec70d-a59b-40f4-b23c-2f52a07ef207'::uuid,
        'fallback-senado.master@hml.cidademdia.invalid',
        'FALLBACK-SENADO.MASTER@HML.CIDADEMDIA.INVALID',
        :'master_hash', 'Active', now(), NULL, now(), now()
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
        ('FALLBACK-PREFEITURA.MASTER@HML.CIDADEMDIA.INVALID', '6d9ec70d-a59b-40f4-b23c-2f52a07ef301'::uuid, 'Cidademdia — Prefeitura'),
        ('FALLBACK-CAMARA.MASTER@HML.CIDADEMDIA.INVALID', '6d9ec70d-a59b-40f4-b23c-2f52a07ef302'::uuid, 'Cidademdia — Câmara Municipal'),
        ('FALLBACK-GOVERNO.MASTER@HML.CIDADEMDIA.INVALID', '6d9ec70d-a59b-40f4-b23c-2f52a07ef303'::uuid, 'Cidademdia — Governo do Estado'),
        ('FALLBACK-ASSEMBLEIA.MASTER@HML.CIDADEMDIA.INVALID', '6d9ec70d-a59b-40f4-b23c-2f52a07ef304'::uuid, 'Cidademdia — Assembleia Legislativa'),
        ('FALLBACK-SUS.MASTER@HML.CIDADEMDIA.INVALID', '6d9ec70d-a59b-40f4-b23c-2f52a07ef305'::uuid, 'Cidademdia — SUS'),
        ('FALLBACK-CAMARA-DEPUTADOS.MASTER@HML.CIDADEMDIA.INVALID', '6d9ec70d-a59b-40f4-b23c-2f52a07ef306'::uuid, 'Cidademdia — Câmara dos Deputados'),
        ('FALLBACK-SENADO.MASTER@HML.CIDADEMDIA.INVALID', '6d9ec70d-a59b-40f4-b23c-2f52a07ef307'::uuid, 'Cidademdia — Senado Federal')
) AS seed(normalized_email, profile_id, display_name)
JOIN users user_account
  ON user_account.normalized_email = seed.normalized_email
ON CONFLICT (user_id) DO UPDATE
SET
    display_name = EXCLUDED.display_name,
    updated_at = now();

DELETE FROM user_roles user_role
USING users user_account, roles role
WHERE user_role.user_id = user_account.id
  AND user_role.role_id = role.id
  AND role.key = 'CITIZEN'
  AND user_account.normalized_email LIKE 'FALLBACK-%@HML.CIDADEMDIA.INVALID';

INSERT INTO user_roles (user_id, role_id, created_at)
SELECT user_account.id, role.id, now()
FROM users user_account
JOIN roles role ON role.key = 'MASTER'
WHERE user_account.normalized_email LIKE 'FALLBACK-%@HML.CIDADEMDIA.INVALID'
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
        ('cidademdia-fallback-prefeitura', 'FALLBACK-PREFEITURA.MASTER@HML.CIDADEMDIA.INVALID', '6d9ec70d-a59b-40f4-b23c-2f52a07ef401'::uuid),
        ('cidademdia-fallback-camara-municipal', 'FALLBACK-CAMARA.MASTER@HML.CIDADEMDIA.INVALID', '6d9ec70d-a59b-40f4-b23c-2f52a07ef402'::uuid),
        ('cidademdia-fallback-governo-estado', 'FALLBACK-GOVERNO.MASTER@HML.CIDADEMDIA.INVALID', '6d9ec70d-a59b-40f4-b23c-2f52a07ef403'::uuid),
        ('cidademdia-fallback-assembleia-legislativa', 'FALLBACK-ASSEMBLEIA.MASTER@HML.CIDADEMDIA.INVALID', '6d9ec70d-a59b-40f4-b23c-2f52a07ef404'::uuid),
        ('cidademdia-fallback-sus', 'FALLBACK-SUS.MASTER@HML.CIDADEMDIA.INVALID', '6d9ec70d-a59b-40f4-b23c-2f52a07ef405'::uuid),
        ('cidademdia-fallback-camara-deputados', 'FALLBACK-CAMARA-DEPUTADOS.MASTER@HML.CIDADEMDIA.INVALID', '6d9ec70d-a59b-40f4-b23c-2f52a07ef406'::uuid),
        ('cidademdia-fallback-senado-federal', 'FALLBACK-SENADO.MASTER@HML.CIDADEMDIA.INVALID', '6d9ec70d-a59b-40f4-b23c-2f52a07ef407'::uuid)
) AS seed(slug, normalized_email, membership_id)
JOIN institutions institution
  ON institution.slug = seed.slug
JOIN users user_account
  ON user_account.normalized_email = seed.normalized_email
ON CONFLICT (institution_id, user_id, membership_role) DO UPDATE
SET
    status = 'ACTIVE',
    ended_at = NULL,
    updated_at = now();

COMMIT;
SQL

echo "==> Validando fallbacks globais de HML"
VALIDATION="$(
  "${COMPOSE[@]}" exec -T db sh -lc '
    psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At
  ' <<'SQL'
WITH candidates AS (
    SELECT DISTINCT
        institution.id AS institution_id,
        user_account.id AS master_user_id
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
      AND institution.slug LIKE 'cidademdia-fallback-%'
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

[ "$PAIRS" = "5" ] || fail "Esperados 5 fallbacks; encontrados ${PAIRS:-0}."
[ "$AMBIGUOUS_INSTITUTIONS" = "0" ] || fail "Há fallback com múltiplas Masters."
[ "$AMBIGUOUS_MASTERS" = "0" ] || fail "Há Master de fallback vinculada a múltiplas instituições."

echo "HML_DEFAULT_OCCURRENCE_DESTINATIONS=OK count=7"
echo "HML_DEFAULT_OCCURRENCE_MASTERS=OK count=7"
