#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

HOME="$ROOT/apps/web/src/modules/home/PublicHome.tsx"
HOME_REFINEMENT="$ROOT/apps/web/src/modules/home/home-refinement.css"
COMMERCIAL_MODAL="$ROOT/apps/web/src/components/CommercialSignupModal.tsx"
COMMERCIAL_CSS="$ROOT/apps/web/src/styles/commercial-signup-modal.css"

for file in "$HOME" "$HOME_REFINEMENT" "$COMMERCIAL_MODAL" "$COMMERCIAL_CSS"; do
  test -f "$file" || fail "arquivo ausente: $file"
done

grep -q 'Brand, Button' "$COMMERCIAL_MODAL" || fail "modal comercial não reutiliza o Brand oficial"
grep -q '<Brand className="commercial-signup-modal__brand"' "$COMMERCIAL_MODAL" || fail "logotipo CidadeEmDia ausente do header do modal"
if grep -q 'commercial-signup-modal__icon' "$COMMERCIAL_MODAL"; then
  fail "símbolo antigo ainda está renderizado no modal comercial"
fi
grep -q 'commercial-signup-modal__brand-header' "$COMMERCIAL_CSS" || fail "header do logotipo comercial sem estilo dedicado"
grep -q 'commercial-signup-modal__brand' "$COMMERCIAL_CSS" || fail "logotipo comercial sem estilo dedicado"

grep -q 'Apoie ocorrências da sua região' "$HOME_REFINEMENT" || fail "benefício de apoio ausente do hero"
grep -q 'Publique ocorrências gratuitamente' "$HOME_REFINEMENT" || fail "benefício de publicação gratuita ausente do hero"
grep -q 'Acompanhe detalhes e atualizações' "$HOME_REFINEMENT" || fail "benefício de acompanhamento ausente do hero"
grep -q 'public-home__hero::after' "$HOME_REFINEMENT" || fail "benefícios não estão isolados em camada complementar do hero"

grep -q 'Uma cidade melhor<br />' "$HOME" || fail "título original do hero foi alterado ou removido"
grep -q 'quem precisa <span className="public-home__hero-green">é ouvido</span><br />' "$HOME" || fail "destaque verde original do hero foi alterado"
grep -q 'por quem <span className="public-home__hero-orange">pode resolver.</span>' "$HOME" || fail "destaque laranja original do hero foi alterado"
grep -q 'O CIDADEMDIA conecta cidadãos e gestores, facilitando a comunicação e o acompanhamento das demandas,' "$HOME" || fail "texto institucional original do hero foi alterado"
grep -q 'tornando a gestão mais ágil, transparente e eficiente.' "$HOME" || fail "texto institucional original do hero foi alterado"
grep -q '>Conheça os planos<' "$HOME" || fail "CTA Conheça os planos foi alterado ou removido"
grep -q 'Como funciona' "$HOME" || fail "CTA Como funciona foi alterado ou removido"

echo "commercial_signup_brand_header=OK"
echo "hero_participation_benefits=OK"
echo "hero_original_copy_preserved=OK"
echo "hero_original_ctas_preserved=OK"
echo "PUBLIC HOME COMMERCIAL GUARD: OK"
