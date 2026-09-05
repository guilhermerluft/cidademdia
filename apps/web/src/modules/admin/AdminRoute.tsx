import { Navigate } from 'react-router-dom';
import { AppBottomNavigation, AppHeader } from '../../app/layout/AppHeader';
import { useNavigationAccess } from '../../app/layout/AppNavigation';
import { Brand } from '../../components/ui';
import { useAuth } from '../auth/AuthProvider';
import { AdminConsole } from './AdminConsole';

export function AdminRoute() {
  const { status, user, logout } = useAuth();
  const navigationAccess = useNavigationAccess(status === 'authenticated' ? user : null);

  if (status === 'loading') {
    return (
      <>
        <AppHeader />
        <main className="admin-route-loading" aria-busy="true">
          <Brand />
          <span>Carregando administração...</span>
        </main>
      </>
    );
  }

  if (status !== 'authenticated' || !user) {
    return <Navigate to="/" replace />;
  }

  if (!user.roles.includes('ADMIN')) {
    return <Navigate to="/" replace />;
  }

  return (
    <div className="admin-console-page">
      <AppHeader
        user={user}
        permissions={navigationAccess.permissions}
        onLogout={logout}
      />
      <AdminConsole />
      <AppBottomNavigation
        user={user}
        permissions={navigationAccess.permissions}
      />
    </div>
  );
}
