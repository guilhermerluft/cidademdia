#!/usr/bin/env bash
set -Eeuo pipefail

EXPECTED_HEAD="${1:-}"
ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
ENV_FILE="${CIDADEMDIA_ENV_FILE:-$ROOT/.env}"
BASE="${CIDADEMDIA_BASE_URL:-https://homolog.cidademdia.com.br}"
WT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
QA_DIR="${CIDADEMDIA_QA_DIR:-$HOME/cidademdia-qa/home-$(date +%Y%m%d-%H%M%S)}"

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
echo "PUBLIC HOME — FEATURE HOMOLOG"
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
echo "=== 1. MERCADO PAGO ANTES ==="
PROVIDER_BEFORE="$(dbq "
  SELECT
    (SELECT count(*) FROM billing_provider_subscriptions)::text || '|' ||
    (SELECT count(*) FROM payments)::text || '|' ||
    (SELECT count(*) FROM payment_events)::text;
")"
echo "provider_before=$PROVIDER_BEFORE"
test "$PROVIDER_BEFORE" = "0|0|0" || fail "provider state inesperado"

echo
echo "=== 2. BUILD / DEPLOY FEATURE ==="
compose build api web
compose up -d --no-deps api web
compose restart nginx

READY=0
for _ in $(seq 1 40); do
  HEALTH="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/health/ready" || true)"
  WEB="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/" || true)"
  if [ "$HEALTH" = "200" ] && [ "$WEB" = "200" ]; then
    READY=1
    break
  fi
  sleep 2
done

test "$READY" = "1" || fail "health/web não ficaram prontos"
echo "health=200"
echo "web=200"

echo
echo "=== 3. BUNDLE HOME ==="
WEB_ID="$(compose ps -q web)"
test -n "$WEB_ID" || fail "container web da feature não encontrado"

docker exec "$WEB_ID" sh -lc 'grep -R -q "Mídias do CIDADEMDIA" /usr/share/nginx/html/assets' \
  || fail "seção institucional ausente do bundle"
docker exec "$WEB_ID" sh -lc 'grep -R -q "/public/occurrences" /usr/share/nginx/html/assets' \
  || fail "consulta pública de ocorrências ausente do bundle"
docker exec "$WEB_ID" sh -lc 'grep -R -q "geolocation" /usr/share/nginx/html/assets' \
  || fail "geolocalização pública ausente do bundle"
docker exec "$WEB_ID" sh -lc 'grep -R -q "platform" /usr/share/nginx/html/assets' \
  || fail "escopo de mídia institucional ausente do bundle"
echo "home_bundle=OK"

echo
echo "=== 4. MÍDIA INSTITUCIONAL ==="
mkdir -p "$QA_DIR"
MEDIA_JSON="$QA_DIR/media.json"
MEDIA_CODE="$(curl -sS -o "$MEDIA_JSON" -w '%{http_code}' --get \
  --data-urlencode 'publisher=platform' \
  --data-urlencode 'limit=12' \
  "$BASE/api/v1/posts/placements/horizontal")"
echo "platform_media_http=$MEDIA_CODE"
test "$MEDIA_CODE" = "200" || fail "mídia institucional pública != 200"

python3 - "$MEDIA_JSON" <<'PY'
import json, sys
with open(sys.argv[1], encoding='utf-8') as fh:
    payload = json.load(fh)
items = payload.get('items') or []
for item in items:
    if item.get('masterUserId') is not None:
        raise SystemExit('ERRO: mídia de Master vazou no escopo platform')
print(f'platform_media_items={len(items)}')
print('platform_media_scope=OK')
PY

INVALID_PUBLISHER="$(curl -sS -o /dev/null -w '%{http_code}' --get \
  --data-urlencode 'publisher=master-anything' \
  "$BASE/api/v1/posts/placements/horizontal")"
echo "invalid_publisher=$INVALID_PUBLISHER"
test "$INVALID_PUBLISHER" = "400" || fail "publisher inválido deveria retornar 400"

echo
echo "=== 5. OCORRÊNCIAS PÚBLICAS / FALLBACK ==="
OCC_JSON="$QA_DIR/occurrences-sp.json"
OCC_CODE="$(curl -sS -o "$OCC_JSON" -w '%{http_code}' --get \
  --data-urlencode 'city=São Paulo' \
  --data-urlencode 'limit=6' \
  "$BASE/api/v1/public/occurrences")"
echo "public_occurrences_sp=$OCC_CODE"
test "$OCC_CODE" = "200" || fail "ocorrências públicas SP != 200"

python3 - "$OCC_JSON" <<'PY'
import json, sys
with open(sys.argv[1], encoding='utf-8') as fh:
    payload = json.load(fh)
items = payload.get('items') or []
if len(items) > 6:
    raise SystemExit('ERRO: limite público não respeitado')
blocked = {'ENCERRADA', 'CANCELADA'}
private_keys = {'authorUserId', 'postalCode', 'externalProtocolNumber', 'externalProtocolAgency', 'latitude', 'longitude'}
for item in items:
    if item.get('status') in blocked:
        raise SystemExit('ERRO: ocorrência terminal retornada na home')
    leaked = private_keys.intersection(item.keys())
    if leaked:
        raise SystemExit('ERRO: campo privado vazou: ' + ','.join(sorted(leaked)))
    for required in ('id', 'publicCode', 'categoryName', 'categorySlug', 'title', 'status', 'addressText', 'createdAt', 'updatedAt'):
        if required not in item:
            raise SystemExit('ERRO: campo público ausente: ' + required)
print(f'public_occurrences_items={len(items)}')
print('public_occurrences_sanitized=OK')
PY

GEO_CODE="$(curl -sS -o "$QA_DIR/occurrences-geo.json" -w '%{http_code}' --get \
  --data-urlencode 'latitude=-23.55052' \
  --data-urlencode 'longitude=-46.63331' \
  --data-urlencode 'radiusKm=25' \
  --data-urlencode 'limit=6' \
  "$BASE/api/v1/public/occurrences")"
echo "public_occurrences_geo=$GEO_CODE"
test "$GEO_CODE" = "200" || fail "consulta pública por coordenadas != 200"

echo
echo "=== 6. HOME DESKTOP / MOBILE ==="
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
  const context = await browser.newContext({
    ignoreHTTPSErrors: true,
    viewport,
    geolocation: { latitude: -23.55052, longitude: -46.63331 },
    permissions: ['geolocation'],
  });
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));

  await page.goto(process.env.BASE, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.locator('.public-home').waitFor({ state: 'visible', timeout: 15000 });
  await page.locator('.public-home__hero').waitFor({ state: 'visible' });
  await page.locator('.public-home__phone').waitFor({ state: 'visible' });
  await page.locator('#midias h2').waitFor({ state: 'visible' });
  await page.locator('#ocorrencias h2').waitFor({ state: 'visible' });

  const title = await page.locator('#public-home-title').innerText();
  if (!title.includes('Uma cidade melhor') || !title.includes('pode resolver')) {
    throw new Error('hero copy inesperada');
  }

  const dims = await page.evaluate(() => ({
    viewport: document.documentElement.clientWidth,
    scrollWidth: document.documentElement.scrollWidth,
  }));
  if (dims.scrollWidth - dims.viewport > 2) {
    throw new Error(`overflow horizontal: ${dims.scrollWidth}/${dims.viewport}`);
  }

  const heroVisible = await page.locator('.public-home__hero').isVisible();
  if (!heroVisible) throw new Error('hero público não visível');

  const bottomNavVisible = await page.locator('.public-home__bottom-nav').isVisible();
  if (mobile && !bottomNavVisible) throw new Error('bottom nav mobile ausente');
  if (!mobile && bottomNavVisible) throw new Error('bottom nav apareceu no desktop');

  if (errors.length) throw new Error(`pageerror: ${errors.join(' | ')}`);
  await page.screenshot({ path: `/work/${screenshot}`, fullPage: true });
  await context.close();
}

try {
  await validate({ width: 1440, height: 1000 }, 'home-desktop.png', false);
  console.log('home_desktop=OK');
  await validate({ width: 390, height: 844 }, 'home-mobile.png', true);
  console.log('home_mobile=OK');
} finally {
  await browser.close();
}
JS

test -f "$QA_DIR/home-desktop.png" || fail "screenshot desktop ausente"
test -f "$QA_DIR/home-mobile.png" || fail "screenshot mobile ausente"
echo "screenshot_desktop=$QA_DIR/home-desktop.png"
echo "screenshot_mobile=$QA_DIR/home-mobile.png"

echo
echo "=== 7. ESTADO FINAL ==="
PROVIDER_AFTER="$(dbq "
  SELECT
    (SELECT count(*) FROM billing_provider_subscriptions)::text || '|' ||
    (SELECT count(*) FROM payments)::text || '|' ||
    (SELECT count(*) FROM payment_events)::text;
")"
FINAL_HEALTH="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/health/ready")"
FINAL_WEB="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/")"
MAIN_CLEAN="$(test -z "$(git -C "$ROOT" status --porcelain)" && echo YES || echo NO)"

echo "provider_after=$PROVIDER_AFTER"
echo "health=$FINAL_HEALTH"
echo "web=$FINAL_WEB"
echo "main_clean=$MAIN_CLEAN"
test "$PROVIDER_AFTER" = "$PROVIDER_BEFORE" || fail "Mercado Pago mudou durante o gate"
test "$FINAL_HEALTH" = "200" || fail "health final != 200"
test "$FINAL_WEB" = "200" || fail "web final != 200"
test "$MAIN_CLEAN" = "YES" || fail "main ficou suja"

echo
echo "============================================================"
echo "PUBLIC HOME — FEATURE HOMOLOG: OK"
echo "HEAD: $EXPECTED_HEAD"
echo "CIDADEMDIA MEDIA SCOPE: OK"
echo "PUBLIC OCCURRENCES: OK"
echo "PUBLIC DATA SANITIZED: OK"
echo "LOCATION FALLBACK: SÃO PAULO"
echo "GEO RADIUS: 25 KM"
echo "DESKTOP: OK"
echo "MOBILE: OK"
echo "WEB: 200"
echo "HEALTH: 200"
echo "MERCADO PAGO PROVIDER: $PROVIDER_AFTER"
echo "MAIN WORKTREE: CLEAN"
echo "SCREENSHOT DESKTOP: $QA_DIR/home-desktop.png"
echo "SCREENSHOT MOBILE: $QA_DIR/home-mobile.png"
echo "============================================================"
