#!/usr/bin/env bash
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

BASE_URL="${BASE_URL:-http://localhost:8080}"
EXPECTED_BRANCH="${EXPECTED_BRANCH:-}"
EXPECTED_HEAD="${EXPECTED_HEAD:-}"
PASSWORD='CidadeEmDia#SecurityE2E2026!'
RUN_ID="$(date +%s)-$RANDOM"

CITIZEN_A_EMAIL="security-citizen-a-${RUN_ID}@example.invalid"
CITIZEN_B_EMAIL="security-citizen-b-${RUN_ID}@example.invalid"
MASTER_A_EMAIL="security-master-a-${RUN_ID}@example.invalid"
MASTER_B_EMAIL="security-master-b-${RUN_ID}@example.invalid"
SUB_EMAIL="security-sub-${RUN_ID}@example.invalid"

CATEGORY_ID="$(python3 -c 'import uuid; print(uuid.uuid4())')"
CATEGORY_SLUG="security-horizontal-${RUN_ID}"

TMP_PREFIX="/tmp/cidademdia-security-${RUN_ID}"

compose() {
  docker compose \
    --env-file .env \
    -f infra/docker-compose.yml \
    "$@"
}

db_scalar() {
  local sql="$1"

  compose exec -T db sh -lc \
    'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atc "$1"' \
    sh "$sql"
}

json_value() {
  python3 - "$1" "$2" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as f:
    value = json.load(f)

for part in sys.argv[2].split("."):
    value = value[part]

if value is None:
    print("")
elif isinstance(value, bool):
    print(str(value).lower())
else:
    print(value)
PY
}

problem_code() {
  python3 - "$1" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as f:
    data = json.load(f)

print(data.get("code") or data.get("error") or "")
PY
}

register_user() {
  local email="$1"
  local name="$2"
  local output="$3"
  local headers="${4:-}"

  local args=(
    -sS
    -o "$output"
    -w '%{http_code}'
    -X POST
    "$BASE_URL/api/v1/auth/register"
    -H 'Content-Type: application/json'
    -d "{\"email\":\"$email\",\"password\":\"$PASSWORD\",\"displayName\":\"$name\"}"
  )

  if [ -n "$headers" ]; then
    args=(-sS -D "$headers" -o "$output" -w '%{http_code}' -X POST "$BASE_URL/api/v1/auth/register" -H 'Content-Type: application/json' -d "{\"email\":\"$email\",\"password\":\"$PASSWORD\",\"displayName\":\"$name\"}")
  fi

  curl "${args[@]}"
}

login_user() {
  local email="$1"
  local output="$2"

  curl -sS \
    -o "$output" \
    -w '%{http_code}' \
    -X POST \
    "$BASE_URL/api/v1/auth/login" \
    -H 'Content-Type: application/json' \
    -d "{\"email\":\"$email\",\"password\":\"$PASSWORD\"}"
}

extract_refresh_cookie() {
  local headers="$1"

  python3 - "$headers" <<'PY'
import re
import sys

text = open(sys.argv[1], encoding="utf-8").read()
match = re.search(r"(?im)^set-cookie:\s*cidademdia_refresh=([^;\r\n]+)", text)
print(match.group(1) if match else "")
PY
}

assert_problem_code() {
  local file="$1"
  local expected="$2"
  local actual
  actual="$(problem_code "$file")"
  echo "Code=$actual"
  [ "$actual" = "$expected" ]
}

show_fixture_ids_on_error() {
  local exit_code=$?
  if [ "$exit_code" -ne 0 ]; then
    echo
    echo "=== SECURITY E2E INTERROMPIDO ==="
    echo "Os fixtures não foram removidos automaticamente para permitir diagnóstico controlado."
    echo "CITIZEN_A_ID=${CITIZEN_A_ID:-}"
    echo "CITIZEN_B_ID=${CITIZEN_B_ID:-}"
    echo "MASTER_A_ID=${MASTER_A_ID:-}"
    echo "MASTER_B_ID=${MASTER_B_ID:-}"
    echo "SUB_ID=${SUB_ID:-}"
    echo "SUB_LINK=${SUB_LINK:-}"
    echo "CATEGORY_ID=$CATEGORY_ID"
    echo "OCCURRENCE_ID=${OCCURRENCE_ID:-}"
    echo "TARGET_A_ID=${TARGET_A_ID:-}"
    echo "TARGET_B_ID=${TARGET_B_ID:-}"
    echo "CONVERSATION_ID=${CONVERSATION_ID:-}"
  fi
  exit "$exit_code"
}
trap show_fixture_ids_on_error EXIT

echo "=== 1. GIT ==="
echo "BRANCH=$(git branch --show-current)"
echo "HEAD=$(git rev-parse HEAD)"

if [ -n "$EXPECTED_BRANCH" ]; then
  [ "$(git branch --show-current)" = "$EXPECTED_BRANCH" ]
fi

if [ -n "$EXPECTED_HEAD" ]; then
  [ "$(git rev-parse HEAD)" = "$EXPECTED_HEAD" ]
fi

if [ -n "$(git status --porcelain)" ]; then
  echo "ERRO: worktree não está limpo."
  git status --short
  exit 1
fi

echo "Git: OK"

echo
echo "=== 2. SMOKE INICIAL ==="
./infra/scripts/smoke-test.sh

echo
echo "=== 3. REGISTRANDO IDENTIDADES DE SEGURANÇA ==="

HTTP="$(register_user "$CITIZEN_A_EMAIL" "Security Citizen A" "${TMP_PREFIX}-citizen-a.json" "${TMP_PREFIX}-citizen-a.headers")"
echo "Citizen A HTTP=$HTTP"
[ "$HTTP" = "201" ]

HTTP="$(register_user "$CITIZEN_B_EMAIL" "Security Citizen B" "${TMP_PREFIX}-citizen-b.json")"
echo "Citizen B HTTP=$HTTP"
[ "$HTTP" = "201" ]

HTTP="$(register_user "$MASTER_A_EMAIL" "Security Master A" "${TMP_PREFIX}-master-a.json")"
echo "Master A HTTP=$HTTP"
[ "$HTTP" = "201" ]

HTTP="$(register_user "$MASTER_B_EMAIL" "Security Master B" "${TMP_PREFIX}-master-b.json")"
echo "Master B HTTP=$HTTP"
[ "$HTTP" = "201" ]

HTTP="$(register_user "$SUB_EMAIL" "Security Subaccount" "${TMP_PREFIX}-sub.json")"
echo "Subaccount HTTP=$HTTP"
[ "$HTTP" = "201" ]

CITIZEN_A_ID="$(json_value "${TMP_PREFIX}-citizen-a.json" user.id)"
CITIZEN_B_ID="$(json_value "${TMP_PREFIX}-citizen-b.json" user.id)"
MASTER_A_ID="$(json_value "${TMP_PREFIX}-master-a.json" user.id)"
MASTER_B_ID="$(json_value "${TMP_PREFIX}-master-b.json" user.id)"
SUB_ID="$(json_value "${TMP_PREFIX}-sub.json" user.id)"

CITIZEN_A_TOKEN="$(json_value "${TMP_PREFIX}-citizen-a.json" accessToken)"
CITIZEN_B_TOKEN="$(json_value "${TMP_PREFIX}-citizen-b.json" accessToken)"

REFRESH_TOKEN="$(extract_refresh_cookie "${TMP_PREFIX}-citizen-a.headers")"
[ -n "$REFRESH_TOKEN" ]

echo "CITIZEN_A_ID=$CITIZEN_A_ID"
echo "CITIZEN_B_ID=$CITIZEN_B_ID"
echo "MASTER_A_ID=$MASTER_A_ID"
echo "MASTER_B_ID=$MASTER_B_ID"
echo "SUB_ID=$SUB_ID"
echo "Identidades: OK"

echo
echo "=== 4. REFRESH TOKEN ROTACIONA E REVOGA NO LOGOUT ==="

REFRESH_HTTP="$(
  curl -sS \
    -D "${TMP_PREFIX}-refresh.headers" \
    -o "${TMP_PREFIX}-refresh.json" \
    -w '%{http_code}' \
    -X POST \
    "$BASE_URL/api/v1/auth/refresh" \
    -H "Cookie: cidademdia_refresh=$REFRESH_TOKEN"
)"

echo "Refresh HTTP=$REFRESH_HTTP"
[ "$REFRESH_HTTP" = "200" ]

ROTATED_REFRESH_TOKEN="$(extract_refresh_cookie "${TMP_PREFIX}-refresh.headers")"
[ -n "$ROTATED_REFRESH_TOKEN" ]
[ "$ROTATED_REFRESH_TOKEN" != "$REFRESH_TOKEN" ]

echo "Rotação de refresh token: OK"

LOGOUT_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-logout.body" \
    -w '%{http_code}' \
    -X POST \
    "$BASE_URL/api/v1/auth/logout" \
    -H "Cookie: cidademdia_refresh=$ROTATED_REFRESH_TOKEN"
)"

echo "Logout HTTP=$LOGOUT_HTTP"
[ "$LOGOUT_HTTP" = "204" ]

REVOKED_REFRESH_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-refresh-revoked.json" \
    -w '%{http_code}' \
    -X POST \
    "$BASE_URL/api/v1/auth/refresh" \
    -H "Cookie: cidademdia_refresh=$ROTATED_REFRESH_TOKEN"
)"

echo "Refresh revogado HTTP=$REVOKED_REFRESH_HTTP"
[ "$REVOKED_REFRESH_HTTP" = "401" ]

echo "Refresh revogado não reutilizável: OK"

echo
echo "=== 5. PROMOVENDO DUAS MASTERS ==="

compose exec -T db sh -lc \
  'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' <<SQL
INSERT INTO user_roles (user_id, role_id, created_at)
SELECT '$MASTER_A_ID', id, NOW() FROM roles WHERE key='MASTER'
ON CONFLICT DO NOTHING;

INSERT INTO user_roles (user_id, role_id, created_at)
SELECT '$MASTER_B_ID', id, NOW() FROM roles WHERE key='MASTER'
ON CONFLICT DO NOTHING;
SQL

HTTP="$(login_user "$MASTER_A_EMAIL" "${TMP_PREFIX}-master-a-login.json")"
echo "Master A login HTTP=$HTTP"
[ "$HTTP" = "200" ]

HTTP="$(login_user "$MASTER_B_EMAIL" "${TMP_PREFIX}-master-b-login.json")"
echo "Master B login HTTP=$HTTP"
[ "$HTTP" = "200" ]

MASTER_A_TOKEN="$(json_value "${TMP_PREFIX}-master-a-login.json" accessToken)"
MASTER_B_TOKEN="$(json_value "${TMP_PREFIX}-master-b-login.json" accessToken)"

echo "Masters: OK"

echo
echo "=== 6. POLÍTICA ADMIN BLOQUEIA PERFIS COMUNS ==="

ADMIN_CITIZEN_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-admin-citizen.json" \
    -w '%{http_code}' \
    "$BASE_URL/api/v1/admin/status" \
    -H "Authorization: Bearer $CITIZEN_B_TOKEN"
)"

ADMIN_MASTER_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-admin-master.json" \
    -w '%{http_code}' \
    "$BASE_URL/api/v1/admin/status" \
    -H "Authorization: Bearer $MASTER_A_TOKEN"
)"

echo "Citizen admin HTTP=$ADMIN_CITIZEN_HTTP"
echo "Master admin HTTP=$ADMIN_MASTER_HTTP"
[ "$ADMIN_CITIZEN_HTTP" = "403" ]
[ "$ADMIN_MASTER_HTTP" = "403" ]

echo "Admin policy: OK"

echo
echo "=== 7. CATEGORIA E OCORRÊNCIA DO CIDADÃO A ==="

compose exec -T db sh -lc \
  'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' <<SQL
INSERT INTO occurrence_categories (
  id, name, slug, status, display_order, created_at, updated_at
)
VALUES (
  '$CATEGORY_ID',
  'Security Horizontal E2E',
  '$CATEGORY_SLUG',
  'ACTIVE',
  999,
  NOW(),
  NOW()
);
SQL

CREATE_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-occurrence.json" \
    -w '%{http_code}' \
    -X POST \
    "$BASE_URL/api/v1/occurrences" \
    -H 'Content-Type: application/json' \
    -H "Authorization: Bearer $CITIZEN_A_TOKEN" \
    -d "{
      \"categoryId\":\"$CATEGORY_ID\",
      \"title\":\"Security Horizontal E2E\",
      \"description\":\"Fixture de autorização horizontal.\",
      \"addressText\":\"Praça da Matriz, Porto Alegre - RS\",
      \"latitude\":-30.0331,
      \"longitude\":-51.2300,
      \"postalCode\":\"90010-150\",
      \"cityId\":null,
      \"stateCode\":\"RS\",
      \"externalProtocolNumber\":null,
      \"externalProtocolAgency\":null,
      \"mediaIds\":null
    }"
)"

echo "Occurrence create HTTP=$CREATE_HTTP"
[ "$CREATE_HTTP" = "201" ]

OCCURRENCE_ID="$(json_value "${TMP_PREFIX}-occurrence.json" id)"
PUBLIC_CODE="$(json_value "${TMP_PREFIX}-occurrence.json" publicCode)"

echo "OCCURRENCE_ID=$OCCURRENCE_ID"
echo "PUBLIC_CODE=$PUBLIC_CODE"

echo
echo "=== 8. CIDADÃO B NÃO ENXERGA NEM MUTA A OCORRÊNCIA DE A ==="

READ_B_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-citizen-b-read.json" \
    -w '%{http_code}' \
    "$BASE_URL/api/v1/occurrences/$OCCURRENCE_ID" \
    -H "Authorization: Bearer $CITIZEN_B_TOKEN"
)"

echo "Read by id HTTP=$READ_B_HTTP"
[ "$READ_B_HTTP" = "404" ]

READ_CODE_B_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-citizen-b-code.json" \
    -w '%{http_code}' \
    "$BASE_URL/api/v1/occurrences/by-code/$PUBLIC_CODE" \
    -H "Authorization: Bearer $CITIZEN_B_TOKEN"
)"

echo "Read by public code HTTP=$READ_CODE_B_HTTP"
[ "$READ_CODE_B_HTTP" = "404" ]

TARGETS_B_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-citizen-b-targets.json" \
    -w '%{http_code}' \
    "$BASE_URL/api/v1/occurrences/$OCCURRENCE_ID/targets" \
    -H "Authorization: Bearer $CITIZEN_B_TOKEN"
)"

echo "Targets HTTP=$TARGETS_B_HTTP"
[ "$TARGETS_B_HTTP" = "404" ]

ADD_TARGET_B_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-citizen-b-add-target.json" \
    -w '%{http_code}' \
    -X POST \
    "$BASE_URL/api/v1/occurrences/$OCCURRENCE_ID/targets" \
    -H 'Content-Type: application/json' \
    -H "Authorization: Bearer $CITIZEN_B_TOKEN" \
    -d "{\"masterUserId\":\"$MASTER_A_ID\"}"
)"

echo "Foreign target mutation HTTP=$ADD_TARGET_B_HTTP"
[ "$ADD_TARGET_B_HTTP" = "404" ]
assert_problem_code "${TMP_PREFIX}-citizen-b-add-target.json" "occurrence_not_found"

echo "Citizen A→B horizontal isolation: OK"

echo
echo "=== 9. CIDADÃO A CRIA TARGETS PARA MASTER A E B ==="

TARGET_A_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-target-a.json" \
    -w '%{http_code}' \
    -X POST \
    "$BASE_URL/api/v1/occurrences/$OCCURRENCE_ID/targets" \
    -H 'Content-Type: application/json' \
    -H "Authorization: Bearer $CITIZEN_A_TOKEN" \
    -d "{\"masterUserId\":\"$MASTER_A_ID\"}"
)"

echo "Target A HTTP=$TARGET_A_HTTP"
[ "$TARGET_A_HTTP" = "201" ]

TARGET_B_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-target-b.json" \
    -w '%{http_code}' \
    -X POST \
    "$BASE_URL/api/v1/occurrences/$OCCURRENCE_ID/targets" \
    -H 'Content-Type: application/json' \
    -H "Authorization: Bearer $CITIZEN_A_TOKEN" \
    -d "{\"masterUserId\":\"$MASTER_B_ID\"}"
)"

echo "Target B HTTP=$TARGET_B_HTTP"
[ "$TARGET_B_HTTP" = "201" ]

TARGET_A_ID="$(json_value "${TMP_PREFIX}-target-a.json" id)"
TARGET_B_ID="$(json_value "${TMP_PREFIX}-target-b.json" id)"

echo "TARGET_A_ID=$TARGET_A_ID"
echo "TARGET_B_ID=$TARGET_B_ID"

echo
echo "=== 10. MASTER B NÃO PODE DECIDIR TARGET DE MASTER A ==="

MASTER_B_GET_A_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-master-b-get-a.json" \
    -w '%{http_code}' \
    "$BASE_URL/api/v1/occurrences/$OCCURRENCE_ID/targets/$TARGET_A_ID" \
    -H "Authorization: Bearer $MASTER_B_TOKEN"
)"

echo "Foreign target read HTTP=$MASTER_B_GET_A_HTTP"
[ "$MASTER_B_GET_A_HTTP" = "404" ]

MASTER_B_ACCEPT_A_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-master-b-accept-a.json" \
    -w '%{http_code}' \
    -X POST \
    "$BASE_URL/api/v1/occurrences/$OCCURRENCE_ID/targets/$TARGET_A_ID/accept" \
    -H "Authorization: Bearer $MASTER_B_TOKEN"
)"

echo "Foreign accept HTTP=$MASTER_B_ACCEPT_A_HTTP"
[ "$MASTER_B_ACCEPT_A_HTTP" = "404" ]
assert_problem_code "${TMP_PREFIX}-master-b-accept-a.json" "target_not_found"

echo "Master A→B horizontal isolation: OK"

echo
echo "=== 11. MASTER A NÃO PODE DECIDIR TARGET DE MASTER B ==="

MASTER_A_REJECT_B_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-master-a-reject-b.json" \
    -w '%{http_code}' \
    -X POST \
    "$BASE_URL/api/v1/occurrences/$OCCURRENCE_ID/targets/$TARGET_B_ID/reject" \
    -H 'Content-Type: application/json' \
    -H "Authorization: Bearer $MASTER_A_TOKEN" \
    -d '{"reason":"Tentativa horizontal deve falhar"}'
)"

echo "Foreign reject HTTP=$MASTER_A_REJECT_B_HTTP"
[ "$MASTER_A_REJECT_B_HTTP" = "404" ]
assert_problem_code "${TMP_PREFIX}-master-a-reject-b.json" "target_not_found"

echo "Master B→A horizontal isolation: OK"

echo
echo "=== 12. MASTER A ACEITA SOMENTE O PRÓPRIO TARGET ==="

ACCEPT_A_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-accept-a.json" \
    -w '%{http_code}' \
    -X POST \
    "$BASE_URL/api/v1/occurrences/$OCCURRENCE_ID/targets/$TARGET_A_ID/accept" \
    -H "Authorization: Bearer $MASTER_A_TOKEN"
)"

echo "Own accept HTTP=$ACCEPT_A_HTTP"
[ "$ACCEPT_A_HTTP" = "200" ]

echo "Own target decision: OK"

echo
echo "=== 13. CHAT NÃO VAZA PARA CIDADÃO OU MASTER ESTRANHOS ==="

MASTER_CHAT_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-master-a-chat.json" \
    -w '%{http_code}' \
    "$BASE_URL/api/v1/chat/targets/$TARGET_A_ID/conversation" \
    -H "Authorization: Bearer $MASTER_A_TOKEN"
)"

echo "Master A chat HTTP=$MASTER_CHAT_HTTP"
[ "$MASTER_CHAT_HTTP" = "200" ]

CONVERSATION_ID="$(json_value "${TMP_PREFIX}-master-a-chat.json" id)"
echo "CONVERSATION_ID=$CONVERSATION_ID"

CITIZEN_B_CHAT_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-citizen-b-chat.json" \
    -w '%{http_code}' \
    "$BASE_URL/api/v1/chat/targets/$TARGET_A_ID/conversation" \
    -H "Authorization: Bearer $CITIZEN_B_TOKEN"
)"

echo "Citizen B chat HTTP=$CITIZEN_B_CHAT_HTTP"
[ "$CITIZEN_B_CHAT_HTTP" = "403" ]
assert_problem_code "${TMP_PREFIX}-citizen-b-chat.json" "chat_access_denied"

MASTER_B_CHAT_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-master-b-chat.json" \
    -w '%{http_code}' \
    "$BASE_URL/api/v1/chat/targets/$TARGET_A_ID/conversation" \
    -H "Authorization: Bearer $MASTER_B_TOKEN"
)"

echo "Master B chat HTTP=$MASTER_B_CHAT_HTTP"
[ "$MASTER_B_CHAT_HTTP" = "403" ]
assert_problem_code "${TMP_PREFIX}-master-b-chat.json" "chat_access_denied"

echo "Chat horizontal isolation: OK"

echo
echo "=== 14. SUBCONTA COM PERMISSION, MAS SEM ASSIGNMENT, CONTINUA BLOQUEADA ==="

LINK_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-sub-link.json" \
    -w '%{http_code}' \
    -X POST \
    "$BASE_URL/api/v1/master/subaccounts" \
    -H 'Content-Type: application/json' \
    -H "Authorization: Bearer $MASTER_A_TOKEN" \
    -d "{
      \"email\":\"$SUB_EMAIL\",
      \"permissions\":[\"occurrence.read.targeted\",\"chat.read\",\"chat.message.send\"]
    }"
)"

echo "Subaccount link HTTP=$LINK_HTTP"
[ "$LINK_HTTP" = "201" ]

SUB_LINK="$(json_value "${TMP_PREFIX}-sub-link.json" linkId)"
echo "SUB_LINK=$SUB_LINK"

SUB_LOGIN_HTTP="$(login_user "$SUB_EMAIL" "${TMP_PREFIX}-sub-login.json")"
echo "Subaccount login HTTP=$SUB_LOGIN_HTTP"
[ "$SUB_LOGIN_HTTP" = "200" ]

SUB_TOKEN="$(json_value "${TMP_PREFIX}-sub-login.json" accessToken)"

SUB_LIST_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-sub-list.json" \
    -w '%{http_code}' \
    "$BASE_URL/api/v1/subaccount/occurrence-assignments" \
    -H "Authorization: Bearer $SUB_TOKEN"
)"

echo "Subaccount assignment list HTTP=$SUB_LIST_HTTP"
[ "$SUB_LIST_HTTP" = "200" ]

python3 - "${TMP_PREFIX}-sub-list.json" <<'PY'
import json
import sys
with open(sys.argv[1], encoding="utf-8") as f:
    data = json.load(f)
assert data == [], data
print("Subaccount assignment list vazia: OK")
PY

SUB_CHAT_HTTP="$(
  curl -sS \
    -o "${TMP_PREFIX}-sub-chat.json" \
    -w '%{http_code}' \
    "$BASE_URL/api/v1/chat/targets/$TARGET_A_ID/conversation" \
    -H "Authorization: Bearer $SUB_TOKEN"
)"

echo "Subaccount chat HTTP=$SUB_CHAT_HTTP"
[ "$SUB_CHAT_HTTP" = "403" ]
assert_problem_code "${TMP_PREFIX}-sub-chat.json" "chat_access_denied"

echo "Permission sem assignment não concede acesso: OK"

echo
echo "=== 15. CONSISTÊNCIA ANTES DO CLEANUP ==="

STATE="$(
  db_scalar "
    SELECT
      (SELECT COUNT(*) FROM occurrences WHERE id='$OCCURRENCE_ID')
      || '|' ||
      (SELECT COUNT(*) FROM occurrence_targets WHERE id IN ('$TARGET_A_ID','$TARGET_B_ID'))
      || '|' ||
      (SELECT COUNT(*) FROM chat_conversations WHERE id='$CONVERSATION_ID')
      || '|' ||
      (SELECT COUNT(*) FROM master_subaccounts WHERE \"Id\"='$SUB_LINK')
      || '|' ||
      (SELECT COUNT(*) FROM users WHERE id IN ('$CITIZEN_A_ID','$CITIZEN_B_ID','$MASTER_A_ID','$MASTER_B_ID','$SUB_ID'));
  "
)"

echo "occurrence|targets|chat|sub_link|users=$STATE"
[ "$STATE" = "1|2|1|1|5" ]

echo "Fixtures consistentes: OK"

echo
echo "=== 16. CLEANUP ==="

compose exec -T db sh -lc \
  'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' <<SQL
BEGIN;

DELETE FROM occurrences
WHERE id='$OCCURRENCE_ID';

DELETE FROM master_subaccounts
WHERE "Id"='$SUB_LINK';

DELETE FROM occurrence_categories
WHERE id='$CATEGORY_ID';

DELETE FROM users
WHERE id IN (
  '$CITIZEN_A_ID',
  '$CITIZEN_B_ID',
  '$MASTER_A_ID',
  '$MASTER_B_ID',
  '$SUB_ID'
);

COMMIT;
SQL

AFTER="$(
  db_scalar "
    SELECT
      (SELECT COUNT(*) FROM occurrences WHERE id='$OCCURRENCE_ID')
      || '|' ||
      (SELECT COUNT(*) FROM occurrence_targets WHERE id IN ('$TARGET_A_ID','$TARGET_B_ID'))
      || '|' ||
      (SELECT COUNT(*) FROM chat_conversations WHERE id='$CONVERSATION_ID')
      || '|' ||
      (SELECT COUNT(*) FROM master_subaccounts WHERE \"Id\"='$SUB_LINK')
      || '|' ||
      (SELECT COUNT(*) FROM occurrence_categories WHERE id='$CATEGORY_ID')
      || '|' ||
      (SELECT COUNT(*) FROM users WHERE id IN ('$CITIZEN_A_ID','$CITIZEN_B_ID','$MASTER_A_ID','$MASTER_B_ID','$SUB_ID'));
  "
)"

echo "occurrence|targets|chat|sub_link|category|users=$AFTER"
[ "$AFTER" = "0|0|0|0|0|0" ]

echo "Fixtures removidos: OK"

echo
echo "=== 17. TEMP FILES ==="
rm -f "${TMP_PREFIX}"-*
echo "Arquivos temporários removidos: OK"

echo
echo "=== 18. SMOKE FINAL ==="
./infra/scripts/smoke-test.sh

echo
echo "=== 19. GIT FINAL ==="
echo "BRANCH=$(git branch --show-current)"
echo "HEAD=$(git rev-parse HEAD)"

if [ -n "$EXPECTED_BRANCH" ]; then
  [ "$(git branch --show-current)" = "$EXPECTED_BRANCH" ]
fi

if [ -n "$EXPECTED_HEAD" ]; then
  [ "$(git rev-parse HEAD)" = "$EXPECTED_HEAD" ]
fi

if [ -z "$(git status --porcelain)" ]; then
  echo "WORKTREE=CLEAN"
else
  echo "WORKTREE=DIRTY"
  git status --short
  exit 1
fi

trap - EXIT

echo
echo "=== SECURITY HORIZONTAL AUTHORIZATION OK ==="
