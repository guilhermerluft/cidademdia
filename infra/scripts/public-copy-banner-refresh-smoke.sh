#!/usr/bin/env bash
set -Eeuo pipefail

EXPECTED_HEAD="${1:-}"
ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
BASE="${CIDADEMDIA_BASE_URL:-https://homolog.cidademdia.com.br}"
WT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
QA_DIR="${CIDADEMDIA_QA_DIR:-$HOME/cidademdia-qa/public-copy-banner-$(date +%Y%m%d-%H%M%S)}"

fail() {
  echo
  echo "ERRO: $*" >&2
  exit 1
}

for cmd in git docker curl; do
  command -v "$cmd" >/dev/null 2>&1 || fail "comando ausente: $cmd"
done

test -n "$EXPECTED_HEAD" || fail "informe o HEAD esperado"
test -d "$ROOT/.git" || fail "repo principal não encontrado"
test "$(git -C "$WT" rev-parse HEAD)" = "$EXPECTED_HEAD" || fail "worktree fora do HEAD esperado"
test "$(git -C "$ROOT" branch --show-current)" = "main" || fail "repo principal não está na main"
test -z "$(git -C "$ROOT" status --porcelain)" || fail "main local está suja"

mkdir -p "$QA_DIR"

echo "============================================================"
echo "PUBLIC COPY + BANNER REFRESH — HOMOLOG SMOKE"
echo "============================================================"
echo "HEAD: $EXPECTED_HEAD"
echo "BASE: $BASE"

for path in / /planos /representantes; do
  code="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE$path")"
  test "$code" = "200" || fail "$path != 200 ($code)"
  echo "$path=$code"
done

docker run --rm -i \
  --network host \
  -v "$QA_DIR:/work" \
  -e BASE="$BASE" \
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

const removedHomeMedia = 'Os vídeos publicados pelo painel administrador do CIDADEMDIA aparecerão aqui.';
const removedHomeOccurrences = 'Novas demandas públicas aparecerão aqui quando forem registradas.';
const removedPlansNotice = 'Os valores e condições de pagamento abaixo são promocionais e carregados diretamente do catálogo público vigente do CIDADEMDIA.';
const oldMastersSubtitle = 'Consulte órgãos e agentes públicos cadastrados no CidadeEmDia, inclusive perfis que ainda não aderiram à plataforma.';
const newMastersSubtitle = 'Consulte órgãos e agentes públicos cadastrados no CidadeEmDia.';

const browser = await chromium.launch({
  executablePath: process.env.CHROME,
  headless: true,
  args: ['--no-sandbox', '--disable-dev-shm-usage'],
});

async function openPage(path, viewport) {
  const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport });
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto(`${process.env.BASE}${path}`, { waitUntil: 'networkidle', timeout: 30000 });
  return { context, page, errors };
}

async function validateHome(viewport, screenshot) {
  const { context, page, errors } = await openPage('/', viewport);
  try {
    await page.locator('.public-home__hero').waitFor({ state: 'visible', timeout: 15000 });

    const backgroundImage = await page.locator('.public-home__hero').evaluate(
      element => getComputedStyle(element).backgroundImage,
    );
    if (!backgroundImage.includes('banner-city')) {
      throw new Error(`banner anexado não está ativo no hero: ${backgroundImage}`);
    }

    const body = await page.locator('body').innerText();
    for (const removed of [removedHomeMedia, removedHomeOccurrences]) {
      if (body.includes(removed)) throw new Error(`texto removido ainda visível na Home: ${removed}`);
    }

    if (errors.length) throw new Error(`pageerror na Home: ${errors.join(' | ')}`);
    await page.screenshot({ path: `/work/${screenshot}`, fullPage: true });
  } finally {
    await context.close();
  }
}

async function validatePlans() {
  const { context, page, errors } = await openPage('/planos', { width: 1440, height: 1000 });
  try {
    await page.locator('.plans-page').waitFor({ state: 'visible', timeout: 15000 });
    const body = await page.locator('body').innerText();
    if (body.includes(removedPlansNotice)) {
      throw new Error('aviso promocional removido ainda está visível em /planos');
    }
    if (errors.length) throw new Error(`pageerror em /planos: ${errors.join(' | ')}`);
    await page.screenshot({ path: '/work/plans-desktop.png', fullPage: true });
  } finally {
    await context.close();
  }
}

async function validateMasters() {
  const { context, page, errors } = await openPage('/representantes', { width: 1440, height: 1000 });
  try {
    await page.locator('.institution-directory').waitFor({ state: 'visible', timeout: 15000 });
    await page.getByText(newMastersSubtitle, { exact: true }).waitFor({ state: 'visible', timeout: 15000 });
    const body = await page.locator('body').innerText();
    if (body.includes(oldMastersSubtitle)) {
      throw new Error('subtítulo antigo ainda está visível em /representantes');
    }
    if (errors.length) throw new Error(`pageerror em /representantes: ${errors.join(' | ')}`);
    await page.screenshot({ path: '/work/masters-desktop.png', fullPage: true });
  } finally {
    await context.close();
  }
}

try {
  await validateHome({ width: 1440, height: 1000 }, 'home-desktop.png');
  console.log('home_banner_desktop=OK');
  await validateHome({ width: 390, height: 844 }, 'home-mobile.png');
  console.log('home_banner_mobile=OK');
  await validatePlans();
  console.log('plans_copy=OK');
  await validateMasters();
  console.log('masters_copy=OK');
} finally {
  await browser.close();
}
JS

for screenshot in home-desktop.png home-mobile.png plans-desktop.png masters-desktop.png; do
  test -f "$QA_DIR/$screenshot" || fail "screenshot ausente: $screenshot"
done

echo "attached_home_banner=OK"
echo "home_placeholder_copy_removed=OK"
echo "plans_notice_removed=OK"
echo "masters_subtitle_updated=OK"
echo "MAIN WORKTREE: CLEAN"
echo "SCREENSHOTS: $QA_DIR"
echo "PUBLIC COPY + BANNER REFRESH — HOMOLOG SMOKE: OK"
echo "============================================================"
