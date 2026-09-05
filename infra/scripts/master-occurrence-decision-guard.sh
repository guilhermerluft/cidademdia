#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PANEL="$ROOT/apps/web/src/modules/occurrenceAssignments/OccurrenceAssignmentPanel.tsx"
SERVICE="$ROOT/apps/web/src/modules/occurrenceAssignments/occurrenceAssignmentService.ts"
BACKEND_CONTRACT="$ROOT/apps/api/src/CidadeEmDia.Application/Occurrences/IOccurrenceAssignmentService.cs"
BACKEND_SERVICE="$ROOT/apps/api/src/CidadeEmDia.Infrastructure/Occurrences/OccurrenceAssignmentService.cs"
STYLES="$ROOT/apps/web/src/styles/assignments.css"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

echo "=== MASTER OCCURRENCE DECISION ==="

test -f "$PANEL" || fail "painel de assignments não encontrado"
test -f "$SERVICE" || fail "serviço de assignments não encontrado"
test -f "$BACKEND_CONTRACT" || fail "contrato backend de assignments não encontrado"
test -f "$BACKEND_SERVICE" || fail "serviço backend de assignments não encontrado"
test -f "$STYLES" || fail "estilos de assignments não encontrados"

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

grep -q 'PublicOccurrenceMediaItem? CoverMedia' "$BACKEND_CONTRACT" \
  || fail "contrato Master não expõe a primeira foto"
grep -q 'LoadCoverMediaAsync' "$BACKEND_SERVICE" \
  || fail "backend não carrega a primeira foto da ocorrência"
grep -q 'media.ContentType.StartsWith("image/")' "$BACKEND_SERVICE" \
  || fail "capa da Master não está limitada à primeira imagem"
grep -q 'coverMedia' "$SERVICE" \
  || fail "frontend não recebe a capa da ocorrência"
grep -q 'assignment-card__cover' "$PANEL" \
  || fail "painel Master não renderiza a primeira foto"
grep -q 'getPublicOccurrenceDetails' "$PANEL" \
  || fail "painel Master não carrega os detalhes da ocorrência"
grep -q 'PublicOccurrenceDetailsModal' "$PANEL" \
  || fail "painel Master não reutiliza o modal de detalhes"
grep -q 'Ver ocorrência' "$PANEL" \
  || fail "painel Master não oferece abertura explícita da ocorrência"
grep -q '.assignment-card__overview' "$STYLES" \
  || fail "layout de preview da ocorrência não foi estilizado"

echo "accept_action=OK"
echo "reject_action=OK"
echo "rejection_reason=OK"
echo "assignment_after_accept=OK"
echo "cover_media=OK"
echo "details_modal=OK"
echo "MASTER OCCURRENCE DECISION: OK"
