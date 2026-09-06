import { useNavigate } from 'react-router-dom';
import { AppBottomNavigation, AppHeader } from '../../app/layout/AppHeader';
import { useNavigationAccess } from '../../app/layout/AppNavigation';
import { Brand } from '../../components/ui';
import { useAuth } from '../auth/AuthProvider';
import { HowItWorksPage } from './HowItWorksPage';

export function HowItWorksRoute() {
  const navigate = useNavigate();
  const { status, user, logout } = useAuth();
  const access = useNavigationAccess(status === 'authenticated' ? user : null);

  if (status === 'loading') {
    return (
      <>
        <AppHeader />
        <main className="how-it-works-page how-it-works-page--loading" aria-busy="true">
          <Brand />
          <span>Carregando Como funciona...</span>
        </main>
        <AppBottomNavigation />
      </>
    );
  }

  const authenticatedUser = status === 'authenticated' ? user : null;

  return (
    <HowItWorksPage
      user={authenticatedUser}
      permissions={access.permissions}
      onLogout={authenticatedUser ? logout : undefined}
      onLogin={authenticatedUser ? undefined : () => navigate('/?auth=login')}
      onRegister={authenticatedUser ? undefined : () => navigate('/?auth=register')}
    />
  );
}
