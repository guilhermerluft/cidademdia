#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

MAIN="$ROOT/apps/web/src/main.tsx"
HOME="$ROOT/apps/web/src/modules/home/PublicHome.tsx"
HOME_ASSETS="$ROOT/apps/web/src/modules/home/home-assets.css"
HOME_REFINEMENT="$ROOT/apps/web/src/modules/home/home-refinement.css"
HOME_DIR="$ROOT/apps/web/src/modules/home"
HOME_BANNER="$ROOT/apps/web/src/modules/home/assets/banner-city.jpg"
COMMERCIAL_MODAL="$ROOT/apps/web/src/components/CommercialSignupModal.tsx"
COMMERCIAL_CSS="$ROOT/apps/web/src/styles/commercial-signup-modal.css"

for file in "$MAIN" "$HOME" "$HOME_ASSETS" "$HOME_REFINEMENT" "$HOME_BANNER" "$COMMERCIAL_MODAL" "$COMMERCIAL_CSS"; do
  test -f "$file" || fail "arquivo ausente: $file"
done

grep -q 'Brand, Button' "$COMMERCIAL_MODAL" || fail "modal comercial não reutiliza o Brand oficial"
grep -q '<Brand className="commercial-signup-modal__brand"' "$COMMERCIAL_MODAL" || fail "logotipo CidadeEmDia ausente do header do modal"
if grep -q 'commercial-signup-modal__icon' "$COMMERCIAL_MODAL"; then
  fail "símbolo antigo ainda está renderizado no modal comercial"
fi
grep -q 'commercial-signup-modal__brand-header' "$COMMERCIAL_CSS" || fail "header do logotipo comercial sem estilo dedicado"
grep -q 'commercial-signup-modal__brand' "$COMMERCIAL_CSS" || fail "logotipo comercial sem estilo dedicado"

if grep -q "home-benefits-refinement.css" "$MAIN"; then
  fail "stylesheet antigo dos cards de benefício ainda está carregado"
fi
if grep -q 'public-home__hero-benefits' "$HOME"; then
  fail "container antigo dos cards de benefício ainda está renderizado"
fi
if grep -q 'public-home__hero-benefit' "$HOME"; then
  fail "cards antigos de benefício ainda estão renderizados"
fi
if grep -RInE '\.public-home__hero::after[[:space:]]*\{' "$HOME_DIR" --include='*.css' >/dev/null; then
  fail "pseudo-elemento ::after de benefícios ainda existe no hero"
fi

grep -q "banner-city.jpg" "$HOME_ASSETS" || fail "novo banner fotográfico não está configurado no hero"
! grep -q 'Os vídeos publicados pelo painel administrador do CIDADEMDIA aparecerão aqui.' "$HOME" || fail "placeholder antigo de mídias ainda está presente"
! grep -q 'Novas demandas públicas aparecerão aqui quando forem registradas.' "$HOME" || fail "placeholder antigo de ocorrências ainda está presente"

grep -q 'O CIDADEMDIA conecta cidadãos e gestores, permitindo <strong>publicar ocorrências gratuitamente</strong> e acompanhar cada demanda,' "$HOME" || fail "novo texto comercial do hero ausente ou sem destaque"
grep -q 'tornando a gestão mais ágil, transparente e eficiente.' "$HOME" || fail "fechamento institucional do hero foi alterado"
if grep -q 'facilitando a comunicação e o acompanhamento das demandas' "$HOME"; then
  fail "texto antigo do hero ainda está presente"
fi

grep -q 'Uma cidade melhor<br />' "$HOME" || fail "título original do hero foi alterado ou removido"
grep -q 'quem precisa <span className="public-home__hero-green">é ouvido</span><br />' "$HOME" || fail "destaque verde original do hero foi alterado"
grep -q 'por quem <span className="public-home__hero-orange">pode resolver.</span>' "$HOME" || fail "destaque laranja original do hero foi alterado"
grep -q '<Button size="lg" onClick={() => setHowItWorksOpen(true)}>' "$HOME" || fail "CTA primário não abre Como funciona"
grep -q '<span className="public-home__cta-play" aria-hidden="true">▶</span>' "$HOME" || fail "ícone do CTA Como funciona foi removido"
grep -q 'Como funciona' "$HOME" || fail "CTA Como funciona foi alterado ou removido"
grep -q 'className="public-home__outline-cta" type="button" onClick={() => navigate('\''/planos'\'')}' "$HOME" || fail "CTA secundário não aponta para Planos"
grep -q 'Conheça os planos' "$HOME" || fail "CTA Conheça os planos foi alterado ou removido"

if grep -Fq 'public-home__hero-actions .ced-button:first-child::before' "$HOME_REFINEMENT"; then
  fail "ícone de planos ainda está preso ao primeiro CTA por posição"
fi
grep -Fq 'public-home__outline-cta::before' "$HOME_REFINEMENT" || fail "CTA Conheça os planos está sem ícone dedicado"
grep -Fq "content: '\f19c';" "$HOME_REFINEMENT" || fail "ícone de planos não está configurado"
grep -Fq 'public-home__cta-play::before' "$HOME_REFINEMENT" || fail "ícone de Como funciona não está configurado"
grep -Fq "content: '\f144';" "$HOME_REFINEMENT" || fail "ícone de play de Como funciona não está configurado"

echo "commercial_signup_brand_header=OK"
echo "hero_benefit_cards_removed=OK"
echo "hero_benefit_pseudo_after_removed=OK"
echo "hero_attached_banner=OK"
echo "home_placeholder_copy_removed=OK"
echo "hero_free_occurrence_copy=OK"
echo "hero_free_occurrence_copy_bold=OK"
echo "hero_original_title_preserved=OK"
echo "hero_cta_order=OK"
echo "hero_cta_icons=OK"
echo "PUBLIC HOME COMMERCIAL GUARD: OK"
