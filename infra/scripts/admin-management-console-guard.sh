#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WEB="$ROOT/apps/web/src"
API="$ROOT/apps/api/src"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

echo "=== ADMIN MANAGEMENT CONSOLE ==="

grep -q 'path="/admin"' "$WEB/main.tsx" \
  || fail "rota /admin não registrada"
grep -q 'href="/admin"' "$WEB/app/layout/AppHeader.tsx" \
  || fail "Administração não está no dropdown da conta"
grep -q '>Administração<' "$WEB/app/layout/AppHeader.tsx" \
  || fail "dropdown não usa o rótulo Administração"
grep -q "\['media', 'team', 'admin'\]" "$WEB/app/layout/AppHeader.tsx" \
  || fail "header não removeu Mídias, Equipe e Admin da navegação principal"

! grep -q 'AdminPanel' "$WEB/modules/home/HomeAccountModules.tsx" \
  || fail "AdminPanel ainda está acoplado à Home"
! grep -q 'PostManagementPanel' "$WEB/modules/home/HomeAccountModules.tsx" \
  || fail "gestão de mídia Admin ainda está acoplada à Home"

grep -q "label: 'Planos'" "$WEB/modules/admin/AdminConsole.tsx" \
  || fail "console Admin não possui aba Planos"
grep -q "label: 'Banner'" "$WEB/modules/admin/AdminConsole.tsx" \
  || fail "console Admin não possui aba Banner"
grep -q "label: 'Mídias CidadeEmDia'" "$WEB/modules/admin/AdminConsole.tsx" \
  || fail "console Admin não possui aba de mídias oficiais"
grep -q '<PostManagementPanel />' "$WEB/modules/admin/AdminConsole.tsx" \
  || fail "console Admin não reutiliza gestão de publicações"

grep -q 'PostPlacementKeys.Hero' "$ROOT/apps/api/tests/CidadeEmDia.UnitTests/ContentDomainTests.cs" \
  || fail "placement hero sem cobertura de domínio"
grep -q 'public const string Hero = "hero"' "$API/CidadeEmDia.Domain/Content/ContentEntities.cs" \
  || fail "placement hero não existe no domínio"
grep -q "listPlacementPosts('hero'" "$WEB/modules/home/HeroBannerBootstrap.tsx" \
  || fail "Home não carrega banner administrável"
grep -q "placementKey: 'hero'" "$WEB/modules/admin/AdminHeroBannerPanel.tsx" \
  || fail "painel Admin não publica banner no placement hero"
grep -q 'HERO_BANNER_UPDATED_EVENT' "$WEB/modules/admin/AdminHeroBannerPanel.tsx" \
  || fail "troca de banner não atualiza a Home"

grep -q 'MapAdminPlanManagementEndpoints' "$API/CidadeEmDia.Api/Program.cs" \
  || fail "endpoint de gestão de planos não está mapeado"
grep -q 'current.Close(effectiveAt)' "$API/CidadeEmDia.Infrastructure/Administration/AdminPlanManagementService.cs" \
  || fail "alteração de plano não encerra a versão anterior"
grep -q 'new PlanVersion(' "$API/CidadeEmDia.Infrastructure/Administration/AdminPlanManagementService.cs" \
  || fail "alteração de plano não cria nova versão"
grep -q 'PLAN_VERSION_CHANGED' "$API/CidadeEmDia.Infrastructure/Administration/AdminPlanManagementService.cs" \
  || fail "alteração de plano não gera auditoria"
grep -q 'updateAdminPlan(editing.planVersionId' "$WEB/modules/admin/AdminPlansEditor.tsx" \
  || fail "editor de planos não usa endpoint administrativo"

echo "admin_dropdown=OK"
echo "primary_header_clean=OK"
echo "admin_route=OK"
echo "admin_plans_versioned=OK"
echo "admin_hero_banner=OK"
echo "admin_platform_media=OK"
echo "ADMIN MANAGEMENT CONSOLE: OK"
