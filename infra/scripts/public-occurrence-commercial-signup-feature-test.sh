#!/usr/bin/env bash
set -Eeuo pipefail

EXPECTED_HEAD="${1:-}"
ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
ENV_FILE="${CIDADEMDIA_ENV_FILE:-$ROOT/.env}"
BASE="${CIDADEMDIA_BASE_URL:-https://homolog.cidademdia.com.br}"
WT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
QA_DIR="${CIDADEMDIA_QA_DIR:-$HOME/cidademdia-qa/commercial-signup-$(date +%Y%m%d-%H%M%S)}"
QA_SUFFIX="$(date +%s)-$$"
QA_EMAIL="qa-commercial-${QA_SUFFIX}@cidademdia.local"
QA_PASSWORD="QaCommercial#${QA_SUFFIX}!"
QA_NAME="QA Comercial ${QA_SUFFIX}"

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
    "psql -v ON_ERROR_STOP=1 -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" <<'SQL'
DO \$\$
DECLARE
  uid uuid;
BEGIN
  SELECT id INTO uid FROM users WHERE email = '${QA_EMAIL}';
  IF uid IS NOT NULL THEN
    DELETE FROM occurrences WHERE author_user_id = uid;
    DELETE FROM occurrence_media WHERE uploader_user_id = uid;
    DELETE FROM users WHERE id = uid;
  END IF;
END
\$\$;
SQL" >/dev/null 2>&1 || true
}
trap cleanup_qa_user EXIT

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

const png = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Z1xkAAAAASUVORK5CYII=',
  'base64',
);

const browser = await chromium.launch({
  executablePath: process.env.CHROME,
  headless: true,
  args: ['--no-sandbox', '--disable-dev-shm-usage'],
});

const authContext = await browser.newContext({ ignoreHTTPSErrors: true });

async function protectedRequest(method, url, token, options = {}) {
  return authContext.request.fetch(url, {
    method,
    ...options,
    headers: {
      ...(options.headers || {}),
      Authorization: `Bearer ${token}`,
    },
  });
}

async function createImage(token) {
  const request = await protectedRequest(
    'POST',
    `${process.env.BASE}/api/v1/occurrence-media/uploads`,
    token,
    { data: { fileName: 'qa-commercial.png', contentType: 'image/png', sizeBytes: png.length } },
  );
  if (request.status() !== 201) throw new Error(`solicitação de upload falhou: ${request.status()} ${await request.text()}`);

  const upload = await request.json();
  const binary = await fetch(upload.uploadUrl, {
    method: 'PUT',
    headers: { 'Content-Type': 'image/png' },
    body: png,
  });
  if (!binary.ok) throw new Error(`upload R2 falhou: ${binary.status}`);

  const confirm = await protectedRequest(
    'POST',
    `${process.env.BASE}/api/v1/occurrence-media/${upload.id}/confirm`,
    token,
  );
  if (confirm.status() !== 200) throw new Error(`confirmação de mídia falhou: ${confirm.status()} ${await confirm.text()}`);
  return confirm.json();
}

try {
  const register = await authContext.request.post(`${process.env.BASE}/api/v1/auth/register`, {
    data: {
      email: process.env.QA_EMAIL,
      password: process.env.QA_PASSWORD,
      displayName: process.env.QA_NAME,
    },
  });
  if (register.status() !== 201) throw new Error(`registro QA falhou: ${register.status()} ${await register.text()}`);
  const session = await register.json();
  const token = session.accessToken;
  if (!token) throw new Error('registro QA não retornou accessToken');

  const categoriesResponse = await protectedRequest('GET', `${process.env.BASE}/api/v1/occurrences/categories`, token);
  if (categoriesResponse.status() !== 200) throw new Error(`categorias falharam: ${categoriesResponse.status()}`);
  const category = (await categoriesResponse.json())[0];
  if (!category?.id) throw new Error('nenhuma categoria ativa disponível');

  const mastersResponse = await protectedRequest('GET', `${process.env.BASE}/api/v1/occurrences/masters`, token);
  if (mastersResponse.status() !== 200) throw new Error(`Masters falharam: ${mastersResponse.status()}`);
  const master = (await mastersResponse.json())[0];
  if (!master?.id) throw new Error('nenhuma conta Master elegível disponível');

  const media = await createImage(token);
  const title = `QA comercial ${Date.now()}`;
  const create = await protectedRequest(
    'POST',
    `${process.env.BASE}/api/v1/occurrences`,
    token,
    {
      data: {
        categoryId: category.id,
        masterUserId: master.id,
        title,
        description: 'Ocorrência QA para validar modal comercial de cadastro.',
        street: 'Praça da Sé',
        number: '1',
        neighborhood: 'Sé',
        city: 'São Paulo',
        latitude: -23.55052,
        longitude: -46.633308,
        postalCode: '01001000',
        cityId: null,
        stateCode: 'SP',
        externalProtocolNumber: `QA-COMERCIAL-${Date.now()}`,
        externalProtocolAgency: 'QA',
        mediaIds: [media.id],
      },
    },
  );
  if (create.status() !== 201) throw new Error(`criação ocorrência falhou: ${create.status()} ${await create.text()}`);
  const occurrence = await create.json();
  console.log(`commercial_signup_occurrence_created=OK code=${occurrence.publicCode}`);

  const anonymousContext = await browser.newContext({
    ignoreHTTPSErrors: true,
    viewport: { width: 1440, height: 1000 },
    permissions: [],
  });

  try {
    const page = await anonymousContext.newPage();
    await page.goto(`${process.env.BASE}/ocorrencias`, { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.locator('.public-occurrences').waitFor({ state: 'visible', timeout: 15000 });

    const card = page.locator(`.public-occurrences__card[data-occurrence-id="${occurrence.id}"]`);
    await card.waitFor({ state: 'visible', timeout: 20000 });

    const support = card.getByRole('button', { name: 'Entrar para apoiar ocorrência. 0 apoios', exact: true });
    await support.click();

    let commercialDialog = page.getByRole('dialog', { name: 'Crie sua conta gratuita para interagir' });
    await commercialDialog.waitFor({ state: 'visible', timeout: 5000 });
    await commercialDialog.getByRole('link', { name: 'CidadeEmDia', exact: true }).waitFor({ state: 'visible' });
    await commercialDialog.getByText('Publique ocorrências gratuitamente', { exact: true }).waitFor({ state: 'visible' });
    await commercialDialog.getByRole('button', { name: 'Cadastre-se', exact: true }).waitFor({ state: 'visible' });
    if (await page.getByRole('dialog', { name: title }).count() !== 0) {
      throw new Error('apoio anônimo abriu detalhes da ocorrência');
    }
    console.log('commercial_signup_brand_visible=OK');
    console.log('anonymous_support_commercial_modal=OK');

    await commercialDialog.getByRole('button', { name: 'Fechar convite para cadastro', exact: true }).click();
    await commercialDialog.waitFor({ state: 'hidden', timeout: 5000 });

    await card.getByRole('heading', { name: title, exact: true }).click();
    commercialDialog = page.getByRole('dialog', { name: 'Crie sua conta gratuita para interagir' });
    await commercialDialog.waitFor({ state: 'visible', timeout: 5000 });
    if (await page.getByRole('dialog', { name: title }).count() !== 0) {
      throw new Error('clique anônimo na ocorrência abriu detalhes');
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
    await page.getByRole('button', { name: 'Criar conta', exact: true }).waitFor({ state: 'visible' });
    console.log('commercial_signup_cta_registration_form=OK');

    await page.goto(process.env.BASE, { waitUntil: 'domcontentloaded', timeout: 30000 });
    const hero = page.locator('.public-home__hero');
    await hero.waitFor({ state: 'visible', timeout: 10000 });
    await page.getByRole('heading', { name: /Uma cidade melhor.*é ouvido.*pode resolver\./ }).waitFor({ state: 'visible' });
    await page.getByText(/O CIDADEMDIA conecta cidadãos e gestores, facilitando a comunicação e o acompanhamento das demandas,/).waitFor({ state: 'visible' });
    await page.getByRole('button', { name: 'Conheça os planos', exact: true }).waitFor({ state: 'visible' });
    await page.getByRole('button', { name: 'Como funciona', exact: true }).waitFor({ state: 'visible' });

    const benefitContent = await hero.evaluate((element) => getComputedStyle(element, '::after').content);
    for (const phrase of [
      'Apoie ocorrências da sua região',
      'Publique ocorrências gratuitamente',
      'Acompanhe detalhes e atualizações',
    ]) {
      if (!benefitContent.includes(phrase)) throw new Error(`benefício ausente do hero: ${phrase}; content=${benefitContent}`);
    }
    await hero.screenshot({ path: '/work/home-hero-benefits-desktop.png' });
    console.log('home_hero_original_content_visible=OK');
    console.log('home_hero_benefits_desktop=OK');

    await page.setViewportSize({ width: 390, height: 844 });
    await page.reload({ waitUntil: 'domcontentloaded', timeout: 30000 });
    const mobileHero = page.locator('.public-home__hero');
    await mobileHero.waitFor({ state: 'visible', timeout: 10000 });
    const mobileBenefitContent = await mobileHero.evaluate((element) => getComputedStyle(element, '::after').content);
    if (!mobileBenefitContent.includes('Publique ocorrências gratuitamente')) {
      throw new Error(`benefícios não renderizaram no hero mobile: ${mobileBenefitContent}`);
    }
    await mobileHero.screenshot({ path: '/work/home-hero-benefits-mobile.png' });
    console.log('home_hero_benefits_mobile=OK');

    await page.goto(`${process.env.BASE}/ocorrencias`, { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.locator(`.public-occurrences__card[data-occurrence-id="${occurrence.id}"]`).waitFor({ state: 'visible', timeout: 20000 });
    await page.locator(`.public-occurrences__card[data-occurrence-id="${occurrence.id}"]`).getByRole('heading', { name: title, exact: true }).click();
    const mobileDialog = page.getByRole('dialog', { name: 'Crie sua conta gratuita para interagir' });
    await mobileDialog.waitFor({ state: 'visible', timeout: 5000 });
    await mobileDialog.getByRole('link', { name: 'CidadeEmDia', exact: true }).waitFor({ state: 'visible' });
    const box = await mobileDialog.boundingBox();
    if (!box || box.width > 390 || box.height > 844) throw new Error(`modal comercial mobile fora da viewport: ${JSON.stringify(box)}`);
    await page.screenshot({ path: '/work/commercial-signup-mobile.png', fullPage: true });
    console.log('commercial_signup_mobile=OK');
  } finally {
    await anonymousContext.close();
  }
} finally {
  await authContext.close();
  await browser.close();
}
JS

test -f "$QA_DIR/commercial-signup-desktop.png" || fail "screenshot desktop ausente"
test -f "$QA_DIR/commercial-signup-mobile.png" || fail "screenshot mobile ausente"
test -f "$QA_DIR/home-hero-benefits-desktop.png" || fail "screenshot desktop do hero ausente"
test -f "$QA_DIR/home-hero-benefits-mobile.png" || fail "screenshot mobile do hero ausente"

cleanup_qa_user
QA_LEFT="$(compose exec -T db sh -lc "psql -At -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -c \"SELECT count(*) FROM users WHERE email = '${QA_EMAIL}';\"")"
test "$QA_LEFT" = "0" || fail "usuário QA não foi removido"
trap - EXIT

test -z "$(git -C "$ROOT" status --porcelain)" || fail "main worktree ficou suja"

echo "============================================================"
echo "PUBLIC OCCURRENCE COMMERCIAL SIGNUP — FEATURE HOMOLOG: OK"
echo "COMMERCIAL MODAL BRAND: OK"
echo "ANONYMOUS SUPPORT COMMERCIAL MODAL: OK"
echo "ANONYMOUS DETAILS COMMERCIAL MODAL: OK"
echo "DETAILS BLOCKED FOR ANONYMOUS: OK"
echo "CTA TO REGISTRATION FORM: OK"
echo "HERO ORIGINAL CONTENT: OK"
echo "HERO PARTICIPATION BENEFITS DESKTOP: OK"
echo "HERO PARTICIPATION BENEFITS MOBILE: OK"
echo "DESKTOP COMMERCIAL MODAL: OK"
echo "MOBILE COMMERCIAL MODAL: OK"
echo "QA CLEANUP: OK"
echo "MAIN WORKTREE: CLEAN"
echo "SCREENSHOTS: $QA_DIR/commercial-signup-desktop.png | $QA_DIR/commercial-signup-mobile.png | $QA_DIR/home-hero-benefits-desktop.png | $QA_DIR/home-hero-benefits-mobile.png"
echo "============================================================"
