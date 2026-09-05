#!/usr/bin/env bash
set -Eeuo pipefail

EXPECTED_HEAD="${1:-}"
ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
BASE="${CIDADEMDIA_BASE_URL:-https://homolog.cidademdia.com.br}"
WT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
QA_DIR="${CIDADEMDIA_QA_DIR:-$HOME/cidademdia-qa/masters-directory-$(date +%Y%m%d-%H%M%S)}"

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

mkdir -p "$QA_DIR"

echo "============================================================"
echo "PUBLIC MASTERS DIRECTORY — SMOKE"
echo "============================================================"
echo "HEAD: $EXPECTED_HEAD"

CODE="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/representantes")"
test "$CODE" = "200" || fail "/representantes != 200"
echo "masters_directory_http=200"

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

const browser = await chromium.launch({
  executablePath: process.env.CHROME,
  headless: true,
  args: ['--no-sandbox', '--disable-dev-shm-usage'],
});

async function validate(viewport, screenshot, mobile) {
  const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport });
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));

  await page.goto(`${process.env.BASE}/representantes`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.locator('.representatives-page').waitFor({ state: 'visible', timeout: 15000 });
  await page.locator('.institution-directory').waitFor({ state: 'visible', timeout: 15000 });

  const headerLabels = await page.locator('.app-header__nav .app-header__nav-item').allInnerTexts();
  if (headerLabels.join('|') !== 'Início|Planos|Ocorrências|Masters') {
    throw new Error(`header público inesperado: ${headerLabels.join('|')}`);
  }

  await page.getByRole('heading', { name: 'Órgãos e agentes públicos' }).waitFor({ state: 'visible' });
  await page.getByText('Buscar órgão ou agente público', { exact: true }).waitFor({ state: 'visible' });

  const visibleText = await page.locator('body').innerText();
  for (const forbidden of [
    'Instituições e representantes',
    'Consulte órgãos e representantes',
    'Buscar instituição ou representante',
    'Nenhum representante cadastrado ainda.',
    'Carregando representantes...',
  ]) {
    if (visibleText.includes(forbidden)) {
      throw new Error(`texto público antigo ainda visível: ${forbidden}`);
    }
  }

  if (mobile) {
    const bottomLabels = await page.locator('.app-bottom-nav__item').allInnerTexts();
    if (!bottomLabels.some(label => label.includes('Masters'))) {
      throw new Error('bottom nav mobile não exibe Masters');
    }
  }

  if (errors.length) throw new Error(`pageerror em /representantes: ${errors.join(' | ')}`);
  await page.screenshot({ path: `/work/${screenshot}`, fullPage: true });
  await context.close();
}

try {
  await validate({ width: 1440, height: 1000 }, 'masters-directory-desktop.png', false);
  console.log('masters_directory_desktop=OK');
  await validate({ width: 390, height: 844 }, 'masters-directory-mobile.png', true);
  console.log('masters_directory_mobile=OK');
} finally {
  await browser.close();
}
JS

test -f "$QA_DIR/masters-directory-desktop.png" || fail "screenshot desktop ausente"
test -f "$QA_DIR/masters-directory-mobile.png" || fail "screenshot mobile ausente"

echo "PUBLIC MASTERS NAVIGATION: OK"
echo "PUBLIC INSTITUTION TERMINOLOGY: OK"
echo "DESKTOP: OK"
echo "MOBILE: OK"
echo "MAIN WORKTREE: CLEAN"
echo "SCREENSHOT DESKTOP: $QA_DIR/masters-directory-desktop.png"
echo "SCREENSHOT MOBILE: $QA_DIR/masters-directory-mobile.png"
echo "PUBLIC MASTERS DIRECTORY — SMOKE: OK"
echo "============================================================"
