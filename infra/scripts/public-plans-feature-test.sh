#!/usr/bin/env bash
set -Eeuo pipefail

EXPECTED_HEAD="${1:-}"
ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
ENV_FILE="${CIDADEMDIA_ENV_FILE:-$ROOT/.env}"
BASE="${CIDADEMDIA_BASE_URL:-https://homolog.cidademdia.com.br}"
WT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
QA_DIR="${CIDADEMDIA_QA_DIR:-$HOME/cidademdia-qa/plans-$(date +%Y%m%d-%H%M%S)}"
APPROVED_HOME_HEAD="d3b0b56b4a601d260feea1535c49968f99bfd776"

fail() { echo; echo "ERRO: $*" >&2; exit 1; }

for cmd in git docker curl python3 grep; do command -v "$cmd" >/dev/null 2>&1 || fail "comando ausente: $cmd"; done

root_compose() { docker compose --env-file "$ENV_FILE" -f "$ROOT/infra/docker-compose.yml" "$@"; }
CURRENT_WEB_ID="$(root_compose ps -q web)"
test -n "$CURRENT_WEB_ID" || fail "container web atual não encontrado"
PROJECT="$(docker inspect "$CURRENT_WEB_ID" --format '{{ index .Config.Labels "com.docker.compose.project" }}')"
test -n "$PROJECT" || fail "compose project não identificado"
compose() { docker compose -p "$PROJECT" --env-file "$ENV_FILE" -f "$WT/infra/docker-compose.yml" "$@"; }
dbq() { root_compose exec -T db sh -lc 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At' <<< "$1"; }

echo "============================================================"
echo "PUBLIC PLANS — FEATURE HOMOLOG"
echo "============================================================"

test -n "$EXPECTED_HEAD" || fail "informe o HEAD esperado"
test -d "$ROOT/.git" || fail "repo principal não encontrado"
test -f "$ENV_FILE" || fail ".env não encontrado"
test "$(git -C "$WT" rev-parse HEAD)" = "$EXPECTED_HEAD" || fail "worktree fora do HEAD esperado"
test "$(git -C "$ROOT" branch --show-current)" = "main" || fail "repo principal não está na main"
test -z "$(git -C "$ROOT" status --porcelain)" || fail "main local está suja"

echo "HEAD: $EXPECTED_HEAD"
echo "MAIN: $(git -C "$ROOT" rev-parse HEAD)"

echo
echo "=== 1. HOME CONGELADA / ESCOPO AUTORIZADO ==="
for path in \
  apps/web/src/modules/home/home.css \
  apps/web/src/modules/home/home-refinement.css \
  apps/web/src/modules/home/home-assets.css \
  apps/web/src/modules/home/assets/hero-city.svg; do
  git -C "$WT" diff --quiet "$APPROVED_HOME_HEAD" "$EXPECTED_HEAD" -- "$path" || fail "arquivo congelado da Home foi alterado: $path"
done
HOME_DIFF="$(git -C "$WT" diff --unified=0 "$APPROVED_HOME_HEAD" "$EXPECTED_HEAD" -- apps/web/src/modules/home/PublicHome.tsx)"
printf '%s\n' "$HOME_DIFF" | grep -q "navigate('/planos')" || fail "CTA Conheça os planos não aponta para /planos"
grep -q "label: 'Planos'.*href: '/planos'" "$WT/apps/web/src/app/layout/DashboardShell.tsx" || fail "Planos ausente da navegação autenticada"
echo "home_frozen_visuals=OK"
echo "home_to_plans_source=OK"
echo "authenticated_header_plans_source=OK"

echo
echo "=== 2. MERCADO PAGO ANTES ==="
PROVIDER_BEFORE="$(dbq "SELECT (SELECT count(*) FROM billing_provider_subscriptions)::text || '|' || (SELECT count(*) FROM payments)::text || '|' || (SELECT count(*) FROM payment_events)::text;")"
echo "provider_before=$PROVIDER_BEFORE"
test "$PROVIDER_BEFORE" = "0|0|0" || fail "provider state inesperado"

echo
echo "=== 3. BUILD / DEPLOY FEATURE ==="
compose build api web
compose up -d --no-deps api web
compose restart nginx
READY=0
for _ in $(seq 1 40); do
  HEALTH="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/health/ready" || true)"
  HOME_CODE="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/" || true)"
  PLANS_CODE="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/planos" || true)"
  if [ "$HEALTH" = "200" ] && [ "$HOME_CODE" = "200" ] && [ "$PLANS_CODE" = "200" ]; then READY=1; break; fi
  sleep 2
done
test "$READY" = "1" || fail "health/home/planos não ficaram prontos"
echo "health=200"
echo "home=200"
echo "plans=200"

echo
echo "=== 4. CATÁLOGO PÚBLICO DE BILLING ==="
mkdir -p "$QA_DIR"
CATALOG_JSON="$QA_DIR/catalog.json"
CATALOG_CODE="$(curl -sS -o "$CATALOG_JSON" -w '%{http_code}' "$BASE/api/v1/billing/catalog")"
test "$CATALOG_CODE" = "200" || fail "catálogo público de billing != 200"
python3 - "$CATALOG_JSON" <<'PY'
import json, sys
with open(sys.argv[1], encoding='utf-8') as fh: payload = json.load(fh)
if not isinstance(payload, list): raise SystemExit('ERRO: catálogo deveria ser lista')
required = {'offerId','planVersionId','planKey','planName','billingIntervalMonths','priceCents','signupFeeCents','subaccountLimit','monthlyPublicationLimit'}
for item in payload:
    missing = required.difference(item)
    if missing: raise SystemExit('ERRO: campos ausentes: ' + ','.join(sorted(missing)))
print(f'billing_catalog_items={len(payload)}')
print('billing_catalog_intervals=' + ','.join(map(str, sorted({int(i['billingIntervalMonths']) for i in payload}))))
PY

echo
echo "=== 5. BUNDLE ==="
WEB_ID="$(compose ps -q web)"
test -n "$WEB_ID" || fail "container web da feature não encontrado"
docker exec "$WEB_ID" sh -lc 'grep -R -q "Periodicidade" /usr/share/nginx/html/assets' || fail "seletor de periodicidade ausente"
docker exec "$WEB_ID" sh -lc 'grep -R -q "MEGA PROMOÇÃO" /usr/share/nginx/html/assets' || fail "Mega Promoção ausente"
docker exec "$WEB_ID" sh -lc 'grep -R -q "Acompanhe as demandas compartilhadas com sua gestão" /usr/share/nginx/html/assets' || fail "benefícios do footer ausentes"
docker exec "$WEB_ID" sh -lc 'grep -R -q "/billing/catalog" /usr/share/nginx/html/assets' || fail "catálogo real ausente"
echo "plans_bundle=OK"

echo
echo "=== 6. HOME / PLANOS DESKTOP E MOBILE ==="
docker run --rm -i --network host -v "$QA_DIR:/work" -e BASE="$BASE" node:22-alpine sh -lc '
  set -eu
  apk add --no-cache chromium nss freetype harfbuzz ca-certificates ttf-freefont >/dev/null
  cd /tmp
  npm init -y >/dev/null 2>&1
  npm install --no-save --no-audit --no-fund playwright-core@1.55.0 >/dev/null
  export CHROME="$(command -v chromium-browser || command -v chromium)"
  node --input-type=module -
' <<'JS'
import { chromium } from 'playwright-core';

const browser = await chromium.launch({ executablePath: process.env.CHROME, headless: true, args: ['--no-sandbox','--disable-dev-shm-usage'] });

async function openPage(viewport) {
  const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport });
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  return { context, page, errors };
}

async function validateHome() {
  const { context, page, errors } = await openPage({ width: 1440, height: 1000 });
  await page.goto(process.env.BASE, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.locator('.public-home').waitFor({ state: 'visible', timeout: 15000 });
  await page.locator('.public-home__desktop-nav a[href="/planos"]').waitFor({ state: 'visible' });
  const cta = page.locator('.public-home__hero-actions button.ced-button').filter({ hasText: 'Conheça os planos' }).first();
  await cta.waitFor({ state: 'visible' });
  await cta.click();
  await page.waitForURL(url => url.pathname === '/planos', { timeout: 10000 });
  if (errors.length) throw new Error(errors.join(' | '));
  await context.close();
}

async function validatePlans(viewport, screenshot, mobile) {
  const { context, page, errors } = await openPage(viewport);
  await page.goto(`${process.env.BASE}/planos`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.locator('.plans-v3').waitFor({ state: 'visible', timeout: 15000 });
  await page.locator('.plans-v3__regular-grid').waitFor({ state: 'visible', timeout: 15000 });

  if (await page.getByText('PLANOS PARA TODOS OS TIPOS DE GESTÃO', { exact: false }).count()) throw new Error('headline removida reapareceu');
  if (await page.getByText('Escolha a estrutura ideal para acompanhar ocorrências', { exact: false }).count()) throw new Error('parágrafo removido reapareceu');
  if (await page.getByText('Estrutura da operação', { exact: true }).count()) throw new Error('bloco Estrutura da operação reapareceu');
  if (await page.getByText('Escolha o plano. O ciclo já está definido.', { exact: true }).count()) throw new Error('título removido reapareceu');

  const cycles = page.locator('.plans-v3__cycle-button');
  if (await cycles.count() !== 4) throw new Error('seletor deveria ter 4 periodicidades');
  for (const label of ['Mensal','Trimestral','Semestral','Anual']) await page.getByRole('button', { name: new RegExp(label) }).waitFor({ state: 'visible' });

  const cards = page.locator('.plans-v3__plan');
  if (await cards.count() !== 3) throw new Error('esperados 3 planos regulares');
  for (const name of ['Individual','Master 5','Master 10']) await page.getByRole('heading', { name, exact: true }).waitFor({ state: 'visible' });

  const premium = page.locator('.plans-v3__premium');
  await premium.waitFor({ state: 'visible' });
  const premiumBefore = await premium.innerText();
  if (!premiumBefore.includes('MEGA PROMOÇÃO') || !premiumBefore.includes('Ouro Anual')) throw new Error('premium anual incompleto');

  const firstEnabledAlternate = page.locator('.plans-v3__cycle-button:not([disabled])').filter({ hasNotText: /^Mensal$/ }).first();
  if (await firstEnabledAlternate.count()) {
    const regularBefore = await cards.allInnerTexts();
    await firstEnabledAlternate.click();
    await page.waitForTimeout(150);
    const regularAfter = await cards.allInnerTexts();
    const premiumAfter = await premium.innerText();
    if (JSON.stringify(regularBefore) === JSON.stringify(regularAfter)) throw new Error('seletor não alterou os planos regulares');
    if (premiumBefore !== premiumAfter) throw new Error('premium anual foi alterado pelo seletor');
  }

  const benefitFooter = page.locator('.plans-v3__info-footer .plans-v3__benefits');
  await benefitFooter.waitFor({ state: 'visible' });
  if (await benefitFooter.locator('article').count() !== 4) throw new Error('footer deveria conter 4 benefícios');
  for (const text of [
    'Acompanhe as demandas compartilhadas com sua gestão.',
    'Distribua acessos conforme a estrutura contratada.',
    'Não perca movimentações importantes da operação.',
    'Publique de acordo com a franquia vigente do plano.'
  ]) await benefitFooter.getByText(text, { exact: true }).waitFor({ state: 'visible' });

  const pricingBox = await page.locator('.plans-v3__pricing-layout').boundingBox();
  const benefitsBox = await benefitFooter.boundingBox();
  if (!pricingBox || !benefitsBox || benefitsBox.y <= pricingBox.y + pricingBox.height) throw new Error('benefícios não estão abaixo da área de pricing');

  if (await page.locator('.plans-v3__trust article').count() !== 5) throw new Error('faixa de confiança deveria conter 5 blocos');
  await page.getByRole('button', { name: 'Fale conosco' }).waitFor({ state: 'visible' });

  const dims = await page.evaluate(() => ({ viewport: document.documentElement.clientWidth, scrollWidth: document.documentElement.scrollWidth }));
  if (dims.scrollWidth - dims.viewport > 2) throw new Error(`overflow horizontal: ${dims.scrollWidth}/${dims.viewport}`);
  if (errors.length) throw new Error(errors.join(' | '));
  await page.screenshot({ path: `/work/${screenshot}`, fullPage: true });
  await context.close();
}

try {
  await validateHome();
  console.log('home_to_plans=OK');
  await validatePlans({ width: 1440, height: 1000 }, 'plans-desktop.png', false);
  console.log('plans_desktop=OK');
  await validatePlans({ width: 390, height: 844 }, 'plans-mobile.png', true);
  console.log('plans_mobile=OK');
} finally {
  await browser.close();
}
JS

test -f "$QA_DIR/plans-desktop.png" || fail "screenshot desktop ausente"
test -f "$QA_DIR/plans-mobile.png" || fail "screenshot mobile ausente"

echo
echo "=== 7. ESTADO FINAL ==="
PROVIDER_AFTER="$(dbq "SELECT (SELECT count(*) FROM billing_provider_subscriptions)::text || '|' || (SELECT count(*) FROM payments)::text || '|' || (SELECT count(*) FROM payment_events)::text;")"
FINAL_HEALTH="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/health/ready")"
FINAL_HOME="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/")"
FINAL_PLANS="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/planos")"
MAIN_CLEAN="$(test -z "$(git -C "$ROOT" status --porcelain)" && echo YES || echo NO)"

test "$PROVIDER_AFTER" = "$PROVIDER_BEFORE" || fail "provider state mudou"
test "$FINAL_HEALTH" = "200" || fail "health final != 200"
test "$FINAL_HOME" = "200" || fail "home final != 200"
test "$FINAL_PLANS" = "200" || fail "planos final != 200"
test "$MAIN_CLEAN" = "YES" || fail "main worktree ficou suja"

echo "============================================================"
echo "PUBLIC PLANS — FEATURE HOMOLOG: OK"
echo "HEAD: $EXPECTED_HEAD"
echo "HOME FROZEN VISUALS: OK"
echo "HOME CTA -> /planos: OK"
echo "PERIODICITY SELECTOR: OK"
echo "REGULAR PLANS: 3"
echo "PREMIUM ANNUAL FIXED: OK"
echo "REMOVED HEADINGS: OK"
echo "BENEFITS IN FOOTER: 4"
echo "TRUST INFO: 5"
echo "DESKTOP: OK"
echo "MOBILE: OK"
echo "WEB HOME: $FINAL_HOME"
echo "WEB PLANOS: $FINAL_PLANS"
echo "HEALTH: $FINAL_HEALTH"
echo "MERCADO PAGO PROVIDER: $PROVIDER_AFTER"
echo "MAIN WORKTREE: CLEAN"
echo "SCREENSHOT DESKTOP: $QA_DIR/plans-desktop.png"
echo "SCREENSHOT MOBILE: $QA_DIR/plans-mobile.png"
echo "============================================================"
