#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONTRACTS="$ROOT/apps/api/src/CidadeEmDia.Application/Institutions/InstitutionContracts.cs"
ENDPOINTS="$ROOT/apps/api/src/CidadeEmDia.Api/Endpoints/InstitutionEndpoints.cs"
SERVICE="$ROOT/apps/api/src/CidadeEmDia.Infrastructure/Institutions/InstitutionService.cs"
WEB_TYPES="$ROOT/apps/web/src/modules/institutions/types.ts"
WEB_SERVICE="$ROOT/apps/web/src/modules/institutions/institutionService.ts"
DIRECTORY="$ROOT/apps/web/src/modules/institutions/InstitutionDirectory.tsx"
NAVIGATION="$ROOT/apps/web/src/app/layout/AppNavigation.tsx"

fail() {
  echo "PUBLIC MASTER DIRECTORY GUARD: FAIL — $1" >&2
  exit 1
}

for file in "$CONTRACTS" "$ENDPOINTS" "$SERVICE" "$WEB_TYPES" "$WEB_SERVICE" "$DIRECTORY" "$NAVIGATION"; do
  test -f "$file" || fail "arquivo ausente: $file"
done

grep -Fq 'Task<MasterDirectoryPage> ListActiveMastersAsync(' "$CONTRACTS" || fail "contrato da listagem pública de Masters ausente"
grep -Fq 'user.Status == UserStatus.Active' "$SERVICE" || fail "listagem não restringe usuários ativos"
grep -Fq 'IdentityRoleKeys.Master' "$SERVICE" || fail "listagem não restringe role MASTER"
grep -Fq 'return new MasterDirectoryPage(' "$SERVICE" || fail "serviço não retorna diretório de Masters"
grep -Fq 'string.IsNullOrWhiteSpace(master.DisplayName) ? "Conta Master" : master.DisplayName' "$SERVICE" || fail "Master sem perfil pode desaparecer da resposta"

grep -Fq 'api.MapGet("/masters"' "$ENDPOINTS" || fail "endpoint público /masters ausente"
grep -Fq 'service.ListActiveMastersAsync(' "$ENDPOINTS" || fail "endpoint /masters não usa a fonte de Masters ativas"

grep -Fq 'export interface MasterDirectoryItem' "$WEB_TYPES" || fail "DTO web de Master ausente"
grep -Fq "api.get<MasterDirectoryPage>('/masters'" "$WEB_SERVICE" || fail "frontend não consulta /masters"
grep -Fq "import { listActiveMasters } from './institutionService';" "$DIRECTORY" || fail "diretório não usa listActiveMasters"
grep -Fq 'void listActiveMasters({' "$DIRECTORY" || fail "diretório não carrega as contas Master"
if grep -Fq 'void listInstitutions({' "$DIRECTORY"; then
  fail "diretório público ainda depende de listInstitutions para montar a lista"
fi
grep -Fq 'Master ativo' "$DIRECTORY" || fail "cards não identificam a conta Master ativa"
grep -Fq 'Conta Master ativa sem órgão público vinculado.' "$DIRECTORY" || fail "Master sem órgão não possui estado visível"
grep -Fq "label: 'Masters'" "$NAVIGATION" || fail "navegação pública não identifica a tela como Masters"

MASTER_DTO="$(sed -n '/public sealed record MasterDirectoryItem(/,/);/p' "$CONTRACTS")"
if printf '%s\n' "$MASTER_DTO" | grep -Eqi 'Email|Document|Phone'; then
  fail "DTO público de Master expõe dado sensível"
fi

echo "active_master_role_filter=OK"
echo "active_master_status_filter=OK"
echo "master_without_institution_visible=OK"
echo "master_public_contract_sanitized=OK"
echo "master_public_endpoint=OK"
echo "master_public_ui=OK"
echo "PUBLIC MASTER DIRECTORY GUARD: OK"
