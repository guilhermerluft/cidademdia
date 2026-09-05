import { useNavigate } from 'react-router-dom';
import { AppBottomNavigation, AppHeader } from '../../app/layout/AppHeader';
import { useNavigationAccess } from '../../app/layout/AppNavigation';
import { Brand } from '../../components/ui';
import { useAuth } from '../auth/AuthProvider';
import { InstitutionDirectory } from './InstitutionDirectory';

export function RepresentativesRoute() {
  const navigate = useNavigate();
  const { status, user, logout } = useAuth();
  const access = useNavigationAccess(status === 'authenticated' ? user : null);

  if (status === 'loading') {
    return (
      <>
        <AppHeader active="representatives" />
        <main className="representatives-page representatives-page--loading" aria-busy="true">
          <Brand />
          <span>Carregando órgãos e agentes públicos...</span>
        </main>
        <AppBottomNavigation active="representatives" />
      </>
    );
  }

  const authenticatedUser = status === 'authenticated' ? user : null;

  return (
    <div className="representatives-page-shell">
      <AppHeader
        active="representatives"
        user={authenticatedUser}
        permissions={access.permissions}
        onLogout={authenticatedUser ? logout : undefined}
        onLogin={authenticatedUser ? undefined : () => navigate('/?auth=login')}
        onRegister={authenticatedUser ? undefined : () => navigate('/?auth=register')}
      />
      <main className="representatives-page" aria-labelledby="institution-directory-title">
        <div className="representatives-page__container">
          <InstitutionDirectory />
        </div>
      </main>
      <AppBottomNavigation
        active="representatives"
        user={authenticatedUser}
        permissions={access.permissions}
        onLogin={authenticatedUser ? undefined : () => navigate('/?auth=login')}
        onRegister={authenticatedUser ? undefined : () => navigate('/?auth=register')}
      />
    </div>
  );
}
