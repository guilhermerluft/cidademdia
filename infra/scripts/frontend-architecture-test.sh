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
grep -q '<AppHeader' "$WEB/modules/occurrences/PublicOccurrencesRoute.tsx" \
  || fail "Ocorrências não usa AppHeader compartilhado"
grep -q '<AppHeader' "$WEB/modules/panel/UserPanelRoute.tsx" \
  || fail "Painel não usa AppHeader compartilhado"
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
  || fail "Home autenticada não entrega logout ao header compartilhado"

! grep -q 'DashboardHome' "$WEB/app/App.tsx" \
  || fail "App ainda referencia DashboardHome"
! grep -q 'DashboardShell' "$WEB/app/App.tsx" \
  || fail "App ainda envolve a Home autenticada em DashboardShell"
! grep -q 'listSubaccountContexts' "$WEB/app/App.tsx" \
  || fail "App duplicou consulta de permissões/contexto de subconta"

grep -q 'export function useNavigationAccess' "$WEB/app/layout/AppNavigation.tsx" \
  || fail "estado de acesso não está centralizado em useNavigationAccess"
grep -q 'export function getUserPanelAccess' "$WEB/app/layout/AppNavigation.tsx" \
  || fail "permissões do painel não estão centralizadas"
grep -q "permissions.includes('occurrence.read.targeted')" "$WEB/app/layout/AppNavigation.tsx" \
  || fail "painel não respeita occurrence.read.targeted"
grep -q "permissions.includes('chat.read')" "$WEB/app/layout/AppNavigation.tsx" \
  || fail "painel não respeita chat.read"
grep -q "href: '/ocorrencias'" "$WEB/app/layout/AppNavigation.tsx" \
  || fail "Ocorrências não aponta para /ocorrencias"
grep -A6 "id: 'occurrences'" "$WEB/app/layout/AppNavigation.tsx" | grep -q 'public: true' \
  || fail "listagem pública de ocorrências não está visível para visitantes"

grep -q 'path="/painel"' "$WEB/main.tsx" \
  || fail "rota /painel não registrada"
grep -q 'getUserPanelAccess(user, navigationAccess.permissions)' "$WEB/modules/panel/UserPanelRoute.tsx" \
  || fail "rota /painel não usa regra central de acesso"
grep -q 'panelAccess.canAccessPanel' "$WEB/modules/panel/UserPanelRoute.tsx" \
  || fail "rota /painel não bloqueia usuário sem permissão"
grep -q 'OccurrenceCenter' "$WEB/modules/panel/UserPanel.tsx" \
  || fail "Minhas ocorrências não foi movido para /painel"
grep -q 'OccurrenceAssignmentPanel' "$WEB/modules/panel/UserPanel.tsx" \
  || fail "ocorrências Master/subconta não foram movidas para /painel"
grep -q 'ChatInbox' "$WEB/modules/panel/UserPanel.tsx" \
  || fail "Conversas não foi movido para /painel"

! grep -q 'OccurrenceCenter' "$WEB/modules/home/HomeAccountModules.tsx" \
  || fail "Minhas ocorrências voltou a ser renderizado na Home"
! grep -q 'OccurrenceAssignmentPanel' "$WEB/modules/home/HomeAccountModules.tsx" \
  || fail "ocorrências privadas voltaram a ser renderizadas na Home"
! grep -q 'ChatInbox' "$WEB/modules/home/HomeAccountModules.tsx" \
  || fail "Conversas voltou a ser renderizado na Home"

grep -q 'getUserPanelAccess(user, permissions)' "$WEB/app/layout/AppHeader.tsx" \
  || fail "dropdown do perfil não usa a mesma regra de acesso do painel"
grep -q 'href="/painel"' "$WEB/app/layout/AppHeader.tsx" \
  || fail "dropdown do perfil não contém acesso ao painel"
grep -q 'app-header__account-dropdown' "$WEB/app/layout/AppHeader.tsx" \
  || fail "dropdown do perfil não está implementado"
grep -q 'app-header__account-menu-item--logout' "$WEB/app/layout/AppHeader.tsx" \
  || fail "Sair não está dentro do dropdown do perfil"
! grep -q 'app-header__logout' "$WEB/app/layout/AppHeader.tsx" \
  || fail "logout voltou a ficar sempre visível no header"

grep -q 'requestBrowserCoordinates' "$WEB/modules/home/PublicHome.tsx" \
  || fail "Home não reutiliza a resolução central de localização pública"
grep -q 'PublicOccurrenceCard' "$WEB/modules/home/PublicHome.tsx" \
  || fail "Home não reutiliza o card público de ocorrência"
grep -q 'loadGoogleMaps' "$WEB/modules/occurrences/OccurrenceLocationPicker.tsx" \
  || fail "picker de ocorrência não reutiliza loader central de mapas"

grep -q 'user={user}' "$WEB/modules/home/PublicHome.tsx" \
  || fail "PublicHome não entrega usuário ao header/bottom nav compartilhados"
grep -q 'permissions={permissions}' "$WEB/modules/home/PublicHome.tsx" \
  || fail "PublicHome não entrega permissões à navegação compartilhada"

echo "shared_header=OK"
echo "single_home=OK"
echo "authenticated_home_reuses_public_home=OK"
echo "centralized_navigation_access=OK"
echo "public_occurrences_navigation=OK"
echo "shared_public_occurrence_location=OK"
echo "shared_google_maps_loader=OK"
echo "user_panel_permissions=OK"
echo "home_private_operations_removed=OK"
echo "account_dropdown_logout=OK"
echo "FRONTEND ARCHITECTURE: OK"
