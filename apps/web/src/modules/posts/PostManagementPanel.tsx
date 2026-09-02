import { useEffect, useMemo, useState } from 'react';
import type { ChangeEvent, FormEvent } from 'react';
import { isAxiosError } from 'axios';
import { Badge, Button, Card, CardBody, SectionHeading } from '../../components/ui';
import {
  archivePost,
  createPostDraft,
  listManagedPosts,
  preparePostMedia,
  publishPost,
} from './postService';
import type {
  CreatePostPayload,
  PostItem,
  PostPlacementKey,
  PostType,
} from './types';

const TYPE_OPTIONS: Array<{ value: PostType; label: string }> = [
  { value: 'text', label: 'Texto' },
  { value: 'image', label: 'Imagem' },
  { value: 'video', label: 'Vídeo' },
  { value: 'link', label: 'Link' },
  { value: 'carousel', label: 'Carrossel' },
];

const PLACEMENT_OPTIONS: Array<{ value: PostPlacementKey; label: string }> = [
  { value: 'feed', label: 'Feed' },
  { value: 'horizontal', label: 'Horizontal' },
  { value: 'vertical', label: 'Vertical' },
];

function statusBadge(status: PostItem['status']) {
  if (status === 'published') return <Badge variant="success">Publicado</Badge>;
  if (status === 'archived') return <Badge variant="neutral">Arquivado</Badge>;
  return <Badge variant="warning">Rascunho</Badge>;
}

function typeLabel(type: PostType) {
  return TYPE_OPTIONS.find((option) => option.value === type)?.label ?? type;
}

function formatDate(value?: string | null) {
  if (!value) return '—';
  return new Intl.DateTimeFormat('pt-BR', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(value));
}

function getErrorMessage(error: unknown) {
  if (isAxiosError(error)) {
    const data = error.response?.data as { error?: string; detail?: string } | undefined;

    switch (data?.error) {
      case 'publication_limit_reached':
        return 'A cota mensal de publicações desta conta foi atingida.';
      case 'subscription_access_denied':
      case 'subscription_not_found':
        return 'A assinatura atual não permite publicar neste momento.';
      case 'storage_not_configured':
        return 'O armazenamento de mídia ainda não está configurado neste ambiente.';
      case 'post_media_not_ready':
        return 'Uma ou mais mídias ainda não foram confirmadas pelo armazenamento.';
      case 'post_image_media_required':
        return 'Publicações de imagem precisam de uma imagem válida.';
      case 'post_video_media_required':
        return 'Publicações de vídeo precisam de um vídeo válido.';
      case 'post_carousel_media_required':
        return 'O carrossel precisa de pelo menos duas mídias.';
      case 'post_link_invalid':
        return 'Informe um link HTTP ou HTTPS válido.';
      case 'post_body_required':
        return 'Informe o texto da publicação.';
    }
  }

  return error instanceof Error
    ? error.message
    : 'Não foi possível concluir a operação agora.';
}

export function PostManagementPanel() {
  const [posts, setPosts] = useState<PostItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [actingPostId, setActingPostId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const [type, setType] = useState<PostType>('text');
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [linkUrl, setLinkUrl] = useState('');
  const [placements, setPlacements] = useState<PostPlacementKey[]>(['feed']);
  const [priority, setPriority] = useState(0);
  const [displayOrder, setDisplayOrder] = useState(0);
  const [files, setFiles] = useState<File[]>([]);

  const requiresMedia = type === 'image' || type === 'video' || type === 'carousel';
  const accepts = useMemo(() => {
    if (type === 'image') return 'image/jpeg,image/png,image/webp';
    if (type === 'video') return 'video/mp4,video/webm';
    if (type === 'carousel') return 'image/jpeg,image/png,image/webp,video/mp4,video/webm';
    return undefined;
  }, [type]);

  useEffect(() => {
    void refresh();
  }, []);

  async function refresh() {
    setLoading(true);
    try {
      const page = await listManagedPosts(1, 20);
      setPosts(page.items);
    } catch (requestError) {
      setError(getErrorMessage(requestError));
    } finally {
      setLoading(false);
    }
  }

  function togglePlacement(key: PostPlacementKey) {
    setPlacements((current) =>
      current.includes(key)
        ? current.filter((item) => item !== key)
        : [...current, key],
    );
  }

  function handleFiles(event: ChangeEvent<HTMLInputElement>) {
    setFiles(Array.from(event.target.files ?? []));
  }

  function validateForm() {
    if (placements.length === 0) return 'Selecione pelo menos um placement.';
    if (type === 'text' && !body.trim()) return 'Informe o texto da publicação.';
    if (type === 'link' && !linkUrl.trim()) return 'Informe o link da publicação.';
    if (type === 'image' && files.length !== 1) return 'Selecione exatamente uma imagem.';
    if (type === 'video' && files.length !== 1) return 'Selecione exatamente um vídeo.';
    if (type === 'carousel' && files.length < 2) return 'Selecione pelo menos duas mídias para o carrossel.';
    return null;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setMessage(null);

    const validationError = validateForm();
    if (validationError) {
      setError(validationError);
      return;
    }

    setSubmitting(true);

    try {
      const payload: CreatePostPayload = {
        type,
        title: title.trim() || null,
        body: body.trim() || null,
        linkUrl: linkUrl.trim() || null,
        placements: placements.map((placementKey) => ({
          placementKey,
          priority,
          displayOrder,
        })),
      };

      const draft = await createPostDraft(payload);

      if (requiresMedia) {
        await preparePostMedia(draft.id, files);
      }

      await publishPost(draft.id);
      setMessage('Publicação criada e publicada com sucesso.');
      setTitle('');
      setBody('');
      setLinkUrl('');
      setFiles([]);
      await refresh();
    } catch (requestError) {
      setError(getErrorMessage(requestError));
      await refresh();
    } finally {
      setSubmitting(false);
    }
  }

  async function handlePublish(postId: string) {
    setActingPostId(postId);
    setError(null);
    setMessage(null);

    try {
      await publishPost(postId);
      setMessage('Rascunho publicado com sucesso.');
      await refresh();
    } catch (requestError) {
      setError(getErrorMessage(requestError));
    } finally {
      setActingPostId(null);
    }
  }

  async function handleArchive(postId: string) {
    setActingPostId(postId);
    setError(null);
    setMessage(null);

    try {
      await archivePost(postId);
      setMessage('Publicação arquivada.');
      await refresh();
    } catch (requestError) {
      setError(getErrorMessage(requestError));
    } finally {
      setActingPostId(null);
    }
  }

  return (
    <section className="dashboard-section posts-panel" id="dashboard-posts" aria-labelledby="posts-title">
      <SectionHeading
        title="Publicações"
        subtitle="Crie conteúdos para feed, formato horizontal e vertical. A cota é consumida somente quando a publicação entra no ar."
      />

      <div className="posts-panel__layout">
        <Card className="posts-composer">
          <CardBody>
            <form className="posts-composer__form" onSubmit={handleSubmit}>
              <div className="posts-composer__heading">
                <div>
                  <span className="posts-composer__eyebrow">Nova publicação</span>
                  <h3>Conteúdo e placement</h3>
                </div>
                <Badge variant="info">Sem aprovação prévia</Badge>
              </div>

              <label>
                Formato
                <select value={type} onChange={(event) => {
                  setType(event.target.value as PostType);
                  setFiles([]);
                }}>
                  {TYPE_OPTIONS.map((option) => (
                    <option key={option.value} value={option.value}>{option.label}</option>
                  ))}
                </select>
              </label>

              <label>
                Título opcional
                <input
                  maxLength={200}
                  value={title}
                  onChange={(event) => setTitle(event.target.value)}
                  placeholder="Título da publicação"
                />
              </label>

              {type !== 'link' && (
                <label>
                  Texto
                  <textarea
                    maxLength={5000}
                    rows={4}
                    value={body}
                    onChange={(event) => setBody(event.target.value)}
                    placeholder="Escreva o conteúdo da publicação"
                  />
                </label>
              )}

              {type === 'link' && (
                <label>
                  Link
                  <input
                    type="url"
                    value={linkUrl}
                    onChange={(event) => setLinkUrl(event.target.value)}
                    placeholder="https://..."
                  />
                </label>
              )}

              {requiresMedia && (
                <label>
                  {type === 'carousel' ? 'Mídias do carrossel' : type === 'image' ? 'Imagem' : 'Vídeo'}
                  <input
                    type="file"
                    accept={accepts}
                    multiple={type === 'carousel'}
                    onChange={handleFiles}
                  />
                  <small>{files.length > 0 ? `${files.length} arquivo(s) selecionado(s)` : 'O upload é enviado diretamente ao armazenamento R2.'}</small>
                </label>
              )}

              <fieldset className="posts-placement-fieldset">
                <legend>Onde exibir</legend>
                <div className="posts-placement-options">
                  {PLACEMENT_OPTIONS.map((option) => (
                    <label key={option.value} className="posts-placement-option">
                      <input
                        type="checkbox"
                        checked={placements.includes(option.value)}
                        onChange={() => togglePlacement(option.value)}
                      />
                      <span>{option.label}</span>
                    </label>
                  ))}
                </div>
              </fieldset>

              <div className="posts-order-grid">
                <label>
                  Prioridade
                  <input
                    type="number"
                    min={0}
                    value={priority}
                    onChange={(event) => setPriority(Math.max(0, Number(event.target.value) || 0))}
                  />
                </label>
                <label>
                  Ordem
                  <input
                    type="number"
                    min={0}
                    value={displayOrder}
                    onChange={(event) => setDisplayOrder(Math.max(0, Number(event.target.value) || 0))}
                  />
                </label>
              </div>

              {message && <p className="posts-panel__success" role="status">{message}</p>}
              {error && <p className="posts-panel__error" role="alert">{error}</p>}

              <Button type="submit" disabled={submitting} fullWidth>
                {submitting ? 'Publicando...' : 'Criar e publicar'}
              </Button>
            </form>
          </CardBody>
        </Card>

        <div className="posts-list" aria-live="polite">
          <div className="posts-list__header">
            <div>
              <span className="posts-composer__eyebrow">Gestão</span>
              <h3>Publicações recentes</h3>
            </div>
            <Button variant="ghost" size="sm" onClick={() => void refresh()} disabled={loading}>
              Atualizar
            </Button>
          </div>

          {loading ? (
            <Card><CardBody><p>Carregando publicações...</p></CardBody></Card>
          ) : posts.length === 0 ? (
            <Card><CardBody><p>Nenhuma publicação criada ainda.</p></CardBody></Card>
          ) : (
            posts.map((post) => (
              <Card className="posts-list__item" key={post.id}>
                <CardBody>
                  <div className="posts-list__meta">
                    <div className="posts-list__badges">
                      {statusBadge(post.status)}
                      <Badge variant="info">{typeLabel(post.type)}</Badge>
                    </div>
                    <time dateTime={post.createdAt}>{formatDate(post.createdAt)}</time>
                  </div>

                  <h4>{post.title || post.body?.slice(0, 72) || post.linkUrl || 'Publicação sem título'}</h4>
                  {post.body && <p>{post.body}</p>}
                  {post.linkUrl && <a href={post.linkUrl} target="_blank" rel="noreferrer">Abrir link</a>}

                  <div className="posts-list__placements">
                    {post.placements.map((placement) => (
                      <span key={placement.placementKey}>
                        {placement.placementKey} · prioridade {placement.priority} · ordem {placement.displayOrder}
                      </span>
                    ))}
                  </div>

                  {post.media.length > 0 && (
                    <div className="posts-list__media">
                      {post.media.map((media) => (
                        media.readUrl && media.contentType.startsWith('image/')
                          ? <img key={media.id} src={media.readUrl} alt="Mídia da publicação" loading="lazy" />
                          : media.readUrl && media.contentType.startsWith('video/')
                            ? <video key={media.id} src={media.readUrl} controls preload="metadata" />
                            : <span key={media.id}>{media.contentType}</span>
                      ))}
                    </div>
                  )}

                  <div className="posts-list__actions">
                    {post.status === 'draft' && (
                      <Button
                        size="sm"
                        onClick={() => void handlePublish(post.id)}
                        disabled={actingPostId === post.id}
                      >
                        Publicar
                      </Button>
                    )}
                    {post.status === 'published' && (
                      <Button
                        size="sm"
                        variant="secondary"
                        onClick={() => void handleArchive(post.id)}
                        disabled={actingPostId === post.id}
                      >
                        Arquivar
                      </Button>
                    )}
                  </div>
                </CardBody>
              </Card>
            ))
          )}
        </div>
      </div>
    </section>
  );
}
