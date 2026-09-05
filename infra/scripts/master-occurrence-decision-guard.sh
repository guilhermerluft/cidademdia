#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PANEL="$ROOT/apps/web/src/modules/occurrenceAssignments/OccurrenceAssignmentPanel.tsx"
SERVICE="$ROOT/apps/web/src/modules/occurrenceAssignments/occurrenceAssignmentService.ts"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

echo "=== MASTER OCCURRENCE DECISION ==="

test -f "$PANEL" || fail "painel de assignments não encontrado"
test -f "$SERVICE" || fail "serviço de assignments não encontrado"

grep -q 'acceptOccurrenceTarget' "$SERVICE" \
  || fail "frontend não expõe ação de aceite"
grep -q '/targets/${targetId}/accept' "$SERVICE" \
  || fail "aceite não usa endpoint de decisão existente"
grep -q 'rejectOccurrenceTarget' "$SERVICE" \
  || fail "frontend não expõe ação de recusa"
grep -q '/targets/${targetId}/reject' "$SERVICE" \
  || fail "recusa não usa endpoint de decisão existente"
grep -q "target.targetStatus === 'PENDING'" "$PANEL" \
  || fail "painel não identifica ocorrência aguardando decisão"
grep -q "'Aceitar ocorrência'" "$PANEL" \
  || fail "painel Master não apresenta ação de aceite"
grep -q "'Confirmar recusa'" "$PANEL" \
  || fail "painel Master não apresenta confirmação de recusa"
grep -q 'maxLength={1000}' "$PANEL" \
  || fail "motivo da recusa não respeita limite do domínio"
grep -q "target.targetStatus === 'ACCEPTED'" "$PANEL" \
  || fail "distribuição não está condicionada ao aceite"
grep -q 'Subconta responsável' "$PANEL" \
  || fail "distribuição pós-aceite não está presente"

echo "accept_action=OK"
echo "reject_action=OK"
echo "rejection_reason=OK"
echo "assignment_after_accept=OK"
echo "MASTER OCCURRENCE DECISION: OK"
