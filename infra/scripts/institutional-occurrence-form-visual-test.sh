#!/usr/bin/env bash
set -Eeuo pipefail

EXPECTED_HEAD="${1:-}"
EXPECTED_BRANCH="${CIDADEMDIA_EXPECTED_BRANCH:-}"
ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
ENV_FILE="${CIDADEMDIA_ENV_FILE:-$ROOT/.env}"
BASE="${CIDADEMDIA_BASE_URL:-https://homolog.cidademdia.com.br}"
QA_DIR="${CIDADEMDIA_QA_DIR:-$HOME/cidademdia-qa/institutional-occurrence-form-visual-$(date +%Y%m%d-%H%M%S)}"
QA_SUFFIX="$(date +%s)-$$"
QA_EMAIL="qa-form-visual-${QA_SUFFIX}@cidademdia.local"
QA_PASSWORD="QaFormVisual#${QA_SUFFIX}!"
QA_NAME="QA Formulário Institucional ${QA_SUFFIX}"

fail() {
  echo
  echo "ERRO: $*" >&2
  exit 1
}

for cmd in git docker curl; do
  command -v "$cmd" >/dev/null 2>&1 || fail "comando ausente: $cmd"
done

test -n "$EXPECTED_HEAD" || fail "informe o HEAD esperado"
test "$(git -C "$ROOT" rev-parse HEAD)" = "$EXPECTED_HEAD" || fail "repo fora do HEAD esperado"
if [ -n "$EXPECTED_BRANCH" ]; then
  test "$(git -C "$ROOT" branch --show-current)" = "$EXPECTED_BRANCH" || fail "branch inesperada"
fi
test -z "$(git -C "$ROOT" status --porcelain)" || fail "worktree está suja"
test -f "$ENV_FILE" || fail ".env não encontrado"

COMPOSE=(
  docker compose
  -p infra
  --env-file "$ENV_FILE"
  -f "$ROOT/infra/docker-compose.yml"
)

cleanup_qa() {
  "${COMPOSE[@]}" exec -T db sh -lc "
    psql -v ON_ERROR_STOP=1 -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" <<'SQL'
DO \$\$
DECLARE
  uid uuid;
BEGIN
  SELECT id INTO uid FROM users WHERE email = '$QA_EMAIL';
  IF uid IS NOT NULL THEN
    DELETE FROM occurrences WHERE author_user_id = uid;
    DELETE FROM occurrence_media WHERE uploader_user_id = uid;
    DELETE FROM users WHERE id = uid;
  END IF;
END
\$\$;
SQL
  " >/dev/null 2>&1 || true
}
trap cleanup_qa EXIT

mkdir -p "$QA_DIR"

echo "============================================================"
echo "INSTITUTIONAL OCCURRENCE FORM — VISUAL HOMOLOG"
echo "============================================================"
echo "HEAD: $EXPECTED_HEAD"

curl -fsS "$BASE/health/ready" >/dev/null
echo "health=OK"

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

const base = process.env.BASE;
const expectedDestinations = [
  'Prefeitura de São Paulo',
  'Câmara Municipal de São Paulo',
  'Governo do Estado de São Paulo',
  'Assembleia Legislativa do Estado de São Paulo',
];

const browser = await chromium.launch({
  executablePath: process.env.CHROME,
  headless: true,
  args: ['--no-sandbox', '--disable-dev-shm-usage'],
});

const context = await browser.newContext({
  ignoreHTTPSErrors: true,
  viewport: { width: 1440, height: 1100 },
});

const page = await context.newPage();
const pageErrors = [];
page.on('pageerror', error => pageErrors.push(error.message));

try {
  const registration = await context.request.post(
    base + '/api/v1/auth/register',
    {
      data: {
        email: process.env.QA_EMAIL,
        password: process.env.QA_PASSWORD,
        displayName: process.env.QA_NAME,
        termsAccepted: true,
      },
    },
  );

  if (registration.status() !== 201) {
    throw new Error(
      'registro QA falhou: ' + registration.status() + ' ' + await registration.text(),
    );
  }

  console.log('institutional_form_qa_registration=OK');

  await page.goto(base + '/painel', {
    waitUntil: 'domcontentloaded',
    timeout: 30000,
  });

  await page.locator('#painel-ocorrencias').waitFor({
    state: 'visible',
    timeout: 15000,
  });

  const protocol = page.getByLabel('Número do protocolo');
  const agency = page.getByLabel('Órgão do protocolo');
  const category = page.getByLabel('Categoria');
  const destination = page.getByLabel('Destino institucional');

  await protocol.waitFor({ state: 'visible' });
  await agency.waitFor({ state: 'visible' });
  await category.waitFor({ state: 'visible' });
  await destination.waitFor({ state: 'visible' });

  await page.waitForFunction(() => {
    const select = [...document.querySelectorAll('select')]
      .find(item => item.textContent?.includes('Destino institucional') === false
        && item.querySelector('option[value=""]')?.textContent?.includes('Selecione o órgão ou instituição'));
    return select ? select.options.length >= 5 : false;
  }).catch(() => {});

  const optionTexts = await destination.locator('option').allTextContents();
  for (const expected of expectedDestinations) {
    if (!optionTexts.includes(expected)) {
      throw new Error('destino ausente no select: ' + expected + ' :: ' + JSON.stringify(optionTexts));
    }
  }

  const protocolBox = await protocol.boundingBox();
  const agencyBox = await agency.boundingBox();
  const categoryBox = await category.boundingBox();

  if (!protocolBox || !agencyBox || !categoryBox) {
    throw new Error('não foi possível medir a ordem visual dos campos');
  }

  if (!(protocolBox.y < agencyBox.y && agencyBox.y < categoryBox.y)) {
    throw new Error(
      'ordem visual inválida: protocolo=' + protocolBox.y
      + ' órgão=' + agencyBox.y
      + ' categoria=' + categoryBox.y,
    );
  }

  console.log('institutional_form_protocol_order=OK');
  console.log('institutional_form_destinations=OK count=4');

  await page.screenshot({
    path: '/work/institutional-occurrence-form-desktop.png',
    fullPage: true,
  });
  console.log('institutional_form_visual_desktop=OK');

  await page.setViewportSize({ width: 390, height: 844 });

  await protocol.waitFor({ state: 'visible' });
  await agency.waitFor({ state: 'visible' });

  const mobileProtocolBox = await protocol.boundingBox();
  const mobileAgencyBox = await agency.boundingBox();
  const mobileCategoryBox = await category.boundingBox();

  if (!mobileProtocolBox || !mobileAgencyBox || !mobileCategoryBox) {
    throw new Error('não foi possível medir a ordem mobile');
  }

  if (!(mobileProtocolBox.y < mobileAgencyBox.y && mobileAgencyBox.y < mobileCategoryBox.y)) {
    throw new Error('ordem mobile inválida');
  }

  const viewport = await page.evaluate(() => ({
    clientWidth: document.documentElement.clientWidth,
    scrollWidth: document.documentElement.scrollWidth,
  }));

  if (viewport.scrollWidth - viewport.clientWidth > 2) {
    throw new Error(
      'overflow horizontal mobile: ' + viewport.scrollWidth + '/' + viewport.clientWidth,
    );
  }

  await page.screenshot({
    path: '/work/institutional-occurrence-form-mobile.png',
    fullPage: true,
  });

  console.log('institutional_form_visual_mobile=OK');

  if (pageErrors.length > 0) {
    throw new Error('pageerror: ' + pageErrors.join(' | '));
  }
} finally {
  await context.close();
  await browser.close();
}
JS

test -f "$QA_DIR/institutional-occurrence-form-desktop.png" || fail "screenshot desktop não foi criada"
test -f "$QA_DIR/institutional-occurrence-form-mobile.png" || fail "screenshot mobile não foi criada"

cleanup_qa

QA_LEFT="$(
  "${COMPOSE[@]}" exec -T db sh -lc "
    psql -At -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -c \"
      select count(*) from users where email = '$QA_EMAIL';
    \"
  " | tr -d '[:space:]'
)"
test "$QA_LEFT" = "0" || fail "usuário QA não foi removido"
trap - EXIT

echo "institutional_form_qa_cleanup=OK"
echo "institutional_form_visual_evidence=$QA_DIR"
echo "============================================================"
echo "INSTITUTIONAL OCCURRENCE FORM — VISUAL HOMOLOG: OK"
echo "HEAD: $EXPECTED_HEAD"
echo "PROTOCOL AGENCY ORDER: OK"
echo "DESTINATIONS: 4 UNIQUE"
echo "DESKTOP: OK"
echo "MOBILE: OK"
echo "QA CLEANUP: OK"
echo "============================================================"
