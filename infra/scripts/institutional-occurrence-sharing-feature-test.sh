#!/usr/bin/env bash
set -Eeuo pipefail

EXPECTED_HEAD="${1:-}"
ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
ENV_FILE="${CIDADEMDIA_ENV_FILE:-$ROOT/.env}"
BASE="${CIDADEMDIA_BASE_URL:-https://homolog.cidademdia.com.br}"
QA_DIR="${CIDADEMDIA_QA_DIR:-$HOME/cidademdia-qa/institutional-occurrence-sharing-$(date +%Y%m%d-%H%M%S)}"
QA_SUFFIX="$(date +%s)-$$"
QA_EMAIL="qa-institutional-${QA_SUFFIX}@cidademdia.local"
QA_PASSWORD="QaInstitutional#${QA_SUFFIX}!"
QA_NAME="QA Compartilhamento Institucional ${QA_SUFFIX}"

fail() {
  echo
  echo "ERRO: $*" >&2
  exit 1
}

for cmd in git docker curl python3; do
  command -v "$cmd" >/dev/null 2>&1 || fail "comando ausente: $cmd"
done

test -n "$EXPECTED_HEAD" || fail "informe o HEAD esperado"
test "$(git -C "$ROOT" rev-parse HEAD)" = "$EXPECTED_HEAD" || fail "repo fora do HEAD esperado"
test "$(git -C "$ROOT" branch --show-current)" = "feat/institutional-occurrence-sharing" || fail "branch inesperada"
test -z "$(git -C "$ROOT" status --porcelain)" || fail "worktree está suja"
test -f "$ENV_FILE" || fail ".env não encontrado"
: "${HML_INSTITUTION_MASTER_PASSWORD:?Defina HML_INSTITUTION_MASTER_PASSWORD com a senha das Masters institucionais.}"

COMPOSE=(docker compose -p infra --env-file "$ENV_FILE" -f "$ROOT/infra/docker-compose.yml")

cleanup_qa() {
  "${COMPOSE[@]}" exec -T db sh -lc "
    psql -v ON_ERROR_STOP=1 -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" <<'SQL'
DO \$\$
DECLARE
  uid uuid;
BEGIN
  SELECT id INTO uid FROM users WHERE email = '$QA_EMAIL';
  IF uid IS NOT NULL THEN
    DELETE FROM occurrences WHERE author_user_id = uid;
    DELETE FROM occurrence_media WHERE uploader_user_id = uid;
    DELETE FROM users WHERE id = uid;
  END IF;
END
\$\$;
SQL
  " >/dev/null 2>&1 || true
}
trap cleanup_qa EXIT

mkdir -p "$QA_DIR"

echo "============================================================"
echo "INSTITUTIONAL OCCURRENCE SHARING — FEATURE HOMOLOG"
echo "============================================================"
echo "HEAD: $EXPECTED_HEAD"

echo
echo "=== 1. GUARD + HEALTH ==="
bash "$ROOT/infra/scripts/institutional-occurrence-sharing-guard.sh"
curl -fsS "$BASE/health/ready" >/dev/null
echo "health=OK"

echo
echo "=== 2. API E2E ==="
docker run --rm -i   --network host   -v "$QA_DIR:/work"   -e BASE="$BASE"   -e QA_EMAIL="$QA_EMAIL"   -e QA_PASSWORD="$QA_PASSWORD"   -e QA_NAME="$QA_NAME"   -e HML_INSTITUTION_MASTER_PASSWORD="$HML_INSTITUTION_MASTER_PASSWORD"   node:22-alpine   node --input-type=module - <<'JS'
import fs from 'node:fs';
import crypto from 'node:crypto';

const BASE = process.env.BASE;
const password = process.env.HML_INSTITUTION_MASTER_PASSWORD;
const png = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Z1xkAAAAASUVORK5CYII=',
  'base64',
);

const names = {
  prefeitura: 'Prefeitura de São Paulo',
  camara: 'Câmara Municipal de São Paulo',
  governo: 'Governo do Estado de São Paulo',
  alesp: 'Assembleia Legislativa do Estado de São Paulo',
};

const emails = {
  prefeitura: 'prefeitura-sp.master@hml.cidademdia.invalid',
  camara: 'camara-sp.master@hml.cidademdia.invalid',
  governo: 'governo-sp.master@hml.cidademdia.invalid',
  alesp: 'alesp.master@hml.cidademdia.invalid',
};

async function api(method, path, token, data) {
  const headers = {};
  if (token) headers.Authorization = 'Bearer ' + token;
  const options = { method, headers };
  if (data !== undefined) {
    headers['Content-Type'] = 'application/json';
    options.body = JSON.stringify(data);
  }

  const response = await fetch(BASE + path, options);
  const text = await response.text();
  let body = null;
  if (text) {
    try { body = JSON.parse(text); } catch { body = text; }
  }
  return { response, body, text };
}

function expect(result, status, label) {
  if (result.response.status !== status) {
    throw new Error(label + ': esperado HTTP ' + status + ', recebeu ' + result.response.status + ' ' + result.text);
  }
}

async function login(email) {
  const result = await api('POST', '/api/v1/auth/login', null, { email, password });
  expect(result, 200, 'login ' + email);
  if (!result.body?.accessToken || !result.body?.user?.roles?.includes('MASTER')) {
    throw new Error('conta não autenticou como MASTER: ' + email);
  }
  return result.body;
}

async function readyImage(token) {
  const request = await api('POST', '/api/v1/occurrence-media/uploads', token, {
    fileName: 'qa-institutional.png',
    contentType: 'image/png',
    sizeBytes: png.length,
  });
  expect(request, 201, 'solicitação de mídia');

  const put = await fetch(request.body.uploadUrl, {
    method: 'PUT',
    headers: { 'Content-Type': 'image/png' },
    body: png,
  });
  if (!put.ok) throw new Error('upload R2 falhou: HTTP ' + put.status);

  const confirm = await api('POST', '/api/v1/occurrence-media/' + request.body.id + '/confirm', token);
  expect(confirm, 200, 'confirmação de mídia');
  if (confirm.body?.status !== 'READY') throw new Error('mídia não ficou READY');
  return confirm.body;
}

const register = await api('POST', '/api/v1/auth/register', null, {
  email: process.env.QA_EMAIL,
  password: process.env.QA_PASSWORD,
  displayName: process.env.QA_NAME,
  termsAccepted: true,
});
expect(register, 201, 'registro cidadão QA');
const citizen = register.body;
const citizenToken = citizen.accessToken;
console.log('institutional_qa_citizen=OK');

const categories = await api('GET', '/api/v1/occurrences/categories', citizenToken);
expect(categories, 200, 'categorias');
const category = categories.body?.[0];
if (!category?.id) throw new Error('nenhuma categoria ativa');

const destinationResponse = await api('GET', '/api/v1/occurrences/destinations', citizenToken);
expect(destinationResponse, 200, 'destinos');
const destinations = destinationResponse.body;
if (!Array.isArray(destinations)) throw new Error('destinos não retornaram lista');

const byName = Object.fromEntries(
  Object.entries(names).map(([key, name]) => {
    const rows = destinations.filter(item => item.displayName === name);
    if (rows.length !== 1) throw new Error('destino deve existir uma vez: ' + name);
    if (Object.prototype.hasOwnProperty.call(rows[0], 'masterUserId')) {
      throw new Error('destino expôs masterUserId: ' + name);
    }
    return [key, rows[0]];
  }),
);
console.log('institutional_destinations=OK count=4');

const image = await readyImage(citizenToken);
console.log('institutional_media=READY');

const create = await api('POST', '/api/v1/occurrences', citizenToken, {
  categoryId: category.id,
  masterUserId: null,
  institutionId: byName.prefeitura.id,
  addressee: null,
  title: 'QA institucional ' + Date.now(),
  description: 'E2E de compartilhamento institucional via Conta Master.',
  street: 'Praça da Sé',
  number: '1',
  neighborhood: 'Sé',
  city: 'São Paulo',
  latitude: -23.55052,
  longitude: -46.633308,
  postalCode: '01001000',
  cityId: null,
  stateCode: 'SP',
  externalProtocolNumber: 'QA-INSTITUCIONAL-' + Date.now(),
  externalProtocolAgency: 'QA HML',
  mediaIds: [image.id],
});
expect(create, 201, 'criação institucional');
const occurrence = create.body;
console.log('institutional_occurrence_created=OK code=' + occurrence.publicCode);

const camaraAdd = await api(
  'POST',
  '/api/v1/occurrences/' + occurrence.id + '/targets',
  citizenToken,
  { masterUserId: null, institutionId: byName.camara.id, addressee: 'Vereador QA / Partido QA' },
);
expect(camaraAdd, 201, 'target Câmara');

const governoAdd = await api(
  'POST',
  '/api/v1/occurrences/' + occurrence.id + '/targets',
  citizenToken,
  { masterUserId: null, institutionId: byName.governo.id, addressee: null },
);
expect(governoAdd, 201, 'target Governo');

const duplicate = await api(
  'POST',
  '/api/v1/occurrences/' + occurrence.id + '/targets',
  citizenToken,
  { masterUserId: null, institutionId: byName.prefeitura.id, addressee: null },
);
expect(duplicate, 409, 'duplicidade');
if (duplicate.body?.code !== 'duplicate_target') throw new Error('duplicidade sem duplicate_target');
console.log('institutional_duplicate_blocked=OK');

const fourth = await api(
  'POST',
  '/api/v1/occurrences/' + occurrence.id + '/targets',
  citizenToken,
  { masterUserId: null, institutionId: byName.alesp.id, addressee: null },
);
expect(fourth, 409, 'quarto destino');
if (fourth.body?.code !== 'target_limit_reached') throw new Error('quarto destino sem target_limit_reached');
console.log('institutional_target_limit=OK');

const targetsResponse = await api('GET', '/api/v1/occurrences/' + occurrence.id + '/targets', citizenToken);
expect(targetsResponse, 200, 'targets cidadão');
const targets = targetsResponse.body;
if (!Array.isArray(targets) || targets.length !== 3) throw new Error('esperados 3 targets');
if (targets.some(item => !item.masterUserId)) throw new Error('target sem masterUserId');

const targetByInstitution = new Map(targets.map(item => [item.institutionId, item]));
const prefeituraTarget = targetByInstitution.get(byName.prefeitura.id);
const camaraTarget = targetByInstitution.get(byName.camara.id);
const governoTarget = targetByInstitution.get(byName.governo.id);
if (!prefeituraTarget || !camaraTarget || !governoTarget) throw new Error('destino reverso dos targets incompleto');
if (prefeituraTarget.addressee !== null) throw new Error('addressee vazio não permaneceu null');
if (camaraTarget.addressee !== 'Vereador QA / Partido QA') throw new Error('addressee preenchido não foi preservado');
console.log('institutional_addressee_empty=OK');
console.log('institutional_addressee_filled=OK');
console.log('institutional_targets_have_master=OK count=3');

const masters = {};
for (const [key, email] of Object.entries(emails)) masters[key] = await login(email);
console.log('institutional_master_logins=OK count=4');

const expected = new Map([
  [masters.prefeitura.user.id, prefeituraTarget.id],
  [masters.camara.user.id, camaraTarget.id],
  [masters.governo.user.id, governoTarget.id],
]);

for (const [key, session] of Object.entries(masters)) {
  const result = await api('GET', '/api/v1/master/occurrence-targets', session.accessToken);
  expect(result, 200, 'lista Master ' + key);
  const rows = result.body.filter(item => item.occurrenceId === occurrence.id);
  const targetId = expected.get(session.user.id);

  if (targetId) {
    if (rows.length !== 1 || rows[0].targetId !== targetId) {
      throw new Error('isolamento inválido para Master ' + key);
    }
    if (rows[0].destinationDisplayName !== names[key]) {
      throw new Error('nome do destino inválido para Master ' + key);
    }
  } else if (rows.length !== 0) {
    throw new Error('ALESP enxergou target que não recebeu');
  }
}
console.log('institutional_master_isolation=OK');

const wrongAccept = await api(
  'POST',
  '/api/v1/occurrences/' + occurrence.id + '/targets/' + prefeituraTarget.id + '/accept',
  masters.governo.accessToken,
);
expect(wrongAccept, 404, 'aceite pela Master errada');
if (wrongAccept.body?.code !== 'target_not_found') throw new Error('isolamento de decisão não retornou target_not_found');
console.log('institutional_wrong_master_decision_blocked=OK');

const accepted = await api(
  'POST',
  '/api/v1/occurrences/' + occurrence.id + '/targets/' + prefeituraTarget.id + '/accept',
  masters.prefeitura.accessToken,
);
expect(accepted, 200, 'aceite Prefeitura');
if (accepted.body?.targetStatus !== 'ACCEPTED' || accepted.body?.masterUserId !== masters.prefeitura.user.id) {
  throw new Error('aceite retornou estado incorreto');
}
console.log('institutional_accept=OK');

const rejected = await api(
  'POST',
  '/api/v1/occurrences/' + occurrence.id + '/targets/' + camaraTarget.id + '/reject',
  masters.camara.accessToken,
  { reason: 'QA: recusa para validar fluxo institucional.' },
);
expect(rejected, 200, 'recusa Câmara');
if (rejected.body?.targetStatus !== 'REJECTED' || rejected.body?.masterUserId !== masters.camara.user.id) {
  throw new Error('recusa retornou estado incorreto');
}
console.log('institutional_reject=OK');

const citizenChat = await api(
  'GET',
  '/api/v1/chat/targets/' + prefeituraTarget.id + '/conversation',
  citizenToken,
);
expect(citizenChat, 200, 'chat cidadão');

const masterChat = await api(
  'GET',
  '/api/v1/chat/targets/' + prefeituraTarget.id + '/conversation',
  masters.prefeitura.accessToken,
);
expect(masterChat, 200, 'chat Master');
if (citizenChat.body?.id !== masterChat.body?.id) throw new Error('chat cidadão/Master divergente');

const wrongChat = await api(
  'GET',
  '/api/v1/chat/targets/' + prefeituraTarget.id + '/conversation',
  masters.governo.accessToken,
);
expect(wrongChat, 403, 'chat Master errada');
if (wrongChat.body?.code !== 'chat_access_denied') throw new Error('chat não bloqueou Master errada');
console.log('institutional_chat_isolation=OK');

const rejectedChat = await api(
  'GET',
  '/api/v1/chat/targets/' + camaraTarget.id + '/conversation',
  citizenToken,
);
expect(rejectedChat, 404, 'chat de target rejeitado');
if (rejectedChat.body?.code !== 'conversation_not_found') throw new Error('target rejeitado criou chat');

const messageText = 'Mensagem QA institucional ' + Date.now();
const sent = await api(
  'POST',
  '/api/v1/chat/conversations/' + citizenChat.body.id + '/messages',
  citizenToken,
  { clientMessageId: crypto.randomUUID(), content: messageText },
);
expect(sent, 201, 'mensagem chat');

const messages = await api(
  'GET',
  '/api/v1/chat/conversations/' + citizenChat.body.id + '/messages?pageSize=50',
  masters.prefeitura.accessToken,
);
expect(messages, 200, 'leitura chat Master');
if (!messages.body?.items?.some(item => item.content === messageText)) throw new Error('mensagem não chegou à Master');
console.log('institutional_chat_message=OK');

const finalTargets = await api('GET', '/api/v1/occurrences/' + occurrence.id + '/targets', citizenToken);
expect(finalTargets, 200, 'estado final targets');
const finalById = new Map(finalTargets.body.map(item => [item.id, item.status]));
if (finalById.get(prefeituraTarget.id) !== 'ACCEPTED') throw new Error('Prefeitura não terminou ACCEPTED');
if (finalById.get(camaraTarget.id) !== 'REJECTED') throw new Error('Câmara não terminou REJECTED');
if (finalById.get(governoTarget.id) !== 'PENDING') throw new Error('Governo não permaneceu PENDING');
console.log('institutional_final_target_states=OK');

fs.writeFileSync('/work/result.json', JSON.stringify({ occurrenceId: occurrence.id }, null, 2));
JS

test -f "$QA_DIR/result.json" || fail "result.json não foi criado"

OCCURRENCE_ID="$(
  python3 - "$QA_DIR/result.json" <<'PY'
import json, sys
with open(sys.argv[1], encoding='utf-8') as fh:
    print(json.load(fh)['occurrenceId'])
PY
)"

echo
echo "=== 3. PERSISTÊNCIA ==="
DB_STATE="$(
  "${COMPOSE[@]}" exec -T db sh -lc "
    psql -v ON_ERROR_STOP=1 -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -Atc \"
      select
        count(*) || '|' ||
        count(distinct target.master_user_id) || '|' ||
        count(*) filter (where membership.id is not null) || '|' ||
        count(*) filter (where target.addressee = 'Vereador QA / Partido QA')
      from occurrence_targets target
      left join institution_memberships membership
        on membership.user_id = target.master_user_id
       and membership.status = 'ACTIVE'
       and membership.membership_role = 'INSTITUTION_ADMIN'
      where target.occurrence_id = '$OCCURRENCE_ID'::uuid;
    \"
  " | tr -d '[:space:]'
)"
echo "institutional_target_db_state=$DB_STATE"
test "$DB_STATE" = "3|3|3|1" || fail "estado de persistência inesperado"

echo
echo "=== 4. LIMPEZA + SMOKE ==="
cleanup_qa
QA_LEFT="$(
  "${COMPOSE[@]}" exec -T db sh -lc "
    psql -At -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -c \"
      select count(*) from users where email = '$QA_EMAIL';
    \"
  " | tr -d '[:space:]'
)"
test "$QA_LEFT" = "0" || fail "usuário QA não foi removido"
trap - EXIT

bash "$ROOT/infra/scripts/smoke-test.sh" http://localhost:8080
test -z "$(git -C "$ROOT" status --porcelain)" || fail "worktree ficou suja"

echo "============================================================"
echo "INSTITUTIONAL OCCURRENCE SHARING — FEATURE HOMOLOG: OK"
echo "HEAD: $EXPECTED_HEAD"
echo "DESTINATIONS: 4 UNIQUE"
echo "TARGETS: 3 MASTERS"
echo "ADDRESSEE EMPTY/FILLED: OK"
echo "DUPLICATE + FOURTH TARGET: BLOCKED"
echo "MASTER ISOLATION: OK"
echo "ACCEPT + REJECT: OK"
echo "CHAT + CHAT ISOLATION: OK"
echo "DB TARGETS: 3|3|3|1"
echo "QA CLEANUP: OK"
echo "SMOKE: OK"
echo "============================================================"
