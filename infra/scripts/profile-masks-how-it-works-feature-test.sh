#!/usr/bin/env bash
set -Eeuo pipefail

EXPECTED_HEAD="${1:-}"
ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
ENV_FILE="${CIDADEMDIA_ENV_FILE:-$ROOT/.env}"
BASE="${CIDADEMDIA_BASE_URL:-https://homolog.cidademdia.com.br}"
MEDIA_DIR="${CIDADEMDIA_RUNTIME_MEDIA_DIR:-/opt/cidademdia/runtime-media}"
VIDEO_FILE="$MEDIA_DIR/como-funciona.mp4"
WT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
QA_DIR="${CIDADEMDIA_QA_DIR:-$HOME/cidademdia-qa/profile-masks-how-it-works-$(date +%Y%m%d-%H%M%S)}"
QA_SUFFIX="$(date +%s)-$$"
QA_EMAIL="qa-masks-video-${QA_SUFFIX}@cidademdia.local"
QA_PASSWORD="QaMasksVideo#${QA_SUFFIX}!"
QA_NAME="QA Máscaras Vídeo ${QA_SUFFIX}"

fail() {
  echo
  echo "ERRO: $*" >&2
  exit 1
}

for cmd in git docker curl grep stat; do
  command -v "$cmd" >/dev/null 2>&1 || fail "comando ausente: $cmd"
done

test -n "$EXPECTED_HEAD" || fail "informe o HEAD esperado"
test "$(git -C "$WT" rev-parse HEAD)" = "$EXPECTED_HEAD" || fail "worktree fora do HEAD esperado"
test "$(git -C "$ROOT" branch --show-current)" = "main" || fail "repo principal não está na main"
test -z "$(git -C "$ROOT" status --porcelain)" || fail "main local está suja"
test -f "$ENV_FILE" || fail ".env não encontrado"
test -s "$VIDEO_FILE" || fail "vídeo operacional ausente em $VIDEO_FILE"
VIDEO_BYTES="$(stat -c '%s' "$VIDEO_FILE")"
test "$VIDEO_BYTES" -gt 100000 || fail "vídeo operacional parece inválido: ${VIDEO_BYTES} bytes"

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
    "psql -v ON_ERROR_STOP=1 -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -c \"DELETE FROM users WHERE email = '${QA_EMAIL}';\"" \
    >/dev/null 2>&1 || true
}
trap cleanup_qa_user EXIT

echo "============================================================"
echo "PROFILE MASKS + HOW IT WORKS VIDEO — FEATURE HOMOLOG"
echo "============================================================"
echo "HEAD: $EXPECTED_HEAD"
echo "MAIN: $(git -C "$ROOT" rev-parse HEAD)"
echo "VIDEO: $VIDEO_FILE (${VIDEO_BYTES} bytes)"

echo
echo "=== 1. ARQUITETURA ==="
bash "$WT/infra/scripts/frontend-architecture-test.sh"
echo "runtime_video_present=OK"

echo
echo "=== 2. BUILD / DEPLOY WEB + NGINX ==="
compose build web
compose up -d --no-deps web
compose up -d --no-deps --force-recreate nginx

READY=0
for _ in $(seq 1 40); do
  HEALTH="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/health/ready" || true)"
  HOME_CODE="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/" || true)"
  PROFILE_CODE="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/perfil" || true)"
  VIDEO_CODE="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/media/como-funciona.mp4" || true)"
  if [ "$HEALTH" = "200" ] && [ "$HOME_CODE" = "200" ] && [ "$PROFILE_CODE" = "200" ] && [ "$VIDEO_CODE" = "200" ]; then
    READY=1
    break
  fi
  sleep 2
done

test "$READY" = "1" || fail "health/home/perfil/vídeo não ficaram prontos"
echo "health=200"
echo "home=200"
echo "profile_shell=200"
echo "how_it_works_video=200"

HEADERS="$QA_DIR/video-headers.txt"
PREFIX="$QA_DIR/video-prefix.bin"
mkdir -p "$QA_DIR"
RANGE_CODE="$(curl -sS -D "$HEADERS" -o "$PREFIX" -r 0-31 -w '%{http_code}' "$BASE/media/como-funciona.mp4")"
test "$RANGE_CODE" = "206" || fail "vídeo não suporta Range; HTTP $RANGE_CODE"
grep -qi '^Content-Type: video/mp4' "$HEADERS" || fail "vídeo não foi servido como video/mp4"
test "$(stat -c '%s' "$PREFIX")" = "32" || fail "Range do vídeo não retornou 32 bytes"
echo "how_it_works_video_range=OK"
echo "how_it_works_video_content_type=OK"

echo
echo "=== 3. BROWSER — MODAL + MÁSCARAS ==="
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

async function waitForVideoMetadata(video) {
  await video.evaluate(element => new Promise((resolve, reject) => {
    if (element.readyState >= 1 && Number.isFinite(element.duration)) {
      resolve(undefined);
      return;
    }

    const timeout = window.setTimeout(() => reject(new Error('timeout carregando metadata do vídeo')), 15000);
    element.addEventListener('loadedmetadata', () => {
      window.clearTimeout(timeout);
      resolve(undefined);
    }, { once: true });
    element.addEventListener('error', () => {
      window.clearTimeout(timeout);
      reject(new Error('erro carregando vídeo Como funciona'));
    }, { once: true });
  }));
}

try {
  await page.goto(`${process.env.BASE}/`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  const howButton = page.getByRole('button', { name: 'Como funciona', exact: true });
  await howButton.waitFor({ state: 'visible', timeout: 15000 });
  await howButton.click();

  const dialog = page.getByRole('dialog', { name: 'Como funciona', exact: true });
  await dialog.waitFor({ state: 'visible', timeout: 10000 });
  const video = dialog.locator('video');
  await video.waitFor({ state: 'visible', timeout: 10000 });
  const source = await video.getAttribute('src');
  if (source !== '/media/como-funciona.mp4') {
    throw new Error(`src inesperado do vídeo: ${source}`);
  }
  await waitForVideoMetadata(video);
  const duration = await video.evaluate(element => element.duration);
  if (duration < 54 || duration > 57) {
    throw new Error(`duração inesperada do vídeo: ${duration}`);
  }
  await video.evaluate(element => element.pause());
  console.log(`how_it_works_modal_video=OK duration=${duration.toFixed(2)}`);
  await page.screenshot({ path: '/work/how-it-works-desktop.png', fullPage: true });

  await page.getByRole('button', { name: 'Fechar vídeo Como funciona', exact: true }).click();
  await dialog.waitFor({ state: 'hidden', timeout: 5000 });
  await howButton.click();
  await dialog.waitFor({ state: 'visible', timeout: 5000 });
  await page.keyboard.press('Escape');
  await dialog.waitFor({ state: 'hidden', timeout: 5000 });
  console.log('how_it_works_modal_close=OK');

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

  await page.goto(`${process.env.BASE}/perfil`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.getByRole('heading', { name: 'Meu perfil', exact: true }).waitFor({ state: 'visible', timeout: 15000 });

  const documentInput = page.getByPlaceholder('CPF ou CNPJ');
  const phoneInput = page.getByPlaceholder('(00) 00000-0000');

  await documentInput.fill('abc52998224725xyz');
  if (await documentInput.inputValue() !== '529.982.247-25') {
    throw new Error(`máscara CPF incorreta: ${await documentInput.inputValue()}`);
  }

  await documentInput.fill('12.345.678/9012-345678ABC');
  if (await documentInput.inputValue() !== '12.345.678/9012-34') {
    throw new Error(`máscara/limite CNPJ incorreto: ${await documentInput.inputValue()}`);
  }

  await phoneInput.fill('abc519999988889999xyz');
  if (await phoneInput.inputValue() !== '(51) 99999-8888') {
    throw new Error(`máscara/limite telefone incorreto: ${await phoneInput.inputValue()}`);
  }
  console.log('profile_input_masks=OK');

  await documentInput.fill('52998224725');
  await phoneInput.fill('51999998888');
  await page.getByRole('button', { name: 'Salvar alterações', exact: true }).click();
  await page.getByText('Perfil atualizado com sucesso.', { exact: true }).waitFor({ state: 'visible', timeout: 15000 });

  const profileResponse = await context.request.get(`${process.env.BASE}/api/v1/profile`);
  if (profileResponse.status() !== 200) throw new Error(`GET profile após edição: ${profileResponse.status()}`);
  const profilePayload = await profileResponse.json();
  if (profilePayload.document !== '52998224725') {
    throw new Error(`documento não persistiu normalizado: ${profilePayload.document}`);
  }
  if (profilePayload.phone !== '51999998888') {
    throw new Error(`telefone não persistiu normalizado: ${profilePayload.phone}`);
  }
  console.log('profile_masked_values_persist=OK');

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`${process.env.BASE}/`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  const mobileHowButton = page.getByRole('button', { name: 'Como funciona', exact: true });
  await mobileHowButton.waitFor({ state: 'visible', timeout: 15000 });
  await mobileHowButton.click();
  const mobileDialog = page.getByRole('dialog', { name: 'Como funciona', exact: true });
  await mobileDialog.waitFor({ state: 'visible', timeout: 5000 });
  const mobileVideo = mobileDialog.locator('video');
  await mobileVideo.waitFor({ state: 'visible', timeout: 5000 });
  const box = await mobileVideo.boundingBox();
  if (!box || box.width > 366 || box.height > 714) {
    throw new Error(`player mobile fora do viewport: ${JSON.stringify(box)}`);
  }
  await mobileVideo.evaluate(element => element.pause());
  await page.screenshot({ path: '/work/how-it-works-mobile.png', fullPage: true });
  console.log('how_it_works_modal_mobile=OK');

  if (errors.length) throw new Error(`pageerror: ${errors.join(' | ')}`);
} finally {
  await context.close();
  await browser.close();
}
JS

test -f "$QA_DIR/how-it-works-desktop.png" || fail "screenshot desktop do modal ausente"
test -f "$QA_DIR/how-it-works-mobile.png" || fail "screenshot mobile do modal ausente"

echo
echo "=== 4. LIMPEZA / ESTADO FINAL ==="
cleanup_qa_user
QA_LEFT="$(compose exec -T db sh -lc "psql -At -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -c \"SELECT count(*) FROM users WHERE email = '${QA_EMAIL}';\"")"
test "$QA_LEFT" = "0" || fail "usuário QA não foi removido"
trap - EXIT

test -z "$(git -C "$ROOT" status --porcelain)" || fail "main worktree ficou suja"
echo "qa_cleanup=OK"
echo "============================================================"
echo "PROFILE MASKS + HOW IT WORKS VIDEO — FEATURE HOMOLOG: OK"
echo "CPF MASK + 11 DIGIT LIMIT: OK"
echo "CNPJ MASK + 14 DIGIT LIMIT: OK"
echo "PHONE MASK + 11 DIGIT LIMIT: OK"
echo "INVALID CHARACTERS REMOVED: OK"
echo "HOW IT WORKS MODAL: OK"
echo "HOW IT WORKS VIDEO METADATA: OK"
echo "HOW IT WORKS VIDEO RANGE: OK"
echo "HOW IT WORKS MOBILE: OK"
echo "QA CLEANUP: OK"
echo "MAIN WORKTREE: CLEAN"
echo "SCREENSHOTS: $QA_DIR/how-it-works-desktop.png | $QA_DIR/how-it-works-mobile.png"
echo "============================================================"
