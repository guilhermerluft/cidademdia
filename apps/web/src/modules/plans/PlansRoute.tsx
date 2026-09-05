import { useNavigate } from 'react-router-dom';
import { AppBottomNavigation, AppHeader } from '../../app/layout/AppHeader';
import { DashboardShell } from '../../app/layout/DashboardShell';
import { Brand } from '../../components/ui';
import { useAuth } from '../auth/AuthProvider';
import { PublicPlans } from './PublicPlans';

export function PlansRoute() {
  const navigate = useNavigate();
  const { status, user, logout } = useAuth();

  if (status === 'loading') {
    return (
      <>
        <AppHeader active="plans" />
        <main className="plans-page plans-page__route-loading" aria-busy="true">
          <Brand />
          <span>Carregando planos...</span>
        </main>
        <AppBottomNavigation active="plans" />
      </>
    );
  }

  if (status === 'authenticated' && user) {
    return (
      <DashboardShell user={user} onLogout={logout}>
        <PublicPlans
          embedded
          onContact={() => navigate('/')}
        />
      </DashboardShell>
    );
  }

  return (
    <>
      <AppHeader
        active="plans"
        onLogin={() => navigate('/')}
        onRegister={() => navigate('/')}
      />
      <PublicPlans
        onSelectOffer={() => navigate('/')}
        onContact={() => navigate('/')}
      />
      <AppBottomNavigation
        active="plans"
        onLogin={() => navigate('/')}
        onRegister={() => navigate('/')}
      />
    </>
  );
}
