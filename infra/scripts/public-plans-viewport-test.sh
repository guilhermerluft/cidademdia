#!/usr/bin/env bash
set -Eeuo pipefail

BASE="${CIDADEMDIA_BASE_URL:-https://homolog.cidademdia.com.br}"
QA_DIR="${CIDADEMDIA_QA_DIR:-$HOME/cidademdia-qa/plans-viewport-$(date +%Y%m%d-%H%M%S)}"

mkdir -p "$QA_DIR"

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

const viewports = [
  { width: 1600, height: 900, name: '1600x900' },
  { width: 1440, height: 900, name: '1440x900' },
  { width: 1366, height: 768, name: '1366x768' },
];

try {
  for (const viewport of viewports) {
    const context = await browser.newContext({
      ignoreHTTPSErrors: true,
      viewport: { width: viewport.width, height: viewport.height },
    });
    const page = await context.newPage();
    const pageErrors = [];
    page.on('pageerror', error => pageErrors.push(error.message));

    await page.goto(`${process.env.BASE}/planos`, { waitUntil: 'domcontentloaded', timeout: 30000 });
    await page.locator('.plans-page__plans-grid').waitFor({ state: 'visible', timeout: 15000 });

    const result = await page.evaluate(() => {
      const root = document.documentElement;
      const body = document.body;
      const viewportHeight = root.clientHeight;
      const viewportWidth = root.clientWidth;
      const selectors = [
        '#plans-page-title',
        '.plans-page__benefits',
        '.plans-page__plans-grid',
        '.plans-page__mega-card',
        '.plans-page__mega-benefits',
        '.plans-page__complementary',
      ];

      const boxes = Object.fromEntries(selectors.map(selector => {
        const element = document.querySelector(selector);
        if (!element) return [selector, null];
        const rect = element.getBoundingClientRect();
        return [selector, { top: rect.top, right: rect.right, bottom: rect.bottom, left: rect.left }];
      }));

      const chooseButtons = [...document.querySelectorAll('.plans-page__choose-button')]
        .map(element => element.getBoundingClientRect())
        .map(rect => ({ top: rect.top, bottom: rect.bottom, left: rect.left, right: rect.right }));

      return {
        viewportHeight,
        viewportWidth,
        documentScrollHeight: root.scrollHeight,
        bodyScrollHeight: body.scrollHeight,
        documentScrollWidth: root.scrollWidth,
        boxes,
        chooseButtons,
      };
    });

    if (result.documentScrollHeight > result.viewportHeight + 2 || result.bodyScrollHeight > result.viewportHeight + 2) {
      throw new Error(`${viewport.name}: página exige scroll vertical (${result.documentScrollHeight}/${result.viewportHeight})`);
    }

    if (result.documentScrollWidth > result.viewportWidth + 2) {
      throw new Error(`${viewport.name}: página exige scroll horizontal (${result.documentScrollWidth}/${result.viewportWidth})`);
    }

    for (const [selector, box] of Object.entries(result.boxes)) {
      if (!box) throw new Error(`${viewport.name}: elemento ausente ${selector}`);
      if (box.top < -1 || box.bottom > result.viewportHeight + 1 || box.left < -1 || box.right > result.viewportWidth + 1) {
        throw new Error(`${viewport.name}: elemento fora da viewport ${selector} (${JSON.stringify(box)})`);
      }
    }

    if (result.chooseButtons.length !== 12) {
      throw new Error(`${viewport.name}: esperado 12 botões de escolha, recebeu ${result.chooseButtons.length}`);
    }

    for (const [index, box] of result.chooseButtons.entries()) {
      if (box.top < -1 || box.bottom > result.viewportHeight + 1) {
        throw new Error(`${viewport.name}: botão de plano ${index + 1} ficou cortado`);
      }
    }

    if (pageErrors.length) {
      throw new Error(`${viewport.name}: pageerror: ${pageErrors.join(' | ')}`);
    }

    await page.screenshot({ path: `/work/plans-${viewport.name}.png`, fullPage: false });
    console.log(`${viewport.name}=OK`);
    await context.close();
  }
} finally {
  await browser.close();
}
JS

echo "PUBLIC PLANS SINGLE VIEWPORT: OK"
echo "SCREENSHOTS: $QA_DIR"
