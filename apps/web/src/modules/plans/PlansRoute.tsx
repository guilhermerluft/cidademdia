import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AppBottomNavigation, AppHeader } from '../../app/layout/AppHeader';
import { DashboardShell } from '../../app/layout/DashboardShell';
import { Brand } from '../../components/ui';
import { toast } from '../../components/toast';
import { useAuth } from '../auth/AuthProvider';
import { PublicPlans } from './PublicPlans';
import { createBillingCheckout, type PublicPlanOffer } from './plansService';

export function PlansRoute() {
  const navigate = useNavigate();
  const { status, user, logout } = useAuth();
  const [checkoutPlanVersionId, setCheckoutPlanVersionId] = useState<string | null>(null);

  async function handleCheckout(offer: PublicPlanOffer) {
    if (checkoutPlanVersionId) return;

    setCheckoutPlanVersionId(offer.planVersionId);

    try {
      const checkout = await createBillingCheckout(offer.planVersionId);

      if (!checkout.checkoutUrl) {
        throw new Error('checkout_url_missing');
      }

      window.location.assign(checkout.checkoutUrl);
    } catch (error) {
      console.error('Falha ao iniciar checkout do Mercado Pago.', error);
      toast.error('Não foi possível iniciar o pagamento agora. Tente novamente em instantes.');
      setCheckoutPlanVersionId(null);
    }
  }

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
          onSelectOffer={handleCheckout}
          onContact={() => navigate('/')}
        />
      </DashboardShell>
    );
  }

  return (
    <>
      <AppHeader
        active="plans"
        onLogin={() => navigate('/?auth=login')}
        onRegister={() => navigate('/?auth=register')}
      />
      <PublicPlans
        onSelectOffer={() => navigate('/?auth=register')}
        onContact={() => navigate('/')}
      />
      <AppBottomNavigation
        active="plans"
        onLogin={() => navigate('/?auth=login')}
        onRegister={() => navigate('/?auth=register')}
      />
    </>
  );
}
