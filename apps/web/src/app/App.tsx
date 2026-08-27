import { useState } from 'react';
import type { FormEvent } from 'react';
import { useAuth } from '../modules/auth/AuthProvider';

type AuthMode = 'login' | 'register';

export function App() {
  const { status, user, login, register, logout } = useAuth();
  const [mode, setMode] = useState<AuthMode>('login');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (status === 'loading') {
    return (
      <main className="auth-shell">
        <section className="auth-card auth-card--compact">
          <span className="auth-eyebrow">CidadeEmDia</span>
          <h1>Carregando sessão</h1>
          <p>Validando sua sessão segura.</p>
        </section>
      </main>
    );
  }

  if (status === 'authenticated' && user) {
    return (
      <main className="auth-shell">
        <section className="auth-card auth-card--compact">
          <span className="auth-eyebrow">Homologação</span>
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

          <button className="auth-button auth-button--secondary" type="button" onClick={() => void logout()}>
            Sair
          </button>
        </section>
      </main>
    );
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);

    try {
      if (mode === 'register') {
        await register({ email, password, displayName });
      } else {
        await login({ email, password });
      }
    } catch {
      setError(mode === 'register'
        ? 'Não foi possível criar a conta. Confira os dados e tente novamente.'
        : 'E-mail ou senha inválidos.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="auth-shell">
      <section className="auth-card">
        <div className="auth-intro">
          <span className="auth-eyebrow">CidadeEmDia</span>
          <h1>{mode === 'login' ? 'Acesse sua conta' : 'Crie sua conta'}</h1>
          <p>
            {mode === 'login'
              ? 'Entre para acompanhar ocorrências e acessar os recursos do seu perfil.'
              : 'Cadastre-se para começar a utilizar a nova plataforma.'}
          </p>
        </div>

        <div className="auth-tabs" role="tablist" aria-label="Autenticação">
          <button
            className={mode === 'login' ? 'auth-tab auth-tab--active' : 'auth-tab'}
            type="button"
            onClick={() => {
              setMode('login');
              setError(null);
            }}
          >
            Entrar
          </button>
          <button
            className={mode === 'register' ? 'auth-tab auth-tab--active' : 'auth-tab'}
            type="button"
            onClick={() => {
              setMode('register');
              setError(null);
            }}
          >
            Criar conta
          </button>
        </div>

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

          <label>
            Senha
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

          {error && <p className="auth-error" role="alert">{error}</p>}

          <button className="auth-button" type="submit" disabled={submitting}>
            {submitting ? 'Aguarde...' : mode === 'login' ? 'Entrar' : 'Criar conta'}
          </button>
        </form>
      </section>
    </main>
  );
}
