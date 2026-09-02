import { useEffect, useState } from 'react';
import { Badge, Button, Card, CardBody, SectionHeading } from '../../components/ui';
import { listPlacementPosts } from './postService';
import type { PostItem } from './types';

function formatPublishedAt(value?: string | null) {
  if (!value) return '';
  return new Intl.DateTimeFormat('pt-BR', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
}

function renderMedia(post: PostItem) {
  const media = post.media.filter((item) => item.readUrl);
  if (media.length === 0) return null;

  return (
    <div className={post.type === 'carousel' ? 'post-feed__media post-feed__media--carousel' : 'post-feed__media'}>
      {media.map((item) => (
        item.contentType.startsWith('image/')
          ? <img key={item.id} src={item.readUrl ?? undefined} alt={post.title ?? 'Mídia publicada'} loading="lazy" />
          : item.contentType.startsWith('video/')
            ? <video key={item.id} src={item.readUrl ?? undefined} controls preload="metadata" />
            : null
      ))}
    </div>
  );
}

export function PostFeed() {
  const [posts, setPosts] = useState<PostItem[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;

    void listPlacementPosts('feed', undefined, 10)
      .then((page) => {
        if (!active) return;
        setPosts(page.items);
        setNextCursor(page.nextCursor ?? null);
      })
      .catch(() => {
        if (active) setError('Não foi possível carregar as publicações agora.');
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, []);

  async function loadMore() {
    if (!nextCursor || loadingMore) return;

    setLoadingMore(true);
    setError(null);

    try {
      const page = await listPlacementPosts('feed', nextCursor, 10);
      setPosts((current) => [...current, ...page.items]);
      setNextCursor(page.nextCursor ?? null);
    } catch {
      setError('Não foi possível carregar mais publicações agora.');
    } finally {
      setLoadingMore(false);
    }
  }

  return (
    <section className="dashboard-section post-feed" id="dashboard-feed" aria-labelledby="post-feed-title">
      <SectionHeading
        title="CidadeEmDia"
        subtitle="Publicações e informações recentes da plataforma."
      />

      {loading ? (
        <Card><CardBody><p>Carregando publicações...</p></CardBody></Card>
      ) : error && posts.length === 0 ? (
        <Card><CardBody><p className="posts-panel__error" role="alert">{error}</p></CardBody></Card>
      ) : posts.length === 0 ? (
        <Card><CardBody><p>Ainda não há publicações no feed.</p></CardBody></Card>
      ) : (
        <div className="post-feed__list">
          {posts.map((post) => (
            <article key={post.id}>
              <Card className="post-feed__card">
                <CardBody>
                  <div className="post-feed__header">
                    <div>
                      <Badge variant={post.masterUserId ? 'info' : 'primary'}>
                        {post.masterUserId ? 'Conta Master' : 'CidadeEmDia'}
                      </Badge>
                      <time dateTime={post.publishedAt ?? post.createdAt}>
                        {formatPublishedAt(post.publishedAt ?? post.createdAt)}
                      </time>
                    </div>
                    <span className="post-feed__type">{post.type}</span>
                  </div>

                  {post.title && <h3>{post.title}</h3>}
                  {post.body && <p className="post-feed__body">{post.body}</p>}
                  {renderMedia(post)}
                  {post.linkUrl && (
                    <a className="post-feed__link" href={post.linkUrl} target="_blank" rel="noreferrer">
                      Acessar conteúdo →
                    </a>
                  )}
                </CardBody>
              </Card>
            </article>
          ))}
        </div>
      )}

      {error && posts.length > 0 && <p className="posts-panel__error" role="alert">{error}</p>}

      {nextCursor && (
        <div className="post-feed__more">
          <Button variant="secondary" onClick={() => void loadMore()} disabled={loadingMore}>
            {loadingMore ? 'Carregando...' : 'Carregar mais'}
          </Button>
        </div>
      )}
    </section>
  );
}
