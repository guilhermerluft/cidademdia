#!/usr/bin/env bash
set -Eeuo pipefail

EXPECTED_HEAD="${1:-}"
ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
ENV_FILE="${CIDADEMDIA_ENV_FILE:-$ROOT/.env}"
BASE="${CIDADEMDIA_BASE_URL:-https://homolog.cidademdia.com.br}"
WT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
RUN_TAG="CHAT-AUDIO-SMOKE-$(date +%s)-$$"
TMP_DIR="$(mktemp -d)"
OCCURRENCE_ID=""
CONVERSATION_ID=""
AUDIO_MEDIA_ID=""
R2_OBJECT_KEY=""

fail() {
  echo
  echo "ERRO: $*" >&2
  exit 1
}

root_compose() {
  docker compose --env-file "$ENV_FILE" -f "$ROOT/infra/docker-compose.yml" "$@"
}

CURRENT_WEB_ID="$(root_compose ps -q web)"
test -n "$CURRENT_WEB_ID" || fail "container web atual não encontrado"
PROJECT="$(docker inspect "$CURRENT_WEB_ID" --format '{{ index .Config.Labels "com.docker.compose.project" }}')"
test -n "$PROJECT" || fail "compose project não encontrado"

compose() {
  docker compose -p "$PROJECT" --env-file "$ENV_FILE" -f "$WT/infra/docker-compose.yml" "$@"
}

dbq() {
  root_compose exec -T db sh -lc \
    'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At' \
    <<< "$1"
}

dbexec() {
  root_compose exec -T db sh -lc \
    'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' \
    <<< "$1"
}

env_value() {
  local key="$1"
  awk -F= -v key="$key" '$1 == key { sub(/^[^=]*=/, ""); print; exit }' "$ENV_FILE"
}

delete_r2_object() {
  local object_key="$1"
  test -n "$object_key" || return 0

  local account_id access_key secret_key bucket delete_url
  account_id="$(env_value R2_ACCOUNT_ID)"
  access_key="$(env_value R2_ACCESS_KEY_ID)"
  secret_key="$(env_value R2_SECRET_ACCESS_KEY)"
  bucket="$(env_value R2_BUCKET)"

  if [ -z "$account_id" ] || [ -z "$access_key" ] || [ -z "$secret_key" ] || [ -z "$bucket" ]; then
    return 0
  fi

  delete_url="$(
    R2_ACCOUNT_ID="$account_id" \
    R2_ACCESS_KEY_ID="$access_key" \
    R2_SECRET_ACCESS_KEY="$secret_key" \
    R2_BUCKET="$bucket" \
    R2_OBJECT_KEY="$object_key" \
    python3 - <<'PY'
import datetime, hashlib, hmac, os, urllib.parse

def enc(value):
    return urllib.parse.quote(str(value), safe='-_.~')

def hmac_bytes(key, value):
    return hmac.new(key, value.encode(), hashlib.sha256).digest()

account = os.environ['R2_ACCOUNT_ID']
access = os.environ['R2_ACCESS_KEY_ID']
secret = os.environ['R2_SECRET_ACCESS_KEY']
bucket = os.environ['R2_BUCKET']
object_key = os.environ['R2_OBJECT_KEY']
now = datetime.datetime.now(datetime.timezone.utc)
stamp = now.strftime('%Y%m%dT%H%M%SZ')
date = now.strftime('%Y%m%d')
region = 'auto'
service = 's3'
host = f'{account}.r2.cloudflarestorage.com'
credential_scope = f'{date}/{region}/{service}/aws4_request'
path = '/' + enc(bucket) + '/' + '/'.join(enc(part) for part in object_key.split('/') if part)
params = {
    'X-Amz-Algorithm': 'AWS4-HMAC-SHA256',
    'X-Amz-Credential': f'{access}/{credential_scope}',
    'X-Amz-Date': stamp,
    'X-Amz-Expires': '120',
    'X-Amz-SignedHeaders': 'host',
}
query = '&'.join(f'{enc(k)}={enc(v)}' for k, v in sorted(params.items()))
canonical = '\n'.join(['DELETE', path, query, f'host:{host}\n', 'host', 'UNSIGNED-PAYLOAD'])
string_to_sign = '\n'.join([
    'AWS4-HMAC-SHA256',
    stamp,
    credential_scope,
    hashlib.sha256(canonical.encode()).hexdigest(),
])
k_date = hmac_bytes(('AWS4' + secret).encode(), date)
k_region = hmac_bytes(k_date, region)
k_service = hmac_bytes(k_region, service)
k_signing = hmac_bytes(k_service, 'aws4_request')
signature = hmac.new(k_signing, string_to_sign.encode(), hashlib.sha256).hexdigest()
print(f'https://{host}{path}?{query}&X-Amz-Signature={signature}')
PY
  )"

  curl -fsS -X DELETE "$delete_url" -o /dev/null 2>/dev/null || true
}

cleanup() {
  set +e
  if [ -n "$R2_OBJECT_KEY" ]; then
    delete_r2_object "$R2_OBJECT_KEY"
  fi

  if [ -n "$OCCURRENCE_ID" ]; then
    dbexec "
      DELETE FROM chat_messages
      WHERE conversation_id IN (
        SELECT id FROM chat_conversations WHERE occurrence_id = '$OCCURRENCE_ID'::uuid
      );
      DELETE FROM chat_participants
      WHERE conversation_id IN (
        SELECT id FROM chat_conversations WHERE occurrence_id = '$OCCURRENCE_ID'::uuid
      );
      DELETE FROM chat_conversations WHERE occurrence_id = '$OCCURRENCE_ID'::uuid;
      DELETE FROM occurrence_target_assignments
      WHERE occurrence_target_id IN (
        SELECT id FROM occurrence_targets WHERE occurrence_id = '$OCCURRENCE_ID'::uuid
      );
      DELETE FROM occurrence_targets WHERE occurrence_id = '$OCCURRENCE_ID'::uuid;
      DELETE FROM occurrence_status_history WHERE occurrence_id = '$OCCURRENCE_ID'::uuid;
      DELETE FROM occurrence_complements WHERE occurrence_id = '$OCCURRENCE_ID'::uuid;
      DELETE FROM occurrence_service_forecasts WHERE occurrence_id = '$OCCURRENCE_ID'::uuid;
      DELETE FROM occurrence_media WHERE occurrence_id = '$OCCURRENCE_ID'::uuid;
      DELETE FROM occurrences WHERE id = '$OCCURRENCE_ID'::uuid;
    " >/dev/null 2>&1 || true
  fi

  rm -rf "$TMP_DIR"
}

trap cleanup EXIT

make_token() {
  local user_id="$1"
  USER_ID="$user_id" \
  JWT_SIGNING_KEY="$JWT_SIGNING_KEY" \
  JWT_ISSUER="$JWT_ISSUER" \
  JWT_AUDIENCE="$JWT_AUDIENCE" \
  python3 - <<'PY'
import os, json, time, uuid, hmac, hashlib, base64

def enc(value):
    return base64.urlsafe_b64encode(value).rstrip(b'=').decode()

now = int(time.time())
header = {'alg': 'HS256', 'typ': 'JWT'}
payload = {
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier': os.environ['USER_ID'],
    'jti': uuid.uuid4().hex,
    'iss': os.environ['JWT_ISSUER'],
    'aud': os.environ['JWT_AUDIENCE'],
    'nbf': now - 5,
    'iat': now,
    'exp': now + 1800,
}
h = enc(json.dumps(header, separators=(',', ':')).encode())
p = enc(json.dumps(payload, separators=(',', ':')).encode())
unsigned = h + '.' + p
signature = hmac.new(os.environ['JWT_SIGNING_KEY'].encode(), unsigned.encode(), hashlib.sha256).digest()
print(unsigned + '.' + enc(signature))
PY
}

json_field() {
  local path="$1" field="$2"
  python3 - "$path" "$field" <<'PY'
import json, sys
path, field = sys.argv[1:]
with open(path, encoding='utf-8') as f:
    value = json.load(f)
for part in field.split('.'):
    value = value[part]
print(value)
PY
}

echo "============================================================"
echo "CHAT AUDIO / R2 — FEATURE HOMOLOG"
echo "============================================================"

test -d "$ROOT/.git" || fail "repo principal não encontrado"
test -f "$ENV_FILE" || fail ".env não encontrado"
test -n "$EXPECTED_HEAD" || fail "informe o HEAD esperado como primeiro argumento"
test "$(git -C "$WT" rev-parse HEAD)" = "$EXPECTED_HEAD" || fail "worktree não está no HEAD esperado"
test "$(git -C "$ROOT" branch --show-current)" = "main" || fail "repo principal não está na main"
test -z "$(git -C "$ROOT" status --porcelain)" || fail "main local está suja"

echo "HEAD: $EXPECTED_HEAD"
echo "MAIN: $(git -C "$ROOT" rev-parse HEAD)"

echo
echo "=== 1. CONFIGURAÇÃO R2 ==="
for key in R2_ACCOUNT_ID R2_ACCESS_KEY_ID R2_SECRET_ACCESS_KEY R2_BUCKET; do
  test -n "$(env_value "$key")" || fail "$key ausente"
done
echo "r2_config=OK"

echo
echo "=== 2. MERCADO PAGO ANTES ==="
PROVIDER_BEFORE="$(dbq "
  SELECT
    (SELECT count(*) FROM billing_provider_subscriptions)::text || '|' ||
    (SELECT count(*) FROM payments)::text || '|' ||
    (SELECT count(*) FROM payment_events)::text;
")"
echo "provider_before=$PROVIDER_BEFORE"
test "$PROVIDER_BEFORE" = "0|0|0" || fail "provider state inesperado"

echo
echo "=== 3. BUILD / DEPLOY FEATURE ==="
compose build api web
compose up -d --no-deps api web
compose restart nginx
sleep 3

READY=0
for _ in $(seq 1 40); do
  HEALTH_CODE="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/health/ready" || true)"
  WEB_CODE="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/" || true)"
  if [ "$HEALTH_CODE" = "200" ] && [ "$WEB_CODE" = "200" ]; then
    READY=1
    break
  fi
  sleep 2
done
test "$READY" = "1" || fail "health/web não ficaram prontos"
echo "health=200"
echo "web=200"

echo
echo "=== 4. BUNDLE CHAT AUDIO ==="
WEB_ID="$(compose ps -q web)"
test -n "$WEB_ID" || fail "web feature não encontrado"
docker exec "$WEB_ID" sh -lc 'grep -R -q "audio/uploads" /usr/share/nginx/html/assets' || fail "endpoint audio ausente do bundle"
docker exec "$WEB_ID" sh -lc 'grep -R -q "MediaRecorder" /usr/share/nginx/html/assets' || fail "gravação MediaRecorder ausente do bundle"
echo "chat_audio_bundle=OK"

API_ID="$(compose ps -q api)"
test -n "$API_ID" || fail "api feature não encontrada"
JWT_SIGNING_KEY="$(docker exec "$API_ID" printenv JWT_SIGNING_KEY)"
JWT_ISSUER="$(docker exec "$API_ID" printenv JWT_ISSUER)"
JWT_AUDIENCE="$(docker exec "$API_ID" printenv JWT_AUDIENCE)"
test "${#JWT_SIGNING_KEY}" -ge 32 || fail "JWT signing key inválida"

echo
echo "=== 5. ATORES DE QA ==="
CITIZEN_ROW="$(dbq "
  SELECT u.id::text || '|' || u.email
  FROM users u
  JOIN user_roles ur ON ur.user_id = u.id
  JOIN roles r ON r.id = ur.role_id
  WHERE u.status = 'Active'
    AND r.key = 'CITIZEN'
    AND NOT EXISTS (
      SELECT 1 FROM user_roles umr
      JOIN roles mr ON mr.id = umr.role_id
      WHERE umr.user_id = u.id AND mr.key = 'MASTER'
    )
  ORDER BY u.created_at
  LIMIT 1;
")"
test -n "$CITIZEN_ROW" || fail "cidadão Active exclusivo não encontrado"
CITIZEN_ID="${CITIZEN_ROW%%|*}"
CITIZEN_EMAIL="${CITIZEN_ROW#*|}"

MASTER_ROW="$(dbq "
  SELECT u.id::text || '|' || u.email
  FROM users u
  JOIN user_roles ur ON ur.user_id = u.id
  JOIN roles r ON r.id = ur.role_id
  WHERE u.status = 'Active' AND r.key = 'MASTER'
  ORDER BY u.created_at
  LIMIT 1;
")"
test -n "$MASTER_ROW" || fail "Master Active não encontrado"
MASTER_ID="${MASTER_ROW%%|*}"
MASTER_EMAIL="${MASTER_ROW#*|}"

EXTERNAL_ROW="$(dbq "
  SELECT id::text || '|' || email
  FROM users
  WHERE status = 'Active'
    AND id <> '$CITIZEN_ID'::uuid
    AND id <> '$MASTER_ID'::uuid
  ORDER BY created_at DESC
  LIMIT 1;
")"
test -n "$EXTERNAL_ROW" || fail "usuário externo Active não encontrado"
EXTERNAL_ID="${EXTERNAL_ROW%%|*}"
EXTERNAL_EMAIL="${EXTERNAL_ROW#*|}"

CITIZEN_TOKEN="$(make_token "$CITIZEN_ID")"
MASTER_TOKEN="$(make_token "$MASTER_ID")"
EXTERNAL_TOKEN="$(make_token "$EXTERNAL_ID")"
unset JWT_SIGNING_KEY

echo "citizen=$CITIZEN_EMAIL"
echo "master=$MASTER_EMAIL"
echo "external=$EXTERNAL_EMAIL"
echo "jwt=OK"

echo
echo "=== 6. FIXTURE OCORRÊNCIA / TARGET / CHAT ==="
CATEGORY_ID="$(dbq "SELECT id FROM occurrence_categories WHERE status = 'ACTIVE' ORDER BY display_order, name LIMIT 1;")"
test -n "$CATEGORY_ID" || fail "categoria ativa não encontrada"

OCC_BODY="$TMP_DIR/occurrence.json"
OCC_PAYLOAD="$TMP_DIR/occurrence-payload.json"
python3 - "$OCC_PAYLOAD" "$CATEGORY_ID" "$RUN_TAG" <<'PY'
import json, sys
path, category, tag = sys.argv[1:]
with open(path, 'w', encoding='utf-8') as f:
    json.dump({
        'categoryId': category,
        'title': 'Chat audio smoke',
        'description': 'Fixture temporária do gate de áudio.',
        'addressText': 'Praça da Alfândega, Porto Alegre - RS',
        'latitude': -30.0305,
        'longitude': -51.2300,
        'postalCode': '90010150',
        'cityId': None,
        'stateCode': 'RS',
        'externalProtocolNumber': tag,
        'externalProtocolAgency': 'CHAT_AUDIO_SMOKE',
        'mediaIds': [],
    }, f)
PY
OCC_CODE="$(curl -sS -o "$OCC_BODY" -w '%{http_code}' -X POST \
  -H "Authorization: Bearer $CITIZEN_TOKEN" -H 'Content-Type: application/json' \
  --data-binary "@$OCC_PAYLOAD" "$BASE/api/v1/occurrences")"
test "$OCC_CODE" = "201" || { cat "$OCC_BODY"; fail "ocorrência fixture não criada"; }
OCCURRENCE_ID="$(json_field "$OCC_BODY" id)"

TARGET_BODY="$TMP_DIR/target.json"
TARGET_CODE="$(curl -sS -o "$TARGET_BODY" -w '%{http_code}' -X POST \
  -H "Authorization: Bearer $CITIZEN_TOKEN" -H 'Content-Type: application/json' \
  --data "{\"masterUserId\":\"$MASTER_ID\"}" \
  "$BASE/api/v1/occurrences/$OCCURRENCE_ID/targets")"
test "$TARGET_CODE" = "201" || { cat "$TARGET_BODY"; fail "target fixture não criado"; }
TARGET_ID="$(json_field "$TARGET_BODY" id)"

ACCEPT_CODE="$(curl -sS -o "$TMP_DIR/accept.json" -w '%{http_code}' -X POST \
  -H "Authorization: Bearer $MASTER_TOKEN" \
  "$BASE/api/v1/occurrences/$OCCURRENCE_ID/targets/$TARGET_ID/accept")"
test "$ACCEPT_CODE" = "200" || { cat "$TMP_DIR/accept.json"; fail "target não aceito"; }

CONV_CODE="$(curl -sS -o "$TMP_DIR/conversation.json" -w '%{http_code}' \
  -H "Authorization: Bearer $CITIZEN_TOKEN" \
  "$BASE/api/v1/chat/targets/$TARGET_ID/conversation")"
test "$CONV_CODE" = "200" || { cat "$TMP_DIR/conversation.json"; fail "conversa não criada"; }
CONVERSATION_ID="$(json_field "$TMP_DIR/conversation.json" id)"
echo "occurrence=$OCCURRENCE_ID"
echo "target=$TARGET_ID"
echo "conversation=$CONVERSATION_ID"

echo
echo "=== 7. ÁUDIO WAV DE QA ==="
WAV_FILE="$TMP_DIR/smoke.wav"
python3 - "$WAV_FILE" <<'PY'
import math, struct, sys, wave
path = sys.argv[1]
rate = 8000
frames = int(rate * 0.25)
with wave.open(path, 'wb') as wav:
    wav.setnchannels(1)
    wav.setsampwidth(2)
    wav.setframerate(rate)
    for i in range(frames):
        sample = int(4000 * math.sin(2 * math.pi * 440 * i / rate))
        wav.writeframesraw(struct.pack('<h', sample))
PY
WAV_SIZE="$(stat -c %s "$WAV_FILE")"
test "$WAV_SIZE" -gt 44 || fail "WAV fixture inválido"
echo "wav_size=$WAV_SIZE"

echo
echo "=== 8. FALHA DE UPLOAD NÃO CRIA MENSAGEM ==="
BEFORE_COUNT="$(dbq "SELECT count(*) FROM chat_messages WHERE conversation_id = '$CONVERSATION_ID'::uuid;")"
MISSING_MEDIA_ID="$(python3 - <<'PY'
import uuid
print(uuid.uuid4())
PY
)"
MISSING_CLIENT_ID="$(python3 - <<'PY'
import uuid
print(uuid.uuid4())
PY
)"
MISSING_CODE="$(curl -sS -o "$TMP_DIR/missing.json" -w '%{http_code}' -X POST \
  -H "Authorization: Bearer $CITIZEN_TOKEN" -H 'Content-Type: application/json' \
  --data "{\"clientMessageId\":\"$MISSING_CLIENT_ID\",\"fileName\":\"smoke.wav\",\"contentType\":\"audio/wav\",\"sizeBytes\":$WAV_SIZE}" \
  "$BASE/api/v1/chat/conversations/$CONVERSATION_ID/audio/$MISSING_MEDIA_ID/confirm")"
test "$MISSING_CODE" = "409" || { cat "$TMP_DIR/missing.json"; fail "confirm sem objeto deveria retornar 409"; }
AFTER_MISSING_COUNT="$(dbq "SELECT count(*) FROM chat_messages WHERE conversation_id = '$CONVERSATION_ID'::uuid;")"
test "$AFTER_MISSING_COUNT" = "$BEFORE_COUNT" || fail "falha de upload criou mensagem órfã"
echo "missing_object=409"
echo "orphan_message=0"

echo
echo "=== 9. REQUEST / PUT / CONFIRM ==="
UNAUTH_UPLOAD="$(curl -sS -o /dev/null -w '%{http_code}' -X POST \
  -H 'Content-Type: application/json' \
  --data "{\"fileName\":\"smoke.wav\",\"contentType\":\"audio/wav\",\"sizeBytes\":$WAV_SIZE}" \
  "$BASE/api/v1/chat/conversations/$CONVERSATION_ID/audio/uploads")"
test "$UNAUTH_UPLOAD" = "401" || fail "upload sem auth deveria retornar 401"

UPLOAD_CODE="$(curl -sS -o "$TMP_DIR/upload.json" -w '%{http_code}' -X POST \
  -H "Authorization: Bearer $CITIZEN_TOKEN" -H 'Content-Type: application/json' \
  --data "{\"fileName\":\"smoke.wav\",\"contentType\":\"audio/wav\",\"sizeBytes\":$WAV_SIZE}" \
  "$BASE/api/v1/chat/conversations/$CONVERSATION_ID/audio/uploads")"
test "$UPLOAD_CODE" = "201" || { cat "$TMP_DIR/upload.json"; fail "request de upload != 201"; }
AUDIO_MEDIA_ID="$(json_field "$TMP_DIR/upload.json" mediaId)"
UPLOAD_URL="$(json_field "$TMP_DIR/upload.json" uploadUrl)"
R2_OBJECT_KEY="chat/audio/${CONVERSATION_ID//-/}/${AUDIO_MEDIA_ID//-/}.wav"

PUT_CODE="$(curl -sS -o /dev/null -w '%{http_code}' -X PUT \
  -H 'Content-Type: audio/wav' --data-binary "@$WAV_FILE" "$UPLOAD_URL")"
case "$PUT_CODE" in
  200|201|204) ;;
  *) fail "PUT R2 falhou com HTTP $PUT_CODE" ;;
esac

CLIENT_MESSAGE_ID="$(python3 - <<'PY'
import uuid
print(uuid.uuid4())
PY
)"
CONFIRM_CODE="$(curl -sS -o "$TMP_DIR/message.json" -w '%{http_code}' -X POST \
  -H "Authorization: Bearer $CITIZEN_TOKEN" -H 'Content-Type: application/json' \
  --data "{\"clientMessageId\":\"$CLIENT_MESSAGE_ID\",\"fileName\":\"smoke.wav\",\"contentType\":\"audio/wav\",\"sizeBytes\":$WAV_SIZE}" \
  "$BASE/api/v1/chat/conversations/$CONVERSATION_ID/audio/$AUDIO_MEDIA_ID/confirm")"
test "$CONFIRM_CODE" = "201" || { cat "$TMP_DIR/message.json"; fail "confirm de áudio != 201"; }
MESSAGE_ID="$(json_field "$TMP_DIR/message.json" id)"
MESSAGE_TYPE="$(json_field "$TMP_DIR/message.json" type)"
MESSAGE_MEDIA_ID="$(json_field "$TMP_DIR/message.json" audio.mediaId)"
test "$MESSAGE_TYPE" = "AUDIO" || fail "mensagem não retornou type=AUDIO"
test "$MESSAGE_MEDIA_ID" = "$AUDIO_MEDIA_ID" || fail "mediaId da mensagem divergiu"
AFTER_CONFIRM_COUNT="$(dbq "SELECT count(*) FROM chat_messages WHERE conversation_id = '$CONVERSATION_ID'::uuid;")"
test "$AFTER_CONFIRM_COUNT" = "$((BEFORE_COUNT + 1))" || fail "mensagem de áudio não persistiu exatamente uma vez"
echo "audio_upload=OK"
echo "audio_confirm=OK"
echo "audio_message=OK"

echo
echo "=== 10. PLAYBACK AUTORIZADO / EXTERNO NEGADO ==="
READ_CODE="$(curl -sS -o "$TMP_DIR/read.json" -w '%{http_code}' \
  -H "Authorization: Bearer $CITIZEN_TOKEN" \
  "$BASE/api/v1/chat/conversations/$CONVERSATION_ID/messages/$MESSAGE_ID/audio/read-url")"
test "$READ_CODE" = "200" || { cat "$TMP_DIR/read.json"; fail "read-url autorizado != 200"; }
READ_URL="$(json_field "$TMP_DIR/read.json" readUrl)"
PLAY_CODE="$(curl -sS -o "$TMP_DIR/play.wav" -w '%{http_code}' "$READ_URL")"
case "$PLAY_CODE" in
  200|206) ;;
  *) fail "playback R2 falhou com HTTP $PLAY_CODE" ;;
esac
python3 - "$TMP_DIR/play.wav" <<'PY'
import sys
b = open(sys.argv[1], 'rb').read(12)
assert len(b) >= 12 and b[:4] == b'RIFF' and b[8:12] == b'WAVE'
PY

EXTERNAL_CODE="$(curl -sS -o "$TMP_DIR/external.json" -w '%{http_code}' \
  -H "Authorization: Bearer $EXTERNAL_TOKEN" \
  "$BASE/api/v1/chat/conversations/$CONVERSATION_ID/messages/$MESSAGE_ID/audio/read-url")"
test "$EXTERNAL_CODE" = "403" || { cat "$TMP_DIR/external.json"; fail "usuário externo deveria receber 403"; }
! grep -q 'X-Amz-' "$TMP_DIR/external.json" || fail "resposta negada vazou URL assinada"
echo "authorized_playback=OK"
echo "external_read=403"

echo
echo "=== 11. CONVERSA ENCERRADA BLOQUEIA ÁUDIO ==="
dbexec "UPDATE chat_conversations SET status = 'Closed', closed_at = now() WHERE id = '$CONVERSATION_ID'::uuid;" >/dev/null
CLOSED_CODE="$(curl -sS -o "$TMP_DIR/closed.json" -w '%{http_code}' \
  -H "Authorization: Bearer $CITIZEN_TOKEN" \
  "$BASE/api/v1/chat/conversations/$CONVERSATION_ID/messages/$MESSAGE_ID/audio/read-url")"
test "$CLOSED_CODE" = "410" || { cat "$TMP_DIR/closed.json"; fail "conversa encerrada deveria bloquear áudio com 410"; }
! grep -q 'X-Amz-' "$TMP_DIR/closed.json" || fail "conversa encerrada vazou URL assinada"
echo "closed_conversation_audio=410"

echo
echo "=== 12. CLEANUP ==="
cleanup
trap - EXIT
RESIDUAL_OCC="$(dbq "SELECT count(*) FROM occurrences WHERE external_protocol_number = '$RUN_TAG';")"
RESIDUAL_CHAT="$(dbq "SELECT count(*) FROM chat_conversations WHERE id = '$CONVERSATION_ID'::uuid;")"
echo "occurrence_residual=$RESIDUAL_OCC"
echo "chat_residual=$RESIDUAL_CHAT"
test "$RESIDUAL_OCC" = "0" || fail "ocorrência fixture residual"
test "$RESIDUAL_CHAT" = "0" || fail "chat fixture residual"

echo
echo "=== 13. ESTADO FINAL ==="
PROVIDER_AFTER="$(dbq "
  SELECT
    (SELECT count(*) FROM billing_provider_subscriptions)::text || '|' ||
    (SELECT count(*) FROM payments)::text || '|' ||
    (SELECT count(*) FROM payment_events)::text;
")"
FINAL_HEALTH="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/health/ready")"
FINAL_WEB="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/")"
echo "provider_after=$PROVIDER_AFTER"
echo "health=$FINAL_HEALTH"
echo "web=$FINAL_WEB"
echo "main_clean=$(test -z "$(git -C "$ROOT" status --porcelain)" && echo YES || echo NO)"
test "$PROVIDER_AFTER" = "$PROVIDER_BEFORE"
test "$FINAL_HEALTH" = "200"
test "$FINAL_WEB" = "200"
test -z "$(git -C "$ROOT" status --porcelain)"

echo
echo "============================================================"
echo "CHAT AUDIO / R2 — FEATURE HOMOLOG: OK"
echo "HEAD: $EXPECTED_HEAD"
echo "R2 CONFIG: OK"
echo "AUDIO BUNDLE: OK"
echo "AUDIO UPLOAD: OK"
echo "AUDIO VERIFICATION: OK"
echo "AUDIO MESSAGE: OK"
echo "AUTHORIZED PLAYBACK: OK"
echo "EXTERNAL READ: 403"
echo "CLOSED CONVERSATION AUDIO: 410"
echo "MISSING OBJECT: 409"
echo "ORPHAN MESSAGE: 0"
echo "SMOKE RESIDUAL: 0"
echo "WEB: 200"
echo "HEALTH: 200"
echo "MERCADO PAGO PROVIDER: $PROVIDER_AFTER"
echo "MAIN WORKTREE: CLEAN"
echo "============================================================"
