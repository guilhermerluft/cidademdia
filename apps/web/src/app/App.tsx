import { useState } from 'react';
import type { FormEvent } from 'react';
import { Brand, Button, Card } from '../components/ui';
import { useAuth } from '../modules/auth/AuthProvider';
import * as authService from '../modules/auth/authService';

type AuthMode = 'login' | 'register' | 'forgot' | 'reset';

export function App() {
  const { status, user, login, register, logout } = useAuth();
  const initialResetToken = new URLSearchParams(window.location.search).get('token') ?? '';
  const [mode, setMode] = useState<AuthMode>(initialResetToken ? 'reset' : 'login');
  const [resetToken] = useState(initialResetToken);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

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

  if (status === 'authenticated' && user) {
    return (
      <main className="auth-shell">
        <Card className="auth-card auth-card--compact" elevated>
          <Brand className="auth-brand" />
          <span className="auth-kicker">Homologação</span>
          <h1>Olá, {user.displayName}</h1>
          <p className="auth-muted">Sessão autenticada com access token em memória e refresh seguro por cookie.</p>

          <dl className="auth-profile">
            <div>
              <dt>E-mail</dt>
              <dd>{user.email}</dd>
            </div>
            <div>
              <dt>Perfil</dt>
              <dd>{user.roles.join(', ') || 'Sem perfil'}</dd>
            </div>
          </dl>

          <Button variant="soft" fullWidth onClick={() => void logout()}>
            Sair
          </Button>
        </Card>
      </main>
    );
  }

  function changeMode(nextMode: AuthMode) {
    setMode(nextMode);
    setError(null);
    setMessage(null);
    setPassword('');
    setConfirmPassword('');
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

      if (!resetToken) {
        setError('Link de redefinição inválido. Solicite um novo e-mail.');
        return;
      }

      if (password !== confirmPassword) {
        setError('As senhas não coincidem.');
        return;
      }

      await authService.resetPassword({ token: resetToken, newPassword: password });
      window.history.replaceState({}, document.title, '/');
      setMode('login');
      setPassword('');
      setConfirmPassword('');
      setMessage('Senha redefinida com sucesso. Entre com a nova senha.');
    } catch {
      if (mode === 'register') {
        setError('Não foi possível criar a conta. Confira os dados e tente novamente.');
      } else if (mode === 'login') {
        setError('E-mail ou senha inválidos.');
      } else if (mode === 'forgot') {
        setError('Não foi possível processar a solicitação agora. Tente novamente em instantes.');
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
        : 'Crie uma nova senha';

  const description = mode === 'login'
    ? 'Entre para acompanhar ocorrências e acessar os recursos do seu perfil.'
    : mode === 'register'
      ? 'Cadastre-se para começar a utilizar a nova plataforma.'
      : mode === 'forgot'
        ? 'Informe seu e-mail. Se a conta existir, enviaremos um link seguro de redefinição.'
        : 'Escolha uma nova senha para concluir a recuperação da sua conta.';

  return (
    <main className="auth-shell">
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

        <form className="auth-form" onSubmit={handleSubmit}>
          {mode === 'register' && (
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

          {mode !== 'reset' && (
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

          {(mode === 'login' || mode === 'register' || mode === 'reset') && (
            <label>
              {mode === 'reset' ? 'Nova senha' : 'Senha'}
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

          {mode === 'reset' && (
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

          <Button type="submit" size="lg" fullWidth disabled={submitting}>
            {submitting
              ? 'Aguarde...'
              : mode === 'login'
                ? 'Entrar'
                : mode === 'register'
                  ? 'Criar conta'
                  : mode === 'forgot'
                    ? 'Enviar link'
                    : 'Redefinir senha'}
          </Button>

          {mode === 'login' && (
            <button className="auth-link" type="button" onClick={() => changeMode('forgot')}>
              Esqueci minha senha
            </button>
          )}

          {(mode === 'forgot' || mode === 'reset') && (
            <button className="auth-link" type="button" onClick={() => changeMode('login')}>
              Voltar para entrar
            </button>
          )}
        </form>
      </Card>
    </main>
  );
}
