#!/usr/bin/env bash
set -Eeuo pipefail

EXPECTED_HEAD="${1:-}"
ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
ENV_FILE="${CIDADEMDIA_ENV_FILE:-$ROOT/.env}"
BASE="${CIDADEMDIA_BASE_URL:-https://homolog.cidademdia.com.br}"
WT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
QA_DIR="${CIDADEMDIA_QA_DIR:-$HOME/cidademdia-qa/occurrence-media-presentation-$(date +%Y%m%d-%H%M%S)}"
QA_SUFFIX="$(date +%s)-$$"
QA_EMAIL="qa-occ-media-${QA_SUFFIX}@cidademdia.local"
QA_PASSWORD="QaOccMedia#${QA_SUFFIX}!"
QA_NAME="QA Mídia ${QA_SUFFIX}"

fail() {
  echo
  echo "ERRO: $*" >&2
  exit 1
}

for cmd in git docker curl; do
  command -v "$cmd" >/dev/null 2>&1 || fail "comando ausente: $cmd"
done

test -n "$EXPECTED_HEAD" || fail "informe o HEAD esperado"
test "$(git -C "$WT" rev-parse HEAD)" = "$EXPECTED_HEAD" || fail "worktree fora do HEAD esperado"
test "$(git -C "$ROOT" branch --show-current)" = "main" || fail "repo principal não está na main"
test -z "$(git -C "$ROOT" status --porcelain)" || fail "main local está suja"
test -f "$ENV_FILE" || fail ".env não encontrado"

root_compose() {
  docker compose --env-file "$ENV_FILE" -f "$ROOT/infra/docker-compose.yml" "$@"
}

CURRENT_WEB_ID="$(root_compose ps -q web)"
test -n "$CURRENT_WEB_ID" || fail "container web atual não encontrado"
PROJECT="$(docker inspect "$CURRENT_WEB_ID" --format '{{ index .Config.Labels "com.docker.compose.project" }}')"
test -n "$PROJECT" || fail "compose project não identificado"

compose() {
  docker compose -p "$PROJECT" --env-file "$ENV_FILE" -f "$WT/infra/docker-compose.yml" "$@"
}

cleanup_qa_user() {
  compose exec -T db sh -lc \
    "psql -v ON_ERROR_STOP=1 -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" <<'SQL'
DO \$\$
DECLARE
  uid uuid;
BEGIN
  SELECT id INTO uid FROM users WHERE email = '${QA_EMAIL}';
  IF uid IS NOT NULL THEN
    DELETE FROM occurrences WHERE author_user_id = uid;
    DELETE FROM occurrence_media WHERE uploader_user_id = uid;
    DELETE FROM users WHERE id = uid;
  END IF;
END
\$\$;
SQL" >/dev/null 2>&1 || true
}
trap cleanup_qa_user EXIT

mkdir -p "$QA_DIR"

echo "============================================================"
echo "OCCURRENCE MEDIA PRESENTATION — FEATURE HOMOLOG"
echo "============================================================"
echo "HEAD: $EXPECTED_HEAD"
echo "MAIN: $(git -C "$ROOT" rev-parse HEAD)"

echo
echo "=== 1. GUARD ==="
bash "$WT/infra/scripts/occurrence-media-presentation-guard.sh"

echo
echo "=== 2. BUILD / DEPLOY WEB ==="
compose build web
compose up -d --no-deps web
compose up -d --no-deps --force-recreate nginx

READY=0
for _ in $(seq 1 40); do
  HEALTH="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/health/ready" || true)"
  PANEL="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/painel" || true)"
  if [ "$HEALTH" = "200" ] && [ "$PANEL" = "200" ]; then
    READY=1
    break
  fi
  sleep 2
done

test "$READY" = "1" || fail "health/painel não ficaram prontos"
echo "health=200"
echo "panel_shell=200"

echo
echo "=== 3. BROWSER — UPLOAD + VÍNCULO + APRESENTAÇÃO ==="
docker run --rm -i \
  --network host \
  -v "$QA_DIR:/work" \
  -e BASE="$BASE" \
  -e QA_EMAIL="$QA_EMAIL" \
  -e QA_PASSWORD="$QA_PASSWORD" \
  -e QA_NAME="$QA_NAME" \
  node:22-alpine \
  sh -lc '
    set -eu
    apk add --no-cache chromium nss freetype harfbuzz ca-certificates ttf-freefont >/dev/null
    cd /tmp
    npm init -y >/dev/null 2>&1
    npm install --no-save --no-audit --no-fund playwright-core@1.55.0 >/dev/null
    export CHROME="$(command -v chromium-browser || command -v chromium)"
    node --input-type=module -
  ' <<'JS'
import { chromium } from 'playwright-core';

const png = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Z1xkAAAAASUVORK5CYII=',
  'base64',
);

const browser = await chromium.launch({
  executablePath: process.env.CHROME,
  headless: true,
  args: ['--no-sandbox', '--disable-dev-shm-usage'],
});

const context = await browser.newContext({
  ignoreHTTPSErrors: true,
  viewport: { width: 1440, height: 1000 },
});
const page = await context.newPage();
const errors = [];
page.on('pageerror', error => errors.push(error.message));

try {
  const register = await context.request.post(`${process.env.BASE}/api/v1/auth/register`, {
    data: {
      email: process.env.QA_EMAIL,
      password: process.env.QA_PASSWORD,
      displayName: process.env.QA_NAME,
    },
  });
  if (register.status() !== 201) {
    throw new Error(`registro QA falhou: ${register.status()} ${await register.text()}`);
  }

  const session = await register.json();
  if (!session?.accessToken) throw new Error('registro QA não retornou accessToken');
  const authHeaders = { Authorization: `Bearer ${session.accessToken}` };
  console.log('occurrence_media_qa_auth=OK');

  const categoriesResponse = await context.request.get(`${process.env.BASE}/api/v1/occurrences/categories`, {
    headers: authHeaders,
  });
  if (categoriesResponse.status() !== 200) {
    throw new Error(`categorias falharam: ${categoriesResponse.status()} ${await categoriesResponse.text()}`);
  }
  const categories = await categoriesResponse.json();
  const category = categories[0];
  if (!category?.id) throw new Error('nenhuma categoria ativa disponível para QA');

  const uploadResponse = await context.request.post(`${process.env.BASE}/api/v1/occurrence-media/uploads`, {
    headers: authHeaders,
    data: {
      fileName: 'qa-occurrence.png',
      contentType: 'image/png',
      sizeBytes: png.length,
    },
  });
  if (uploadResponse.status() !== 201) {
    throw new Error(`solicitação de upload falhou: ${uploadResponse.status()} ${await uploadResponse.text()}`);
  }
  const upload = await uploadResponse.json();

  const binaryUpload = await fetch(upload.uploadUrl, {
    method: 'PUT',
    headers: { 'Content-Type': 'image/png' },
    body: png,
  });
  if (!binaryUpload.ok) {
    throw new Error(`upload binário R2 falhou: ${binaryUpload.status}`);
  }
  console.log('occurrence_media_binary_upload=OK');

  const confirmResponse = await context.request.post(
    `${process.env.BASE}/api/v1/occurrence-media/${upload.id}/confirm`,
    { headers: authHeaders },
  );
  if (confirmResponse.status() !== 200) {
    throw new Error(`confirmação da mídia falhou: ${confirmResponse.status()} ${await confirmResponse.text()}`);
  }
  const confirmed = await confirmResponse.json();
  if (confirmed.status !== 'READY') throw new Error(`mídia não ficou READY: ${confirmed.status}`);
  console.log('occurrence_media_confirm=OK');

  const createResponse = await context.request.post(`${process.env.BASE}/api/v1/occurrences`, {
    headers: authHeaders,
    data: {
      categoryId: category.id,
      title: `QA mídia ${Date.now()}`,
      description: 'Validação E2E da apresentação da imagem após upload.',
      addressText: 'Porto Alegre - RS',
      latitude: -30.0346,
      longitude: -51.2177,
      postalCode: null,
      cityId: null,
      stateCode: 'RS',
      externalProtocolNumber: null,
      externalProtocolAgency: null,
      mediaIds: [upload.id],
    },
  });
  if (createResponse.status() !== 201) {
    throw new Error(`criação da ocorrência falhou: ${createResponse.status()} ${await createResponse.text()}`);
  }
  const occurrence = await createResponse.json();
  console.log(`occurrence_with_media_created=OK code=${occurrence.publicCode}`);

  const mediaListResponse = await context.request.get(
    `${process.env.BASE}/api/v1/occurrences/${occurrence.id}/media`,
    { headers: authHeaders },
  );
  if (mediaListResponse.status() !== 200) {
    throw new Error(`listagem da mídia vinculada falhou: ${mediaListResponse.status()}`);
  }
  const mediaList = await mediaListResponse.json();
  if (mediaList.length !== 1 || mediaList[0].id !== upload.id || mediaList[0].status !== 'READY') {
    throw new Error(`mídia vinculada inesperada: ${JSON.stringify(mediaList)}`);
  }
  console.log('occurrence_media_attached=OK');

  const readUrlResponse = await context.request.get(
    `${process.env.BASE}/api/v1/occurrence-media/${upload.id}/read-url`,
    { headers: authHeaders },
  );
  if (readUrlResponse.status() !== 200) {
    throw new Error(`URL assinada falhou: ${readUrlResponse.status()} ${await readUrlResponse.text()}`);
  }
  const read = await readUrlResponse.json();
  const storedImage = await fetch(read.readUrl);
  if (!storedImage.ok) throw new Error(`leitura R2 falhou: ${storedImage.status}`);
  console.log('occurrence_media_signed_read=OK');

  await page.goto(`${process.env.BASE}/painel`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.getByRole('heading', { name: 'Publicadas por você', exact: true }).waitFor({ state: 'visible', timeout: 15000 });

  const card = page.locator('article.occurrence-list-item').filter({ hasText: occurrence.publicCode }).first();
  await card.waitFor({ state: 'visible', timeout: 15000 });
  const image = card.getByRole('img', { name: 'qa-occurrence.png', exact: true });
  await image.waitFor({ state: 'visible', timeout: 15000 });
  const imageState = await image.evaluate(element => ({
    complete: element.complete,
    naturalWidth: element.naturalWidth,
    naturalHeight: element.naturalHeight,
  }));
  if (!imageState.complete || imageState.naturalWidth < 1 || imageState.naturalHeight < 1) {
    throw new Error(`imagem não carregou no card: ${JSON.stringify(imageState)}`);
  }
  console.log('occurrence_media_image_visible=OK');

  await page.screenshot({ path: '/work/occurrence-media-private-card.png', fullPage: true });

  if (errors.length) throw new Error(`pageerror: ${errors.join(' | ')}`);
} finally {
  await context.close();
  await browser.close();
}
JS

test -f "$QA_DIR/occurrence-media-private-card.png" || fail "screenshot da ocorrência com mídia ausente"

echo
echo "=== 4. LIMPEZA / ESTADO FINAL ==="
cleanup_qa_user
QA_LEFT="$(compose exec -T db sh -lc "psql -At -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -c \"SELECT count(*) FROM users WHERE email = '${QA_EMAIL}';\"")"
test "$QA_LEFT" = "0" || fail "usuário QA não foi removido"
trap - EXIT

test -z "$(git -C "$ROOT" status --porcelain)" || fail "main worktree ficou suja"

echo "qa_cleanup=OK"
echo "============================================================"
echo "OCCURRENCE MEDIA PRESENTATION — FEATURE HOMOLOG: OK"
echo "QA AUTH: OK"
echo "R2 UPLOAD + CONFIRM: OK"
echo "MEDIA ATTACHED TO OCCURRENCE: OK"
echo "SIGNED MEDIA READ: OK"
echo "IMAGE VISIBLE IN PRIVATE OCCURRENCE CARD: OK"
echo "QA CLEANUP: OK"
echo "MAIN WORKTREE: CLEAN"
echo "SCREENSHOT: $QA_DIR/occurrence-media-private-card.png"
echo "============================================================"
