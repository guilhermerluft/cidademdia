#!/usr/bin/env bash
set -Eeuo pipefail

EXPECTED_HEAD="${1:-}"
ROOT="${CIDADEMDIA_ROOT:-/opt/cidademdia}"
ENV_FILE="${CIDADEMDIA_ENV_FILE:-$ROOT/.env}"
BASE="${CIDADEMDIA_BASE_URL:-https://homolog.cidademdia.com.br}"
WT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
QA_DIR="${CIDADEMDIA_QA_DIR:-$HOME/cidademdia-qa/profile-representatives-$(date +%Y%m%d-%H%M%S)}"
QA_SUFFIX="$(date +%s)-$$"
QA_EMAIL="qa-profile-${QA_SUFFIX}@cidademdia.local"
QA_PASSWORD="QaProfile#${QA_SUFFIX}!"
QA_NAME="QA Perfil ${QA_SUFFIX}"

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
echo "PROFILE + REPRESENTATIVES — FEATURE HOMOLOG"
echo "============================================================"
echo "HEAD: $EXPECTED_HEAD"
echo "MAIN: $(git -C "$ROOT" rev-parse HEAD)"

echo
echo "=== 1. ARQUITETURA ==="
bash "$WT/infra/scripts/frontend-architecture-test.sh"
grep -q 'href="/perfil"' "$WT/apps/web/src/app/layout/AppHeader.tsx" || fail "Perfil ausente do dropdown"
grep -q "href: '/representantes'" "$WT/apps/web/src/app/layout/AppNavigation.tsx" || fail "Representantes ausente da navegação"
! grep -q 'InstitutionDirectory' "$WT/apps/web/src/modules/home/HomeAccountModules.tsx" || fail "diretório institucional continua na Home"
! grep -q 'id="perfil"' "$WT/apps/web/src/modules/home/HomeAccountModules.tsx" || fail "perfil continua na Home"
echo "profile_dropdown_architecture=OK"
echo "representatives_navigation_architecture=OK"
echo "home_separation_architecture=OK"

echo
echo "=== 2. BUILD / DEPLOY FEATURE ==="
compose build web
compose up -d --no-deps web
compose restart nginx

READY=0
for _ in $(seq 1 40); do
  HEALTH="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/health/ready" || true)"
  HOME_CODE="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/" || true)"
  REPS_CODE="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/representantes" || true)"
  PROFILE_CODE="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/perfil" || true)"
  if [ "$HEALTH" = "200" ] && [ "$HOME_CODE" = "200" ] && [ "$REPS_CODE" = "200" ] && [ "$PROFILE_CODE" = "200" ]; then
    READY=1
    break
  fi
  sleep 2
done

test "$READY" = "1" || fail "health/home/representantes/perfil não ficaram prontos"
echo "health=200"
echo "home=200"
echo "representatives_shell=200"
echo "profile_shell=200"

echo
echo "=== 3. BROWSER — REPRESENTANTES / PERFIL ==="
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

try {
  await page.goto(`${process.env.BASE}/representantes`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.getByRole('heading', { name: 'Instituições e representantes', exact: true }).waitFor({ state: 'visible', timeout: 15000 });
  const repsLink = page.locator('.app-header__nav a[href="/representantes"]');
  await repsLink.waitFor({ state: 'visible', timeout: 15000 });
  if (await repsLink.getAttribute('aria-current') !== 'page') {
    throw new Error('Representantes não está ativo no header');
  }
  const institutionsResponse = await page.waitForResponse(
    response => response.url().includes('/api/v1/institutions') && response.request().method() === 'GET',
    { timeout: 15000 },
  ).catch(() => null);
  if (institutionsResponse && institutionsResponse.status() !== 200) {
    throw new Error(`diretório público retornou ${institutionsResponse.status()}`);
  }
  console.log('public_representatives=OK');

  const anonymousProfile = await context.newPage();
  await anonymousProfile.goto(`${process.env.BASE}/perfil`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await anonymousProfile.waitForURL(url => url.pathname === '/', { timeout: 15000 });
  await anonymousProfile.close();
  console.log('anonymous_profile_redirect=OK');

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
  await page.getByRole('button', { name: new RegExp(`Abrir menu de ${process.env.QA_NAME}`) }).waitFor({ state: 'visible', timeout: 15000 });

  if (await page.getByRole('heading', { name: 'Instituições e representantes', exact: true }).count()) {
    throw new Error('Instituições e representantes continua renderizado na Home autenticada');
  }
  if (await page.locator('#perfil').count()) {
    throw new Error('informações do perfil continuam renderizadas na Home autenticada');
  }
  console.log('home_profile_and_representatives_removed=OK');

  const account = page.getByRole('button', { name: new RegExp(`Abrir menu de ${process.env.QA_NAME}`) });
  await account.hover();
  const profileItem = page.getByRole('menuitem', { name: /Perfil/ });
  await profileItem.waitFor({ state: 'visible', timeout: 5000 });
  if (await profileItem.getAttribute('href') !== '/perfil') {
    throw new Error('item Perfil não aponta para /perfil');
  }
  console.log('profile_dropdown=OK');

  await profileItem.click();
  await page.waitForURL(url => url.pathname === '/perfil', { timeout: 15000 });
  await page.getByRole('heading', { name: 'Meu perfil', exact: true }).waitFor({ state: 'visible', timeout: 15000 });
  await page.getByText(process.env.QA_EMAIL, { exact: true }).first().waitFor({ state: 'visible', timeout: 15000 });
  await page.getByText(process.env.QA_NAME, { exact: true }).first().waitFor({ state: 'visible', timeout: 15000 });
  console.log('authenticated_profile=OK');

  await page.screenshot({ path: '/work/profile-desktop.png', fullPage: true });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`${process.env.BASE}/representantes`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  const mobileReps = page.locator('.app-bottom-nav a[href="/representantes"]');
  await mobileReps.waitFor({ state: 'visible', timeout: 15000 });
  if (await mobileReps.getAttribute('aria-current') !== 'page') {
    throw new Error('Representantes não está ativo na navegação mobile');
  }
  console.log('representatives_mobile=OK');

  if (errors.length) throw new Error(`pageerror: ${errors.join(' | ')}`);
} finally {
  await context.close();
  await browser.close();
}
JS

test -f "$QA_DIR/profile-desktop.png" || fail "screenshot do perfil ausente"

echo
echo "=== 4. LIMPEZA / ESTADO FINAL ==="
cleanup_qa_user
QA_LEFT="$(compose exec -T db sh -lc "psql -At -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -c \"SELECT count(*) FROM users WHERE email = '${QA_EMAIL}';\"")"
test "$QA_LEFT" = "0" || fail "usuário QA não foi removido"
trap - EXIT

test -z "$(git -C "$ROOT" status --porcelain)" || fail "main worktree ficou suja"
echo "qa_cleanup=OK"
echo "============================================================"
echo "PROFILE + REPRESENTATIVES — FEATURE HOMOLOG: OK"
echo "PROTECTED /perfil: OK"
echo "PROFILE ONLY IN DROPDOWN: OK"
echo "PROFILE REMOVED FROM HOME: OK"
echo "PUBLIC /representantes: OK"
echo "REPRESENTATIVES IN SHARED HEADER: OK"
echo "REPRESENTATIVES REMOVED FROM HOME: OK"
echo "MOBILE NAVIGATION: OK"
echo "QA CLEANUP: OK"
echo "MAIN WORKTREE: CLEAN"
echo "SCREENSHOT: $QA_DIR/profile-desktop.png"
echo "============================================================"
