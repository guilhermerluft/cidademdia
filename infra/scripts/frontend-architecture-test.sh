#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WEB="$ROOT/apps/web/src"

fail() {
  echo "ERRO: $*" >&2
  exit 1
}

echo "=== FRONTEND ARCHITECTURE ==="

APP_HEADER_IMPL_COUNT="$(grep -R --include='*.tsx' -F 'export function AppHeader' "$WEB" | wc -l | tr -d ' ')"
test "$APP_HEADER_IMPL_COUNT" = "1" \
  || fail "deve existir exatamente 1 AppHeader; encontrado $APP_HEADER_IMPL_COUNT"

grep -q '<AppHeader' "$WEB/modules/home/PublicHome.tsx" \
  || fail "Home compartilhada não usa AppHeader"
grep -q '<AppHeader' "$WEB/modules/plans/PlansRoute.tsx" \
  || fail "Planos não usa AppHeader compartilhado"
grep -q '<AppHeader' "$WEB/app/layout/DashboardShell.tsx" \
  || fail "shell autenticado não usa AppHeader compartilhado"

if test -e "$WEB/app/dashboard/DashboardHome.tsx"; then
  fail "DashboardHome voltou a existir; a Home deve ser única"
fi

PUBLIC_HOME_IMPL_COUNT="$(grep -R --include='*.tsx' -F 'export function PublicHome' "$WEB" | wc -l | tr -d ' ')"
test "$PUBLIC_HOME_IMPL_COUNT" = "1" \
  || fail "deve existir exatamente 1 implementação de Home; encontrado $PUBLIC_HOME_IMPL_COUNT"

PUBLIC_HOME_USAGE_COUNT="$(grep -F '<PublicHome' "$WEB/app/App.tsx" | wc -l | tr -d ' ')"
test "$PUBLIC_HOME_USAGE_COUNT" = "2" \
  || fail "App deve usar a mesma PublicHome para visitante e autenticado; usos encontrados: $PUBLIC_HOME_USAGE_COUNT"

grep -q 'user={effectiveUser}' "$WEB/app/App.tsx" \
  || fail "Home autenticada não recebe o usuário na mesma PublicHome"
grep -q 'permissions={navigationAccess.permissions}' "$WEB/app/App.tsx" \
  || fail "Home autenticada não recebe permissões centralizadas"
grep -q 'onLogout={logout}' "$WEB/app/App.tsx" \
  || fail "Home autenticada não recebe logout no componente compartilhado"

! grep -q 'DashboardHome' "$WEB/app/App.tsx" \
  || fail "App ainda referencia DashboardHome"
! grep -q 'DashboardShell' "$WEB/app/App.tsx" \
  || fail "App ainda envolve a Home autenticada em DashboardShell"
! grep -q 'listSubaccountContexts' "$WEB/app/App.tsx" \
  || fail "App duplicou consulta de permissões/contexto de subconta"

grep -q 'export function useNavigationAccess' "$WEB/app/layout/AppNavigation.tsx" \
  || fail "estado de acesso não está centralizado em useNavigationAccess"
grep -q "restrictedSubaccountPermission: 'occurrence.read.targeted'" "$WEB/app/layout/AppNavigation.tsx" \
  || fail "visibilidade de ocorrências não está ligada à permissão real"

grep -q "permissions.includes('occurrence.read.targeted')" "$WEB/modules/home/HomeAccountModules.tsx" \
  || fail "módulos da Home não respeitam permissão de leitura de ocorrências"
grep -q "permissions.includes('chat.read')" "$WEB/modules/home/HomeAccountModules.tsx" \
  || fail "módulos da Home não respeitam permissão de leitura de chat"
grep -q "user.roles.includes('MASTER')" "$WEB/modules/home/HomeAccountModules.tsx" \
  || fail "módulos da Home não controlam acesso Master"
grep -q "user.roles.includes('ADMIN')" "$WEB/modules/home/HomeAccountModules.tsx" \
  || fail "módulos da Home não controlam acesso Admin"

grep -q 'user={user}' "$WEB/modules/home/PublicHome.tsx" \
  || fail "PublicHome não entrega usuário ao header/bottom nav compartilhados"
grep -q 'permissions={permissions}' "$WEB/modules/home/PublicHome.tsx" \
  || fail "PublicHome não entrega permissões à navegação compartilhada"

echo "shared_header=OK"
echo "single_home=OK"
echo "authenticated_home_reuses_public_home=OK"
echo "centralized_navigation_access=OK"
echo "permission_driven_home_modules=OK"
echo "FRONTEND ARCHITECTURE: OK"
