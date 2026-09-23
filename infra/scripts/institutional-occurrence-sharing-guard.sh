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
RESOLVER="$ROOT/apps/api/src/CidadeEmDia.Infrastructure/Occurrences/InstitutionalMasterResolver.cs"
MIGRATION="$ROOT/apps/api/src/CidadeEmDia.Infrastructure/Persistence/Migrations/20260922223000_AlignOccurrenceTargetsWithInstitutionalMasters.cs"
ENDPOINTS="$ROOT/apps/api/src/CidadeEmDia.Api/Endpoints/OccurrenceEndpoints.cs"
CENTER="$ROOT/apps/web/src/modules/occurrences/OccurrenceCenter.tsx"
WEB_SERVICE="$ROOT/apps/web/src/modules/occurrences/occurrenceService.ts"
MASTER_PANEL="$ROOT/apps/web/src/modules/occurrenceAssignments/OccurrenceAssignmentPanel.tsx"
PROD_SEED="$ROOT/infra/scripts/seed-production-sp-institutional-destinations.sh"
VISUAL_TEST="$ROOT/infra/scripts/institutional-occurrence-form-visual-test.sh"
FEATURE_TEST="$ROOT/infra/scripts/institutional-occurrence-sharing-feature-test.sh"

for file in "$TARGET" "$OCCURRENCE" "$RESOLVER" "$CONTRACTS" "$SERVICE" "$CREATION" "$DECISION" "$ASSIGNMENT" "$CONFIG" "$MIGRATION" "$ENDPOINTS" "$CENTER" "$WEB_SERVICE" "$MASTER_PANEL" "$PROD_SEED" "$VISUAL_TEST" "$FEATURE_TEST"; do
  test -f "$file" || fail "arquivo ausente: $file"
done

grep -Fq 'public Guid MasterUserId' "$TARGET" || fail "target não exige Master"
if grep -Fq 'InstitutionId' "$TARGET"; then
  fail "target ainda mantém instituição como destino persistido"
fi
if grep -Fq 'ClaimByMaster' "$TARGET"; then
  fail "target ainda possui fluxo de claim institucional"
fi
grep -Fq 'string? Addressee' "$TARGET" || fail "target não armazena endereçamento opcional"
grep -Fq 'AddInstitutionalMasterTarget' "$OCCURRENCE" || fail "domínio não cria target para Master institucional"
grep -Fq 'InstitutionMembershipRoleKeys.InstitutionAdmin' "$RESOLVER" || fail "resolver não exige vínculo INSTITUTION_ADMIN"
grep -Fq 'IdentityRoleKeys.Master' "$RESOLVER" || fail "resolver não exige role MASTER"
grep -Fq 'mastersPerInstitution' "$RESOLVER" || fail "resolver não protege instituição com múltiplas Masters"
grep -Fq 'institutionsPerMaster' "$RESOLVER" || fail "resolver não protege Master vinculada a múltiplas instituições"
grep -Fq 'InstitutionalMasterResolver.ResolveAsync' "$SERVICE" || fail "serviço não resolve instituição para Master"
grep -Fq 'InstitutionalMasterResolver.ResolveAsync' "$CREATION" || fail "criação não resolve instituição para Master"
grep -Fq 'AddInstitutionalMasterTarget' "$CREATION" || fail "criação não persiste target na Master institucional"
grep -Fq 'x.MasterUserId == masterUserId' "$ASSIGNMENT" || fail "painel Master não filtra targets pela Master persistida"
if grep -Fq 'InstitutionMembershipStatusKeys.Active' "$DECISION"; then
  fail "decisão ainda depende de claim por membership institucional"
fi
grep -Fq 'addressee' "$CONFIG" || fail "persistência não preserva addressee"
if grep -Fq 'institution_id' "$CONFIG"; then
  fail "persistência ainda mapeia institution_id no target"
fi
grep -Fq 'WHERE master_user_id IS NULL' "$MIGRATION" || fail "migration corretiva não protege dados incompatíveis"
grep -Fq 'name: "institution_id"' "$MIGRATION" || fail "migration corretiva não remove institution_id"
grep -Fq 'nullable: false' "$MIGRATION" || fail "migration corretiva não restaura master_user_id obrigatório"

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

protocol_line="$(grep -n -F 'Número do protocolo' "$CENTER" | head -n1 | cut -d: -f1)"
agency_line="$(grep -n -F 'Órgão do protocolo' "$CENTER" | head -n1 | cut -d: -f1)"
category_line="$(grep -n -F 'Categoria <span' "$CENTER" | head -n1 | cut -d: -f1)"
test -n "$protocol_line" || fail "campo Número do protocolo ausente"
test -n "$agency_line" || fail "campo Órgão do protocolo ausente"
test -n "$category_line" || fail "campo Categoria ausente"
if ! [ "$protocol_line" -lt "$agency_line" ] || ! [ "$agency_line" -lt "$category_line" ]; then
  fail "Órgão do protocolo deve ficar imediatamente após Número do protocolo e antes de Categoria"
fi

grep -Fq 'Destino:' "$MASTER_PANEL" || fail "painel Master não mostra destino institucional"
grep -Fq 'Endereçado a:' "$MASTER_PANEL" || fail "painel Master não mostra endereçamento opcional"

bash -n "$PROD_SEED" || fail "seed de produção possui sintaxe shell inválida"
bash -n "$VISUAL_TEST" || fail "smoke visual institucional possui sintaxe shell inválida"
bash -n "$FEATURE_TEST" || fail "E2E institucional possui sintaxe shell inválida"
grep -Fq 'CIDADEMDIA_EXPECTED_BRANCH' "$FEATURE_TEST" || fail "E2E institucional continua preso a uma branch fixa"
grep -Fq 'PROTOCOL AGENCY ORDER: OK' "$VISUAL_TEST" || fail "smoke visual não valida ordem do órgão do protocolo"
grep -Fq 'DESTINATIONS: 4 UNIQUE' "$VISUAL_TEST" || fail "smoke visual não valida quatro destinos"
grep -Fq "grep -q '^ASPNETCORE_ENVIRONMENT=Production$'" "$PROD_SEED" || fail "seed de produção não protege ambiente Production"
grep -Fq 'camara-sp.master@cidademdia.com.br' "$PROD_SEED" || fail "seed de produção não provisiona Câmara"
grep -Fq 'governo-sp.master@cidademdia.com.br' "$PROD_SEED" || fail "seed de produção não provisiona Governo"
grep -Fq 'alesp.master@cidademdia.com.br' "$PROD_SEED" || fail "seed de produção não provisiona ALESP"
if grep -Fq '@hml.cidademdia.invalid' "$PROD_SEED"; then
  fail "seed de produção contém conta exclusiva de HML"
fi
grep -Fq 'PRODUCTION_SP_INSTITUTIONS=OK count=4' "$PROD_SEED" || fail "seed de produção não valida as quatro instituições"
grep -Fq 'PRODUCTION_SP_INSTITUTIONAL_MASTERS=OK count=4' "$PROD_SEED" || fail "seed de produção não valida as quatro Masters"

echo 'institutional_master_resolution=OK'
echo 'institutional_destination_domain=OK'
echo 'institutional_destination_persistence=OK'
echo 'institutional_destination_api=OK'
echo 'institutional_destination_form=OK'
echo 'institutional_destination_master_panel=OK'
echo 'INSTITUTIONAL OCCURRENCE SHARING GUARD: OK'
