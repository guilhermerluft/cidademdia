#!/usr/bin/env bash
set -Eeuo pipefail

EXPECTED_HEAD="${1:-}"
ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
BASE="${CIDADEMDIA_BASE_URL:-https://homolog.cidademdia.com.br}"
WT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
QA_DIR="${CIDADEMDIA_QA_DIR:-$HOME/cidademdia-qa/home-benefit-cards-$(date +%Y%m%d-%H%M%S)}"

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

HEALTH="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/health/ready" || true)"
test "$HEALTH" = "200" || fail "homologação não está saudável"

echo "============================================================"
echo "PUBLIC HOME BENEFIT CARDS — FEATURE HOMOLOG"
echo "============================================================"
echo "HEAD: $EXPECTED_HEAD"

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

const context = await browser.newContext({
  ignoreHTTPSErrors: true,
  viewport: { width: 1440, height: 1000 },
  permissions: [],
});

try {
  const page = await context.newPage();
  await page.goto(process.env.BASE, { waitUntil: 'domcontentloaded', timeout: 30000 });

  const hero = page.locator('.public-home__hero');
  const heroInner = page.locator('.public-home__hero-inner');
  const benefits = page.locator('.public-home__hero-benefits');
  const cards = page.locator('.public-home__hero-benefit');

  await hero.waitFor({ state: 'visible', timeout: 10000 });
  await benefits.waitFor({ state: 'visible', timeout: 10000 });

  if (await cards.count() !== 4) {
    throw new Error(`hero deve ter 4 cards; encontrado=${await cards.count()}`);
  }

  const phrases = [
    'Apoie ocorrências da sua região',
    'Publique ocorrências gratuitamente',
    'Acompanhe detalhes e atualizações',
    'Converse com a Conta Master pelo chat',
  ];

  for (const phrase of phrases) {
    await benefits.getByText(phrase, { exact: true }).waitFor({ state: 'visible' });
  }

  for (const iconClass of ['fa-arrow-up', 'fa-bullhorn', 'fa-eye', 'fa-comments']) {
    if (await benefits.locator(`.${iconClass}`).count() !== 1) {
      throw new Error(`ícone Font Awesome ausente ou duplicado: ${iconClass}`);
    }
  }
  console.log('home_benefit_four_cards=OK');
  console.log('home_benefit_master_chat=OK');
  console.log('home_benefit_fontawesome_icons=OK');

  const cardStyles = await cards.evaluateAll((elements) => elements.map((element) => {
    const style = getComputedStyle(element);
    return {
      backgroundColor: style.backgroundColor,
      borderLeftWidth: style.borderLeftWidth,
      borderRightWidth: style.borderRightWidth,
      borderTopWidth: style.borderTopWidth,
      borderBottomWidth: style.borderBottomWidth,
    };
  }));

  for (const style of cardStyles) {
    const rgba = style.backgroundColor.match(/rgba?\(([^)]+)\)/);
    if (!rgba) throw new Error(`background inválido: ${style.backgroundColor}`);
    const parts = rgba[1].split(',').map((part) => Number(part.trim()));
    const alpha = parts.length === 4 ? parts[3] : 1;
    if (!(alpha >= 0.55 && alpha <= 0.8)) {
      throw new Error(`background deve ser translúcido: ${style.backgroundColor}`);
    }
    if ([style.borderLeftWidth, style.borderRightWidth, style.borderTopWidth, style.borderBottomWidth].some((value) => value !== '0px')) {
      throw new Error(`card não deve possuir borda: ${JSON.stringify(style)}`);
    }
  }
  console.log('home_benefit_translucent_cards=OK');
  console.log('home_benefit_no_side_border=OK');

  const heroBox = await hero.boundingBox();
  const innerBox = await heroInner.boundingBox();
  const benefitsBox = await benefits.boundingBox();
  if (!heroBox || !innerBox || !benefitsBox) throw new Error('não foi possível medir hero/cards');

  const rightDelta = Math.abs((benefitsBox.x + benefitsBox.width) - (innerBox.x + innerBox.width));
  if (rightDelta > 3) {
    throw new Error(`cards não respeitam margem lateral do projeto: delta=${rightDelta}`);
  }

  const verticalDelta = Math.abs(
    (benefitsBox.y + benefitsBox.height / 2) - (heroBox.y + heroBox.height / 2),
  );
  if (verticalDelta > 24) {
    throw new Error(`cards não estão centralizados verticalmente: delta=${verticalDelta}`);
  }
  console.log('home_benefit_project_margin_alignment=OK');
  console.log('home_benefit_vertical_center=OK');

  await hero.screenshot({ path: '/work/home-benefit-cards-desktop.png' });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload({ waitUntil: 'domcontentloaded', timeout: 30000 });

  const mobileHero = page.locator('.public-home__hero');
  const mobileBenefits = page.locator('.public-home__hero-benefits');
  const mobileCards = page.locator('.public-home__hero-benefit');
  await mobileBenefits.waitFor({ state: 'visible', timeout: 10000 });

  if (await mobileCards.count() !== 4) throw new Error('mobile não renderizou os quatro cards');
  const mobileHeroBox = await mobileHero.boundingBox();
  const mobileBenefitsBox = await mobileBenefits.boundingBox();
  if (!mobileHeroBox || !mobileBenefitsBox) throw new Error('não foi possível medir hero mobile');
  if (mobileBenefitsBox.x < mobileHeroBox.x || mobileBenefitsBox.x + mobileBenefitsBox.width > mobileHeroBox.x + mobileHeroBox.width + 1) {
    throw new Error('cards mobile extrapolam lateralmente o hero');
  }

  await mobileHero.screenshot({ path: '/work/home-benefit-cards-mobile.png' });
  console.log('home_benefit_mobile_layout=OK');
} finally {
  await context.close();
  await browser.close();
}
JS

test -f "$QA_DIR/home-benefit-cards-desktop.png" || fail "screenshot desktop ausente"
test -f "$QA_DIR/home-benefit-cards-mobile.png" || fail "screenshot mobile ausente"
test -z "$(git -C "$ROOT" status --porcelain)" || fail "main worktree ficou suja"

echo "============================================================"
echo "PUBLIC HOME BENEFIT CARDS — FEATURE HOMOLOG: OK"
echo "FOUR SEPARATE CARDS: OK"
echo "MASTER CHAT BENEFIT: OK"
echo "FONT AWESOME ICONS: OK"
echo "TRANSLUCENT BACKGROUNDS: OK"
echo "NO SIDE BORDER: OK"
echo "PROJECT MARGIN ALIGNMENT: OK"
echo "VERTICAL CENTERING: OK"
echo "MOBILE LAYOUT: OK"
echo "MAIN WORKTREE: CLEAN"
echo "SCREENSHOTS: $QA_DIR/home-benefit-cards-desktop.png | $QA_DIR/home-benefit-cards-mobile.png"
echo "============================================================"
