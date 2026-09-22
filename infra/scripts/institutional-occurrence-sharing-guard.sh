#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

TARGET="$ROOT/apps/api/src/CidadeEmDia.Domain/Occurrences/OccurrenceTarget.cs"
OCCURRENCE="$ROOT/apps/api/src/CidadeEmDia.Domain/Occurrences/Occurrence.cs"
CONTRACTS="$ROOT/apps/api/src/CidadeEmDia.Application/Occurrences/OccurrenceContracts.cs"
SERVICE="$ROOT/apps/api/src/CidadeEmDia.Infrastructure/Occurrences/OccurrenceService.cs"
CREATION="$ROOT/apps/api/src/CidadeEmDia.Infrastructure/Occurrences/OccurrenceCreationService.cs"
DECISION="$ROOT/apps/api/src/CidadeEmDia.Infrastructure/Occurrences/OccurrenceTargetDecisionService.cs"
ASSIGNMENT="$ROOT/apps/api/src/CidadeEmDia.Infrastructure/Occurrences/OccurrenceAssignmentService.cs"
CONFIG="$ROOT/apps/api/src/CidadeEmDia.Infrastructure/Persistence/Configurations/OccurrenceTargetConfiguration.cs"
MIGRATION="$ROOT/apps/api/src/CidadeEmDia.Infrastructure/Persistence/Migrations/20260922162000_AddInstitutionalOccurrenceTargets.cs"
ENDPOINTS="$ROOT/apps/api/src/CidadeEmDia.Api/Endpoints/OccurrenceEndpoints.cs"
CENTER="$ROOT/apps/web/src/modules/occurrences/OccurrenceCenter.tsx"
WEB_SERVICE="$ROOT/apps/web/src/modules/occurrences/occurrenceService.ts"
MASTER_PANEL="$ROOT/apps/web/src/modules/occurrenceAssignments/OccurrenceAssignmentPanel.tsx"

for file in "$TARGET" "$OCCURRENCE" "$CONTRACTS" "$SERVICE" "$CREATION" "$DECISION" "$ASSIGNMENT" "$CONFIG" "$MIGRATION" "$ENDPOINTS" "$CENTER" "$WEB_SERVICE" "$MASTER_PANEL"; do
  test -f "$file" || fail "arquivo ausente: $file"
done

grep -Fq 'Guid? InstitutionId' "$TARGET" || fail "target não aceita instituição"
grep -Fq 'string? Addressee' "$TARGET" || fail "target não armazena endereçamento opcional"
grep -Fq 'ClaimByMaster' "$TARGET" || fail "target institucional não pode ser assumido por Master"
grep -Fq 'AddInstitutionTarget' "$OCCURRENCE" || fail "domínio não cria target institucional"
grep -Fq 'InstitutionalDestinationItem' "$CONTRACTS" || fail "contrato de destino institucional ausente"

grep -Fq 'GetInstitutionalDestinationsAsync' "$SERVICE" || fail "serviço não lista instituições ativas"
grep -Fq 'AddInstitutionTargetAsync' "$SERVICE" || fail "serviço não compartilha com instituição"
grep -Fq 'InstitutionMembershipStatusKeys.Active' "$DECISION" || fail "decisão não valida vínculo institucional ativo"
grep -Fq 'target.ClaimByMaster' "$OCCURRENCE" || fail "aceite institucional não vincula Master responsável"
grep -Fq 'x.InstitutionId.HasValue' "$ASSIGNMENT" || fail "painel Master não recebe targets institucionais pendentes"

grep -Fq 'institution_id' "$CONFIG" || fail "persistência não mapeia institution_id"
grep -Fq 'addressee' "$CONFIG" || fail "persistência não mapeia addressee"
grep -Fq 'ux_occurrence_targets_occurrence_institution' "$CONFIG" || fail "unicidade institucional não está protegida"
grep -Fq 'AddColumn<Guid>' "$MIGRATION" || fail "migration não adiciona institution_id"
grep -Fq 'AddColumn<string>' "$MIGRATION" || fail "migration não adiciona addressee"

grep -Fq '"/destinations"' "$ENDPOINTS" || fail "endpoint de destinos institucionais ausente"
grep -Fq 'Guid? InstitutionId' "$ENDPOINTS" || fail "request não aceita instituição"
grep -Fq 'string? Addressee' "$ENDPOINTS" || fail "request não aceita endereçamento opcional"
grep -Fq 'Exactly one destination must be selected' "$ENDPOINTS" || fail "request não exige exatamente um destino"

grep -Fq "listInstitutionalDestinations" "$WEB_SERVICE" || fail "frontend não consulta destinos institucionais"
grep -Fq "Destino institucional" "$CENTER" || fail "formulário não mostra destino institucional"
grep -Fq "Nome ou partido (opcional)" "$CENTER" || fail "campo opcional de nome/partido ausente"
grep -Fq "maxLength={180}" "$CENTER" || fail "campo opcional não possui limite"
grep -Fq "masterUserId: null" "$CENTER" || fail "novo fluxo ainda tenta selecionar Master individual"
grep -Fq "institutionId: form.institutionId" "$CENTER" || fail "instituição selecionada não é enviada"
grep -Fq "addressee: form.addressee.trim() || null" "$CENTER" || fail "endereçamento opcional não é enviado"

grep -Fq 'Destino:' "$MASTER_PANEL" || fail "painel Master não mostra destino institucional"
grep -Fq 'Endereçado a:' "$MASTER_PANEL" || fail "painel Master não mostra endereçamento opcional"

echo 'institutional_destination_domain=OK'
echo 'institutional_destination_persistence=OK'
echo 'institutional_destination_api=OK'
echo 'institutional_destination_form=OK'
echo 'institutional_destination_master_panel=OK'
echo 'INSTITUTIONAL OCCURRENCE SHARING GUARD: OK'
