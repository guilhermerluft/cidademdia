#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WEB="$ROOT/apps/web"
SEO="$WEB/src/app/SeoMetadata.tsx"
MAIN="$WEB/src/main.tsx"
NAV="$WEB/src/app/layout/AppNavigation.tsx"
HOME="$WEB/src/modules/home/PublicHome.tsx"
FOOTER_CSS="$WEB/src/modules/home/public-footer-contacts.css"
INDEX="$WEB/index.html"
PACKAGE="$WEB/package.json"
STATIC_GENERATOR="$WEB/scripts/generate-seo-pages.mjs"
ABOUT_ROUTE="$WEB/src/modules/about/AboutRoute.tsx"
ABOUT_CSS="$WEB/src/modules/about/about.css"
WEB_NGINX="$WEB/nginx.conf"
FAVICON="$WEB/public/favicon-96.png"
ROBOTS="$WEB/public/robots.txt"
SITEMAP="$WEB/public/sitemap.xml"
NGINX="$ROOT/infra/nginx/cidademdia.conf"
CI="$ROOT/.github/workflows/ci.yml"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

for file in \
  "$SEO" "$MAIN" "$NAV" "$HOME" "$FOOTER_CSS" "$INDEX" "$PACKAGE" "$STATIC_GENERATOR" \
  "$ABOUT_ROUTE" "$ABOUT_CSS" "$WEB_NGINX" "$FAVICON" "$ROBOTS" "$SITEMAP" "$NGINX" "$CI"; do
  test -f "$file" || fail "arquivo SEO ausente: $file"
done

test -s "$FAVICON" || fail "favicon PNG está vazio"

grep -q "const PRODUCTION_ORIGIN = 'https://cidademdia.com.br';" "$SEO" \
  || fail "origem canônica de produção não está centralizada"
grep -q "'cidademdia.com.br', 'www.cidademdia.com.br'" "$SEO" \
  || fail "hosts públicos de produção não estão protegidos"
grep -q "'/ocorrencias'" "$SEO" || fail "SEO de /ocorrencias ausente"
grep -q "'/representantes'" "$SEO" || fail "SEO de /representantes ausente"
grep -q "'/planos'" "$SEO" || fail "SEO de /planos ausente"
grep -q "'/como-funciona'" "$SEO" || fail "SEO de /como-funciona ausente"
grep -q "'/sobre'" "$SEO" || fail "SEO de /sobre ausente"
grep -q "noindex, nofollow" "$SEO" || fail "fallback noindex para rotas privadas/ambientes não produtivos ausente"
grep -q "application/ld+json" "$SEO" || fail "dados estruturados JSON-LD ausentes"
grep -q "Organization" "$SEO" || fail "schema Organization ausente"
grep -q "contactPoint" "$SEO" || fail "canais oficiais ausentes do schema Organization"
grep -q "favicon.svg" "$SEO" || fail "logo oficial ausente do schema Organization"
grep -q "WebSite" "$SEO" || fail "schema WebSite ausente"
grep -q "WebPage" "$SEO" || fail "schema WebPage ausente"
grep -q "AboutPage" "$SEO" || fail "schema AboutPage ausente"
grep -q "BreadcrumbList" "$SEO" || fail "breadcrumbs estruturados ausentes"
grep -Fq 'Cidademdia' "$SEO" || fail "marca Cidademdia ausente dos metadados dinâmicos"
! grep -Fq 'CidadeEmDia' "$SEO" || fail "grafia antiga da marca presente nos metadados dinâmicos"

grep -q "import { SeoMetadata } from './app/SeoMetadata';" "$MAIN" \
  || fail "SeoMetadata não está importado"
grep -q '<SeoMetadata />' "$MAIN" \
  || fail "SeoMetadata não está montado no router"
grep -q 'path="/sobre" element={<AboutRoute />}' "$MAIN" \
  || fail "rota pública /sobre não está registrada"

! grep -Fq "href: '/sobre'" "$NAV" \
  || fail "Sobre não pode aparecer na navegação principal/mobile"
grep -Fq '<a className="public-home__footer-about-link" href="/sobre">Sobre o Cidademdia</a>' "$HOME" \
  || fail "Sobre precisa estar acessível pelo rodapé público"
grep -Fq '.public-home__footer-about-link' "$FOOTER_CSS" \
  || fail "link Sobre do rodapé não possui estilo próprio"

grep -q 'name="description"' "$INDEX" || fail "description base ausente do HTML"
grep -q 'name="robots"' "$INDEX" || fail "robots meta base ausente do HTML"
grep -q 'rel="canonical" href="https://cidademdia.com.br/"' "$INDEX" \
  || fail "canonical base ausente do HTML"
grep -q 'hreflang="pt-BR" href="https://cidademdia.com.br/"' "$INDEX" \
  || fail "hreflang pt-BR base ausente do HTML"
grep -q 'rel="icon" type="image/png" sizes="96x96" href="/favicon-96.png"' "$INDEX" \
  || fail "favicon PNG compatível com Google Search não está declarado"
grep -q 'property="og:title"' "$INDEX" || fail "Open Graph base ausente"
grep -q 'name="twitter:card"' "$INDEX" || fail "Twitter Card base ausente"
grep -q 'id="seo-structured-data"' "$INDEX" || fail "JSON-LD base ausente"
grep -q 'SEO_STATIC_CONTENT_START' "$INDEX" || fail "fallback HTML indexável ausente"
grep -q '<h1>Cidademdia: participação cidadã e ocorrências urbanas</h1>' "$INDEX" \
  || fail "H1 semântico da marca ausente do HTML inicial"
grep -Fq '<footer><a href="/sobre">Sobre o Cidademdia</a>' "$INDEX" \
  || fail "fallback estático não mantém Sobre no rodapé"
! grep -Fq '<a href="/sobre">Sobre</a>' "$INDEX" \
  || fail "fallback estático expõe Sobre na navegação em vez do rodapé"
grep -Fq 'Cidademdia' "$INDEX" || fail "marca Cidademdia ausente do HTML base"
! grep -Fq 'CidadeEmDia' "$INDEX" || fail "grafia antiga da marca presente no HTML base"
! grep -qi 'name="keywords"' "$INDEX" || fail "meta keywords obsoleta não deve ser usada"

grep -q 'generate-seo-pages.mjs' "$PACKAGE" || fail "renderização estática SEO não está ligada ao build"
for route_file in 'como-funciona.html' 'ocorrencias.html' 'representantes.html' 'planos.html' 'sobre.html'; do
  grep -Fq "file: '$route_file'" "$STATIC_GENERATOR" || fail "arquivo estático não configurado: $route_file"
done
grep -Fq '<footer><a href="/sobre">Sobre o Cidademdia</a>' "$STATIC_GENERATOR" \
  || fail "gerador estático não posiciona Sobre no rodapé"
! grep -Fq '<a href="/sobre">Sobre</a>' "$STATIC_GENERATOR" \
  || fail "gerador estático expõe Sobre na navegação"
grep -q 'SEO STATIC RENDER: OK' "$STATIC_GENERATOR" || fail "gerador estático não possui verificação final"

grep -q 'try_files $uri $uri.html /index.html;' "$WEB_NGINX" \
  || fail "Nginx web não serve HTML estático por rota"
grep -q 'location = /sobre/' "$WEB_NGINX" || fail "redirect canônico de /sobre/ ausente"

grep -q '<h1 id="about-page-title">' "$ABOUT_ROUTE" \
  || fail "página Sobre não possui H1 forte da marca"
grep -Fq 'about-page__hero-title-white' "$ABOUT_ROUTE" || fail "título Sobre sem trecho branco"
grep -Fq 'about-page__hero-title-green' "$ABOUT_ROUTE" || fail "título Sobre sem trecho verde"
grep -Fq 'about-page__hero-title-blue' "$ABOUT_ROUTE" || fail "título Sobre sem trecho azul"
grep -Fq 'about-page__hero-title-lime' "$ABOUT_ROUTE" || fail "título Sobre sem trecho verde-lima"
grep -Fq 'Cidademdia:' "$ABOUT_ROUTE" || fail "marca ausente do H1 de Sobre"
grep -Fq 'participação cidadã' "$ABOUT_ROUTE" || fail "participação cidadã ausente do H1 de Sobre"
grep -Fq 'quem pode resolver' "$ABOUT_ROUTE" || fail "mensagem final ausente do H1 de Sobre"
grep -Fq '.about-page__hero-title-line' "$ABOUT_CSS" || fail "layout do título hero de Sobre ausente"
grep -Fq '.about-page__hero-title-green' "$ABOUT_CSS" || fail "cor verde do título de Sobre ausente"
grep -Fq '.about-page__hero-title-blue' "$ABOUT_CSS" || fail "cor azul do título de Sobre ausente"
grep -Fq '.about-page__hero-title-lime' "$ABOUT_CSS" || fail "cor verde-lima do título de Sobre ausente"
grep -q 'atendimento@cidademdia.com.br' "$ABOUT_ROUTE" || fail "contato de atendimento ausente de /sobre"
grep -q 'ouvidoria@cidademdia.com.br' "$ABOUT_ROUTE" || fail "contato de ouvidoria ausente de /sobre"
grep -q 'comercial@cidademdia.com.br' "$ABOUT_ROUTE" || fail "contato comercial ausente de /sobre"

grep -q '^User-agent: \*$' "$ROBOTS" || fail "robots.txt sem regra global"
grep -q '^Disallow: /api/$' "$ROBOTS" || fail "robots.txt não protege API"
grep -q '^Sitemap: https://cidademdia.com.br/sitemap.xml$' "$ROBOTS" \
  || fail "robots.txt não referencia sitemap de produção"
! grep -q '^Disallow: /favicon' "$ROBOTS" || fail "favicon não pode ser bloqueado para crawlers"

for url in \
  'https://cidademdia.com.br/' \
  'https://cidademdia.com.br/como-funciona' \
  'https://cidademdia.com.br/ocorrencias' \
  'https://cidademdia.com.br/representantes' \
  'https://cidademdia.com.br/planos' \
  'https://cidademdia.com.br/sobre'; do
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
echo "seo_entity_authority=OK"
echo "seo_static_render=OK"
echo "seo_about_page=OK"
echo "seo_about_footer_only=OK"
echo "seo_about_hero_palette=OK"
echo "seo_canonical=OK"
echo "seo_favicon=OK"
echo "seo_open_graph=OK"
echo "seo_twitter_card=OK"
echo "seo_structured_data=OK"
echo "seo_brand_name=OK"
echo "seo_private_noindex=OK"
echo "seo_homolog_noindex=OK"
echo "seo_robots=OK"
echo "seo_sitemap=OK"
echo "SEO INDEXING GUARD: OK"
