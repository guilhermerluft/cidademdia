#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ROOT="$ROOT" node --input-type=module <<'NODE'
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const root = process.env.ROOT;
const home = readFileSync(join(root, 'apps/web/src/modules/home/PublicHome.tsx'), 'utf8');
const css = readFileSync(join(root, 'apps/web/src/modules/home/public-footer-contacts.css'), 'utf8');
const footer = home.match(/<footer className="public-home__footer">([\s\S]*?)<\/footer>/)?.[1];
assert.ok(footer, 'O rodapé público deve continuar disponível.');
assert.match(home, /import ['"]\.\/public-footer-contacts\.css['"];?/);
assert.match(footer, /<Brand compact \/>/);
assert.match(footer, /onLogin/);
assert.match(footer, /onRegister/);
assert.match(footer, /<section[^>]*aria-labelledby="public-home-contact-title"/);
assert.match(footer, /<h2 id="public-home-contact-title">Contato<\/h2>/);

const contacts = [
  ['Atendimento', 'atendimento@cidademdia.com.br'],
  ['Ouvidoria', 'ouvidoria@cidademdia.com.br'],
  ['Comercial', 'comercial@cidademdia.com.br'],
];

for (const [label, email] of contacts) {
  const exact = `<a href="mailto:${email}">${email}</a>`;
  assert.equal(footer.split(exact).length - 1, 1, `Link ausente ou duplicado: ${email}`);
  assert.match(footer, new RegExp(`<span>${label}<\\/span>\\s*${exact.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`));
}
assert.equal((footer.match(/href="mailto:/g) ?? []).length, 3);
assert.match(css, /overflow-wrap:\s*anywhere/);
assert.match(css, /@media\s*\(max-width:\s*720px\)/);
assert.match(css, /grid-template-columns:\s*minmax\(0,\s*1fr\)/);
assert.match(css, /:focus-visible/);
console.log('PUBLIC FOOTER CONTACTS GUARD: OK');
NODE
