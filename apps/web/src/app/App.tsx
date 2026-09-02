import { useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import { isAxiosError } from 'axios';
import { Brand, Button, Card } from '../components/ui';
import { useAuth } from '../modules/auth/AuthProvider';
import * as authService from '../modules/auth/authService';
import { PublicHome } from '../modules/home/PublicHome';
import {
  acceptSubaccountInvitation,
  listSubaccountContexts,
  previewSubaccountInvitation,
} from '../modules/subaccounts/subaccountService';
import {
  SUBACCOUNT_PERMISSION_OPTIONS,
  type SubaccountInvitationPreview,
} from '../modules/subaccounts/types';
import { DashboardHome } from './dashboard/DashboardHome';
import { DashboardShell } from './layout/DashboardShell';

type AuthMode = 'home' | 'login' | 'register' | 'forgot' | 'reset' | 'invite';

function readSensitiveTokens() {
  const url = new URL(window.location.href);
  const resetToken = url.searchParams.get('token') ?? '';
  const inviteToken = url.searchParams.get('invite') ?? '';

  if (resetToken || inviteToken) {
    url.searchParams.delete('token');
    url.searchParams.delete('invite');
    const nextUrl = `${url.pathname}${url.search}${url.hash}`;
    window.history.replaceState({}, document.title, nextUrl || '/');
  }

  return { resetToken, inviteToken };
}

function invitationPermissionLabel(key: string) {
  return SUBACCOUNT_PERMISSION_OPTIONS.find((item) => item.key === key)?.label ?? key;
}

function getInvitationErrorMessage(error: unknown) {
  if (isAxiosError(error)) {
    const data = error.response?.data as { error?: string } | undefined;
    switch (data?.error) {
      case 'email_already_registered':
        return 'Este e-mail já possui uma conta. Entre normalmente e peça para a conta Master refazer o vínculo.';
      case 'subaccount_limit_reached':
        return 'A conta Master atingiu o limite de subcontas antes da conclusão deste convite.';
      case 'master_unavailable':
        return 'A conta Master que enviou este convite não está disponível.';
      case 'invalid_or_expired_invitation':
        return 'Este convite é inválido, já foi utilizado ou expirou.';
    }
  }

  return 'Não foi possível aceitar o convite agora. Solicite um novo convite à conta Master.';
}

function AuthBrandPanel() {
  return (
    <aside className="auth-brand-panel" aria-label="CidadeEmDia">
      <div className="auth-brand-panel__content">
        <Brand className="auth-brand-panel__brand" />
        <span className="auth-brand-panel__eyebrow">Conectando cidadãos e gestão pública</span>
        <span className="auth-brand-panel__accent" aria-hidden="true" />
        <h2>Uma cidade melhor começa quando quem precisa é ouvido por quem pode resolver.</h2>
        <p>
          Registre demandas, acompanhe ocorrências e aproxime cidadãos e gestores em uma experiência simples,
          transparente e conectada.
        </p>
      </div>
    </aside>
  );
}

export function App() {
  const { status, user, login, register, logout } = useAuth();
  const [initialTokens] = useState(readSensitiveTokens);
  const [mode, setMode] = useState<AuthMode>(
    initialTokens.inviteToken ? 'invite' : initialTokens.resetToken ? 'reset' : 'home',
  );
  const [resetToken] = useState(initialTokens.resetToken);
  const [inviteToken] = useState(initialTokens.inviteToken);
  const [invitePreview, setInvitePreview] = useState<SubaccountInvitationPreview | null>(null);
  const [inviteLoading, setInviteLoading] = useState(Boolean(initialTokens.inviteToken));
  const [subaccountAccessRevoked, setSubaccountAccessRevoked] = useState(false);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    if (mode !== 'invite' || !inviteToken) return;

    let active = true;
    setInviteLoading(true);
    setError(null);

    void previewSubaccountInvitation(inviteToken)
      .then((preview) => {
        if (!active) return;
        setInvitePreview(preview);
      })
      .catch((requestError) => {
        if (!active) return;
        setInvitePreview(null);
        setError(getInvitationErrorMessage(requestError));
      })
      .finally(() => {
        if (active) setInviteLoading(false);
      });

    return () => {
      active = false;
    };
  }, [inviteToken, mode]);

  useEffect(() => {
    const shouldWatchSubaccountAccess =
      status === 'authenticated' &&
      user?.roles.includes('SUBACCOUNT') &&
      !user.roles.includes('MASTER') &&
      !user.roles.includes('ADMIN') &&
      mode !== 'invite';

    if (!shouldWatchSubaccountAccess) {
      setSubaccountAccessRevoked(false);
      return;
    }

    let active = true;

    async function verifyAccess() {
      try {
        const contexts = await listSubaccountContexts();
        if (active) setSubaccountAccessRevoked(contexts.length === 0);
      } catch {
        // A falha de rede não deve derrubar a sessão nem presumir revogação.
      }
    }

    const handleFocus = () => void verifyAccess();
    void verifyAccess();
    const intervalId = window.setInterval(() => void verifyAccess(), 15000);
    window.addEventListener('focus', handleFocus);

    return () => {
      active = false;
      window.clearInterval(intervalId);
      window.removeEventListener('focus', handleFocus);
    };
  }, [mode, status, user]);

  const dashboardUser = useMemo(() => {
    if (!user || !subaccountAccessRevoked) return user;

    return {
      ...user,
      roles: user.roles.filter((role) => role !== 'SUBACCOUNT'),
    };
  }, [subaccountAccessRevoked, user]);

  if (status === 'loading') {
    return (
      <main className="auth-shell">
        <Card className="auth-card auth-card--compact" elevated aria-busy="true">
          <Brand className="auth-brand" />
          <span className="auth-kicker">Sessão segura</span>
          <h1>Carregando sessão</h1>
          <p>Validando seu acesso à plataforma CidadeEmDia.</p>
        </Card>
      </main>
    );
  }

  if (status === 'authenticated' && dashboardUser && mode !== 'invite') {
    return (
      <DashboardShell user={dashboardUser} onLogout={logout}>
        <DashboardHome user={dashboardUser} />
      </DashboardShell>
    );
  }

  function changeMode(nextMode: AuthMode) {
    setMode(nextMode);
    setError(null);
    setMessage(null);
    setPassword('');
    setConfirmPassword('');
  }

  if (mode === 'home') {
    return (
      <PublicHome
        onLogin={() => changeMode('login')}
        onRegister={() => changeMode('register')}
      />
    );
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    setMessage(null);

    try {
      if (mode === 'register') {
        await register({ email, password, displayName });
        return;
      }

      if (mode === 'login') {
        await login({ email, password });
        return;
      }

      if (mode === 'forgot') {
        await authService.requestPasswordReset({ email });
        setMessage('Se existir uma conta com esse e-mail, enviaremos um link para redefinir a senha.');
        return;
      }

      if (mode === 'invite') {
        if (!inviteToken || !invitePreview) {
          setError('Este convite não está disponível. Solicite um novo convite à conta Master.');
          return;
        }

        if (password !== confirmPassword) {
          setError('As senhas não coincidem.');
          return;
        }

        await acceptSubaccountInvitation({ token: inviteToken, password, displayName });
        setMode('login');
        setPassword('');
        setConfirmPassword('');
        setDisplayName('');
        setMessage('Conta criada e convite aceito. Entre com seu e-mail e a senha que você acabou de definir.');
        setEmail(invitePreview.email);
        return;
      }

      if (!resetToken) {
        setError('Link de redefinição inválido. Solicite um novo e-mail.');
        return;
      }

      if (password !== confirmPassword) {
        setError('As senhas não coincidem.');
        return;
      }

      await authService.resetPassword({ token: resetToken, newPassword: password });
      setMode('login');
      setPassword('');
      setConfirmPassword('');
      setMessage('Senha redefinida com sucesso. Entre com a nova senha.');
    } catch (requestError) {
      if (mode === 'register') {
        setError('Não foi possível criar a conta. Confira os dados e tente novamente.');
      } else if (mode === 'login') {
        setError('E-mail ou senha inválidos.');
      } else if (mode === 'forgot') {
        setError('Não foi possível processar a solicitação agora. Tente novamente em instantes.');
      } else if (mode === 'invite') {
        setError(getInvitationErrorMessage(requestError));
      } else {
        setError('O link é inválido ou expirou. Solicite uma nova redefinição de senha.');
      }
    } finally {
      setSubmitting(false);
    }
  }

  const title = mode === 'login'
    ? 'Acesse sua conta'
    : mode === 'register'
      ? 'Crie sua conta'
      : mode === 'forgot'
        ? 'Recupere sua senha'
        : mode === 'invite'
          ? 'Aceite o convite da equipe'
          : 'Crie uma nova senha';

  const description = mode === 'login'
    ? 'Entre para acompanhar ocorrências e acessar os recursos do seu perfil.'
    : mode === 'register'
      ? 'Cadastre-se para começar a utilizar a nova plataforma.'
      : mode === 'forgot'
        ? 'Informe seu e-mail. Se a conta existir, enviaremos um link seguro de redefinição.'
        : mode === 'invite'
          ? 'Crie sua conta para entrar na equipe com as permissões definidas pela conta Master.'
          : 'Escolha uma nova senha para concluir a recuperação da sua conta.';

  return (
    <main className="auth-shell">
      <div className="auth-layout">
        <AuthBrandPanel />

        <Card className="auth-card" elevated>
          <Brand className="auth-brand" />

          <div className="auth-intro">
            <span className="auth-kicker">Conectando cidadãos e gestão pública</span>
            <h1>{title}</h1>
            <p>{description}</p>
          </div>

          {(mode === 'login' || mode === 'register') && (
            <div className="auth-tabs" role="tablist" aria-label="Autenticação">
              <button
                className={mode === 'login' ? 'auth-tab auth-tab--active' : 'auth-tab'}
                type="button"
                role="tab"
                aria-selected={mode === 'login'}
                onClick={() => changeMode('login')}
              >
                Entrar
              </button>
              <button
                className={mode === 'register' ? 'auth-tab auth-tab--active' : 'auth-tab'}
                type="button"
                role="tab"
                aria-selected={mode === 'register'}
                onClick={() => changeMode('register')}
              >
                Criar conta
              </button>
            </div>
          )}

          {mode === 'invite' && inviteLoading && (
            <p className="auth-muted" role="status">Validando convite...</p>
          )}

          {mode === 'invite' && invitePreview && (
            <dl className="auth-profile">
              <div>
                <dt>Conta Master</dt>
                <dd>{invitePreview.masterDisplayName}</dd>
              </div>
              <div>
                <dt>E-mail convidado</dt>
                <dd>{invitePreview.email}</dd>
              </div>
              <div>
                <dt>Permissões iniciais</dt>
                <dd>{invitePreview.permissions.length > 0
                  ? invitePreview.permissions.map(invitationPermissionLabel).join(', ')
                  : 'Sem permissões operacionais'}</dd>
              </div>
            </dl>
          )}

          <form className="auth-form" onSubmit={handleSubmit}>
            {(mode === 'register' || mode === 'invite') && (
              <label>
                Nome
                <input
                  autoComplete="name"
                  minLength={2}
                  maxLength={160}
                  required
                  value={displayName}
                  onChange={(event) => setDisplayName(event.target.value)}
                />
              </label>
            )}

            {mode !== 'reset' && mode !== 'invite' && (
              <label>
                E-mail
                <input
                  autoComplete="email"
                  type="email"
                  required
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                />
              </label>
            )}

            {(mode === 'login' || mode === 'register' || mode === 'reset' || mode === 'invite') && (
              <label>
                {mode === 'reset' || mode === 'invite' ? 'Nova senha' : 'Senha'}
                <input
                  autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
                  type="password"
                  minLength={8}
                  maxLength={128}
                  required
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                />
              </label>
            )}

            {(mode === 'reset' || mode === 'invite') && (
              <label>
                Confirme a nova senha
                <input
                  autoComplete="new-password"
                  type="password"
                  minLength={8}
                  maxLength={128}
                  required
                  value={confirmPassword}
                  onChange={(event) => setConfirmPassword(event.target.value)}
                />
              </label>
            )}

            {message && <p className="auth-success" role="status">{message}</p>}
            {error && <p className="auth-error" role="alert">{error}</p>}

            <Button
              type="submit"
              size="lg"
              fullWidth
              disabled={submitting || (mode === 'invite' && (inviteLoading || !invitePreview))}
            >
              {submitting
                ? 'Aguarde...'
                : mode === 'login'
                  ? 'Entrar'
                  : mode === 'register'
                    ? 'Criar conta'
                    : mode === 'forgot'
                      ? 'Enviar link'
                      : mode === 'invite'
                        ? 'Criar conta e aceitar convite'
                        : 'Redefinir senha'}
            </Button>

            {mode === 'login' && (
              <button className="auth-link" type="button" onClick={() => changeMode('forgot')}>
                Esqueci minha senha
              </button>
            )}

            {(mode === 'forgot' || mode === 'reset' || mode === 'invite') && (
              <button className="auth-link" type="button" onClick={() => changeMode('login')}>
                Voltar para entrar
              </button>
            )}

            {(mode === 'login' || mode === 'register') && (
              <button className="auth-link" type="button" onClick={() => changeMode('home')}>
                Voltar para a página inicial
              </button>
            )}
          </form>
        </Card>
      </div>
    </main>
  );
}
