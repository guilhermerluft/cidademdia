#!/usr/bin/env bash
set -Eeuo pipefail

EXPECTED_HEAD="${1:-}"
ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
BASE="${CIDADEMDIA_BASE_URL:-https://homolog.cidademdia.com.br}"
WT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
QA_DIR="${CIDADEMDIA_QA_DIR:-$HOME/cidademdia-qa/commercial-signup-$(date +%Y%m%d-%H%M%S)}"

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
echo "PUBLIC OCCURRENCE COMMERCIAL SIGNUP — FEATURE HOMOLOG"
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
  geolocation: { latitude: -23.55052, longitude: -46.633308 },
  permissions: ['geolocation'],
});

const occurrence = {
  id: '11111111-1111-4111-8111-111111111111',
  publicCode: 'QA-HERO-001',
  categoryName: 'Infraestrutura',
  categorySlug: 'infraestrutura',
  title: 'Ocorrência pública de homologação',
  description: 'Ocorrência mockada apenas no navegador para validar interação comercial.',
  status: 'NOVA',
  addressText: 'Praça da Sé, 1 - Sé - São Paulo',
  externalProtocolNumber: 'QA-001',
  supportCount: 0,
  createdAt: '2026-09-05T00:00:00Z',
  updatedAt: '2026-09-05T00:00:00Z',
  coverMedia: null,
};

try {
  const page = await context.newPage();

  await page.route('**/api/v1/public/occurrences**', async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname.endsWith(`/${occurrence.id}`)) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ ...occurrence, media: [] }),
      });
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: [occurrence],
        page: 1,
        pageSize: 12,
        totalItems: 1,
        totalPages: 1,
      }),
    });
  });

  await page.goto(process.env.BASE, { waitUntil: 'domcontentloaded', timeout: 30000 });

  const hero = page.locator('.public-home__hero');
  const heroCopy = page.locator('.public-home__hero-copy > p');
  await hero.waitFor({ state: 'visible', timeout: 10000 });
  await heroCopy.waitFor({ state: 'visible', timeout: 10000 });

  const expectedHeroCopy = 'O CIDADEMDIA conecta cidadãos e gestores, permitindo publicar ocorrências gratuitamente e acompanhar cada demanda, tornando a gestão mais ágil, transparente e eficiente.';
  const heroCopyText = (await heroCopy.textContent() || '').replace(/\s+/g, ' ').trim();
  if (heroCopyText !== expectedHeroCopy) throw new Error(`texto do hero divergente: ${heroCopyText}`);

  const highlightedText = (await heroCopy.locator('strong').textContent() || '').trim();
  if (highlightedText !== 'publicar ocorrências gratuitamente') {
    throw new Error(`destaque do hero divergente: ${highlightedText}`);
  }

  if (await page.locator('.public-home__hero-benefits, .public-home__hero-benefit').count() !== 0) {
    throw new Error('cards de benefícios ainda estão renderizados no hero');
  }

  await page.getByRole('heading', { name: /Uma cidade melhor.*é ouvido.*pode resolver\./ }).waitFor({ state: 'visible' });
  await page.getByRole('button', { name: 'Conheça os planos', exact: true }).waitFor({ state: 'visible' });
  await page.getByRole('button', { name: 'Como funciona', exact: true }).waitFor({ state: 'visible' });

  const card = page.locator(`.public-occurrences__card[data-occurrence-id="${occurrence.id}"]`);
  await card.waitFor({ state: 'visible', timeout: 15000 });

  await card.getByRole('button', { name: 'Entrar para apoiar ocorrência. 0 apoios', exact: true }).click();
  let commercialDialog = page.getByRole('dialog', { name: 'Crie sua conta gratuita para interagir' });
  await commercialDialog.waitFor({ state: 'visible', timeout: 5000 });
  await commercialDialog.getByRole('link', { name: 'CidadeEmDia', exact: true }).waitFor({ state: 'visible' });
  await commercialDialog.getByRole('button', { name: 'Cadastre-se', exact: true }).waitFor({ state: 'visible' });
  console.log('anonymous_support_commercial_modal=OK');

  await commercialDialog.getByRole('button', { name: 'Fechar convite para cadastro', exact: true }).click();
  await commercialDialog.waitFor({ state: 'hidden', timeout: 5000 });

  await card.getByRole('heading', { name: occurrence.title, exact: true }).click();
  commercialDialog = page.getByRole('dialog', { name: 'Crie sua conta gratuita para interagir' });
  await commercialDialog.waitFor({ state: 'visible', timeout: 5000 });
  if (await page.getByRole('dialog', { name: occurrence.title }).count() !== 0) {
    throw new Error('clique anônimo abriu detalhes da ocorrência');
  }
  console.log('anonymous_details_commercial_modal=OK');

  await page.screenshot({ path: '/work/commercial-signup-desktop.png', fullPage: true });

  await commercialDialog.getByRole('button', { name: 'Cadastre-se', exact: true }).click();
  await page.getByRole('heading', { name: 'Crie sua conta', exact: true }).waitFor({ state: 'visible', timeout: 10000 });
  const registerTab = page.getByRole('tab', { name: 'Criar conta', exact: true });
  await registerTab.waitFor({ state: 'visible', timeout: 5000 });
  if ((await registerTab.getAttribute('aria-selected')) !== 'true') {
    throw new Error('CTA não abriu o modo de cadastro');
  }
  console.log('commercial_signup_cta_registration_form=OK');

  await page.goto(process.env.BASE, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await hero.waitFor({ state: 'visible', timeout: 10000 });
  await hero.screenshot({ path: '/work/home-hero-copy-desktop.png' });
  console.log('home_hero_free_occurrence_copy_desktop=OK');
  console.log('home_hero_benefit_cards_removed=OK');

  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload({ waitUntil: 'domcontentloaded', timeout: 30000 });
  const mobileHero = page.locator('.public-home__hero');
  const mobileHeroCopy = page.locator('.public-home__hero-copy > p');
  await mobileHero.waitFor({ state: 'visible', timeout: 10000 });
  const mobileCopyText = (await mobileHeroCopy.textContent() || '').replace(/\s+/g, ' ').trim();
  if (mobileCopyText !== expectedHeroCopy) throw new Error(`texto do hero mobile divergente: ${mobileCopyText}`);
  if ((await mobileHeroCopy.locator('strong').textContent() || '').trim() !== 'publicar ocorrências gratuitamente') {
    throw new Error('destaque do hero não foi preservado no mobile');
  }
  if (await page.locator('.public-home__hero-benefits, .public-home__hero-benefit').count() !== 0) {
    throw new Error('cards de benefícios renderizaram no hero mobile');
  }
  await mobileHero.screenshot({ path: '/work/home-hero-copy-mobile.png' });
  console.log('home_hero_free_occurrence_copy_mobile=OK');
} finally {
  await context.close();
  await browser.close();
}
JS

test -f "$QA_DIR/commercial-signup-desktop.png" || fail "screenshot desktop do modal ausente"
test -f "$QA_DIR/home-hero-copy-desktop.png" || fail "screenshot desktop do hero ausente"
test -f "$QA_DIR/home-hero-copy-mobile.png" || fail "screenshot mobile do hero ausente"
test -z "$(git -C "$ROOT" status --porcelain)" || fail "main worktree ficou suja"

echo "============================================================"
echo "PUBLIC OCCURRENCE COMMERCIAL SIGNUP — FEATURE HOMOLOG: OK"
echo "ANONYMOUS SUPPORT COMMERCIAL MODAL: OK"
echo "ANONYMOUS DETAILS COMMERCIAL MODAL: OK"
echo "DETAILS BLOCKED FOR ANONYMOUS: OK"
echo "CTA TO REGISTRATION FORM: OK"
echo "HERO BENEFIT CARDS REMOVED: OK"
echo "HERO FREE OCCURRENCE COPY DESKTOP: OK"
echo "HERO FREE OCCURRENCE COPY MOBILE: OK"
echo "MAIN WORKTREE: CLEAN"
echo "SCREENSHOTS: $QA_DIR/commercial-signup-desktop.png | $QA_DIR/home-hero-copy-desktop.png | $QA_DIR/home-hero-copy-mobile.png"
echo "============================================================"
