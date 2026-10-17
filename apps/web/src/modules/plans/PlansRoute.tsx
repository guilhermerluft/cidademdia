import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AppBottomNavigation, AppHeader } from '../../app/layout/AppHeader';
import { DashboardShell } from '../../app/layout/DashboardShell';
import { Brand } from '../../components/ui';
import { toast } from '../../components/toast';
import { useAuth } from '../auth/AuthProvider';
import { PublicPlans } from './PublicPlans';
import {
  changeBillingPlan,
  createBillingCheckout,
  getBillingEntitlements,
  type BillingEntitlements,
  type PublicPlanOffer,
} from './plansService';

function formatDate(value?: string | null) {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('pt-BR').format(date);
}

function subscriptionStatusLabel(status?: string | null) {
  switch ((status ?? '').toLowerCase()) {
    case 'active': return 'Ativo';
    case 'pending': return 'Ativação em andamento';
    case 'pastdue':
    case 'past_due': return 'Pagamento pendente';
    case 'canceled': return 'Cancelado';
    default: return status || 'Indisponível';
  }
}

export function PlansRoute() {
  const navigate = useNavigate();
  const { status, user, logout } = useAuth();
  const [checkoutPlanVersionId, setCheckoutPlanVersionId] = useState<string | null>(null);
  const [billing, setBilling] = useState<BillingEntitlements | null>(null);
  const [billingLoading, setBillingLoading] = useState(false);

  async function refreshBilling() {
    if (status !== 'authenticated' || !user) {
      setBilling(null);
      return;
    }

    setBillingLoading(true);
    try {
      setBilling(await getBillingEntitlements());
    } catch (error) {
      const code = (error as { response?: { status?: number } })?.response?.status;
      if (code === 404) {
        setBilling(null);
      } else {
        console.error('Falha ao consultar assinatura atual.', error);
      }
    } finally {
      setBillingLoading(false);
    }
  }

  useEffect(() => {
    void refreshBilling();
  }, [status, user?.id]);

  async function handleOfferSelection(offer: PublicPlanOffer) {
    if (checkoutPlanVersionId) return;

    if (billing?.planVersionId === offer.planVersionId) return;

    if (billing && billing.subscriptionStatus.toLowerCase() !== 'active') {
      toast.info('Sua assinatura atual ainda está em ativação. Aguarde a confirmação antes de alterar o plano.');
      return;
    }

    setCheckoutPlanVersionId(offer.planVersionId);

    try {
      if (billing) {
        const change = await changeBillingPlan(offer.planVersionId);

        if (change.requiresAuthorization && change.checkoutUrl) {
          window.location.assign(change.checkoutUrl);
          return;
        }

        const effectiveAt = change.effectiveAt ? formatDate(change.effectiveAt) : null;
        toast.success(
          effectiveAt
            ? `Alteração de plano programada para ${effectiveAt}.`
            : 'Alteração de plano registrada com sucesso.',
        );
        await refreshBilling();
        return;
      }

      const checkout = await createBillingCheckout(offer.planVersionId);

      if (!checkout.checkoutUrl) {
        throw new Error('checkout_url_missing');
      }

      window.location.assign(checkout.checkoutUrl);
    } catch (error) {
      console.error('Falha ao processar plano no Mercado Pago.', error);
      const errorCode = (error as { response?: { data?: { error?: string } } })
        ?.response?.data?.error;

      if (errorCode === 'plan_already_active') {
        toast.info('Este já é o seu plano atual.');
      } else if (errorCode === 'plan_change_authorization_pending') {
        toast.info('Já existe uma alteração de plano aguardando autorização.');
      } else if (errorCode === 'cancellation_pending') {
        toast.info('Há um cancelamento programado. Reative a assinatura antes de alterar o plano.');
      } else {
        toast.error('Não foi possível processar a alteração agora. Tente novamente em instantes.');
      }
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
        <div className="plans-page plans-page--embedded">
          <div className="plans-page__account-plan-wrap">
            {billingLoading && !billing ? (
              <section className="plans-page__account-plan plans-page__account-plan--loading" aria-busy="true">
                <span>Consultando sua assinatura...</span>
              </section>
            ) : billing ? (
              <section className="plans-page__account-plan" aria-label="Seu plano atual">
                <div className="plans-page__account-plan-title">
                  <span className="plans-page__account-plan-icon"><i className="fa-solid fa-circle-check" /></span>
                  <div>
                    <small>Seu plano atual</small>
                    <h1>{billing.planName} · {billing.categoryName}</h1>
                  </div>
                  <span className={`plans-page__account-plan-status plans-page__account-plan-status--${billing.subscriptionStatus.toLowerCase()}`}>
                    {subscriptionStatusLabel(billing.subscriptionStatus)}
                  </span>
                </div>

                <div className="plans-page__account-plan-grid">
                  <div><small>Próxima renovação</small><strong>{formatDate(billing.currentPeriodEnd)}</strong></div>
                  <div><small>Subcontas incluídas</small><strong>{billing.subaccountLimit}</strong></div>
                  <div><small>Publicações no mês</small><strong>{billing.usedPublications}/{billing.monthlyPublicationLimit}</strong></div>
                  <div><small>Situação</small><strong>{billing.cancelAtPeriodEnd ? 'Cancelamento programado' : 'Renovação ativa'}</strong></div>
                </div>

                {billing.pendingPlanVersionId && (
                  <p className="plans-page__account-plan-note">
                    <i className="fa-solid fa-clock" /> Há uma alteração de plano programada para o próximo ciclo.
                  </p>
                )}
              </section>
            ) : (
              <section className="plans-page__account-plan plans-page__account-plan--empty">
                <div>
                  <small>Assinatura</small>
                  <h1>Você ainda não possui um plano ativo</h1>
                  <p>Escolha uma das opções abaixo para começar.</p>
                </div>
              </section>
            )}
          </div>

          <PublicPlans
            embedded
            currentBilling={billing}
            changingPlanVersionId={checkoutPlanVersionId}
            onSelectOffer={handleOfferSelection}
            onContact={() => navigate('/')}
          />
        </div>
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
