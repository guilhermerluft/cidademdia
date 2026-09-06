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
grep -q '<AppHeader' "$WEB/modules/institutions/RepresentativesRoute.tsx" \
  || fail "diretório público não usa AppHeader compartilhado"
grep -q '<AppHeader' "$WEB/modules/panel/UserPanelRoute.tsx" \
  || fail "Painel não usa AppHeader compartilhado"
grep -q '<AppHeader' "$WEB/modules/profile/ProfileRoute.tsx" \
  || fail "Perfil não usa AppHeader compartilhado"
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
grep -q "href: '/representantes'" "$WEB/app/layout/AppNavigation.tsx" \
  || fail "Masters não aponta para /representantes"
grep -A6 "id: 'representatives'" "$WEB/app/layout/AppNavigation.tsx" | grep -q "label: 'Masters'" \
  || fail "navegação pública não usa o rótulo Masters"
grep -A6 "id: 'representatives'" "$WEB/app/layout/AppNavigation.tsx" | grep -q 'public: true' \
  || fail "Masters não está visível na navegação pública"
! grep -A6 "id: 'profile'" "$WEB/app/layout/AppNavigation.tsx" | grep -q "href: '/#perfil'" \
  || fail "Perfil voltou para a navegação principal/âncora da Home"

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
grep -q 'id="painel-publicacoes"' "$WEB/modules/panel/UserPanel.tsx" \
  || fail "Publicações da conta Master não estão em /painel"
grep -q '<PostManagementPanel />' "$WEB/modules/panel/UserPanel.tsx" \
  || fail "gestão de publicações da conta Master não está em /painel"
grep -q 'id="painel-equipe"' "$WEB/modules/panel/UserPanel.tsx" \
  || fail "Equipe e permissões da conta Master não estão em /painel"
grep -q '<MasterTeamPanel />' "$WEB/modules/panel/UserPanel.tsx" \
  || fail "gestão de equipe da conta Master não está em /painel"
grep -q "access.mode === 'master'" "$WEB/modules/panel/UserPanel.tsx" \
  || fail "módulos operacionais de Master não estão condicionados ao modo master"

grep -q 'path="/perfil"' "$WEB/main.tsx" \
  || fail "rota /perfil não registrada"
grep -q "status !== 'authenticated'" "$WEB/modules/profile/ProfileRoute.tsx" \
  || fail "rota /perfil não está protegida por autenticação"
grep -q "api.get<PrivateUserProfile>('/profile')" "$WEB/modules/profile/profileService.ts" \
  || fail "Perfil não consome o endpoint privado existente"
grep -q "api.put<PrivateUserProfile>('/profile'" "$WEB/modules/profile/profileService.ts" \
  || fail "edição de documento/telefone não reutiliza PUT /profile"
grep -q "'/profile/avatar/upload'" "$WEB/modules/profile/profileService.ts" \
  || fail "upload de avatar não usa endpoint central do perfil"
grep -q "'/profile/avatar/confirm'" "$WEB/modules/profile/profileService.ts" \
  || fail "confirmação de avatar não usa endpoint central do perfil"
grep -q 'prepareProfileAvatar' "$WEB/modules/profile/ProfilePage.tsx" \
  || fail "tela de perfil não implementa upload confirmado de avatar"
grep -q 'updateMyProfile' "$WEB/modules/profile/ProfilePage.tsx" \
  || fail "tela de perfil não permite atualizar documento/telefone"
grep -q 'formatBrazilianDocument' "$WEB/modules/profile/ProfilePage.tsx" \
  || fail "campo de documento não usa máscara central"
grep -q 'formatBrazilianPhone' "$WEB/modules/profile/ProfilePage.tsx" \
  || fail "campo de telefone não usa máscara central"
grep -q 'onlyDigits(value, 14)' "$WEB/modules/profile/profileMasks.ts" \
  || fail "máscara de CPF/CNPJ não limita em 14 dígitos"
grep -q 'onlyDigits(value, 11)' "$WEB/modules/profile/profileMasks.ts" \
  || fail "máscara de telefone não limita em 11 dígitos"
grep -q 'href="/perfil"' "$WEB/app/layout/AppHeader.tsx" \
  || fail "dropdown do perfil não aponta para /perfil"

grep -q 'path="/representantes"' "$WEB/main.tsx" \
  || fail "rota /representantes não registrada"
grep -q 'InstitutionDirectory' "$WEB/modules/institutions/RepresentativesRoute.tsx" \
  || fail "rota /representantes não reutiliza o diretório institucional"
grep -q 'title="Órgãos e agentes públicos"' "$WEB/modules/institutions/InstitutionDirectory.tsx" \
  || fail "diretório público não usa o título Órgãos e agentes públicos"
grep -q '>Agentes públicos<' "$WEB/modules/institutions/InstitutionDirectory.tsx" \
  || fail "cards do diretório não usam o rótulo Agentes públicos"
grep -q 'Buscar órgão ou agente público' "$WEB/modules/institutions/InstitutionDirectory.tsx" \
  || fail "busca do diretório ainda não usa terminologia de órgão/agente público"
grep -q 'Carregando órgãos e agentes públicos' "$WEB/modules/institutions/RepresentativesRoute.tsx" \
  || fail "loading da rota pública ainda não usa terminologia de órgão/agente público"
for old_copy in \
  'Instituições e representantes' \
  'Consulte órgãos e representantes' \
  'Buscar instituição ou representante' \
  'nome do representante' \
  'Nenhum representante cadastrado' \
  '>Representantes<' \
  'Carregando representantes'; do
  if grep -Fq "$old_copy" "$WEB/modules/institutions/InstitutionDirectory.tsx" "$WEB/modules/institutions/RepresentativesRoute.tsx"; then
    fail "termo público antigo ainda presente em /representantes: $old_copy"
  fi
done

! grep -q 'OccurrenceCenter' "$WEB/modules/home/HomeAccountModules.tsx" \
  || fail "Minhas ocorrências voltou a ser renderizado na Home"
! grep -q 'OccurrenceAssignmentPanel' "$WEB/modules/home/HomeAccountModules.tsx" \
  || fail "ocorrências privadas voltaram a ser renderizadas na Home"
! grep -q 'ChatInbox' "$WEB/modules/home/HomeAccountModules.tsx" \
  || fail "Conversas voltou a ser renderizado na Home"
! grep -q 'InstitutionDirectory' "$WEB/modules/home/HomeAccountModules.tsx" \
  || fail "diretório institucional voltou a ser renderizado na Home"
! grep -q 'dashboard-profile-section' "$WEB/modules/home/HomeAccountModules.tsx" \
  || fail "informações do perfil voltaram a ser renderizadas na Home"
! grep -q 'id="perfil"' "$WEB/modules/home/HomeAccountModules.tsx" \
  || fail "âncora de perfil voltou a ser renderizada na Home"
grep -q 'if (isMaster) return null;' "$WEB/modules/home/HomeAccountModules.tsx" \
  || fail "conta Master ainda recebe módulos operacionais na Home"
! grep -q 'MasterTeamPanel' "$WEB/modules/home/HomeAccountModules.tsx" \
  || fail "Equipe e permissões ainda está acoplada à Home"

grep -q 'getUserPanelAccess(user, permissions)' "$WEB/app/layout/AppHeader.tsx" \
  || fail "dropdown do perfil não usa a mesma regra de acesso do painel"
grep -q 'href="/painel"' "$WEB/app/layout/AppHeader.tsx" \
  || fail "dropdown do perfil não contém acesso ao painel"
grep -q 'href="/perfil"' "$WEB/app/layout/AppHeader.tsx" \
  || fail "dropdown da conta não contém acesso ao perfil"
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

grep -q "navigate('/como-funciona')" "$WEB/modules/home/PublicHome.tsx" \
  || fail "CTA Como funciona não aponta para a rota dedicada"
! grep -q 'setHowItWorksOpen' "$WEB/modules/home/PublicHome.tsx" \
  || fail "Home voltou a manter estado para abrir o modal Como funciona"
! grep -q '<HowItWorksModal' "$WEB/modules/home/PublicHome.tsx" \
  || fail "Home voltou a montar o modal Como funciona"
grep -q 'path="/como-funciona"' "$WEB/main.tsx" \
  || fail "rota dedicada /como-funciona não está registrada"
grep -q "'/media/como-funciona.mp4'" "$WEB/modules/home/HowItWorksModal.tsx" \
  || fail "vídeo operacional Como funciona foi alterado"
grep -q 'CIDADEMDIA_RUNTIME_MEDIA_DIR' "$ROOT/infra/docker-compose.yml" \
  || fail "diretório persistente de mídia não está montado no nginx"
grep -q 'location = /media/como-funciona.mp4' "$ROOT/infra/nginx/cidademdia.conf" \
  || fail "nginx não expõe exclusivamente o vídeo Como funciona"

echo "shared_header=OK"
echo "single_home=OK"
echo "authenticated_home_reuses_public_home=OK"
echo "centralized_navigation_access=OK"
echo "public_occurrences_navigation=OK"
echo "public_masters_navigation=OK"
echo "profile_route_separated_from_home=OK"
echo "editable_profile=OK"
echo "profile_avatar_flow=OK"
echo "profile_input_masks=OK"
echo "public_institution_terminology=OK"
echo "shared_public_occurrence_location=OK"
echo "shared_google_maps_loader=OK"
echo "user_panel_permissions=OK"
echo "master_operations_in_panel=OK"
echo "master_home_public_only=OK"
echo "home_private_operations_removed=OK"
echo "account_dropdown_profile_and_logout=OK"
echo "how_it_works_dedicated_route=OK"
echo "FRONTEND ARCHITECTURE: OK"
