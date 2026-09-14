import { useNavigate } from 'react-router-dom';
import { AppBottomNavigation, AppHeader } from '../../app/layout/AppHeader';
import { useNavigationAccess } from '../../app/layout/AppNavigation';
import { Brand } from '../../components/ui';
import { useAuth } from '../auth/AuthProvider';

export function AboutRoute() {
  const navigate = useNavigate();
  const { status, user, logout } = useAuth();
  const access = useNavigationAccess(status === 'authenticated' ? user : null);

  if (status === 'loading') {
    return (
      <>
        <AppHeader active="about" />
        <main className="about-page about-page--loading" aria-busy="true">
          <Brand />
          <span>Carregando informações do Cidademdia...</span>
        </main>
        <AppBottomNavigation active="about" />
      </>
    );
  }

  const authenticatedUser = status === 'authenticated' ? user : null;
  const login = authenticatedUser ? undefined : () => navigate('/?auth=login');
  const register = authenticatedUser ? undefined : () => navigate('/?auth=register');

  return (
    <div className="about-page-shell">
      <AppHeader
        active="about"
        user={authenticatedUser}
        permissions={access.permissions}
        onLogout={authenticatedUser ? logout : undefined}
        onLogin={login}
        onRegister={register}
      />

      <main className="about-page">
        <section className="about-page__hero" aria-labelledby="about-page-title">
          <span className="about-page__eyebrow">Sobre a plataforma</span>
          <h1 id="about-page-title">
            <span className="about-page__hero-title-line">
              <span className="about-page__hero-title-white">Cidademdia:</span>{' '}
              <span className="about-page__hero-title-green">participação cidadã</span>
            </span>
            <span className="about-page__hero-title-line">
              <span className="about-page__hero-title-white">para aproximar</span>{' '}
              <span className="about-page__hero-title-blue">quem precisa</span>
            </span>
            <span className="about-page__hero-title-line">
              <span className="about-page__hero-title-white">de</span>{' '}
              <span className="about-page__hero-title-lime">quem pode resolver</span>
            </span>
          </h1>
          <p>
            O Cidademdia é uma plataforma digital para registrar ocorrências urbanas, acompanhar demandas e aproximar
            cidadãos, órgãos, gestores e agentes públicos em uma experiência mais transparente e organizada.
          </p>
          <div className="about-page__actions">
            <a className="about-page__primary-link" href="/como-funciona">Entenda como funciona</a>
            <a className="about-page__secondary-link" href="/ocorrencias">Ver ocorrências públicas</a>
          </div>
        </section>

        <section className="about-page__section" aria-labelledby="about-purpose-title">
          <div className="about-page__heading">
            <span>Propósito</span>
            <h2 id="about-purpose-title">Transformar demandas urbanas em acompanhamento visível</h2>
          </div>
          <p>
            A proposta do Cidademdia é facilitar o caminho entre o registro de um problema e o acompanhamento da sua
            evolução. Cidadãos podem publicar ocorrências e consultar informações públicas, enquanto contas institucionais
            organizam o recebimento e o acompanhamento das demandas relacionadas à sua atuação.
          </p>
        </section>

        <section className="about-page__pillars" aria-label="Como o Cidademdia atua">
          <article>
            <span aria-hidden="true">01</span>
            <h2>Registrar</h2>
            <p>O cidadão informa a ocorrência, sua localização e as evidências disponíveis.</p>
          </article>
          <article>
            <span aria-hidden="true">02</span>
            <h2>Conectar</h2>
            <p>A demanda pode ser direcionada a contas e agentes capazes de acompanhar o contexto apresentado.</p>
          </article>
          <article>
            <span aria-hidden="true">03</span>
            <h2>Acompanhar</h2>
            <p>Status, histórico e informações da ocorrência permanecem organizados em um único fluxo.</p>
          </article>
        </section>

        <section className="about-page__section about-page__section--audience" aria-labelledby="about-audience-title">
          <div className="about-page__heading">
            <span>Para quem</span>
            <h2 id="about-audience-title">Uma plataforma para cidadãos e gestão pública</h2>
          </div>
          <div className="about-page__audience-grid">
            <article>
              <h3>Cidadãos</h3>
              <p>Para registrar situações da cidade, acompanhar ocorrências e consultar informações públicas.</p>
            </article>
            <article>
              <h3>Órgãos, gestores e agentes públicos</h3>
              <p>Para receber demandas, organizar acompanhamento, comunicar atualizações e estruturar equipes de atendimento.</p>
            </article>
          </div>
        </section>

        <section className="about-page__section about-page__section--contact" aria-labelledby="about-contact-title">
          <div className="about-page__heading">
            <span>Canais oficiais</span>
            <h2 id="about-contact-title">Fale com o Cidademdia</h2>
          </div>
          <div className="about-page__contact-grid">
            <a href="mailto:atendimento@cidademdia.com.br"><span>Atendimento</span>atendimento@cidademdia.com.br</a>
            <a href="mailto:ouvidoria@cidademdia.com.br"><span>Ouvidoria</span>ouvidoria@cidademdia.com.br</a>
            <a href="mailto:comercial@cidademdia.com.br"><span>Comercial</span>comercial@cidademdia.com.br</a>
          </div>
        </section>
      </main>

      <footer className="about-page__footer">
        <Brand compact />
        <p>Cidademdia — conectando cidadãos e quem pode resolver.</p>
      </footer>

      <AppBottomNavigation
        active="about"
        user={authenticatedUser}
        permissions={access.permissions}
        onLogin={login}
        onRegister={register}
      />
    </div>
  );
}
