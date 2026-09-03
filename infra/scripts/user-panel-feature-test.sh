#!/usr/bin/env bash
set -Eeuo pipefail

EXPECTED_HEAD="${1:-}"
ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
ENV_FILE="${CIDADEMDIA_ENV_FILE:-$ROOT/.env}"
BASE="${CIDADEMDIA_BASE_URL:-https://homolog.cidademdia.com.br}"
WT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
QA_DIR="${CIDADEMDIA_QA_DIR:-$HOME/cidademdia-qa/user-panel-$(date +%Y%m%d-%H%M%S)}"
QA_SUFFIX="$(date +%s)-$$"
QA_EMAIL="qa-panel-${QA_SUFFIX}@cidademdia.local"
QA_PASSWORD="QaPanel#${QA_SUFFIX}!"
QA_NAME="QA Painel ${QA_SUFFIX}"

fail() {
  echo
  echo "ERRO: $*" >&2
  exit 1
}

for cmd in git docker curl grep; do
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
    "psql -v ON_ERROR_STOP=1 -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -c \"DELETE FROM users WHERE email = '${QA_EMAIL}';\"" \
    >/dev/null 2>&1 || true
}
trap cleanup_qa_user EXIT

echo "============================================================"
echo "USER PANEL — FEATURE HOMOLOG"
echo "============================================================"
echo "HEAD: $EXPECTED_HEAD"
echo "MAIN: $(git -C "$ROOT" rev-parse HEAD)"

echo
echo "=== 1. ARQUITETURA / PERMISSÕES ==="
bash "$WT/infra/scripts/frontend-architecture-test.sh"
grep -q 'path="/painel"' "$WT/apps/web/src/main.tsx" || fail "rota /painel não registrada"
grep -q 'getUserPanelAccess' "$WT/apps/web/src/modules/panel/UserPanelRoute.tsx" || fail "rota não usa acesso central"
grep -q 'href="/painel"' "$WT/apps/web/src/app/layout/AppHeader.tsx" || fail "dropdown não aponta para /painel"
! grep -q 'app-header__logout' "$WT/apps/web/src/app/layout/AppHeader.tsx" || fail "logout voltou a ficar exposto no header"
! grep -q 'selectedCategory' "$WT/apps/web/src/modules/occurrences/OccurrenceCenter.tsx" || fail "badge dinâmico de categoria voltou ao header de Nova ocorrência"
grep -q 'geocodePublicOccurrenceCity' "$WT/apps/web/src/modules/occurrences/OccurrenceGeoFilter.tsx" || fail "filtro privado não reutiliza geocodificação compartilhada"
grep -q 'requestBrowserCoordinates' "$WT/apps/web/src/modules/occurrences/OccurrenceGeoFilter.tsx" || fail "filtro privado não reutiliza geolocalização compartilhada"
grep -q 'type="number"' "$WT/apps/web/src/modules/occurrences/OccurrenceGeoFilter.tsx" || fail "raio privado não usa input numérico padronizado"
grep -q 'aria-label="Limpar filtros"' "$WT/apps/web/src/modules/occurrences/OccurrenceGeoFilter.tsx" || fail "limpeza compacta perdeu nome acessível"
grep -q 'fa-eraser' "$WT/apps/web/src/modules/occurrences/OccurrenceGeoFilter.tsx" || fail "botão compacto de limpeza perdeu o ícone"
echo "panel_architecture=OK"
echo "occurrence_tracking_filter_architecture=OK"

echo
echo "=== 2. BUILD / DEPLOY FEATURE ==="
compose build api web
compose up -d --no-deps api web
compose restart nginx

READY=0
for _ in $(seq 1 40); do
  HEALTH="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/health/ready" || true)"
  HOME_CODE="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/" || true)"
  PANEL_CODE="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/painel" || true)"
  if [ "$HEALTH" = "200" ] && [ "$HOME_CODE" = "200" ] && [ "$PANEL_CODE" = "200" ]; then
    READY=1
    break
  fi
  sleep 2
done

test "$READY" = "1" || fail "health/home/painel não ficaram prontos"
echo "health=200"
echo "home=200"
echo "panel_shell=200"

echo
echo "=== 3. BROWSER — HOME / DROPDOWN / PAINEL / LOGOUT ==="
mkdir -p "$QA_DIR"
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

function overlaps(a, b) {
  return a.x < b.x + b.width
    && a.x + a.width > b.x
    && a.y < b.y + b.height
    && a.y + a.height > b.y;
}

try {
  const anonymousPanel = await context.newPage();
  await anonymousPanel.goto(`${process.env.BASE}/painel`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await anonymousPanel.waitForURL(url => url.pathname === '/', { timeout: 15000 });
  await anonymousPanel.close();
  console.log('anonymous_panel_redirect=OK');

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

  await page.goto(`${process.env.BASE}/`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  const account = page.getByRole('button', { name: new RegExp(`Abrir menu de ${process.env.QA_NAME}`) });
  await account.waitFor({ state: 'visible', timeout: 15000 });

  if (await page.getByRole('heading', { name: 'Minhas ocorrências', exact: true }).count()) {
    throw new Error('Minhas ocorrências ainda está renderizada na Home');
  }
  if (await page.locator('#dashboard-chat').count()) {
    throw new Error('Conversas ainda está renderizada na Home');
  }

  const logoutItem = page.getByRole('menuitem', { name: /Sair/ });
  if (await logoutItem.isVisible()) {
    throw new Error('Sair está visível antes de abrir o dropdown');
  }

  await account.hover();
  const panelItem = page.getByRole('menuitem', { name: /Painel/ });
  await panelItem.waitFor({ state: 'visible', timeout: 5000 });
  await logoutItem.waitFor({ state: 'visible', timeout: 5000 });
  console.log('desktop_profile_hover=OK');
  console.log('logout_hidden_until_dropdown=OK');

  const accountBox = await account.boundingBox();
  const panelBox = await panelItem.boundingBox();
  if (!accountBox || !panelBox) {
    throw new Error('não foi possível medir a transição entre perfil e dropdown');
  }

  await page.mouse.move(
    accountBox.x + accountBox.width / 2,
    accountBox.y + accountBox.height - 2,
  );
  await page.mouse.move(
    panelBox.x + panelBox.width / 2,
    panelBox.y + panelBox.height / 2,
    { steps: 18 },
  );
  await page.waitForTimeout(320);
  if (!(await panelItem.isVisible())) {
    throw new Error('dropdown fechou durante a transição real do mouse até Painel');
  }
  console.log('desktop_pointer_transition=OK');

  await page.screenshot({ path: '/work/header-dropdown-desktop.png', fullPage: false });

  await panelItem.click();
  await page.waitForURL(url => url.pathname === '/painel', { timeout: 15000 });
  await page.getByRole('heading', { name: 'Painel', exact: true }).waitFor({ state: 'visible', timeout: 15000 });
  await page.getByRole('heading', { name: 'Minhas ocorrências', exact: true }).waitFor({ state: 'visible', timeout: 15000 });
  await page.getByText('Conversas', { exact: true }).first().waitFor({ state: 'visible', timeout: 15000 });

  const trackingFilters = page.getByLabel('Filtros das ocorrências publicadas por você', { exact: true });
  await trackingFilters.waitFor({ state: 'visible', timeout: 15000 });
  await trackingFilters.getByRole('textbox', { name: 'Cidade', exact: true }).waitFor({ state: 'visible', timeout: 15000 });
  await trackingFilters.getByRole('spinbutton', { name: 'Raio em km', exact: true }).waitFor({ state: 'visible', timeout: 15000 });

  const useLocationButton = trackingFilters.getByRole('button', { name: 'Usar minha localização', exact: true });
  const filterButton = trackingFilters.getByRole('button', { name: 'Filtrar', exact: true });
  const clearButton = trackingFilters.getByRole('button', { name: 'Limpar filtros', exact: true });
  await useLocationButton.waitFor({ state: 'visible', timeout: 15000 });
  await filterButton.waitFor({ state: 'visible', timeout: 15000 });
  await clearButton.waitFor({ state: 'visible', timeout: 15000 });

  const locationBox = await useLocationButton.boundingBox();
  const filterBox = await filterButton.boundingBox();
  const clearBox = await clearButton.boundingBox();
  if (!locationBox || !filterBox || !clearBox) {
    throw new Error('não foi possível medir os botões dos filtros de acompanhamento');
  }
  if (locationBox.y + locationBox.height > filterBox.y + 1) {
    throw new Error('Usar minha localização não está acima da linha de ações');
  }
  if (overlaps(filterBox, clearBox) || overlaps(locationBox, filterBox) || overlaps(locationBox, clearBox)) {
    throw new Error('botões dos filtros de acompanhamento estão se sobrepondo');
  }
  if (clearBox.width > 52) {
    throw new Error(`botão Limpar deixou de ser compacto: ${clearBox.width}px`);
  }
  console.log('occurrence_tracking_filter_layout=OK');
  console.log('occurrence_tracking_filters=OK');
  console.log('citizen_panel_modules=OK');

  await page.screenshot({ path: '/work/user-panel-desktop.png', fullPage: true });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`${process.env.BASE}/`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  const mobileAccount = page.getByRole('button', { name: new RegExp(`Abrir menu de ${process.env.QA_NAME}`) });
  await mobileAccount.waitFor({ state: 'visible', timeout: 15000 });
  await mobileAccount.click();
  await page.getByRole('menuitem', { name: /Painel/ }).waitFor({ state: 'visible', timeout: 5000 });
  await page.getByRole('menuitem', { name: /Sair/ }).waitFor({ state: 'visible', timeout: 5000 });
  console.log('mobile_profile_click=OK');

  await page.screenshot({ path: '/work/header-dropdown-mobile.png', fullPage: false });

  await page.getByRole('menuitem', { name: /Sair/ }).click();
  await page.getByRole('button', { name: 'Entrar', exact: true }).waitFor({ state: 'visible', timeout: 15000 });
  if (await page.locator('.app-header__account-menu').count()) {
    throw new Error('menu autenticado permaneceu após logout');
  }
  console.log('dropdown_logout=OK');

  if (errors.length) throw new Error(`pageerror: ${errors.join(' | ')}`);
} finally {
  await context.close();
  await browser.close();
}
JS

test -f "$QA_DIR/header-dropdown-desktop.png" || fail "screenshot dropdown desktop ausente"
test -f "$QA_DIR/user-panel-desktop.png" || fail "screenshot painel desktop ausente"
test -f "$QA_DIR/header-dropdown-mobile.png" || fail "screenshot dropdown mobile ausente"
echo "screenshots=$QA_DIR"

echo
echo "=== 4. LIMPEZA / ESTADO FINAL ==="
cleanup_qa_user
QA_LEFT="$(compose exec -T db sh -lc "psql -At -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -c \"SELECT count(*) FROM users WHERE email = '${QA_EMAIL}';\"")"
test "$QA_LEFT" = "0" || fail "usuário QA não foi removido"
trap - EXIT

FINAL_HEALTH="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/health/ready")"
FINAL_HOME="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/")"
FINAL_PANEL="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/painel")"
MAIN_CLEAN="$(test -z "$(git -C "$ROOT" status --porcelain)" && echo YES || echo NO)"

test "$FINAL_HEALTH" = "200" || fail "health final != 200"
test "$FINAL_HOME" = "200" || fail "home final != 200"
test "$FINAL_PANEL" = "200" || fail "painel final != 200"
test "$MAIN_CLEAN" = "YES" || fail "main worktree ficou suja"

echo "qa_cleanup=OK"
echo "============================================================"
echo "USER PANEL — FEATURE HOMOLOG: OK"
echo "HEAD: $EXPECTED_HEAD"
echo "PROTECTED /painel: OK"
echo "HOME PRIVATE MODULES REMOVED: OK"
echo "PERMISSION-DRIVEN PANEL: OK"
echo "PROFILE DROPDOWN: OK"
echo "DESKTOP HOVER: OK"
echo "DESKTOP POINTER TRANSITION: OK"
echo "OCCURRENCE TRACKING FILTERS: OK"
echo "OCCURRENCE TRACKING FILTER LAYOUT: OK"
echo "NEW OCCURRENCE HEADER: OK"
echo "MOBILE CLICK: OK"
echo "LOGOUT ONLY IN DROPDOWN: OK"
echo "QA CLEANUP: OK"
echo "MAIN WORKTREE: CLEAN"
echo "SCREENSHOTS: $QA_DIR"
echo "============================================================"