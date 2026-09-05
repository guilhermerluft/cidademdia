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
if grep -q "description: 'Publique conteúdos institucionais de acordo com a franquia contratada.'" "$PLANS"; then
  fail "texto antigo de Postagens mensais ainda está presente"
fi

BENEFIT_TITLES="$(grep -c "    title: '" "$PLANS")"
test "$BENEFIT_TITLES" -ge 5 || fail "quantidade de benefícios inferior a cinco"

grep -q 'plans-page__promotion-breadcrumb' "$PLANS" || fail "breadcrumb promocional ausente"
grep -q 'Oferta promocional' "$PLANS" || fail "rótulo promocional ausente"
grep -q 'Os valores e condições de pagamento abaixo são promocionais' "$PLANS" || fail "aviso de valores promocionais ausente"
grep -q 'fa-solid fa-tags' "$PLANS" || fail "breadcrumb promocional não usa ícone Font Awesome"

grep -q 'grid-template-columns: repeat(5' "$CSS" || fail "faixa de benefícios não está preparada para cinco cards"
grep -q 'plans-page__benefit-icon--purple' "$CSS" || fail "estilo do card de conversa ausente"
grep -q 'plans-page__promotion-breadcrumb' "$CSS" || fail "breadcrumb promocional sem estilo dedicado"

echo "plans_five_benefits=OK"
echo "plans_citizen_chat_benefit=OK"
echo "plans_extra_posts_copy=OK"
echo "plans_promotional_breadcrumb=OK"
echo "plans_promotional_values_notice=OK"
echo "PUBLIC PLANS COMMERCIAL GUARD: OK"
