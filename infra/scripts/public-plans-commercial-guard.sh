#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PLANS="$ROOT/apps/web/src/modules/plans/PublicPlans.tsx"
CSS="$ROOT/apps/web/src/modules/plans/plans-commercial-highlights.css"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

for file in "$PLANS" "$CSS"; do
  test -f "$file" || fail "arquivo ausente: $file"
done

grep -q "title: 'Acesso às ocorrências e conversa com o cidadão'" "$PLANS" || fail "benefício consolidado de ocorrências/chat ausente"
grep -q "mantendo contato direto com o cidadão pelo chat." "$PLANS" || fail "descrição consolidada de ocorrências/chat ausente"
! grep -q "title: 'Converse com o cidadão'" "$PLANS" || fail "benefício de conversa continua separado"
grep -q "com a possibilidade de adquirir pacotes de postagens extras sempre que necessário." "$PLANS" || fail "postagens extras não estão destacadas"

BENEFIT_TITLES="$(awk '/^const BENEFITS = \[/,/^\] as const;/' "$PLANS" | grep -c "    title: '")"
test "$BENEFIT_TITLES" = "4" || fail "esperados exatamente quatro benefícios consolidados; encontrado $BENEFIT_TITLES"
! grep -q 'plans-page__benefit-icon' "$PLANS" || fail "ícones ainda estão renderizados na faixa de benefícios"
grep -q 'grid-template-columns: repeat(4, minmax(0, 1fr))' "$CSS" || fail "quatro benefícios não permanecem inline no desktop"
! grep -q 'overflow-x: auto' "$CSS" || fail "faixa de benefícios ainda usa rolagem horizontal"
grep -A10 '^\.plans-page__benefits article {' "$CSS" | grep -q 'align-items: center' || fail "articles dos benefícios não estão centralizados horizontalmente"
grep -A10 '^\.plans-page__benefits article {' "$CSS" | grep -q 'justify-content: center' || fail "articles dos benefícios não estão centralizados verticalmente"
grep -A10 '^\.plans-page__benefits article {' "$CSS" | grep -q 'text-align: center' || fail "texto dos benefícios não está centralizado"
grep -A8 '^@media (max-width: 1180px)' "$CSS" | grep -q 'grid-template-columns: 1fr' || fail "benefícios não empilham em telas menores"

grep -q "title: 'Master Individual'" "$PLANS" || fail "plano Individual não foi renomeado para Master Individual"
! grep -q "title: 'Individual'" "$PLANS" || fail "nome antigo Individual ainda está presente"

grep -q 'plans-page__plan-promotion' "$PLANS" || fail "selo promocional não está dentro dos cards de plano"
grep -q '<span>Oferta promocional</span>' "$PLANS" || fail "selo promocional não usa texto reduzido"
grep -q 'fa-solid fa-tags' "$PLANS" || fail "selo promocional não usa ícone Font Awesome"
! grep -q 'plans-page__promotion-breadcrumb' "$PLANS" || fail "breadcrumb promocional global antigo ainda existe"
grep -A14 '^\.plans-page__plan-promotion {' "$CSS" | grep -q 'flex-direction: row' || fail "ícone e Oferta promocional não estão inline"

grep -q '<strong>{publicationLimit} POSTAGENS/MÊS</strong>' "$PLANS" || fail "limite mensal não usa número no lugar de QTD"
! grep -q 'QTD POSTAGENS/MÊS' "$PLANS" || fail "rótulo QTD ainda está presente"
if grep -qi 'POSTAGEMENS' "$PLANS" "$CSS"; then
  fail "typo POSTAGEMENS ainda está presente"
fi

grep -q 'Os valores e condições de pagamento abaixo são promocionais' "$PLANS" || fail "aviso de valores promocionais ausente"

echo "plans_four_benefits_inline_desktop=OK"
echo "plans_benefits_stacked_responsive=OK"
echo "plans_benefits_centered=OK"
echo "plans_benefit_icons_removed=OK"
echo "plans_occurrence_chat_consolidated=OK"
echo "plans_extra_posts_copy=OK"
echo "plans_master_individual_name=OK"
echo "plans_promotion_icon_text_inline=OK"
echo "plans_numeric_post_limits=OK"
echo "plans_post_count_spelling=OK"
echo "plans_promotional_values_notice=OK"
echo "PUBLIC PLANS COMMERCIAL GUARD: OK"
