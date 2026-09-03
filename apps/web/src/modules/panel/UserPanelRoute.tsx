import { Navigate } from 'react-router-dom';
import { AppBottomNavigation, AppHeader } from '../../app/layout/AppHeader';
import { getUserPanelAccess, useNavigationAccess } from '../../app/layout/AppNavigation';
import { Brand } from '../../components/ui';
import { useAuth } from '../auth/AuthProvider';
import { UserPanel } from './UserPanel';

export function UserPanelRoute() {
  const { status, user, logout } = useAuth();
  const navigationAccess = useNavigationAccess(status === 'authenticated' ? user : null);

  if (status === 'loading') {
    return (
      <>
        <AppHeader />
        <main className="user-panel user-panel--loading" aria-busy="true">
          <Brand />
          <span>Carregando painel...</span>
        </main>
      </>
    );
  }

  if (status !== 'authenticated' || !user) {
    return <Navigate to="/" replace />;
  }

  const panelAccess = getUserPanelAccess(user, navigationAccess.permissions);
  if (!panelAccess.canAccessPanel) {
    return <Navigate to="/" replace />;
  }

  return (
    <div className="user-panel-page">
      <AppHeader
        user={user}
        permissions={navigationAccess.permissions}
        onLogout={logout}
      />
      <UserPanel user={user} access={panelAccess} />
      <AppBottomNavigation
        user={user}
        permissions={navigationAccess.permissions}
      />
    </div>
  );
}
