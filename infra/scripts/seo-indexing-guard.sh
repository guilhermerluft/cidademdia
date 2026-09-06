#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WEB="$ROOT/apps/web"
SEO="$WEB/src/app/SeoMetadata.tsx"
MAIN="$WEB/src/main.tsx"
INDEX="$WEB/index.html"
ROBOTS="$WEB/public/robots.txt"
SITEMAP="$WEB/public/sitemap.xml"
NGINX="$ROOT/infra/nginx/cidademdia.conf"
CI="$ROOT/.github/workflows/ci.yml"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

for file in "$SEO" "$MAIN" "$INDEX" "$ROBOTS" "$SITEMAP" "$NGINX" "$CI"; do
  test -f "$file" || fail "arquivo SEO ausente: $file"
done

grep -q "const PRODUCTION_ORIGIN = 'https://cidademdia.com.br';" "$SEO" \
  || fail "origem canônica de produção não está centralizada"
grep -q "'cidademdia.com.br', 'www.cidademdia.com.br'" "$SEO" \
  || fail "hosts públicos de produção não estão protegidos"
grep -q "'/ocorrencias'" "$SEO" || fail "SEO de /ocorrencias ausente"
grep -q "'/representantes'" "$SEO" || fail "SEO de /representantes ausente"
grep -q "'/planos'" "$SEO" || fail "SEO de /planos ausente"
grep -q "'/como-funciona'" "$SEO" || fail "SEO de /como-funciona ausente"
grep -q "noindex, nofollow" "$SEO" || fail "fallback noindex para rotas privadas/ambientes não produtivos ausente"
grep -q "application/ld+json" "$SEO" || fail "dados estruturados JSON-LD ausentes"
grep -q "WebSite" "$SEO" || fail "schema WebSite ausente"
grep -q "WebPage" "$SEO" || fail "schema WebPage ausente"

grep -q "import { SeoMetadata } from './app/SeoMetadata';" "$MAIN" \
  || fail "SeoMetadata não está importado"
grep -q '<SeoMetadata />' "$MAIN" \
  || fail "SeoMetadata não está montado no router"

grep -q 'name="description"' "$INDEX" || fail "description base ausente do HTML"
grep -q 'name="robots"' "$INDEX" || fail "robots meta base ausente do HTML"
grep -q 'rel="canonical" href="https://cidademdia.com.br"' "$INDEX" \
  || fail "canonical base ausente do HTML"
grep -q 'property="og:title"' "$INDEX" || fail "Open Graph base ausente"
grep -q 'name="twitter:card"' "$INDEX" || fail "Twitter Card base ausente"
grep -q 'id="seo-structured-data"' "$INDEX" || fail "JSON-LD base ausente"
! grep -qi 'name="keywords"' "$INDEX" || fail "meta keywords obsoleta não deve ser usada"

grep -q '^User-agent: \*$' "$ROBOTS" || fail "robots.txt sem regra global"
grep -q '^Disallow: /api/$' "$ROBOTS" || fail "robots.txt não protege API"
grep -q '^Sitemap: https://cidademdia.com.br/sitemap.xml$' "$ROBOTS" \
  || fail "robots.txt não referencia sitemap de produção"

for url in \
  'https://cidademdia.com.br/' \
  'https://cidademdia.com.br/como-funciona' \
  'https://cidademdia.com.br/ocorrencias' \
  'https://cidademdia.com.br/representantes' \
  'https://cidademdia.com.br/planos'; do
  grep -Fq "<loc>$url</loc>" "$SITEMAP" || fail "URL pública ausente do sitemap: $url"
done

! grep -Fq '<loc>https://cidademdia.com.br/admin</loc>' "$SITEMAP" || fail "admin não pode entrar no sitemap"
! grep -Fq '<loc>https://cidademdia.com.br/painel</loc>' "$SITEMAP" || fail "painel não pode entrar no sitemap"
! grep -Fq '<loc>https://cidademdia.com.br/perfil</loc>' "$SITEMAP" || fail "perfil não pode entrar no sitemap"

grep -q 'homolog.cidademdia.com.br "noindex, nofollow, noarchive";' "$NGINX" \
  || fail "HML não recebe X-Robots-Tag noindex"
grep -q 'add_header X-Robots-Tag' "$NGINX" \
  || fail "header X-Robots-Tag não está aplicado"

grep -q 'SEO indexing guard' "$CI" \
  || fail "guard SEO não está ligado ao CI"

echo "seo_public_metadata=OK"
echo "seo_canonical=OK"
echo "seo_open_graph=OK"
echo "seo_twitter_card=OK"
echo "seo_structured_data=OK"
echo "seo_private_noindex=OK"
echo "seo_homolog_noindex=OK"
echo "seo_robots=OK"
echo "seo_sitemap=OK"
echo "SEO INDEXING GUARD: OK"
