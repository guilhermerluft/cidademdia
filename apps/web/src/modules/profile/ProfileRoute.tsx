import { Navigate } from 'react-router-dom';
import { AppBottomNavigation, AppHeader } from '../../app/layout/AppHeader';
import { useNavigationAccess } from '../../app/layout/AppNavigation';
import { Brand } from '../../components/ui';
import { useAuth } from '../auth/AuthProvider';
import { ProfilePage } from './ProfilePage';

export function ProfileRoute() {
  const { status, user, logout } = useAuth();
  const navigationAccess = useNavigationAccess(status === 'authenticated' ? user : null);

  if (status === 'loading') {
    return (
      <>
        <AppHeader />
        <main className="profile-page profile-page--loading" aria-busy="true">
          <Brand />
          <span>Carregando perfil...</span>
        </main>
      </>
    );
  }

  if (status !== 'authenticated' || !user) {
    return <Navigate to="/" replace />;
  }

  return (
    <div className="profile-page-shell">
      <AppHeader
        active="profile"
        user={user}
        permissions={navigationAccess.permissions}
        onLogout={logout}
      />
      <ProfilePage user={user} />
      <AppBottomNavigation
        active="profile"
        user={user}
        permissions={navigationAccess.permissions}
      />
    </div>
  );
}
