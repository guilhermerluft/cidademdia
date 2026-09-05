import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AppBottomNavigation, AppHeader } from '../../app/layout/AppHeader';
import { Brand, Button } from '../../components/ui';
import type { AuthenticatedUser } from '../auth/types';
import { PublicOccurrenceCard } from '../occurrences/PublicOccurrenceCard';
import { PublicOccurrenceDetailsModal } from '../occurrences/PublicOccurrenceDetailsModal';
import {
  DEFAULT_PUBLIC_OCCURRENCE_CITY,
  DEFAULT_PUBLIC_OCCURRENCE_RADIUS_KM,
  requestBrowserCoordinates,
} from '../occurrences/publicOccurrenceLocation';
import { listPlacementPosts } from '../posts/postService';
import type { PostItem } from '../posts/types';
import { HomeAccountModules } from './HomeAccountModules';
import { HowItWorksModal } from './HowItWorksModal';
import {
  getPublicOccurrenceDetails,
  listPublicOccurrences,
  listPublicPlans,
  type PublicOccurrenceDetails,
  type PublicOccurrenceItem,
  type PublicPlanOffer,
} from './homeService';

interface PublicHomeProps {
  user?: AuthenticatedUser | null;
  permissions?: readonly string[];
  onLogin?: () => void;
  onRegister?: () => void;
  onLogout?: () => void | Promise<void>;
}

function formatMoney(valueInCents: number) {
  return new Intl.NumberFormat('pt-BR', {
    style: 'currency',
    currency: 'BRL',
  }).format(valueInCents / 100);
}

function useSlidesPerView() {
  const [slides, setSlides] = useState(3);

  useEffect(() => {
    const update = () => {
      if (window.innerWidth <= 720) setSlides(1);
      else if (window.innerWidth <= 1080) setSlides(2);
      else setSlides(3);
    };

    update();
    window.addEventListener('resize', update);
    return () => window.removeEventListener('resize', update);
  }, []);

  return slides;
}

function VideoCard({ post }: { post: PostItem }) {
  const media = post.media.find((item) =>
    item.status === 'ready'
    && item.readUrl
    && item.contentType.startsWith('video/'));
  const [playing, setPlaying] = useState(false);
  const [duration, setDuration] = useState<string>('Vídeo');

  if (!media?.readUrl) return null;

  function handleMetadata(event: React.SyntheticEvent<HTMLVideoElement>) {
    const seconds = event.currentTarget.duration;
    if (!Number.isFinite(seconds)) return;

    const minutes = Math.floor(seconds / 60);
    const remainder = Math.floor(seconds % 60).toString().padStart(2, '0');
    setDuration(`${minutes}:${remainder}`);
  }

  return (
    <article className="public-home__media-card">
      <div className="public-home__media-cover">
        <video
          src={media.readUrl}
          muted={!playing}
          playsInline
          preload="metadata"
          controls={playing}
          autoPlay={playing}
          onLoadedMetadata={handleMetadata}
          onEnded={() => setPlaying(false)}
          aria-label={post.title || 'Vídeo do CidadeEmDia'}
        />
        {!playing && (
          <button
            className="public-home__play"
            type="button"
            aria-label={`Reproduzir ${post.title || 'vídeo'}`}
            onClick={() => setPlaying(true)}
          >
            <span aria-hidden="true">▶</span>
          </button>
        )}
        <div className="public-home__media-overlay" aria-hidden="true" />
        <div className="public-home__media-caption">
          <h3>{post.title || 'CIDADEMDIA em movimento'}</h3>
          <span>{duration}</span>
        </div>
      </div>
    </article>
  );
}

function PlanCard({ offer, onStart }: { offer: PublicPlanOffer; onStart: () => void }) {
  const intervalLabel = offer.billingIntervalMonths === 1
    ? 'mensal'
    : offer.billingIntervalMonths === 3
      ? 'trimestral'
      : offer.billingIntervalMonths === 6
        ? 'semestral'
        : offer.billingIntervalMonths === 12
          ? 'anual'
          : `a cada ${offer.billingIntervalMonths} meses`;

  return (
    <article className="public-home__plan-card">
      <span>{offer.categoryName}</span>
      <h3>{offer.planName}</h3>
      <strong>{formatMoney(offer.priceCents)}</strong>
      <small>{intervalLabel}</small>
      <ul>
        <li>{offer.subaccountLimit} subconta{offer.subaccountLimit === 1 ? '' : 's'}</li>
        <li>{offer.monthlyPublicationLimit} publicaç{offer.monthlyPublicationLimit === 1 ? 'ão' : 'ões'} por mês</li>
      </ul>
      <Button variant="soft" onClick={onStart}>Começar</Button>
    </article>
  );
}

export function PublicHome({
  user,
  permissions = [],
  onLogin,
  onRegister,
  onLogout,
}: PublicHomeProps) {
  const navigate = useNavigate();
  const [posts, setPosts] = useState<PostItem[]>([]);
  const [postsLoading, setPostsLoading] = useState(true);
  const [postsUnavailable, setPostsUnavailable] = useState(false);
  const [mediaPage, setMediaPage] = useState(0);
  const [showAllMedia, setShowAllMedia] = useState(false);
  const [occurrences, setOccurrences] = useState<PublicOccurrenceItem[]>([]);
  const [occurrencesLoading, setOccurrencesLoading] = useState(true);
  const [occurrencesUnavailable, setOccurrencesUnavailable] = useState(false);
  const [occurrenceLocationLabel, setOccurrenceLocationLabel] = useState(DEFAULT_PUBLIC_OCCURRENCE_CITY);
  const [showAllOccurrences, setShowAllOccurrences] = useState(false);
  const [selectedOccurrence, setSelectedOccurrence] = useState<PublicOccurrenceDetails | null>(null);
  const [detailLoadingId, setDetailLoadingId] = useState<string | null>(null);
  const [occurrenceDetailError, setOccurrenceDetailError] = useState<string | null>(null);
  const [plans, setPlans] = useState<PublicPlanOffer[]>([]);
  const [howItWorksOpen, setHowItWorksOpen] = useState(false);
  const slidesPerView = useSlidesPerView();

  useEffect(() => {
    let active = true;

    async function loadPosts() {
      setPostsLoading(true);
      setPostsUnavailable(false);

      try {
        const horizontal = await listPlacementPosts('horizontal', undefined, 12, 'platform');
        let officialVideos = horizontal.items.filter((post) =>
          post.masterUserId == null
          && post.media.some((media) =>
            media.status === 'ready'
            && media.readUrl
            && media.contentType.startsWith('video/')));

        if (officialVideos.length === 0) {
          const feed = await listPlacementPosts('feed', undefined, 12, 'platform');
          officialVideos = feed.items.filter((post) =>
            post.masterUserId == null
            && post.media.some((media) =>
              media.status === 'ready'
              && media.readUrl
              && media.contentType.startsWith('video/')));
        }

        if (active) setPosts(officialVideos);
      } catch {
        if (active) {
          setPosts([]);
          setPostsUnavailable(true);
        }
      } finally {
        if (active) setPostsLoading(false);
      }
    }

    void loadPosts();
    return () => { active = false; };
  }, []);

  useEffect(() => {
    let active = true;

    async function useFallbackCity() {
      try {
        const items = await listPublicOccurrences({ city: DEFAULT_PUBLIC_OCCURRENCE_CITY, limit: 6 });
        if (!active) return;
        setOccurrences(items);
        setOccurrenceLocationLabel(DEFAULT_PUBLIC_OCCURRENCE_CITY);
        setOccurrencesUnavailable(false);
      } catch {
        if (!active) return;
        setOccurrences([]);
        setOccurrenceLocationLabel(DEFAULT_PUBLIC_OCCURRENCE_CITY);
        setOccurrencesUnavailable(true);
      } finally {
        if (active) setOccurrencesLoading(false);
      }
    }

    async function useCoordinates(latitude: number, longitude: number) {
      try {
        const items = await listPublicOccurrences({
          latitude,
          longitude,
          radiusKm: DEFAULT_PUBLIC_OCCURRENCE_RADIUS_KM,
          limit: 6,
        });
        if (!active) return;
        setOccurrences(items);
        setOccurrenceLocationLabel('próximas a você');
        setOccurrencesUnavailable(false);
      } catch {
        await useFallbackCity();
      } finally {
        if (active) setOccurrencesLoading(false);
      }
    }

    setOccurrencesLoading(true);
    setOccurrencesUnavailable(false);

    void requestBrowserCoordinates().then((coordinates) => {
      if (!active) return;
      if (coordinates) {
        void useCoordinates(coordinates.latitude, coordinates.longitude);
      } else {
        void useFallbackCity();
      }
    });

    return () => { active = false; };
  }, []);

  useEffect(() => {
    let active = true;
    void listPublicPlans()
      .then((items) => { if (active) setPlans(items); })
      .catch(() => { if (active) setPlans([]); });
    return () => { active = false; };
  }, []);

  const mediaPages = useMemo(() => {
    const pages: PostItem[][] = [];
    for (let index = 0; index < posts.length; index += slidesPerView) {
      pages.push(posts.slice(index, index + slidesPerView));
    }
    return pages;
  }, [posts, slidesPerView]);

  useEffect(() => {
    if (mediaPage >= mediaPages.length) setMediaPage(0);
  }, [mediaPage, mediaPages.length]);

  const visibleOccurrences = showAllOccurrences ? occurrences : occurrences.slice(0, 3);
  const visiblePlans = plans.slice(0, 4);
  const handlePlanStart = user
    ? () => navigate('/planos')
    : (onRegister ?? (() => navigate('/planos')));

  function goToMediaPage(direction: number) {
    if (mediaPages.length <= 1) return;
    setMediaPage((current) => (current + direction + mediaPages.length) % mediaPages.length);
  }

  async function openOccurrence(occurrence: PublicOccurrenceItem) {
    if (detailLoadingId) return;
    setDetailLoadingId(occurrence.id);
    setOccurrenceDetailError(null);
    try {
      const details = await getPublicOccurrenceDetails(occurrence.id);
      setSelectedOccurrence(details);
    } catch {
      setOccurrenceDetailError('Não foi possível abrir os detalhes dessa ocorrência. Tente novamente.');
    } finally {
      setDetailLoadingId(null);
    }
  }

  return (
    <div className="public-home">
      <AppHeader
        active="home"
        user={user}
        permissions={permissions}
        onLogin={onLogin}
        onRegister={onRegister}
        onLogout={onLogout}
      />

      <main>
        <section className="public-home__hero" id="inicio" aria-labelledby="public-home-title">
          <div className="public-home__hero-inner">
            <div className="public-home__hero-copy">
              <h1 id="public-home-title">
                Uma cidade melhor<br />
                começa quando<br />
                quem precisa <span className="public-home__hero-green">é ouvido</span><br />
                por quem <span className="public-home__hero-orange">pode resolver.</span>
              </h1>
              <span className="public-home__hero-line" aria-hidden="true" />
              <p>
                O CIDADEMDIA conecta cidadãos e gestores, permitindo <strong>publicar ocorrências gratuitamente</strong> e acompanhar cada demanda,
                tornando a gestão mais ágil, transparente e eficiente.
              </p>

              <div className="public-home__hero-actions">
                <Button size="lg" onClick={() => setHowItWorksOpen(true)}>
                  <span className="public-home__cta-play" aria-hidden="true">▶</span>
                  Como funciona
                </Button>
                <button className="public-home__outline-cta" type="button" onClick={() => navigate('/planos')}>
                  Conheça os planos
                </button>
              </div>
            </div>

            <div className="public-home__hero-visual" aria-label="Cidade conectada pelo CidadeEmDia">
              <div className="public-home__sky" aria-hidden="true">
                <div className="public-home__sun" />
                <div className="public-home__building public-home__building--1" />
                <div className="public-home__building public-home__building--2" />
                <div className="public-home__building public-home__building--3" />
                <div className="public-home__building public-home__building--4" />
                <div className="public-home__building public-home__building--5" />
                <div className="public-home__tree-line" />
                <div className="public-home__water" />
              </div>

              <div className="public-home__phone">
                <div className="public-home__phone-speaker" aria-hidden="true" />
                <div className="public-home__phone-screen">
                  <Brand compact className="public-home__phone-brand" />
                  <div className="public-home__phone-summary">
                    <div><span className="blue">▣</span><small>Recebidas</small></div>
                    <div><span className="orange">◫</span><small>Em andamento</small></div>
                    <div><span className="green">↗</span><small>Encaminhadas</small></div>
                  </div>
                  <div className="public-home__phone-list">
                    <div><span className="blue">▣</span><strong>Recebidas</strong><b>›</b></div>
                    <div><span className="orange">◫</span><strong>Em andamento</strong><b>›</b></div>
                    <div><span className="green">↗</span><strong>Encaminhadas</strong><b>›</b></div>
                    <div><span className="resolved">✓</span><strong>Resolvidas</strong><b>›</b></div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section className="public-home__section public-home__media-section" id="midias">
          <div className="public-home__section-titlebar">
            <div className="public-home__section-title-wrap">
              <span className="public-home__section-icon public-home__section-icon--media" aria-hidden="true">▶</span>
              <div>
                <h2>Mídias do CIDADEMDIA</h2>
                <p>Acompanhe conteúdos e informações importantes.</p>
              </div>
            </div>
            {posts.length > 0 && (
              <button className="public-home__see-all" type="button" onClick={() => setShowAllMedia((value) => !value)}>
                {showAllMedia ? 'Ver carrossel' : 'Ver todas'} <span aria-hidden="true">›</span>
              </button>
            )}
          </div>

          {postsLoading ? (
            <div className="public-home__media-page" aria-busy="true">
              {[0, 1, 2].map((item) => <div className="public-home__media-skeleton" key={item} />)}
            </div>
          ) : posts.length === 0 ? (
            <div className="public-home__empty">
              <strong>{postsUnavailable ? 'Mídias temporariamente indisponíveis.' : 'Nenhum vídeo institucional publicado ainda.'}</strong>
            </div>
          ) : showAllMedia ? (
            <div className="public-home__media-all">
              {posts.map((post) => <VideoCard post={post} key={post.id} />)}
            </div>
          ) : (
            <div className="public-home__carousel">
              <button className="public-home__carousel-arrow public-home__carousel-arrow--left" type="button" onClick={() => goToMediaPage(-1)} aria-label="Mídias anteriores">‹</button>
              <div className="public-home__carousel-window">
                {mediaPages[mediaPage] && (
                  <div className="public-home__media-page">
                    {mediaPages[mediaPage].map((post) => <VideoCard post={post} key={post.id} />)}
                  </div>
                )}
              </div>
              <button className="public-home__carousel-arrow public-home__carousel-arrow--right" type="button" onClick={() => goToMediaPage(1)} aria-label="Próximas mídias">›</button>
            </div>
          )}

          {!showAllMedia && mediaPages.length > 1 && (
            <div className="public-home__dots" aria-label="Páginas de mídia">
              {mediaPages.map((_, index) => (
                <button
                  type="button"
                  key={index}
                  className={index === mediaPage ? 'public-home__dot public-home__dot--active' : 'public-home__dot'}
                  aria-label={`Ir para página ${index + 1}`}
                  aria-current={index === mediaPage ? 'true' : undefined}
                  onClick={() => setMediaPage(index)}
                />
              ))}
            </div>
          )}
        </section>

        <section className="public-home__section public-home__occurrences-section" id="ocorrencias">
          <div className="public-home__section-titlebar">
            <div className="public-home__section-title-wrap">
              <span className="public-home__section-icon public-home__section-icon--occurrence" aria-hidden="true">☷</span>
              <div>
                <h2>Ocorrências</h2>
                <p>Últimas demandas abertas {occurrenceLocationLabel === 'próximas a você' ? 'próximas à sua localização.' : `em ${occurrenceLocationLabel}.`}</p>
              </div>
            </div>
            {occurrences.length > 3 && (
              <button className="public-home__see-all" type="button" onClick={() => navigate('/ocorrencias')}>
                Ver todas <span aria-hidden="true">›</span>
              </button>
            )}
          </div>

          {occurrencesLoading ? (
            <div className="public-home__occurrence-list" aria-busy="true">
              {[0, 1, 2].map((item) => <div className="public-home__occurrence-skeleton" key={item} />)}
            </div>
          ) : visibleOccurrences.length > 0 ? (
            <div className="public-home__occurrence-list" aria-busy={detailLoadingId ? 'true' : undefined}>
              {visibleOccurrences.map((occurrence) => (
                <PublicOccurrenceCard
                  occurrence={occurrence}
                  onOpen={openOccurrence}
                  key={occurrence.id}
                />
              ))}
            </div>
          ) : (
            <div className="public-home__empty">
              <strong>{occurrencesUnavailable ? 'Não foi possível carregar as ocorrências agora.' : 'Nenhuma ocorrência aberta encontrada nesta região.'}</strong>
              {occurrencesUnavailable && <span>Tente novamente em instantes.</span>}
            </div>
          )}

          {occurrenceDetailError && (
            <p className="public-home__visitor-note" role="alert">{occurrenceDetailError}</p>
          )}

          {!user && (
            <p className="public-home__visitor-note">
              Você está vendo informações públicas. Para registrar, acompanhar ou interagir com uma ocorrência, entre ou crie sua conta.
            </p>
          )}
        </section>

        <section className="public-home__section public-home__plans" id="planos">
          <div className="public-home__section-titlebar">
            <div className="public-home__section-title-wrap">
              <span className="public-home__section-icon public-home__section-icon--plans" aria-hidden="true">◇</span>
              <div>
                <h2>Planos</h2>
                <p>Opções para contas Master publicarem, receberem ocorrências e organizarem sua equipe.</p>
              </div>
            </div>
          </div>

          {visiblePlans.length > 0 ? (
            <div className="public-home__plan-grid">
              {visiblePlans.map((offer) => <PlanCard offer={offer} onStart={handlePlanStart} key={offer.offerId} />)}
            </div>
          ) : (
            <div className="public-home__empty public-home__empty--compact">
              <strong>Consulte os planos disponíveis na página dedicada.</strong>
              <Button variant="soft" onClick={() => navigate('/planos')}>Ver planos</Button>
            </div>
          )}
        </section>

        <section className="public-home__section public-home__how" id="como-funciona">
          <div className="public-home__section-titlebar public-home__section-titlebar--center">
            <div>
              <h2>Como funciona</h2>
              <p>Um fluxo simples para transformar uma demanda em acompanhamento real.</p>
            </div>
          </div>
          <div className="public-home__how-grid">
            <article><span>01</span><h3>Registre</h3><p>Informe o problema, local e evidências da ocorrência.</p></article>
            <article><span>02</span><h3>Compartilhe</h3><p>Escolha contas Master que podem receber e acompanhar a demanda.</p></article>
            <article><span>03</span><h3>Acompanhe</h3><p>Veja status, respostas e histórico em um único lugar.</p></article>
          </div>
        </section>

        {user && <HomeAccountModules user={user} permissions={permissions} />}
      </main>

      <footer className="public-home__footer">
        <div className="public-home__footer-inner">
          <Brand compact />
          <p>CIDADEMDIA — conectando cidadãos e quem pode resolver.</p>
          {!user && (onLogin || onRegister) && (
            <div className="public-home__footer-actions">
              {onLogin && <button type="button" onClick={onLogin}>Entrar</button>}
              {onRegister && <button type="button" onClick={onRegister}>Criar conta</button>}
            </div>
          )}
        </div>
      </footer>

      <HowItWorksModal open={howItWorksOpen} onClose={() => setHowItWorksOpen(false)} />
      {selectedOccurrence && (
        <PublicOccurrenceDetailsModal
          occurrence={selectedOccurrence}
          onClose={() => setSelectedOccurrence(null)}
        />
      )}

      <AppBottomNavigation
        active="home"
        user={user}
        permissions={permissions}
        onLogin={onLogin}
        onRegister={onRegister}
      />
    </div>
  );
}