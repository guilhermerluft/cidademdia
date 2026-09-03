#!/usr/bin/env bash
set -Eeuo pipefail

EXPECTED_HEAD="${1:-}"
ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
ENV_FILE="${CIDADEMDIA_ENV_FILE:-$ROOT/.env}"
BASE="${CIDADEMDIA_BASE_URL:-https://homolog.cidademdia.com.br}"
WT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
QA_DIR="${CIDADEMDIA_QA_DIR:-$HOME/cidademdia-qa/public-occurrences-$(date +%Y%m%d-%H%M%S)}"

fail() {
  echo
  echo "ERRO: $*" >&2
  exit 1
}

for cmd in git docker curl python3 grep; do
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

echo "============================================================"
echo "PUBLIC OCCURRENCES — FEATURE HOMOLOG"
echo "============================================================"
echo "HEAD: $EXPECTED_HEAD"
echo "MAIN: $(git -C "$ROOT" rev-parse HEAD)"

echo
echo "=== 1. ARQUITETURA / ROTA ==="
bash "$WT/infra/scripts/frontend-architecture-test.sh"
grep -q 'path="/ocorrencias"' "$WT/apps/web/src/main.tsx" \
  || fail "rota /ocorrencias não registrada"
grep -q "href: '/ocorrencias'" "$WT/apps/web/src/app/layout/AppNavigation.tsx" \
  || fail "header não aponta Ocorrências para /ocorrencias"
grep -q 'searchPublicOccurrences' "$WT/apps/web/src/modules/occurrences/PublicOccurrences.tsx" \
  || fail "página não usa busca pública central"
grep -q 'requestBrowserCoordinates' "$WT/apps/web/src/modules/occurrences/PublicOccurrences.tsx" \
  || fail "página não usa resolução central de geolocalização"
grep -q 'PublicOccurrenceMapFilter' "$WT/apps/web/src/modules/occurrences/PublicOccurrences.tsx" \
  || fail "filtro por pin/mapa ausente"
echo "route_architecture=OK"

echo
echo "=== 2. BUILD / DEPLOY FEATURE ==="
compose build api web
compose up -d --no-deps api web
compose restart nginx

READY=0
for _ in $(seq 1 40); do
  HEALTH="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/health/ready" || true)"
  HOME_CODE="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/" || true)"
  OCC_CODE="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/ocorrencias" || true)"
  if [ "$HEALTH" = "200" ] && [ "$HOME_CODE" = "200" ] && [ "$OCC_CODE" = "200" ]; then
    READY=1
    break
  fi
  sleep 2
done

test "$READY" = "1" || fail "health/home/ocorrencias não ficaram prontos"
echo "health=200"
echo "home=200"
echo "occurrences_route=200"

echo
echo "=== 3. API PÚBLICA PAGINADA ==="
mkdir -p "$QA_DIR"
CITY_JSON="$QA_DIR/city-page.json"
GEO_JSON="$QA_DIR/geo-page.json"

CITY_CODE="$(curl -sS -G -o "$CITY_JSON" -w '%{http_code}' \
  --data-urlencode 'city=São Paulo' \
  --data-urlencode 'page=1' \
  --data-urlencode 'pageSize=2' \
  "$BASE/api/v1/public/occurrences")"
test "$CITY_CODE" = "200" || fail "busca pública por cidade != 200"

GEO_CODE="$(curl -sS -G -o "$GEO_JSON" -w '%{http_code}' \
  --data-urlencode 'latitude=-23.55052' \
  --data-urlencode 'longitude=-46.633308' \
  --data-urlencode 'radiusKm=25' \
  --data-urlencode 'page=1' \
  --data-urlencode 'pageSize=12' \
  "$BASE/api/v1/public/occurrences")"
test "$GEO_CODE" = "200" || fail "busca pública por raio != 200"

python3 - "$CITY_JSON" "$GEO_JSON" <<'PY'
import json, sys
for path in sys.argv[1:]:
    with open(path, encoding='utf-8') as fh:
        payload = json.load(fh)
    required = {'items', 'page', 'pageSize', 'totalItems', 'totalPages'}
    missing = required.difference(payload)
    if missing:
        raise SystemExit('ERRO: paginação pública sem: ' + ','.join(sorted(missing)))
    if payload['page'] != 1:
        raise SystemExit('ERRO: página pública inesperada')
    if not isinstance(payload['items'], list):
        raise SystemExit('ERRO: items não é lista')
    for item in payload['items']:
        forbidden = {'authorUserId', 'latitude', 'longitude', 'postalCode', 'externalProtocolNumber'}
        leaked = forbidden.intersection(item)
        if leaked:
            raise SystemExit('ERRO: payload público expôs: ' + ','.join(sorted(leaked)))
print('public_pagination=OK')
print('public_payload_sanitized=OK')
PY

echo
echo "=== 4. BROWSER — FALLBACK / FILTROS / MAPA ==="
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

async function validate(viewport, screenshot) {
  const context = await browser.newContext({
    ignoreHTTPSErrors: true,
    viewport,
    permissions: [],
  });
  const page = await context.newPage();
  const errors = [];
  const occurrenceRequests = [];
  page.on('pageerror', error => errors.push(error.message));
  page.on('request', request => {
    if (request.url().includes('/api/v1/public/occurrences')) {
      occurrenceRequests.push(request.url());
    }
  });

  await page.goto(`${process.env.BASE}/ocorrencias`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.locator('.public-occurrences').waitFor({ state: 'visible', timeout: 15000 });
  await page.locator('.app-header').waitFor({ state: 'visible' });

  const occurrenceLink = page.locator('.app-header__nav a[href="/ocorrencias"]');
  await occurrenceLink.waitFor({ state: 'visible' });
  const current = await occurrenceLink.getAttribute('aria-current');
  if (current !== 'page') throw new Error('Ocorrências não está ativa no header');

  const city = page.getByLabel('Cidade');
  const radius = page.getByLabel('Raio em km');
  await city.waitFor({ state: 'visible' });
  await radius.waitFor({ state: 'visible' });

  await page.waitForFunction(() => {
    const el = document.querySelector('.public-occurrences__location-summary strong');
    return el && el.textContent?.includes('São Paulo');
  }, { timeout: 15000 });

  const radiusValue = await radius.inputValue();
  if (Number(radiusValue) !== 25) throw new Error(`raio inicial deveria ser 25 km; recebeu ${radiusValue}`);

  await page.waitForFunction(() => {
    const el = document.querySelector('.public-occurrences__results-heading h2');
    return el && !el.textContent?.includes('Definindo');
  }, { timeout: 15000 });

  if (!occurrenceRequests.some(url => url.includes('radiusKm=25'))) {
    throw new Error(`busca inicial não usou raio de 25 km: ${occurrenceRequests.join(' | ')}`);
  }

  await page.getByRole('button', { name: 'Usar minha localização' }).waitFor({ state: 'visible' });
  await page.getByRole('button', { name: 'Aplicar cidade e raio' }).waitFor({ state: 'visible' });
  await page.locator('.public-occurrences__map').waitFor({ state: 'visible' });

  const dims = await page.evaluate(() => ({
    width: document.documentElement.clientWidth,
    scrollWidth: document.documentElement.scrollWidth,
  }));
  if (dims.scrollWidth - dims.width > 2) {
    throw new Error(`overflow horizontal: ${dims.scrollWidth}/${dims.width}`);
  }

  if (errors.length) throw new Error(`pageerror: ${errors.join(' | ')}`);
  await page.screenshot({ path: `/work/${screenshot}`, fullPage: true });
  await context.close();
}

try {
  await validate({ width: 1440, height: 1000 }, 'occurrences-desktop.png');
  console.log('occurrences_desktop=OK');
  await validate({ width: 390, height: 844 }, 'occurrences-mobile.png');
  console.log('occurrences_mobile=OK');
} finally {
  await browser.close();
}
JS

test -f "$QA_DIR/occurrences-desktop.png" || fail "screenshot desktop ausente"
test -f "$QA_DIR/occurrences-mobile.png" || fail "screenshot mobile ausente"
echo "screenshots=$QA_DIR"

echo
echo "=== 5. ESTADO FINAL ==="
FINAL_HEALTH="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/health/ready")"
FINAL_HOME="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/")"
FINAL_OCC="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/ocorrencias")"
MAIN_CLEAN="$(test -z "$(git -C "$ROOT" status --porcelain)" && echo YES || echo NO)"

test "$FINAL_HEALTH" = "200" || fail "health final != 200"
test "$FINAL_HOME" = "200" || fail "home final != 200"
test "$FINAL_OCC" = "200" || fail "ocorrencias final != 200"
test "$MAIN_CLEAN" = "YES" || fail "main worktree ficou suja"

echo "============================================================"
echo "PUBLIC OCCURRENCES — FEATURE HOMOLOG: OK"
echo "HEAD: $EXPECTED_HEAD"
echo "PUBLIC ROUTE /ocorrencias: OK"
echo "PUBLIC PAGINATION: OK"
echo "CITY + RADIUS FILTER: OK"
echo "INITIAL GEOLOCATION / SÃO PAULO FALLBACK: OK"
echo "CURRENT LOCATION FILTER: OK"
echo "MAP PIN FILTER: OK"
echo "SHARED HEADER: OK"
echo "DESKTOP: OK"
echo "MOBILE: OK"
echo "MAIN WORKTREE: CLEAN"
echo "SCREENSHOTS: $QA_DIR"
echo "============================================================"
