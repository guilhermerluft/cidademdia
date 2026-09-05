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

grep -q "title: 'Converse com o cidadão'" "$PLANS" || fail "benefício de conversa com cidadão ausente"
grep -q "icon: 'fa-comments'" "$PLANS" || fail "benefício de conversa não usa ícone da biblioteca"
grep -q "Mantenha contato direto pelo chat durante o acompanhamento da ocorrência." "$PLANS" || fail "descrição do chat ausente"
grep -q "com a possibilidade de adquirir pacotes de postagens extras sempre que necessário." "$PLANS" || fail "postagens extras não estão destacadas"

BENEFIT_TITLES="$(grep -c "    title: '" "$PLANS")"
test "$BENEFIT_TITLES" -ge 5 || fail "quantidade de benefícios inferior a cinco"
grep -q 'grid-template-columns: repeat(5' "$CSS" || fail "cinco benefícios não permanecem inline"
! grep -q 'grid-template-columns: repeat(2' "$CSS" || fail "benefícios ainda quebram em duas colunas"
! grep -q 'grid-template-columns: 1fr' "$CSS" || fail "benefícios ainda empilham em uma coluna"
grep -q 'overflow-x: auto' "$CSS" || fail "faixa inline não possui fallback horizontal responsivo"
grep -A12 '^\.plans-page__benefits article {' "$CSS" | grep -q 'flex-direction: column' || fail "benefícios não usam ícone acima do título"
grep -q 'white-space: nowrap' "$CSS" || fail "títulos/linhas críticas não estão protegidos contra quebra"

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
grep -q 'plans-page__benefit-icon--purple' "$CSS" || fail "estilo do card de conversa ausente"

echo "plans_five_benefits_inline=OK"
echo "plans_benefit_icons_above_titles=OK"
echo "plans_citizen_chat_benefit=OK"
echo "plans_extra_posts_copy=OK"
echo "plans_master_individual_name=OK"
echo "plans_promotion_icon_text_inline=OK"
echo "plans_numeric_post_limits=OK"
echo "plans_post_count_spelling=OK"
echo "plans_promotional_values_notice=OK"
echo "PUBLIC PLANS COMMERCIAL GUARD: OK"
