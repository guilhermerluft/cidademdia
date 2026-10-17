import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { DashboardShell } from '../../app/layout/DashboardShell';
import { Brand } from '../../components/ui';
import { useAuth } from '../auth/AuthProvider';
import { createBillingCheckout, getBillingEntitlements } from './plansService';

type ReturnState = 'checking' | 'authorizing' | 'active' | 'pending' | 'error';

const MAX_ATTEMPTS = 8;
const POLL_DELAY_MS = 2_000;

function wait(ms: number) {
  return new Promise((resolve) => window.setTimeout(resolve, ms));
}

export function BillingReturnRoute() {
  const navigate = useNavigate();
  const { status, user, logout } = useAuth();
  const [returnState, setReturnState] = useState<ReturnState>('checking');

  useEffect(() => {
    if (status !== 'authenticated' || !user) return;

    let cancelled = false;

    async function confirmSubscription() {
      const params = new URLSearchParams(window.location.search);
      const stage = params.get('stage');
      const planVersionId = params.get('planVersionId');

      if (stage === 'signup' && planVersionId) {
        for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt += 1) {
          try {
            const checkout = await createBillingCheckout(planVersionId, true);

            if (cancelled) return;

            if (checkout.checkoutUrl) {
              setReturnState('authorizing');
              window.location.assign(checkout.checkoutUrl);
              return;
            }
          } catch (error) {
            const errorCode = (error as { response?: { data?: { error?: string } } })
              ?.response?.data?.error;

            if (errorCode !== 'signup_fee_pending') {
              console.error('Falha ao continuar assinatura após taxa de adesão.', error);
              if (!cancelled) setReturnState('error');
              return;
            }
          }

          if (attempt < MAX_ATTEMPTS) {
            await wait(POLL_DELAY_MS);
          }
        }

        if (!cancelled) {
          setReturnState('pending');
        }
        return;
      }

      for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt += 1) {
        try {
          const billing = await getBillingEntitlements();

          if (cancelled) return;

          if (billing.accessAllowed) {
            setReturnState('active');
            return;
          }
        } catch (error) {
          console.error('Falha ao consultar assinatura após retorno do Mercado Pago.', error);
        }

        if (attempt < MAX_ATTEMPTS) {
          await wait(POLL_DELAY_MS);
        }
      }

      if (!cancelled) {
        setReturnState('pending');
      }
    }

    void confirmSubscription();

    return () => {
      cancelled = true;
    };
  }, [status, user]);

  if (status === 'loading') {
    return (
      <main className="plans-page plans-page__route-loading" aria-busy="true">
        <Brand />
        <span>Confirmando seu pagamento...</span>
      </main>
    );
  }

  if (status !== 'authenticated' || !user) {
    return (
      <main className="plans-page plans-page__route-loading">
        <Brand />
        <h1>Retorno do pagamento</h1>
        <p>Entre novamente para consultar o status da sua assinatura.</p>
        <button type="button" onClick={() => navigate('/?auth=login')}>
          Entrar
        </button>
      </main>
    );
  }

  return (
    <DashboardShell user={user} onLogout={logout}>
      <main className="plans-page plans-page__route-loading" aria-live="polite">
        <Brand />

        {returnState === 'checking' && (
          <>
            <h1>Confirmando sua assinatura</h1>
            <p>Recebemos o retorno do Mercado Pago e estamos aguardando a confirmação da cobrança.</p>
          </>
        )}

        {returnState === 'authorizing' && (
          <>
            <h1>Taxa de adesão confirmada</h1>
            <p>Agora vamos abrir a autorização da cobrança recorrente do seu plano.</p>
          </>
        )}

        {returnState === 'active' && (
          <>
            <h1>Assinatura ativada</h1>
            <p>Pagamento confirmado. Seu plano já está disponível no CIDADEMDIA.</p>
            <button type="button" onClick={() => navigate('/painel')}>
              Ir para o painel
            </button>
          </>
        )}

        {returnState === 'pending' && (
          <>
            <h1>Pagamento em processamento</h1>
            <p>
              O Mercado Pago ainda está processando a autorização. Assim que a confirmação chegar,
              sua assinatura será atualizada automaticamente.
            </p>
            <button type="button" onClick={() => navigate('/painel')}>
              Ir para o painel
            </button>
          </>
        )}

        {returnState === 'error' && (
          <>
            <h1>Não foi possível confirmar agora</h1>
            <p>Você pode acessar o painel e consultar sua assinatura novamente em alguns instantes.</p>
            <button type="button" onClick={() => navigate('/painel')}>
              Ir para o painel
            </button>
          </>
        )}
      </main>
    </DashboardShell>
  );
}
