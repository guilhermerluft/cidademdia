#!/usr/bin/env bash
set -Eeuo pipefail

EXPECTED_HEAD="${1:-}"
ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
ENV_FILE="${CIDADEMDIA_ENV_FILE:-$ROOT/.env}"
BASE="${CIDADEMDIA_BASE_URL:-https://homolog.cidademdia.com.br}"
WT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
QA_DIR="${CIDADEMDIA_QA_DIR:-$HOME/cidademdia-qa/maps-$(date +%Y%m%d-%H%M%S)}"
RUN_TAG="MAPS-SMOKE-$(date +%s)-$$"

fail() {
  echo
  echo "ERRO: $*" >&2
  exit 1
}

root_compose() {
  docker compose --env-file "$ENV_FILE" -f "$ROOT/infra/docker-compose.yml" "$@"
}

CURRENT_WEB_ID="$(root_compose ps -q web)"
test -n "$CURRENT_WEB_ID" || fail "container web atual não encontrado"

PROJECT="$(docker inspect "$CURRENT_WEB_ID" --format '{{ index .Config.Labels "com.docker.compose.project" }}')"
test -n "$PROJECT" || fail "compose project não encontrado"

compose() {
  docker compose -p "$PROJECT" --env-file "$ENV_FILE" -f "$WT/infra/docker-compose.yml" "$@"
}

dbq() {
  root_compose exec -T db sh -lc \
    'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At' \
    <<< "$1"
}

dbexec() {
  root_compose exec -T db sh -lc \
    'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' \
    <<< "$1"
}

cleanup() {
  set +e
  dbexec "
    DELETE FROM occurrence_status_history
    WHERE occurrence_id IN (
      SELECT id FROM occurrences
      WHERE external_protocol_number LIKE '${RUN_TAG}%'
    );

    DELETE FROM occurrence_complements
    WHERE occurrence_id IN (
      SELECT id FROM occurrences
      WHERE external_protocol_number LIKE '${RUN_TAG}%'
    );

    DELETE FROM occurrence_service_forecasts
    WHERE occurrence_id IN (
      SELECT id FROM occurrences
      WHERE external_protocol_number LIKE '${RUN_TAG}%'
    );

    DELETE FROM occurrences
    WHERE external_protocol_number LIKE '${RUN_TAG}%';
  " >/dev/null 2>&1 || true
}

trap cleanup EXIT

echo "============================================================"
echo "MAPS / POSTGIS — FEATURE HOMOLOG"
echo "============================================================"

test -d "$ROOT/.git" || fail "repo principal não encontrado"
test -f "$ENV_FILE" || fail ".env não encontrado"
test -n "$EXPECTED_HEAD" || fail "informe o HEAD esperado como primeiro argumento"

test "$(git -C "$WT" rev-parse HEAD)" = "$EXPECTED_HEAD" \
  || fail "worktree não está no HEAD esperado"

test "$(git -C "$ROOT" branch --show-current)" = "main" \
  || fail "repo principal não está na main"

test -z "$(git -C "$ROOT" status --porcelain)" \
  || fail "main local está suja"

echo "HEAD: $EXPECTED_HEAD"
echo "MAIN: $(git -C "$ROOT" rev-parse HEAD)"

echo
echo "=== 1. CONFIGURAÇÃO MAPS ==="
if grep -qE '^GOOGLE_MAPS_API_KEY=.+$' "$ENV_FILE"; then
  echo "google_maps_key=PRESENT"
else
  fail "GOOGLE_MAPS_API_KEY ausente"
fi

echo
echo "=== 2. POSTGIS ==="
POSTGIS_VERSION="$(dbq "SELECT extversion FROM pg_extension WHERE extname='postgis';")"
LOCATION_TYPE="$(dbq "
  SELECT format_type(a.atttypid, a.atttypmod)
  FROM pg_attribute a
  JOIN pg_class c ON c.oid = a.attrelid
  WHERE c.relname = 'occurrences'
    AND a.attname = 'location'
    AND a.attnum > 0;
")"
GIST_INDEX="$(dbq "
  SELECT count(*)
  FROM pg_indexes
  WHERE schemaname = 'public'
    AND tablename = 'occurrences'
    AND indexname = 'ix_occurrences_location_gist';
")"

echo "postgis=$POSTGIS_VERSION"
echo "location_type=$LOCATION_TYPE"
echo "gist_index=$GIST_INDEX"
test -n "$POSTGIS_VERSION" || fail "PostGIS ausente"
echo "$LOCATION_TYPE" | grep -qi 'geography' || fail "location não é geography"
test "$GIST_INDEX" = "1" || fail "índice GiST ausente"

echo
echo "=== 3. MERCADO PAGO ANTES ==="
PROVIDER_BEFORE="$(dbq "
  SELECT
    (SELECT count(*) FROM billing_provider_subscriptions)::text || '|' ||
    (SELECT count(*) FROM payments)::text || '|' ||
    (SELECT count(*) FROM payment_events)::text;
")"
echo "provider_before=$PROVIDER_BEFORE"
test "$PROVIDER_BEFORE" = "0|0|0" || fail "provider state inesperado"

echo
echo "=== 4. BUILD / DEPLOY FEATURE ==="
compose build api web
compose up -d --no-deps api web

# api/web podem receber novos IPs ao serem recriados; force o nginx a resolver os upstreams novamente.
compose restart nginx
sleep 3

READY=0
for _ in $(seq 1 40); do
  HEALTH_CODE="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/health/ready" || true)"
  WEB_CODE="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/" || true)"
  if [ "$HEALTH_CODE" = "200" ] && [ "$WEB_CODE" = "200" ]; then
    READY=1
    break
  fi
  sleep 2
done

if [ "$READY" != "1" ]; then
  compose ps
  compose logs --tail=100 api nginx
  fail "health/web não ficaram prontos"
fi

echo "health=200"
echo "web=200"

echo
echo "=== 5. BUNDLE MAPS ==="
WEB_ID="$(compose ps -q web)"
test -n "$WEB_ID" || fail "web feature não encontrado"

docker exec "$WEB_ID" sh -lc \
  'grep -R -q "maps.googleapis.com/maps/api/js" /usr/share/nginx/html/assets' \
  || fail "loader Maps ausente do bundle"

docker exec "$WEB_ID" sh -lc \
  'grep -R -q "PlaceAutocompleteElement" /usr/share/nginx/html/assets' \
  || fail "Places New ausente do bundle"

docker exec "$WEB_ID" sh -lc \
  'grep -R -q "occurrences/geo-search" /usr/share/nginx/html/assets' \
  || fail "geo-search ausente do bundle"

echo "maps_bundle=OK"
echo "places_new_bundle=OK"

echo
echo "=== 6. GEO SEM AUTENTICAÇÃO ==="
GEO_UNAUTH="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/api/v1/occurrences/geo-search?city=Porto%20Alegre")"
echo "geo_unauth=$GEO_UNAUTH"
test "$GEO_UNAUTH" = "401" || fail "geo-search sem auth deveria retornar 401"

echo
echo "=== 7. USUÁRIO / JWT DE QA ==="
ACTOR_ROW="$(dbq "
  SELECT id::text || '|' || email
  FROM users
  WHERE status = 'Active'
  ORDER BY last_login_at DESC NULLS LAST, created_at
  LIMIT 1;
")"
test -n "$ACTOR_ROW" || fail "nenhum usuário Active encontrado"
ACTOR_ID="${ACTOR_ROW%%|*}"
ACTOR_EMAIL="${ACTOR_ROW#*|}"

API_ID="$(compose ps -q api)"
test -n "$API_ID" || fail "api feature não encontrada"
JWT_SIGNING_KEY="$(docker exec "$API_ID" printenv JWT_SIGNING_KEY)"
JWT_ISSUER="$(docker exec "$API_ID" printenv JWT_ISSUER)"
JWT_AUDIENCE="$(docker exec "$API_ID" printenv JWT_AUDIENCE)"
test "${#JWT_SIGNING_KEY}" -ge 32 || fail "JWT signing key inválida"

TOKEN="$(
  USER_ID="$ACTOR_ID" \
  JWT_SIGNING_KEY="$JWT_SIGNING_KEY" \
  JWT_ISSUER="$JWT_ISSUER" \
  JWT_AUDIENCE="$JWT_AUDIENCE" \
  python3 - <<'PY'
import os, json, time, uuid, hmac, hashlib, base64

def enc(value):
    return base64.urlsafe_b64encode(value).rstrip(b'=').decode()

now = int(time.time())
header = {'alg': 'HS256', 'typ': 'JWT'}
payload = {
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier': os.environ['USER_ID'],
    'jti': uuid.uuid4().hex,
    'iss': os.environ['JWT_ISSUER'],
    'aud': os.environ['JWT_AUDIENCE'],
    'nbf': now - 5,
    'iat': now,
    'exp': now + 1800,
}
h = enc(json.dumps(header, separators=(',', ':')).encode())
p = enc(json.dumps(payload, separators=(',', ':')).encode())
unsigned = h + '.' + p
signature = hmac.new(os.environ['JWT_SIGNING_KEY'].encode(), unsigned.encode(), hashlib.sha256).digest()
print(unsigned + '.' + enc(signature))
PY
)"
unset JWT_SIGNING_KEY

test -n "$TOKEN" || fail "JWT de QA não gerado"
echo "qa_actor=$ACTOR_EMAIL"
echo "jwt=OK"

echo
echo "=== 8. CATEGORIA ==="
CATEGORY_BODY="$(mktemp)"
CATEGORY_CODE="$(curl -sS -o "$CATEGORY_BODY" -w '%{http_code}' -H "Authorization: Bearer $TOKEN" "$BASE/api/v1/occurrences/categories")"
test "$CATEGORY_CODE" = "200" || fail "categories != 200"
CATEGORY_ID="$(python3 - "$CATEGORY_BODY" <<'PY'
import json, sys
with open(sys.argv[1], encoding='utf-8') as f:
    data = json.load(f)
if not data:
    raise SystemExit(1)
print(data[0]['id'])
PY
)"
rm -f "$CATEGORY_BODY"
test -n "$CATEGORY_ID" || fail "nenhuma categoria ativa"
echo "category=OK"

create_fixture() {
  local title="$1" address="$2" lat="$3" lng="$4" cep="$5" uf="$6" suffix="$7"
  local body payload code
  body="$(mktemp)"
  payload="$(mktemp)"
  python3 - "$payload" "$CATEGORY_ID" "$RUN_TAG" "$title" "$address" "$lat" "$lng" "$cep" "$uf" "$suffix" <<'PY'
import json, sys
path, category, tag, title, address, lat, lng, cep, uf, suffix = sys.argv[1:]
with open(path, 'w', encoding='utf-8') as f:
    json.dump({
        'categoryId': category,
        'title': title,
        'description': 'Fixture temporária Maps/PostGIS.',
        'addressText': address,
        'latitude': float(lat),
        'longitude': float(lng),
        'postalCode': cep,
        'cityId': None,
        'stateCode': uf,
        'externalProtocolNumber': f'{tag}-{suffix}',
        'externalProtocolAgency': 'MAPS_SMOKE',
        'mediaIds': [],
    }, f)
PY
  code="$(curl -sS -o "$body" -w '%{http_code}' -X POST -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' --data-binary "@$payload" "$BASE/api/v1/occurrences")"
  rm -f "$payload"
  test "$code" = "201" || { cat "$body"; rm -f "$body"; fail "fixture $suffix não criada"; }
  python3 - "$body" <<'PY'
import json, sys
with open(sys.argv[1], encoding='utf-8') as f:
    print(json.load(f)['id'])
PY
  rm -f "$body"
}

echo
echo "=== 9. FIXTURES ==="
NEAR_ID="$(create_fixture 'Maps smoke Porto Alegre' 'Praça da Alfândega, Centro Histórico, Porto Alegre - RS, Brasil' '-30.0305' '-51.2300' '90010150' 'RS' 'NEAR')"
FAR_ID="$(create_fixture 'Maps smoke São Paulo' 'Praça da Sé, Sé, São Paulo - SP, Brasil' '-23.5505' '-46.6333' '01001000' 'SP' 'FAR')"
echo "near=$NEAR_ID"
echo "far=$FAR_ID"

echo
echo "=== 10. PERSISTÊNCIA DO POINT ==="
DETAIL_BODY="$(mktemp)"
DETAIL_CODE="$(curl -sS -o "$DETAIL_BODY" -w '%{http_code}' -H "Authorization: Bearer $TOKEN" "$BASE/api/v1/occurrences/$NEAR_ID")"
test "$DETAIL_CODE" = "200" || fail "details != 200"
python3 - "$DETAIL_BODY" <<'PY'
import json, math, sys
with open(sys.argv[1], encoding='utf-8') as f:
    item = json.load(f)
assert math.isclose(float(item['latitude']), -30.0305, abs_tol=0.000001)
assert math.isclose(float(item['longitude']), -51.2300, abs_tol=0.000001)
assert item['stateCode'] == 'RS'
assert item['postalCode'] == '90010150'
assert 'Porto Alegre' in item['addressText']
PY
rm -f "$DETAIL_BODY"
echo "persisted_point=OK"

echo
echo "=== 11. FILTRO POR RAIO ==="
RADIUS_BODY="$(mktemp)"
RADIUS_CODE="$(curl -sS -o "$RADIUS_BODY" -w '%{http_code}' --get -H "Authorization: Bearer $TOKEN" --data-urlencode 'latitude=-30.0305' --data-urlencode 'longitude=-51.2300' --data-urlencode 'radiusKm=5' --data-urlencode 'page=1' --data-urlencode 'pageSize=50' "$BASE/api/v1/occurrences/geo-search")"
test "$RADIUS_CODE" = "200" || { cat "$RADIUS_BODY"; fail "radius search != 200"; }
python3 - "$RADIUS_BODY" "$NEAR_ID" "$FAR_ID" <<'PY'
import json, sys
path, near_id, far_id = sys.argv[1:]
with open(path, encoding='utf-8') as f:
    data = json.load(f)
ids = {item['id'] for item in data['items']}
assert near_id in ids
assert far_id not in ids
PY
rm -f "$RADIUS_BODY"
echo "postgis_radius=OK"

echo
echo "=== 12. FILTRO POR CIDADE ==="
CITY_BODY="$(mktemp)"
CITY_CODE="$(curl -sS -o "$CITY_BODY" -w '%{http_code}' --get -H "Authorization: Bearer $TOKEN" --data-urlencode 'city=Porto Alegre' --data-urlencode 'page=1' --data-urlencode 'pageSize=50' "$BASE/api/v1/occurrences/geo-search")"
test "$CITY_CODE" = "200" || { cat "$CITY_BODY"; fail "city search != 200"; }
python3 - "$CITY_BODY" "$NEAR_ID" "$FAR_ID" <<'PY'
import json, sys
path, near_id, far_id = sys.argv[1:]
with open(path, encoding='utf-8') as f:
    data = json.load(f)
ids = {item['id'] for item in data['items']}
assert near_id in ids
assert far_id not in ids
PY
rm -f "$CITY_BODY"
echo "city_filter=OK"

echo
echo "=== 13. GEO INVÁLIDO ==="
INVALID_GEO="$(curl -sS -o /dev/null -w '%{http_code}' --get -H "Authorization: Bearer $TOKEN" --data-urlencode 'latitude=-30.03' --data-urlencode 'radiusKm=5' "$BASE/api/v1/occurrences/geo-search")"
echo "invalid_geo=$INVALID_GEO"
test "$INVALID_GEO" = "400" || fail "geo incompleto deveria retornar 400"

echo
echo "=== 14. GOOGLE MAPS / PLACES NEW NO BROWSER ==="
mkdir -p "$QA_DIR"

docker run --rm -i \
  --network host \
  -v "$QA_DIR:/work" \
  -e BASE="$BASE" \
  -e TOKEN="$TOKEN" \
  -e USER_ID="$ACTOR_ID" \
  -e USER_EMAIL="$ACTOR_EMAIL" \
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

function redact(value) {
  return String(value ?? '')
    .replace(/([?&]key=)[^&\s]+/gi, '$1REDACTED')
    .replace(/AIza[0-9A-Za-z_-]+/g, 'REDACTED_KEY');
}

const browser = await chromium.launch({
  executablePath: process.env.CHROME,
  headless: true,
  args: ['--no-sandbox', '--disable-dev-shm-usage'],
});

const context = await browser.newContext({
  ignoreHTTPSErrors: true,
  viewport: { width: 390, height: 844 },
});

await context.route('**/api/v1/auth/refresh', async (route) => {
  await route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      accessToken: process.env.TOKEN,
      accessTokenExpiresAt: new Date(Date.now() + 25 * 60 * 1000).toISOString(),
      user: {
        id: process.env.USER_ID,
        email: process.env.USER_EMAIL,
        displayName: 'Maps QA',
        roles: ['CITIZEN'],
      },
    }),
  });
});

const page = await context.newPage();
page.on('console', (message) => {
  if (message.type() === 'error' || message.type() === 'warning') {
    console.log(`browser_console_${message.type()}=${redact(message.text())}`);
  }
});
page.on('pageerror', (error) => {
  console.log(`browser_pageerror=${redact(error.message)}`);
});
page.on('requestfailed', (request) => {
  if (request.url().includes('googleapis.com') || request.url().includes('gstatic.com')) {
    console.log(`google_request_failed=${redact(request.failure()?.errorText)}:${redact(request.url())}`);
  }
});

try {
  await page.goto(process.env.BASE, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.locator('.dashboard-shell').waitFor({ state: 'visible', timeout: 15000 });
  await page.locator('.occurrence-map-picker').waitFor({ state: 'visible', timeout: 15000 });

  const state = await Promise.race([
    page.locator('.occurrence-map-picker__map.is-ready')
      .waitFor({ state: 'visible', timeout: 30000 })
      .then(() => 'ready'),
    page.locator('.occurrence-map-picker__warning')
      .waitFor({ state: 'visible', timeout: 30000 })
      .then(() => 'error'),
  ]);

  if (state === 'error') {
    const warning = await page.locator('.occurrence-map-picker__warning').innerText();
    throw new Error(`Maps UI fallback ativado: ${warning}`);
  }

  const result = await page.evaluate(async () => {
    const google = globalThis.google;
    if (!google?.maps?.Geocoder || typeof google.maps.importLibrary !== 'function') {
      throw new Error('Google Maps runtime não inicializou');
    }

    const places = await google.maps.importLibrary('places');
    if (!places?.PlaceAutocompleteElement) {
      throw new Error('PlaceAutocompleteElement indisponível');
    }
    if (!places?.AutocompleteSuggestion) {
      throw new Error('AutocompleteSuggestion indisponível');
    }

    const response = await places.AutocompleteSuggestion.fetchAutocompleteSuggestions({
      input: 'Praça da Alfândega Porto Alegre',
      includedRegionCodes: ['br'],
    });

    const suggestions = response?.suggestions ?? [];
    const prediction = suggestions.find((item) => item.placePrediction)?.placePrediction;
    if (!prediction) {
      throw new Error('Places API (New) não retornou sugestões');
    }

    const place = prediction.toPlace();
    await place.fetchFields({
      fields: ['formattedAddress', 'location', 'addressComponents'],
    });

    const latitude = typeof place.location?.lat === 'function' ? place.location.lat() : null;
    const longitude = typeof place.location?.lng === 'function' ? place.location.lng() : null;

    return {
      suggestionCount: suggestions.length,
      formattedAddress: place.formattedAddress ?? '',
      latitude,
      longitude,
      widgetMounted: Boolean(document.querySelector('gmp-place-autocomplete')),
    };
  });

  if (result.suggestionCount < 1) throw new Error('nenhuma sugestão retornada');
  if (!result.widgetMounted) throw new Error('PlaceAutocompleteElement não foi montado');
  if (!result.formattedAddress.toLowerCase().includes('porto alegre')) {
    throw new Error(`endereço inesperado: ${result.formattedAddress}`);
  }
  if (!Number.isFinite(result.latitude) || !Number.isFinite(result.longitude)) {
    throw new Error('Place Details não retornou coordenadas válidas');
  }

  console.log('google_maps_runtime=OK');
  console.log('places_new_runtime=OK');
  console.log(`places_suggestions=${result.suggestionCount}`);
  console.log('place_details=OK');
  console.log('places_widget=OK');

  const dims = await page.evaluate(() => ({
    viewport: document.documentElement.clientWidth,
    scrollWidth: document.documentElement.scrollWidth,
  }));
  if (dims.scrollWidth - dims.viewport > 2) throw new Error(`overflow mobile: ${dims.scrollWidth}/${dims.viewport}`);
  console.log('mobile_overflow=0');

  await page.screenshot({ path: '/work/maps-mobile.png', fullPage: true });
  console.log('PLAYWRIGHT_MAPS=OK');
} finally {
  await context.close();
  await browser.close();
}
JS

test -f "$QA_DIR/maps-mobile.png" || fail "screenshot Maps não gerada"
echo "screenshot=$QA_DIR/maps-mobile.png"

echo
echo "=== 15. CLEANUP ==="
cleanup
trap - EXIT
RESIDUAL="$(dbq "SELECT count(*) FROM occurrences WHERE external_protocol_number LIKE '${RUN_TAG}%';")"
echo "smoke_residual=$RESIDUAL"
test "$RESIDUAL" = "0" || fail "fixtures residuais no banco"

echo
echo "=== 16. MERCADO PAGO / ESTADO FINAL ==="
PROVIDER_AFTER="$(dbq "
  SELECT
    (SELECT count(*) FROM billing_provider_subscriptions)::text || '|' ||
    (SELECT count(*) FROM payments)::text || '|' ||
    (SELECT count(*) FROM payment_events)::text;
")"
FINAL_HEALTH="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/health/ready")"
FINAL_WEB="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/")"

echo "provider_after=$PROVIDER_AFTER"
echo "health=$FINAL_HEALTH"
echo "web=$FINAL_WEB"
echo "main_clean=$(test -z "$(git -C "$ROOT" status --porcelain)" && echo YES || echo NO)"

test "$PROVIDER_AFTER" = "$PROVIDER_BEFORE"
test "$FINAL_HEALTH" = "200"
test "$FINAL_WEB" = "200"
test -z "$(git -C "$ROOT" status --porcelain)"

echo
echo "============================================================"
echo "MAPS / POSTGIS — FEATURE HOMOLOG: OK"
echo "HEAD: $EXPECTED_HEAD"
echo "GOOGLE MAPS KEY: PRESENT"
echo "GOOGLE MAPS RUNTIME: OK"
echo "PLACES API NEW: OK"
echo "AUTOCOMPLETE SUGGESTIONS: OK"
echo "PLACE DETAILS: OK"
echo "MAP MOBILE: OK"
echo "POSTGIS: $POSTGIS_VERSION"
echo "GEOGRAPHY POINT: OK"
echo "GIST INDEX: OK"
echo "PERSISTED POINT: OK"
echo "RADIUS FILTER: OK"
echo "CITY FILTER: OK"
echo "GEO UNAUTH: 401"
echo "INVALID GEO: 400"
echo "SMOKE RESIDUAL: 0"
echo "WEB: 200"
echo "HEALTH: 200"
echo "MERCADO PAGO PROVIDER: $PROVIDER_AFTER"
echo "MAIN WORKTREE: CLEAN"
echo "SCREENSHOT: $QA_DIR/maps-mobile.png"
echo "============================================================"
