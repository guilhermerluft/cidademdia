#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PANEL="$ROOT/apps/web/src/modules/occurrenceAssignments/OccurrenceAssignmentPanel.tsx"
SERVICE="$ROOT/apps/web/src/modules/occurrenceAssignments/occurrenceAssignmentService.ts"
CHAT_MODAL="$ROOT/apps/web/src/modules/chat/OccurrenceChatModal.tsx"
CITIZEN_CHAT="$ROOT/apps/web/src/modules/chat/CitizenOccurrenceChatButton.tsx"
USER_PANEL="$ROOT/apps/web/src/modules/panel/UserPanel.tsx"
HEADER="$ROOT/apps/web/src/app/layout/AppHeader.tsx"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

echo "=== MASTER OCCURRENCE DECISION + CONTEXT CHAT ==="

test -f "$PANEL" || fail "painel de assignments não encontrado"
test -f "$SERVICE" || fail "serviço de assignments não encontrado"
test -f "$CHAT_MODAL" || fail "modal contextual do chat não encontrado"
test -f "$CITIZEN_CHAT" || fail "atalho contextual do cidadão não encontrado"

grep -q 'acceptOccurrenceTarget' "$SERVICE" || fail "frontend não expõe ação de aceite"
grep -q '/targets/${targetId}/accept' "$SERVICE" || fail "aceite não usa endpoint existente"
grep -q 'rejectOccurrenceTarget' "$SERVICE" || fail "frontend não expõe ação de recusa"
grep -q '/targets/${targetId}/reject' "$SERVICE" || fail "recusa não usa endpoint existente"
grep -q "target.targetStatus === 'PENDING'" "$PANEL" || fail "painel não identifica ocorrência aguardando decisão"
grep -q "'Aceitar ocorrência'" "$PANEL" || fail "painel Master não apresenta ação de aceite"
grep -q "'Confirmar recusa'" "$PANEL" || fail "painel Master não apresenta confirmação de recusa"
grep -q 'maxLength={1000}' "$PANEL" || fail "motivo da recusa não respeita limite do domínio"
grep -q "target.targetStatus === 'ACCEPTED'" "$PANEL" || fail "distribuição/chat não está condicionado ao aceite"
grep -q 'Subconta responsável' "$PANEL" || fail "distribuição pós-aceite não está presente"
grep -q 'target.coverMedia.readUrl' "$PANEL" || fail "primeira foto da ocorrência não aparece no painel Master"
grep -q 'PublicOccurrenceDetailsModal' "$PANEL" || fail "painel Master não abre detalhes da ocorrência"
grep -q 'OccurrenceChatModal' "$PANEL" || fail "painel Master/subconta não abre chat contextual"
grep -q "status === 'ACCEPTED'" "$CITIZEN_CHAT" || fail "chat do cidadão não está restrito a target aceito"
grep -q '/occurrences/${occurrenceId}/targets' "$CITIZEN_CHAT" || fail "chat do cidadão não resolve targets da própria ocorrência"
grep -q 'ChatPanel' "$CHAT_MODAL" || fail "modal contextual não reutiliza ChatPanel"

! grep -q '<ChatInbox' "$USER_PANEL" || fail "ChatInbox ainda está renderizado de forma fixa no painel"
! grep -q 'painel-conversas' "$USER_PANEL" || fail "atalho fixo de Conversas ainda está no painel"

grep -q "\['media', 'team', 'admin'\]" "$HEADER" || fail "header voltou a exibir Mídias, Equipe ou Admin"
grep -q 'href="/admin"' "$HEADER" || fail "Administração não está no dropdown da conta"

echo "accept_action=OK"
echo "reject_action=OK"
echo "occurrence_cover=OK"
echo "occurrence_details=OK"
echo "context_chat=OK"
echo "fixed_chat_removed=OK"
echo "admin_dropdown=OK"
echo "MASTER OCCURRENCE DECISION + CONTEXT CHAT: OK"
