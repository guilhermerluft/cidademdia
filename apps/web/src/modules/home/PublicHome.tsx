import { useEffect, useState } from 'react';
import { Brand, Button } from '../../components/ui';
import { listPlacementPosts } from '../posts/postService';
import type { PostItem } from '../posts/types';

interface PublicHomeProps {
  onLogin: () => void;
  onRegister: () => void;
}

function formatPublishedAt(value?: string | null) {
  if (!value) return 'Agora na cidade';

  return new Intl.DateTimeFormat('pt-BR', {
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value));
}

function MediaPreview({ post }: { post: PostItem }) {
  const media = post.media.find((item) => item.status === 'ready' && item.readUrl);

  if (media?.readUrl && media.contentType.startsWith('image/')) {
    return <img src={media.readUrl} alt="" loading="lazy" />;
  }

  if (media?.readUrl && media.contentType.startsWith('video/')) {
    return <video src={media.readUrl} muted playsInline preload="metadata" aria-label="Prévia do vídeo publicado" />;
  }

  return (
    <div className="public-home__media-placeholder" aria-hidden="true">
      <span>{post.type === 'link' ? '↗' : post.type === 'text' ? 'Aa' : '●'}</span>
    </div>
  );
}

function MediaCard({ post }: { post: PostItem }) {
  return (
    <article className="public-home__media-card">
      <div className="public-home__media-cover">
        <MediaPreview post={post} />
        <span className="public-home__media-type">{post.type}</span>
      </div>

      <div className="public-home__media-content">
        <span className="public-home__meta">{formatPublishedAt(post.publishedAt)}</span>
        <h3>{post.title || 'Atualização CidadeEmDia'}</h3>
        {post.body && <p>{post.body}</p>}
        {post.linkUrl && (
          <a href={post.linkUrl} target="_blank" rel="noreferrer">
            Abrir conteúdo <span aria-hidden="true">↗</span>
          </a>
        )}
      </div>
    </article>
  );
}

export function PublicHome({ onLogin, onRegister }: PublicHomeProps) {
  const [posts, setPosts] = useState<PostItem[]>([]);
  const [postsLoading, setPostsLoading] = useState(true);
  const [postsUnavailable, setPostsUnavailable] = useState(false);

  useEffect(() => {
    let active = true;

    async function loadPosts() {
      setPostsLoading(true);
      setPostsUnavailable(false);

      try {
        const horizontal = await listPlacementPosts('horizontal', undefined, 6);
        let items = horizontal.items;

        if (items.length === 0) {
          const feed = await listPlacementPosts('feed', undefined, 6);
          items = feed.items;
        }

        if (active) setPosts(items);
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

    return () => {
      active = false;
    };
  }, []);

  return (
    <div className="public-home">
      <header className="public-home__header">
        <div className="public-home__header-inner">
          <Brand className="public-home__brand" />

          <nav className="public-home__desktop-nav" aria-label="Navegação principal">
            <a href="#como-funciona">Como funciona</a>
            <a href="#midias">Mídias</a>
            <a href="#ocorrencias">Ocorrências</a>
          </nav>

          <div className="public-home__header-actions">
            <Button variant="ghost" onClick={onLogin}>Entrar</Button>
            <Button onClick={onRegister}>Criar conta</Button>
          </div>
        </div>
      </header>

      <main>
        <section className="public-home__hero" aria-labelledby="public-home-title">
          <div className="public-home__hero-inner">
            <div className="public-home__hero-copy">
              <span className="public-home__eyebrow">Cidadania que gera movimento</span>
              <h1 id="public-home-title">
                Sua voz encontra <span>quem pode transformar a cidade.</span>
              </h1>
              <p>
                Registre ocorrências, acompanhe cada etapa e aproxime cidadãos e instituições públicas em um só lugar.
              </p>

              <div className="public-home__hero-actions">
                <Button size="lg" onClick={onRegister}>Quero participar</Button>
                <a className="public-home__secondary-cta" href="#como-funciona">
                  Como funciona <span aria-hidden="true">↓</span>
                </a>
              </div>

              <ul className="public-home__trust-list" aria-label="Benefícios da plataforma">
                <li><span aria-hidden="true">✓</span> Protocolo e acompanhamento</li>
                <li><span aria-hidden="true">✓</span> Comunicação mais transparente</li>
                <li><span aria-hidden="true">✓</span> Participação simples pelo celular</li>
              </ul>
            </div>

            <div className="public-home__hero-visual" aria-label="Exemplo de acompanhamento de uma ocorrência">
              <div className="public-home__hero-glow" aria-hidden="true" />
              <div className="public-home__city-shape public-home__city-shape--one" aria-hidden="true" />
              <div className="public-home__city-shape public-home__city-shape--two" aria-hidden="true" />
              <div className="public-home__city-shape public-home__city-shape--three" aria-hidden="true" />

              <article className="public-home__occurrence-preview">
                <div className="public-home__preview-topline">
                  <span className="public-home__preview-icon" aria-hidden="true">!</span>
                  <span className="public-home__status public-home__status--progress">Em andamento</span>
                </div>
                <span className="public-home__preview-label">Ocorrência #CED-2026</span>
                <h2>Iluminação pública</h2>
                <p>Solicitação encaminhada e sendo acompanhada pela instituição responsável.</p>
                <div className="public-home__preview-progress" aria-label="Progresso da ocorrência">
                  <span />
                  <span />
                  <span />
                </div>
                <div className="public-home__preview-footer">
                  <span>Registrada</span>
                  <span>Recebida</span>
                  <strong>Em análise</strong>
                </div>
              </article>

              <div className="public-home__floating-card public-home__floating-card--top">
                <span className="public-home__floating-dot public-home__floating-dot--green" />
                <div><strong>Conectado</strong><small>cidadão + instituição</small></div>
              </div>

              <div className="public-home__floating-card public-home__floating-card--bottom">
                <span className="public-home__floating-number">24h</span>
                <div><strong>Acompanhe</strong><small>as atualizações no app</small></div>
              </div>
            </div>
          </div>
        </section>

        <section className="public-home__numbers" aria-label="O que a plataforma conecta">
          <div className="public-home__numbers-inner">
            <div><strong>1</strong><span>canal para registrar e acompanhar</span></div>
            <div><strong>3</strong><span>instituições podem receber cada ocorrência</span></div>
            <div><strong>100%</strong><span>digital, responsivo e transparente</span></div>
          </div>
        </section>

        <section className="public-home__section public-home__section--media" id="midias">
          <div className="public-home__section-heading">
            <div>
              <span className="public-home__section-kicker">Cidade em movimento</span>
              <h2>Informação que aproxima você da sua cidade</h2>
            </div>
            <p>Publicações de instituições e do CidadeEmDia reunidas em uma experiência visual e fácil de acompanhar.</p>
          </div>

          {postsLoading ? (
            <div className="public-home__media-grid" aria-busy="true" aria-label="Carregando publicações">
              {[0, 1, 2].map((item) => <div className="public-home__media-skeleton" key={item} />)}
            </div>
          ) : posts.length > 0 ? (
            <div className="public-home__media-grid">
              {posts.slice(0, 6).map((post) => <MediaCard post={post} key={post.id} />)}
            </div>
          ) : (
            <div className="public-home__empty public-home__empty--media">
              <div className="public-home__empty-symbol" aria-hidden="true">◌</div>
              <div>
                <h3>{postsUnavailable ? 'As publicações estão temporariamente indisponíveis' : 'As primeiras publicações aparecerão aqui'}</h3>
                <p>{postsUnavailable
                  ? 'A plataforma continua disponível. Tente novamente em instantes.'
                  : 'Quando CidadeEmDia e instituições publicarem novidades, você poderá acompanhá-las nesta área.'}</p>
              </div>
            </div>
          )}
        </section>

        <section className="public-home__section public-home__section--steps" id="como-funciona">
          <div className="public-home__section-heading public-home__section-heading--center">
            <div>
              <span className="public-home__section-kicker">Simples do início ao acompanhamento</span>
              <h2>Participar da cidade pode ser fácil</h2>
            </div>
            <p>Você registra a situação, escolhe quem deve recebê-la e acompanha a evolução sem perder o histórico.</p>
          </div>

          <div className="public-home__steps">
            <article>
              <span className="public-home__step-number">01</span>
              <div className="public-home__step-icon" aria-hidden="true">＋</div>
              <h3>Registre</h3>
              <p>Descreva a ocorrência, informe o local e adicione fotos ou vídeos quando necessário.</p>
            </article>
            <article>
              <span className="public-home__step-number">02</span>
              <div className="public-home__step-icon" aria-hidden="true">→</div>
              <h3>Encaminhe</h3>
              <p>Compartilhe com as instituições disponíveis e mantenha a conversa ligada à própria ocorrência.</p>
            </article>
            <article>
              <span className="public-home__step-number">03</span>
              <div className="public-home__step-icon" aria-hidden="true">✓</div>
              <h3>Acompanhe</h3>
              <p>Veja status, respostas e atualizações em um histórico organizado e acessível pelo celular.</p>
            </article>
          </div>
        </section>

        <section className="public-home__section" id="ocorrencias">
          <div className="public-home__section-heading">
            <div>
              <span className="public-home__section-kicker">Ocorrências</span>
              <h2>Da rua para quem pode resolver</h2>
            </div>
            <p>As ocorrências pessoais permanecem protegidas por autenticação. Entre para registrar e acompanhar as suas.</p>
          </div>

          <div className="public-home__occurrences-empty">
            <div className="public-home__occurrences-illustration" aria-hidden="true">
              <span className="public-home__map-pin">●</span>
              <span className="public-home__map-line public-home__map-line--one" />
              <span className="public-home__map-line public-home__map-line--two" />
              <span className="public-home__map-block public-home__map-block--one" />
              <span className="public-home__map-block public-home__map-block--two" />
            </div>
            <div className="public-home__occurrences-copy">
              <span className="public-home__status public-home__status--secure">Área protegida</span>
              <h3>Suas ocorrências ficam no seu perfil</h3>
              <p>
                Crie sua conta gratuitamente para abrir uma ocorrência, escolher instituições, anexar evidências e acompanhar as respostas.
              </p>
              <div className="public-home__occurrences-actions">
                <Button size="lg" onClick={onRegister}>Criar minha conta</Button>
                <Button size="lg" variant="soft" onClick={onLogin}>Já tenho conta</Button>
              </div>
            </div>
          </div>
        </section>

        <section className="public-home__cta-band">
          <div>
            <span>Uma cidade melhor é construída todos os dias.</span>
            <h2>Faça parte dessa conversa.</h2>
            <p>Entre no CidadeEmDia e transforme participação em acompanhamento real.</p>
          </div>
          <Button size="lg" onClick={onRegister}>Começar agora</Button>
        </section>
      </main>

      <footer className="public-home__footer">
        <div className="public-home__footer-inner">
          <Brand compact />
          <p>Conectando cidadãos e gestão pública com mais transparência.</p>
          <nav aria-label="Links do rodapé">
            <a href="#como-funciona">Como funciona</a>
            <a href="#midias">Mídias</a>
            <button type="button" onClick={onLogin}>Entrar</button>
          </nav>
        </div>
      </footer>

      <nav className="public-home__bottom-nav" aria-label="Navegação mobile">
        <a href="#public-home-title"><span aria-hidden="true">⌂</span>Início</a>
        <a href="#midias"><span aria-hidden="true">◫</span>Mídias</a>
        <a href="#ocorrencias"><span aria-hidden="true">◎</span>Ocorrências</a>
        <button type="button" onClick={onLogin}><span aria-hidden="true">○</span>Entrar</button>
      </nav>
    </div>
  );
}
