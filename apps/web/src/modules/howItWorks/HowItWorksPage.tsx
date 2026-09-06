import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AppBottomNavigation, AppHeader } from '../../app/layout/AppHeader';
import { Button } from '../../components/ui';
import type { AuthenticatedUser } from '../auth/types';
import { HOW_IT_WORKS_GUIDE_IMAGE } from './assets/guideImage';

const HOW_IT_WORKS_VIDEO_URL = '/media/como-funciona.mp4';

type ContentView = 'both' | 'video' | 'guide';

interface HowItWorksPageProps {
  user?: AuthenticatedUser | null;
  permissions?: readonly string[];
  onLogin?: () => void;
  onRegister?: () => void;
  onLogout?: () => void | Promise<void>;
}

const VIEW_OPTIONS: Array<{
  id: ContentView;
  label: string;
  icon: string;
}> = [
  { id: 'both', label: 'Ver os dois', icon: 'fa-table-columns' },
  { id: 'video', label: 'Assistir vídeo', icon: 'fa-circle-play' },
  { id: 'guide', label: 'Passo a passo', icon: 'fa-list-check' },
];

export function HowItWorksPage({
  user,
  permissions = [],
  onLogin,
  onRegister,
  onLogout,
}: HowItWorksPageProps) {
  const navigate = useNavigate();
  const [view, setView] = useState<ContentView>('both');
  const [guideOpen, setGuideOpen] = useState(false);
  const [videoUnavailable, setVideoUnavailable] = useState(false);

  useEffect(() => {
    if (!guideOpen) return;

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') setGuideOpen(false);
    }

    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [guideOpen]);

  const showVideo = view !== 'guide';
  const showGuide = view !== 'video';

  return (
    <div className="how-it-works-page-shell">
      <AppHeader
        user={user}
        permissions={permissions}
        onLogin={onLogin}
        onRegister={onRegister}
        onLogout={onLogout}
      />

      <main className="how-it-works-page">
        <section className="how-it-works-page__hero" aria-labelledby="how-it-works-page-title">
          <div className="how-it-works-page__hero-copy">
            <span className="how-it-works-page__eyebrow">Como funciona</span>
            <h1 id="how-it-works-page-title">É simples fazer a diferença!</h1>
            <p>
              O CIDADEMDIA conecta você ao poder público de forma rápida, gratuita e segura.
              Veja no vídeo ou siga o passo a passo para registrar sua ocorrência.
            </p>
          </div>

          <div className="how-it-works-page__journey" aria-label="Fluxo do CIDADEMDIA">
            <div>
              <span className="how-it-works-page__journey-icon how-it-works-page__journey-icon--blue" aria-hidden="true">
                <i className="fa-solid fa-people-group" />
              </span>
              <strong>Você informa</strong>
            </div>
            <span className="how-it-works-page__journey-line" aria-hidden="true" />
            <div>
              <span className="how-it-works-page__journey-icon how-it-works-page__journey-icon--green" aria-hidden="true">
                <i className="fa-solid fa-gears" />
              </span>
              <strong>O órgão analisa</strong>
            </div>
            <span className="how-it-works-page__journey-line" aria-hidden="true" />
            <div>
              <span className="how-it-works-page__journey-icon how-it-works-page__journey-icon--yellow" aria-hidden="true">
                <i className="fa-solid fa-chart-column" />
              </span>
              <strong>A cidade resolve</strong>
            </div>
          </div>
        </section>

        <section className="how-it-works-page__content" aria-label="Tutorial Como funciona">
          <div className="how-it-works-page__view-switch" role="group" aria-label="Escolher formato do tutorial">
            {VIEW_OPTIONS.map((option) => (
              <button
                key={option.id}
                type="button"
                className={view === option.id
                  ? 'how-it-works-page__view-option how-it-works-page__view-option--active'
                  : 'how-it-works-page__view-option'}
                aria-pressed={view === option.id}
                onClick={() => setView(option.id)}
              >
                <i className={`fa-solid ${option.icon}`} aria-hidden="true" />
                <span>{option.label}</span>
              </button>
            ))}
          </div>

          <p className="how-it-works-page__view-hint">
            <i className="fa-solid fa-arrows-left-right" aria-hidden="true" />
            Você pode assistir ao vídeo, consultar o passo a passo ou explorar os dois juntos.
          </p>

          <div className={`how-it-works-page__cards how-it-works-page__cards--${view}`}>
            {showVideo && (
              <article className="how-it-works-page__card how-it-works-page__card--video">
                <header className="how-it-works-page__card-header">
                  <span className="how-it-works-page__card-icon how-it-works-page__card-icon--video" aria-hidden="true">
                    <i className="fa-solid fa-play" />
                  </span>
                  <div>
                    <h2>Veja em vídeo como registrar sua ocorrência</h2>
                    <p>Um tutorial rápido para aprender todo o fluxo em poucos minutos.</p>
                  </div>
                </header>

                <div className="how-it-works-page__video-frame">
                  {videoUnavailable ? (
                    <div className="how-it-works-page__video-unavailable" role="status">
                      <i className="fa-solid fa-circle-exclamation" aria-hidden="true" />
                      <strong>Vídeo temporariamente indisponível.</strong>
                      <span>O passo a passo ao lado continua disponível para consulta.</span>
                    </div>
                  ) : (
                    <video
                      src={HOW_IT_WORKS_VIDEO_URL}
                      controls
                      playsInline
                      preload="metadata"
                      onError={() => setVideoUnavailable(true)}
                      aria-label="Vídeo Como funciona do CIDADEMDIA"
                    >
                      Seu navegador não suporta reprodução de vídeo.
                    </video>
                  )}
                </div>

                <div className="how-it-works-page__card-copy">
                  <strong>Como registrar sua ocorrência no CIDADEMDIA</strong>
                  <span>Assista no seu ritmo, pause quando precisar e use a tela cheia para acompanhar cada etapa.</span>
                </div>

                <div className="how-it-works-page__video-meta" aria-label="Recursos do vídeo">
                  <span><i className="fa-regular fa-circle-play" aria-hidden="true" /> Tutorial em vídeo</span>
                  <span><i className="fa-solid fa-sliders" aria-hidden="true" /> Controles nativos</span>
                  <span><i className="fa-solid fa-expand" aria-hidden="true" /> Tela cheia</span>
                </div>
              </article>
            )}

            {showGuide && (
              <article className="how-it-works-page__card how-it-works-page__card--guide">
                <header className="how-it-works-page__card-header">
                  <span className="how-it-works-page__card-icon how-it-works-page__card-icon--guide" aria-hidden="true">
                    <i className="fa-solid fa-list-check" />
                  </span>
                  <div>
                    <h2>Veja o passo a passo em detalhes</h2>
                    <p>Consulte o infográfico completo enquanto registra sua ocorrência.</p>
                  </div>
                  <button
                    className="how-it-works-page__expand-button"
                    type="button"
                    onClick={() => setGuideOpen(true)}
                  >
                    <i className="fa-solid fa-expand" aria-hidden="true" />
                    Ampliar
                  </button>
                </header>

                <button
                  className="how-it-works-page__guide-preview"
                  type="button"
                  onClick={() => setGuideOpen(true)}
                  aria-label="Ampliar infográfico Como registrar sua ocorrência"
                >
                  <img
                    src={HOW_IT_WORKS_GUIDE_IMAGE}
                    alt="Infográfico Como registrar sua ocorrência no CIDADEMDIA em oito passos"
                  />
                  <span className="how-it-works-page__guide-zoom" aria-hidden="true">
                    <i className="fa-solid fa-magnifying-glass-plus" />
                    Clique para ampliar
                  </span>
                </button>
              </article>
            )}
          </div>
        </section>

        <section className="how-it-works-page__ready" aria-labelledby="how-it-works-ready-title">
          <span className="how-it-works-page__ready-icon" aria-hidden="true">
            <i className="fa-solid fa-check" />
          </span>
          <div>
            <span>Agora é com você</span>
            <h2 id="how-it-works-ready-title">Pronto para registrar e acompanhar sua ocorrência?</h2>
            <p>Você informa. O órgão analisa. O CIDADEMDIA ajuda você a acompanhar o andamento.</p>
          </div>
          <div className="how-it-works-page__ready-actions">
            {user ? (
              <Button onClick={() => navigate('/ocorrencias')}>Ver ocorrências</Button>
            ) : (
              <>
                {onRegister && <Button onClick={onRegister}>Criar conta</Button>}
                {onLogin && <Button variant="soft" onClick={onLogin}>Entrar</Button>}
              </>
            )}
          </div>
        </section>
      </main>

      <AppBottomNavigation
        user={user}
        permissions={permissions}
        onLogin={onLogin}
        onRegister={onRegister}
      />

      {guideOpen && (
        <div
          className="how-it-works-page__lightbox"
          role="presentation"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) setGuideOpen(false);
          }}
        >
          <section
            className="how-it-works-page__lightbox-dialog"
            role="dialog"
            aria-modal="true"
            aria-label="Infográfico Como registrar sua ocorrência ampliado"
          >
            <button
              className="how-it-works-page__lightbox-close"
              type="button"
              aria-label="Fechar infográfico ampliado"
              onClick={() => setGuideOpen(false)}
            >
              <i className="fa-solid fa-xmark" aria-hidden="true" />
            </button>
            <img
              src={HOW_IT_WORKS_GUIDE_IMAGE}
              alt="Infográfico Como registrar sua ocorrência no CIDADEMDIA em oito passos"
            />
          </section>
        </div>
      )}
    </div>
  );
}
