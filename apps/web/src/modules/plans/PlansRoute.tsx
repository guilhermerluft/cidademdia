import { useNavigate } from 'react-router-dom';
import { DashboardShell } from '../../app/layout/DashboardShell';
import { Brand } from '../../components/ui';
import { useAuth } from '../auth/AuthProvider';
import { PublicPlansV3 } from './PublicPlansV3';

export function PlansRoute() {
  const navigate = useNavigate();
  const { status, user, logout } = useAuth();

  if (status === 'loading') {
    return (
      <main className="plans-page plans-page__route-loading" aria-busy="true">
        <Brand />
        <span>Carregando planos...</span>
      </main>
    );
  }

  if (status === 'authenticated' && user) {
    return (
      <DashboardShell user={user} onLogout={logout}>
        <PublicPlansV3
          embedded
          onHome={() => navigate('/')}
          onLogin={() => navigate('/')}
          onRegister={() => navigate('/')}
          onContact={() => navigate('/')}
        />
      </DashboardShell>
    );
  }

  return (
    <PublicPlansV3
      onHome={() => navigate('/')}
      onLogin={() => navigate('/')}
      onRegister={() => navigate('/')}
      onSelectOffer={() => navigate('/')}
      onContact={() => navigate('/')}
    />
  );
}
