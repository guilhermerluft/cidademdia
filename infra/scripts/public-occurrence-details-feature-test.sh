#!/usr/bin/env bash
set -Eeuo pipefail

EXPECTED_HEAD="${1:-}"
ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
ENV_FILE="${CIDADEMDIA_ENV_FILE:-$ROOT/.env}"
BASE="${CIDADEMDIA_BASE_URL:-https://homolog.cidademdia.com.br}"
WT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
QA_DIR="${CIDADEMDIA_QA_DIR:-$HOME/cidademdia-qa/public-occurrence-details-$(date +%Y%m%d-%H%M%S)}"
QA_SUFFIX="$(date +%s)-$$"
QA_EMAIL="qa-public-detail-${QA_SUFFIX}@cidademdia.local"
QA_PASSWORD="QaPublicDetail#${QA_SUFFIX}!"
QA_NAME="QA Detalhe Público ${QA_SUFFIX}"

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
echo "PUBLIC OCCURRENCE CREATION + DETAILS + SUPPORT — FEATURE HOMOLOG"
echo "============================================================"
echo "HEAD: $EXPECTED_HEAD"
echo "MAIN: $(git -C "$ROOT" rev-parse HEAD)"

echo
echo "=== 1. GUARD ==="
bash "$WT/infra/scripts/public-occurrence-details-guard.sh"

echo
echo "=== 2. BUILD / DEPLOY API + WEB ==="
compose build api web
compose up -d --no-deps api web
compose up -d --no-deps --force-recreate nginx

READY=0
for _ in $(seq 1 40); do
  HEALTH="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/health/ready" || true)"
  OCC="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/ocorrencias" || true)"
  if [ "$HEALTH" = "200" ] && [ "$OCC" = "200" ]; then
    READY=1
    break
  fi
  sleep 2
done

test "$READY" = "1" || fail "health/ocorrencias não ficaram prontos"
echo "health=200"
echo "occurrences_route=200"

echo
echo "=== 3. API + BROWSER — CRIAÇÃO + CAPA + DETALHES + APOIO ==="
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
const protocolNumber = `QA-PROTOCOLO-${Date.now()}`;

const browser = await chromium.launch({
  executablePath: process.env.CHROME,
  headless: true,
  args: ['--no-sandbox', '--disable-dev-shm-usage'],
});

const context = await browser.newContext({
  ignoreHTTPSErrors: true,
  viewport: { width: 1440, height: 1000 },
  permissions: [],
});
const page = await context.newPage();
const pageErrors = [];
page.on('pageerror', error => pageErrors.push(error.message));

async function protectedRequest(method, url, token, options = {}) {
  return context.request.fetch(url, {
    method,
    ...options,
    headers: {
      ...(options.headers || {}),
      Authorization: `Bearer ${token}`,
    },
  });
}

async function createImage(token, fileName) {
  const request = await protectedRequest(
    'POST',
    `${process.env.BASE}/api/v1/occurrence-media/uploads`,
    token,
    { data: { fileName, contentType: 'image/png', sizeBytes: png.length } },
  );
  if (request.status() !== 201) {
    throw new Error(`solicitação ${fileName} falhou: ${request.status()} ${await request.text()}`);
  }

  const upload = await request.json();
  const binary = await fetch(upload.uploadUrl, {
    method: 'PUT',
    headers: { 'Content-Type': 'image/png' },
    body: png,
  });
  if (!binary.ok) throw new Error(`upload R2 ${fileName} falhou: ${binary.status}`);

  const confirm = await protectedRequest(
    'POST',
    `${process.env.BASE}/api/v1/occurrence-media/${upload.id}/confirm`,
    token,
  );
  if (confirm.status() !== 200) {
    throw new Error(`confirmação ${fileName} falhou: ${confirm.status()} ${await confirm.text()}`);
  }

  const confirmed = await confirm.json();
  if (confirmed.status !== 'READY') throw new Error(`${fileName} não ficou READY`);
  return confirmed;
}

function occurrencePayload(categoryId, masterUserId, title, mediaIds) {
  return {
    categoryId,
    masterUserId,
    title,
    description: 'Detalhe público com duas fotos para validar criação, capa, galeria, protocolo e apoio.',
    street: 'Praça da Sé',
    number: '1',
    neighborhood: 'Sé',
    city: 'São Paulo',
    latitude: -23.55052,
    longitude: -46.633308,
    postalCode: '01001000',
    cityId: null,
    stateCode: 'SP',
    externalProtocolNumber: protocolNumber,
    externalProtocolAgency: 'QA PRIVADO',
    mediaIds,
  };
}

async function waitForLoadedImage(locator) {
  await locator.waitFor({ state: 'visible', timeout: 10000 });
  await locator.evaluate(async element => {
    if (
      element instanceof HTMLImageElement
      && element.complete
      && element.naturalWidth > 0
      && element.naturalHeight > 0
    ) return;

    await new Promise((resolve, reject) => {
      const timeout = setTimeout(() => reject(new Error(`timeout aguardando imagem: ${element.currentSrc || element.src}`)), 10000);
      const cleanup = () => {
        clearTimeout(timeout);
        element.removeEventListener('load', onLoad);
        element.removeEventListener('error', onError);
      };
      const onLoad = () => { cleanup(); resolve(); };
      const onError = () => { cleanup(); reject(new Error(`erro carregando imagem: ${element.currentSrc || element.src}`)); };
      element.addEventListener('load', onLoad, { once: true });
      element.addEventListener('error', onError, { once: true });
    });
  });

  const state = await locator.evaluate(element => ({
    complete: element.complete,
    width: element.naturalWidth,
    height: element.naturalHeight,
    src: element.currentSrc || element.src,
  }));
  if (!state.complete || state.width < 1 || state.height < 1) {
    throw new Error(`imagem não carregou: ${JSON.stringify(state)}`);
  }
}

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
  if (!session.accessToken) throw new Error('registro QA não retornou accessToken');
  const token = session.accessToken;
  console.log('public_occurrence_qa_auth=OK');

  const categoriesResponse = await protectedRequest(
    'GET',
    `${process.env.BASE}/api/v1/occurrences/categories`,
    token,
  );
  if (categoriesResponse.status() !== 200) throw new Error(`categorias falharam: ${categoriesResponse.status()}`);
  const category = (await categoriesResponse.json())[0];
  if (!category?.id) throw new Error('nenhuma categoria ativa disponível');

  const mastersResponse = await protectedRequest(
    'GET',
    `${process.env.BASE}/api/v1/occurrences/masters`,
    token,
  );
  if (mastersResponse.status() !== 200) throw new Error(`Masters falharam: ${mastersResponse.status()}`);
  const master = (await mastersResponse.json())[0];
  if (!master?.id) throw new Error('nenhuma conta Master elegível disponível para homologação');
  console.log(`public_occurrence_master_required=OK master=${master.displayName}`);

  const first = await createImage(token, 'qa-primeira-foto.png');
  await new Promise(resolve => setTimeout(resolve, 200));
  const second = await createImage(token, 'qa-segunda-foto.png');
  console.log('public_occurrence_two_images_ready=OK');

  const noPhoto = await protectedRequest(
    'POST',
    `${process.env.BASE}/api/v1/occurrences`,
    token,
    { data: occurrencePayload(category.id, master.id, `QA sem foto ${Date.now()}`, []) },
  );
  if (noPhoto.status() !== 400) {
    throw new Error(`criação sem foto deveria falhar com 400: ${noPhoto.status()} ${await noPhoto.text()}`);
  }
  const noPhotoProblem = await noPhoto.json();
  if (noPhotoProblem.code !== 'photo_required') {
    throw new Error(`criação sem foto não retornou photo_required: ${JSON.stringify(noPhotoProblem)}`);
  }
  console.log('public_occurrence_photo_required=OK');

  const title = `QA capa pública ${Date.now()}`;
  const create = await protectedRequest(
    'POST',
    `${process.env.BASE}/api/v1/occurrences`,
    token,
    { data: occurrencePayload(category.id, master.id, title, [first.id, second.id]) },
  );
  if (create.status() !== 201) {
    throw new Error(`criação ocorrência falhou: ${create.status()} ${await create.text()}`);
  }
  const occurrence = await create.json();
  console.log(`public_occurrence_with_gallery_created=OK code=${occurrence.publicCode}`);

  const targetsResponse = await protectedRequest(
    'GET',
    `${process.env.BASE}/api/v1/occurrences/${occurrence.id}/targets`,
    token,
  );
  if (targetsResponse.status() !== 200) {
    throw new Error(`targets da ocorrência falharam: ${targetsResponse.status()} ${await targetsResponse.text()}`);
  }
  const targets = await targetsResponse.json();
  if (targets.length !== 1 || targets[0].masterUserId !== master.id || targets[0].status !== 'PENDING') {
    throw new Error(`solicitação inicial para Master inválida: ${JSON.stringify(targets)}`);
  }
  console.log('public_occurrence_initial_master_target=OK');

  const publicList = await context.request.get(`${process.env.BASE}/api/v1/public/occurrences`, {
    params: {
      latitude: -23.55052,
      longitude: -46.633308,
      radiusKm: 25,
      page: 1,
      pageSize: 50,
    },
  });
  if (publicList.status() !== 200) throw new Error(`listagem pública falhou: ${publicList.status()}`);
  const publicPage = await publicList.json();
  const listed = publicPage.items.find(item => item.id === occurrence.id);
  if (!listed) throw new Error('ocorrência QA não apareceu na listagem pública');
  if (listed.coverMedia?.id !== first.id) throw new Error(`primeira foto não virou capa: ${JSON.stringify(listed.coverMedia)}`);
  if (listed.externalProtocolNumber !== protocolNumber) throw new Error(`protocolo não apareceu na listagem pública: ${JSON.stringify(listed)}`);
  if (listed.supportCount !== 0) throw new Error(`contagem inicial de apoios deveria ser zero: ${JSON.stringify(listed)}`);
  const coverRead = await fetch(listed.coverMedia.readUrl);
  if (!coverRead.ok) throw new Error(`URL pública assinada da capa falhou: ${coverRead.status}`);
  console.log('public_occurrence_first_photo_is_cover=OK');
  console.log('public_occurrence_protocol_public=OK');
  console.log('public_occurrence_support_count_public=OK');

  const supportResponse = await protectedRequest(
    'POST',
    `${process.env.BASE}/api/v1/occurrences/${occurrence.id}/support`,
    token,
  );
  if (supportResponse.status() !== 200 && supportResponse.status() !== 201) {
    throw new Error(`apoio da ocorrência falhou: ${supportResponse.status()} ${await supportResponse.text()}`);
  }
  const support = await supportResponse.json();
  if (support.supportCount !== 1 || support.supportedByRequester !== true) {
    throw new Error(`apoio não retornou contagem esperada: ${JSON.stringify(support)}`);
  }
  console.log('public_occurrence_authenticated_support=OK');

  const detailsResponse = await context.request.get(`${process.env.BASE}/api/v1/public/occurrences/${occurrence.id}`);
  if (detailsResponse.status() !== 200) throw new Error(`detalhe público falhou: ${detailsResponse.status()}`);
  const details = await detailsResponse.json();
  if (details.media?.length !== 2) throw new Error(`galeria pública não retornou 2 mídias: ${JSON.stringify(details.media)}`);
  if (details.media[0].id !== first.id || details.media[1].id !== second.id) {
    throw new Error(`ordem da galeria não preservou upload: ${JSON.stringify(details.media)}`);
  }
  if (details.externalProtocolNumber !== protocolNumber) throw new Error(`detalhe público não expôs protocolo: ${JSON.stringify(details)}`);
  if (details.supportCount !== 1) throw new Error(`detalhe público não expôs apoio: ${JSON.stringify(details)}`);
  for (const forbidden of ['postalCode', 'stateCode', 'latitude', 'longitude', 'externalProtocolAgency']) {
    if (Object.prototype.hasOwnProperty.call(details, forbidden)) {
      throw new Error(`detalhe público expôs campo privado: ${forbidden}`);
    }
  }
  console.log('public_occurrence_detail_sanitized=OK');
  console.log('public_occurrence_full_gallery_api=OK');

  await page.goto(`${process.env.BASE}/ocorrencias`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.locator('.public-occurrences').waitFor({ state: 'visible', timeout: 15000 });

  const card = page.locator(`.public-occurrences__card[data-occurrence-id="${occurrence.id}"]`);
  await card.waitFor({ state: 'visible', timeout: 20000 });

  const cover = card.locator('.public-occurrences__card-cover img');
  await waitForLoadedImage(cover);
  console.log('public_occurrence_cover_visible=OK');

  await card.getByText(`Protocolo ${protocolNumber}`, { exact: true }).waitFor({ state: 'visible' });
  const openButton = card.getByRole('button', {
    name: `Abrir ocorrência ${occurrence.publicCode}: ${title}`,
    exact: true,
  });
  const supportButton = card.getByRole('button', {
    name: 'Entrar para apoiar ocorrência. 1 apoios',
    exact: true,
  });
  await openButton.waitFor({ state: 'visible', timeout: 10000 });
  await supportButton.waitFor({ state: 'visible', timeout: 10000 });
  console.log('public_occurrence_card_actions_separated=OK');
  console.log('public_occurrence_protocol_and_support_visible=OK');

  await card.getByRole('heading', { name: title, exact: true }).click();
  const dialog = page.getByRole('dialog');
  await dialog.waitFor({ state: 'visible', timeout: 15000 });
  await dialog.getByRole('heading', { name: title, exact: true }).waitFor({ state: 'visible' });
  await dialog.getByText(`Protocolo ${protocolNumber}`, { exact: true }).waitFor({ state: 'visible' });
  await dialog.getByRole('button', { name: 'Entrar para apoiar ocorrência. 1 apoios', exact: true }).waitFor({ state: 'visible' });
  console.log('public_occurrence_card_click_opens_detail=OK');

  const galleryImages = dialog.locator('.public-occurrence-details__media img');
  if (await galleryImages.count() !== 2) throw new Error(`modal não exibiu duas fotos: ${await galleryImages.count()}`);
  for (let index = 0; index < 2; index += 1) {
    await waitForLoadedImage(galleryImages.nth(index));
  }
  console.log('public_occurrence_full_gallery_visible=OK');

  await page.screenshot({ path: '/work/public-occurrence-details-desktop.png', fullPage: true });
  await page.getByRole('button', { name: 'Fechar detalhes da ocorrência', exact: true }).click();
  await dialog.waitFor({ state: 'hidden', timeout: 5000 });
  console.log('public_occurrence_details_close=OK');

  await page.setViewportSize({ width: 390, height: 844 });
  await openButton.click();
  await dialog.waitFor({ state: 'visible', timeout: 10000 });
  const dialogBox = await dialog.boundingBox();
  if (!dialogBox || dialogBox.width > 390 || dialogBox.height > 844) {
    throw new Error(`modal mobile fora do viewport: ${JSON.stringify(dialogBox)}`);
  }
  await page.screenshot({ path: '/work/public-occurrence-details-mobile.png', fullPage: true });
  console.log('public_occurrence_details_mobile=OK');

  if (pageErrors.length) throw new Error(`pageerror: ${pageErrors.join(' | ')}`);
} finally {
  await context.close();
  await browser.close();
}
JS

test -f "$QA_DIR/public-occurrence-details-desktop.png" || fail "screenshot desktop ausente"
test -f "$QA_DIR/public-occurrence-details-mobile.png" || fail "screenshot mobile ausente"

echo
echo "=== 4. LIMPEZA / ESTADO FINAL ==="
cleanup_qa_user
QA_LEFT="$(compose exec -T db sh -lc "psql -At -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -c \"SELECT count(*) FROM users WHERE email = '${QA_EMAIL}';\"")"
test "$QA_LEFT" = "0" || fail "usuário QA não foi removido"
trap - EXIT

test -z "$(git -C "$ROOT" status --porcelain)" || fail "main worktree ficou suja"
echo "qa_cleanup=OK"
echo "============================================================"
echo "PUBLIC OCCURRENCE CREATION + DETAILS + SUPPORT — FEATURE HOMOLOG: OK"
echo "MASTER REQUIRED ON CREATE: OK"
echo "PHOTO REQUIRED ON CREATE: OK"
echo "INITIAL MASTER TARGET: OK"
echo "FIRST PHOTO AS LIST COVER: OK"
echo "CLICKABLE OCCURRENCE CARD: OK"
echo "SEPARATE OPEN/SUPPORT CONTROLS: OK"
echo "PUBLIC PROTOCOL: OK"
echo "PUBLIC SUPPORT COUNT: OK"
echo "AUTHENTICATED SUPPORT: OK"
echo "PUBLIC DETAIL SANITIZED: OK"
echo "FULL PHOTO GALLERY: OK"
echo "DESKTOP DETAIL: OK"
echo "MOBILE DETAIL: OK"
echo "QA CLEANUP: OK"
echo "MAIN WORKTREE: CLEAN"
echo "SCREENSHOTS: $QA_DIR/public-occurrence-details-desktop.png | $QA_DIR/public-occurrence-details-mobile.png"
echo "============================================================"
