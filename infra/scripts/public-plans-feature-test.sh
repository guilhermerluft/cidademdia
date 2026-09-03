#!/usr/bin/env bash
set -Eeuo pipefail

EXPECTED_HEAD="${1:-}"
ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
ENV_FILE="${CIDADEMDIA_ENV_FILE:-$ROOT/.env}"
BASE="${CIDADEMDIA_BASE_URL:-https://homolog.cidademdia.com.br}"
WT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
QA_DIR="${CIDADEMDIA_QA_DIR:-$HOME/cidademdia-qa/plans-$(date +%Y%m%d-%H%M%S)}"
APPROVED_HOME_HEAD="d3b0b56b4a601d260feea1535c49968f99bfd776"

fail() {
  echo
  echo "ERRO: $*" >&2
  exit 1
}

for cmd in git docker curl python3 grep; do
  command -v "$cmd" >/dev/null 2>&1 || fail "comando ausente: $cmd"
done

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

dbq() {
  root_compose exec -T db sh -lc \
    'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At' \
    <<< "$1"
}

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
echo "=== 1. HOME CONGELADA / HEADER ÚNICO ==="
git -C "$WT" cat-file -e "$APPROVED_HOME_HEAD^{commit}" 2>/dev/null \
  || fail "checkpoint aprovado da Home não está disponível no clone"

for path in \
  apps/web/src/modules/home/home.css \
  apps/web/src/modules/home/home-refinement.css \
  apps/web/src/modules/home/home-assets.css \
  apps/web/src/modules/home/assets/hero-city.svg; do
  git -C "$WT" diff --quiet "$APPROVED_HOME_HEAD" "$EXPECTED_HEAD" -- "$path" \
    || fail "arquivo visual congelado da Home foi alterado: $path"
done

grep -q "<AppHeader" "$WT/apps/web/src/modules/home/PublicHome.tsx" \
  || fail "Home não usa AppHeader compartilhado"
grep -q "navigate('/planos')" "$WT/apps/web/src/modules/home/PublicHome.tsx" \
  || fail "CTA Conheça os planos não aponta para /planos"
grep -q "<AppHeader" "$WT/apps/web/src/app/layout/DashboardShell.tsx" \
  || fail "Dashboard não usa AppHeader compartilhado"
grep -q "<AppHeader" "$WT/apps/web/src/modules/plans/PlansRoute.tsx" \
  || fail "rota de Planos não usa AppHeader compartilhado"
! grep -q "PublicPlansHeader" "$WT/apps/web/src/modules/plans/PublicPlans.tsx" \
  || fail "header duplicado ainda existe em PublicPlans"
! grep -q "public-home__header" "$WT/apps/web/src/modules/home/PublicHome.tsx" \
  || fail "markup antigo do header da Home ainda existe"
! grep -q "dashboard-header" "$WT/apps/web/src/app/layout/DashboardShell.tsx" \
  || fail "markup antigo do header autenticado ainda existe"

grep -q "id: 'plans'" "$WT/apps/web/src/app/layout/AppNavigation.tsx" \
  || fail "Planos ausente da fonte central de navegação"
grep -q "restrictedSubaccountPermission: 'occurrence.read.targeted'" "$WT/apps/web/src/app/layout/AppNavigation.tsx" \
  || fail "regra de permissão de ocorrência da subconta não está centralizada"

HEADER_MARKUP_COUNT="$(grep -R --include='*.tsx' -F '<header className=' "$WT/apps/web/src" | wc -l | tr -d ' ')"
test "$HEADER_MARKUP_COUNT" = "1" \
  || fail "esperado exatamente 1 markup de header compartilhado; encontrado $HEADER_MARKUP_COUNT"
echo "home_frozen_visual_assets=OK"
echo "shared_header_markup=OK"
echo "central_navigation_rules=OK"

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

READY=0
for _ in $(seq 1 40); do
  HEALTH="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/health/ready" || true)"
  HOME_CODE="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/" || true)"
  PLANS_CODE="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/planos" || true)"
  if [ "$HEALTH" = "200" ] && [ "$HOME_CODE" = "200" ] && [ "$PLANS_CODE" = "200" ]; then
    READY=1
    break
  fi
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
echo "billing_catalog_http=$CATALOG_CODE"
test "$CATALOG_CODE" = "200" || fail "catálogo público de billing != 200"

python3 - "$CATALOG_JSON" <<'PY'
import json, sys
with open(sys.argv[1], encoding='utf-8') as fh:
    payload = json.load(fh)
if not isinstance(payload, list):
    raise SystemExit('ERRO: catálogo deveria ser uma lista')
required = {
    'offerId', 'planVersionId', 'planKey', 'planName', 'categoryKey',
    'categoryName', 'billingIntervalMonths', 'priceCents', 'signupFeeCents',
    'subaccountLimit', 'monthlyPublicationLimit', 'version'
}
for item in payload:
    missing = required.difference(item.keys())
    if missing:
        raise SystemExit('ERRO: campos ausentes no catálogo: ' + ','.join(sorted(missing)))
    if int(item['billingIntervalMonths']) <= 0:
        raise SystemExit('ERRO: billingIntervalMonths inválido')
    if int(item['priceCents']) < 0 or int(item['signupFeeCents']) < 0:
        raise SystemExit('ERRO: preço/taxa negativa no catálogo')
print(f'billing_catalog_items={len(payload)}')
print('billing_catalog_contract=OK')
print('billing_catalog_intervals=' + ','.join(str(x) for x in sorted({int(i['billingIntervalMonths']) for i in payload})))
PY

echo
echo "=== 5. BUNDLE / ROTA / ESTRUTURA ==="
WEB_ID="$(compose ps -q web)"
test -n "$WEB_ID" || fail "container web da feature não encontrado"

docker exec "$WEB_ID" sh -lc 'grep -R -q "TIPOS DE GESTÃO" /usr/share/nginx/html/assets' \
  || fail "headline da página de Planos ausente do bundle"
docker exec "$WEB_ID" sh -lc 'grep -R -q "MEGA PROMOÇÃO" /usr/share/nginx/html/assets' \
  || fail "card Mega Promoção ausente do bundle"
docker exec "$WEB_ID" sh -lc 'grep -R -q "/billing/catalog" /usr/share/nginx/html/assets' \
  || fail "catálogo real de billing ausente do bundle"
docker exec "$WEB_ID" sh -lc 'grep -R -q "Adesão única" /usr/share/nginx/html/assets' \
  || fail "informação de adesão ausente do bundle"
echo "plans_bundle=OK"

echo
echo "=== 6. HEADER COMPARTILHADO / PLANOS DESKTOP E MOBILE ==="
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

async function openPage(viewport) {
  const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport });
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  return { context, page, errors };
}

async function publicHeaderLabels(page) {
  return page.locator('.app-header__nav .app-header__nav-item').allInnerTexts();
}

async function validateHomeDesktop() {
  const { context, page, errors } = await openPage({ width: 1440, height: 1000 });
  await page.goto(process.env.BASE, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.locator('.public-home').waitFor({ state: 'visible', timeout: 15000 });
  await page.locator('.app-header').waitFor({ state: 'visible' });

  const labels = await publicHeaderLabels(page);
  if (labels.join('|') !== 'Início|Planos') {
    throw new Error(`header público da Home inesperado: ${labels.join('|')}`);
  }

  const plansHeader = page.locator('.app-header__nav a[href="/planos"]');
  await plansHeader.waitFor({ state: 'visible' });

  const plansSectionVisible = await page.locator('.public-home__plans').isVisible();
  if (plansSectionVisible) throw new Error('seção de Planos voltou para a Home');

  const heroCta = page.locator('.public-home__hero-actions button.ced-button').filter({ hasText: 'Conheça os planos' }).first();
  await heroCta.waitFor({ state: 'visible' });
  await heroCta.click();
  await page.waitForURL(url => url.pathname === '/planos', { timeout: 10000 });

  if (errors.length) throw new Error(`pageerror na Home: ${errors.join(' | ')}`);
  await context.close();
}

async function validatePlans(viewport, screenshot, mobile) {
  const { context, page, errors } = await openPage(viewport);
  await page.goto(`${process.env.BASE}/planos`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.locator('.plans-page').waitFor({ state: 'visible', timeout: 15000 });
  await page.locator('.plans-page__plans-grid').waitFor({ state: 'visible', timeout: 15000 });
  await page.locator('.app-header').waitFor({ state: 'visible' });

  const labels = await publicHeaderLabels(page);
  if (labels.join('|') !== 'Início|Planos') {
    throw new Error(`header público de Planos inesperado: ${labels.join('|')}`);
  }

  const title = await page.locator('#plans-page-title').innerText();
  for (const expected of ['PLANOS PARA', 'TODOS OS', 'TIPOS DE GESTÃO']) {
    if (!title.includes(expected)) throw new Error(`headline ausente: ${expected}`);
  }

  const benefitCount = await page.locator('.plans-page__benefits article').count();
  if (benefitCount !== 4) throw new Error(`benefícios gerais: esperado 4, recebeu ${benefitCount}`);

  const planCardCount = await page.locator('.plans-page__plan-card').count();
  if (planCardCount !== 3) throw new Error(`cards principais: esperado 3, recebeu ${planCardCount}`);

  const planNames = await page.locator('.plans-page__plan-card .plans-page__plan-title h2').allInnerTexts();
  for (const expected of ['Individual', 'Master 5', 'Master 10']) {
    if (!planNames.includes(expected)) throw new Error(`plano ausente: ${expected}`);
  }

  const paymentCount = await page.locator('.plans-page__plan-card .plans-page__payment').count();
  if (paymentCount !== 12) throw new Error(`sub-blocos de pagamento: esperado 12, recebeu ${paymentCount}`);

  const mega = page.locator('.plans-page__mega-card');
  await mega.waitFor({ state: 'visible' });
  const megaText = await mega.innerText();
  for (const expected of ['MEGA PROMOÇÃO', 'PLANO OURO ANUAL', 'ISENTA', '11 MENSALIDADES']) {
    if (!megaText.includes(expected)) throw new Error(`Mega Promoção sem texto: ${expected}`);
  }

  const complementaryCount = await page.locator('.plans-page__complementary article').count();
  if (complementaryCount !== 5) throw new Error(`faixa complementar: esperado 5, recebeu ${complementaryCount}`);
  await page.getByRole('button', { name: 'Fale conosco' }).waitFor({ state: 'visible' });

  if (!mobile) {
    await page.locator('.app-header__nav a[href="/planos"]').waitFor({ state: 'visible' });
  } else {
    const bottomLabels = await page.locator('.app-bottom-nav__item').allInnerTexts();
    for (const expected of ['Início', 'Planos', 'Entrar', 'Criar conta']) {
      if (!bottomLabels.some(label => label.includes(expected))) {
        throw new Error(`bottom nav público sem ${expected}`);
      }
    }
  }

  const dims = await page.evaluate(() => ({
    viewport: document.documentElement.clientWidth,
    scrollWidth: document.documentElement.scrollWidth,
  }));
  if (dims.scrollWidth - dims.viewport > 2) {
    throw new Error(`overflow horizontal em /planos: ${dims.scrollWidth}/${dims.viewport}`);
  }

  if (errors.length) throw new Error(`pageerror em /planos: ${errors.join(' | ')}`);
  await page.screenshot({ path: `/work/${screenshot}`, fullPage: true });
  await context.close();
}

async function validateHomeMobileSharedNavigation() {
  const { context, page, errors } = await openPage({ width: 390, height: 844 });
  await page.goto(process.env.BASE, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.locator('.public-home').waitFor({ state: 'visible', timeout: 15000 });
  await page.locator('.app-bottom-nav').waitFor({ state: 'visible' });
  const labels = await page.locator('.app-bottom-nav__item').allInnerTexts();
  for (const expected of ['Início', 'Planos', 'Entrar', 'Criar conta']) {
    if (!labels.some(label => label.includes(expected))) {
      throw new Error(`bottom nav compartilhado da Home sem ${expected}`);
    }
  }
  if (errors.length) throw new Error(`pageerror na Home mobile: ${errors.join(' | ')}`);
  await context.close();
}

try {
  await validateHomeDesktop();
  console.log('home_to_plans=OK');
  await validateHomeMobileSharedNavigation();
  console.log('home_mobile_shared_navigation=OK');
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
echo "screenshot_desktop=$QA_DIR/plans-desktop.png"
echo "screenshot_mobile=$QA_DIR/plans-mobile.png"

echo
echo "=== 7. ESTADO FINAL ==="
PROVIDER_AFTER="$(dbq "
  SELECT
    (SELECT count(*) FROM billing_provider_subscriptions)::text || '|' ||
    (SELECT count(*) FROM payments)::text || '|' ||
    (SELECT count(*) FROM payment_events)::text;
")"
FINAL_HEALTH="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/health/ready")"
FINAL_HOME="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/")"
FINAL_PLANS="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/planos")"
MAIN_CLEAN="$(test -z "$(git -C "$ROOT" status --porcelain)" && echo YES || echo NO)"

echo "provider_after=$PROVIDER_AFTER"
echo "health=$FINAL_HEALTH"
echo "home=$FINAL_HOME"
echo "plans=$FINAL_PLANS"
echo "main_clean=$MAIN_CLEAN"

test "$PROVIDER_AFTER" = "$PROVIDER_BEFORE" || fail "provider state mudou durante o gate"
test "$FINAL_HEALTH" = "200" || fail "health final != 200"
test "$FINAL_HOME" = "200" || fail "home final != 200"
test "$FINAL_PLANS" = "200" || fail "planos final != 200"
test "$MAIN_CLEAN" = "YES" || fail "main worktree ficou suja"

echo
echo "============================================================"
echo "PUBLIC PLANS — FEATURE HOMOLOG: OK"
echo "HEAD: $EXPECTED_HEAD"
echo "HOME FROZEN VISUAL ASSETS: OK"
echo "SHARED HEADER MARKUP: OK"
echo "CENTRAL NAVIGATION/PERMISSIONS: OK"
echo "PUBLIC HEADER HOME/PLANOS: OK"
echo "AUTHENTICATED SHELL USES SHARED HEADER: OK"
echo "SHARED MOBILE NAVIGATION: OK"
echo "HOME CTA -> /planos: OK"
echo "BILLING CATALOG: OK"
echo "BENEFITS: 4"
echo "PRIMARY PLAN CARDS: 3"
echo "MEGA PROMOÇÃO: OK"
echo "COMPLEMENTARY INFO: 5"
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
