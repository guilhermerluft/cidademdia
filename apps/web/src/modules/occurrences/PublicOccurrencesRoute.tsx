import { useNavigate } from 'react-router-dom';
import { AppBottomNavigation, AppHeader } from '../../app/layout/AppHeader';
import { useNavigationAccess } from '../../app/layout/AppNavigation';
import { Brand } from '../../components/ui';
import { useAuth } from '../auth/AuthProvider';
import { PublicOccurrences } from './PublicOccurrences';

export function PublicOccurrencesRoute() {
  const navigate = useNavigate();
  const { status, user, logout } = useAuth();
  const access = useNavigationAccess(status === 'authenticated' ? user : null);

  if (status === 'loading') {
    return (
      <>
        <AppHeader active="occurrences" />
        <main className="public-occurrences public-occurrences__loading" aria-busy="true">
          <Brand />
          <span>Carregando ocorrências...</span>
        </main>
        <AppBottomNavigation active="occurrences" />
      </>
    );
  }

  const authenticatedUser = status === 'authenticated' ? user : null;

  return (
    <div className="public-occurrences-page">
      <AppHeader
        active="occurrences"
        user={authenticatedUser}
        permissions={access.permissions}
        onLogout={authenticatedUser ? logout : undefined}
        onLogin={authenticatedUser ? undefined : () => navigate('/')}
        onRegister={authenticatedUser ? undefined : () => navigate('/')}
      />
      <PublicOccurrences />
      <AppBottomNavigation
        active="occurrences"
        user={authenticatedUser}
        permissions={access.permissions}
        onLogin={authenticatedUser ? undefined : () => navigate('/')}
        onRegister={authenticatedUser ? undefined : () => navigate('/')}
      />
    </div>
  );
}
