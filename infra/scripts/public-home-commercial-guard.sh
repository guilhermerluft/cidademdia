#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

MAIN="$ROOT/apps/web/src/main.tsx"
HOME="$ROOT/apps/web/src/modules/home/PublicHome.tsx"
HOME_REFINEMENT="$ROOT/apps/web/src/modules/home/home-refinement.css"
HOME_BENEFITS="$ROOT/apps/web/src/modules/home/home-benefits-refinement.css"
COMMERCIAL_MODAL="$ROOT/apps/web/src/components/CommercialSignupModal.tsx"
COMMERCIAL_CSS="$ROOT/apps/web/src/styles/commercial-signup-modal.css"

for file in "$MAIN" "$HOME" "$HOME_REFINEMENT" "$HOME_BENEFITS" "$COMMERCIAL_MODAL" "$COMMERCIAL_CSS"; do
  test -f "$file" || fail "arquivo ausente: $file"
done

grep -q 'Brand, Button' "$COMMERCIAL_MODAL" || fail "modal comercial não reutiliza o Brand oficial"
grep -q '<Brand className="commercial-signup-modal__brand"' "$COMMERCIAL_MODAL" || fail "logotipo CidadeEmDia ausente do header do modal"
if grep -q 'commercial-signup-modal__icon' "$COMMERCIAL_MODAL"; then
  fail "símbolo antigo ainda está renderizado no modal comercial"
fi
grep -q 'commercial-signup-modal__brand-header' "$COMMERCIAL_CSS" || fail "header do logotipo comercial sem estilo dedicado"
grep -q 'commercial-signup-modal__brand' "$COMMERCIAL_CSS" || fail "logotipo comercial sem estilo dedicado"

grep -q "import './modules/home/home-benefits-refinement.css'" "$MAIN" || fail "refinamento visual dos benefícios do hero não está carregado"
grep -q 'Apoie ocorrências da sua região' "$HOME" || fail "benefício de apoio ausente do hero"
grep -q 'Publique ocorrências gratuitamente' "$HOME" || fail "benefício de publicação gratuita ausente do hero"
grep -q 'Acompanhe detalhes e atualizações' "$HOME" || fail "benefício de acompanhamento ausente do hero"
grep -q 'Converse com a Conta Master pelo chat' "$HOME" || fail "benefício de chat com Conta Master ausente do hero"
test "$(grep -c 'className="public-home__hero-benefit public-home__hero-benefit--' "$HOME")" -eq 4 || fail "hero deve renderizar exatamente quatro cards de benefício"

grep -q 'fa-solid fa-arrow-up' "$HOME" || fail "card de apoio não usa ícone Font Awesome"
grep -q 'fa-solid fa-bullhorn' "$HOME" || fail "card de publicação não usa ícone Font Awesome"
grep -q 'fa-solid fa-eye' "$HOME" || fail "card de acompanhamento não usa ícone Font Awesome"
grep -q 'fa-solid fa-comments' "$HOME" || fail "card de chat não usa ícone Font Awesome"

grep -q 'public-home__hero-benefits' "$HOME_BENEFITS" || fail "container de cards do hero não possui estilo dedicado"
grep -q 'grid-column: 2' "$HOME_BENEFITS" || fail "cards não ocupam a coluna visual do hero no desktop"
grep -q 'align-self: center' "$HOME_BENEFITS" || fail "cards não estão centralizados verticalmente no hero"
grep -q 'justify-self: end' "$HOME_BENEFITS" || fail "cards não alinham com a margem lateral direita do hero-inner"
grep -q 'background: rgba(255, 255, 255, .68)' "$HOME_BENEFITS" || fail "cards não usam fundo translúcido para preservar a imagem traseira"
grep -q 'border: none' "$HOME_BENEFITS" || fail "cards ainda possuem borda"
if grep -q 'border-left:' "$HOME_BENEFITS"; then
  fail "cards não podem usar borda lateral"
fi
if grep -q 'data:image/svg+xml' "$HOME_BENEFITS"; then
  fail "benefícios devem usar a biblioteca de ícones instalada, não SVG embutido no CSS"
fi

grep -q 'color: #005f73' "$HOME_BENEFITS" || fail "ícone de apoio não usa teal do projeto"
grep -q 'color: #17a53c' "$HOME_BENEFITS" || fail "ícone de publicação não usa verde do projeto"
grep -q 'color: #f09a13' "$HOME_BENEFITS" || fail "ícone de acompanhamento não usa laranja do projeto"
grep -q 'color: #075ff0' "$HOME_BENEFITS" || fail "ícone de chat não usa azul do projeto"

grep -q 'Uma cidade melhor<br />' "$HOME" || fail "título original do hero foi alterado ou removido"
grep -q 'quem precisa <span className="public-home__hero-green">é ouvido</span><br />' "$HOME" || fail "destaque verde original do hero foi alterado"
grep -q 'por quem <span className="public-home__hero-orange">pode resolver.</span>' "$HOME" || fail "destaque laranja original do hero foi alterado"
grep -q 'O CIDADEMDIA conecta cidadãos e gestores, facilitando a comunicação e o acompanhamento das demandas,' "$HOME" || fail "texto institucional original do hero foi alterado"
grep -q 'tornando a gestão mais ágil, transparente e eficiente.' "$HOME" || fail "texto institucional original do hero foi alterado"
grep -q '>Conheça os planos<' "$HOME" || fail "CTA Conheça os planos foi alterado ou removido"
grep -q 'Como funciona' "$HOME" || fail "CTA Como funciona foi alterado ou removido"

echo "commercial_signup_brand_header=OK"
echo "hero_four_benefit_cards=OK"
echo "hero_master_chat_benefit=OK"
echo "hero_benefit_fontawesome_icons=OK"
echo "hero_benefit_translucent_background=OK"
echo "hero_benefit_no_side_border=OK"
echo "hero_benefit_vertical_center=OK"
echo "hero_benefit_project_margin_alignment=OK"
echo "hero_original_copy_preserved=OK"
echo "hero_original_ctas_preserved=OK"
echo "PUBLIC HOME COMMERCIAL GUARD: OK"
