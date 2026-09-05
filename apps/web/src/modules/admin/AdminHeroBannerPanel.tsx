import { useEffect, useMemo, useState } from 'react';
import type { ChangeEvent, FormEvent } from 'react';
import { isAxiosError } from 'axios';
import { Badge, Button, Card, CardBody, SectionHeading } from '../../components/ui';
import { HERO_BANNER_UPDATED_EVENT } from '../home/HeroBannerBootstrap';
import {
  archivePost,
  createPostDraft,
  listPlacementPosts,
  preparePostMedia,
  publishPost,
} from '../posts/postService';
import type { PostItem } from '../posts/types';

function getBannerMedia(post?: PostItem | null) {
  return post?.media.find((media) =>
    media.status === 'ready'
    && Boolean(media.readUrl)
    && media.contentType.startsWith('image/')) ?? null;
}

function requestErrorMessage(error: unknown) {
  if (isAxiosError(error)) {
    const data = error.response?.data as { error?: string } | undefined;
    switch (data?.error) {
      case 'storage_not_configured':
        return 'O armazenamento de imagens não está configurado neste ambiente.';
      case 'media_size_not_allowed':
        return 'A imagem excede o tamanho permitido.';
      case 'media_type_not_allowed':
      case 'media_extension_not_allowed':
        return 'Use uma imagem JPG, PNG ou WebP.';
      case 'post_media_not_ready':
        return 'A imagem ainda não ficou pronta no armazenamento.';
    }
  }

  return error instanceof Error
    ? error.message
    : 'Não foi possível atualizar o banner agora.';
}

export function AdminHeroBannerPanel() {
  const [current, setCurrent] = useState<PostItem | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const currentMedia = useMemo(() => getBannerMedia(current), [current]);

  async function refresh() {
    setLoading(true);
    setError(null);
    try {
      const page = await listPlacementPosts('hero', undefined, 10, 'platform');
      const banner = page.items.find((post) =>
        post.masterUserId == null
        && post.status === 'published'
        && post.type === 'image'
        && Boolean(getBannerMedia(post)));
      setCurrent(banner ?? null);
    } catch (requestError) {
      setError(requestErrorMessage(requestError));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void refresh();
  }, []);

  useEffect(() => {
    if (!file) {
      setPreviewUrl(null);
      return;
    }

    const url = URL.createObjectURL(file);
    setPreviewUrl(url);
    return () => URL.revokeObjectURL(url);
  }, [file]);

  function chooseFile(event: ChangeEvent<HTMLInputElement>) {
    const selected = event.target.files?.[0] ?? null;
    setMessage(null);
    setError(null);

    if (!selected) {
      setFile(null);
      return;
    }

    const allowed = ['image/jpeg', 'image/png', 'image/webp'];
    if (!allowed.includes(selected.type)) {
      setFile(null);
      setError('Use uma imagem JPG, PNG ou WebP.');
      event.target.value = '';
      return;
    }

    setFile(selected);
  }

  async function publishBanner(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!file) {
      setError('Selecione uma imagem para o banner.');
      return;
    }

    setSaving(true);
    setError(null);
    setMessage(null);

    try {
      const previousId = current?.id ?? null;
      const draft = await createPostDraft({
        type: 'image',
        title: 'Banner principal da página inicial',
        body: null,
        linkUrl: null,
        placements: [{ placementKey: 'hero', priority: 100, displayOrder: 0 }],
      });

      await preparePostMedia(draft.id, [file]);
      await publishPost(draft.id);

      if (previousId && previousId !== draft.id) {
        try {
          await archivePost(previousId);
        } catch {
          // O novo banner já está publicado; a listagem prioriza o mais recente.
        }
      }

      setFile(null);
      setMessage('Banner atualizado e publicado com sucesso.');
      window.dispatchEvent(new Event(HERO_BANNER_UPDATED_EVENT));
      await refresh();
    } catch (requestError) {
      setError(requestErrorMessage(requestError));
    } finally {
      setSaving(false);
    }
  }

  async function restoreFallback() {
    if (!current) return;

    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      await archivePost(current.id);
      setCurrent(null);
      setFile(null);
      setMessage('Banner dinâmico removido. O banner padrão versionado voltou a ser usado.');
      window.dispatchEvent(new Event(HERO_BANNER_UPDATED_EVENT));
    } catch (requestError) {
      setError(requestErrorMessage(requestError));
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="admin-management-section" aria-labelledby="admin-banner-title">
      <SectionHeading
        title="Banner da página inicial"
        subtitle="Troque a imagem principal exibida no topo da Home. O banner padrão permanece disponível como fallback."
      />

      {message ? <p className="admin-management-feedback admin-management-feedback--success" role="status">{message}</p> : null}
      {error ? <p className="admin-management-feedback admin-management-feedback--error" role="alert">{error}</p> : null}

      <div className="admin-banner-layout">
        <Card className="admin-banner-preview-card">
          <CardBody>
            <div className="admin-banner-card-heading">
              <div>
                <span>Banner ativo</span>
                <h3>{currentMedia?.readUrl ? 'Imagem administrável' : 'Banner padrão do sistema'}</h3>
              </div>
              <Badge variant={currentMedia?.readUrl ? 'success' : 'info'}>
                {currentMedia?.readUrl ? 'Publicado' : 'Fallback'}
              </Badge>
            </div>

            <div className="admin-banner-preview">
              {loading ? (
                <span>Carregando banner...</span>
              ) : currentMedia?.readUrl ? (
                <img src={currentMedia.readUrl} alt="Banner atual da página inicial" />
              ) : (
                <div className="admin-banner-fallback-preview" aria-label="Banner padrão do sistema">
                  <span>Banner padrão versionado</span>
                </div>
              )}
            </div>

            {current ? (
              <div className="admin-banner-meta">
                <span>Publicado em {current.publishedAt ? new Date(current.publishedAt).toLocaleString('pt-BR') : '—'}</span>
                <Button variant="ghost" size="sm" onClick={() => void restoreFallback()} disabled={saving}>
                  Restaurar banner padrão
                </Button>
              </div>
            ) : null}
          </CardBody>
        </Card>

        <Card className="admin-banner-editor-card">
          <CardBody>
            <form className="admin-banner-form" onSubmit={publishBanner}>
              <div>
                <span className="admin-management-eyebrow">Nova imagem</span>
                <h3>Alterar banner</h3>
                <p>Formatos aceitos: JPG, PNG e WebP.</p>
              </div>

              <label className="admin-banner-file-field">
                Arquivo do banner
                <input
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  onChange={chooseFile}
                  disabled={saving}
                />
              </label>

              {previewUrl ? (
                <div className="admin-banner-preview admin-banner-preview--candidate">
                  <img src={previewUrl} alt="Prévia do novo banner" />
                </div>
              ) : null}

              {file ? <small>{file.name} · {(file.size / 1024 / 1024).toFixed(2)} MB</small> : null}

              <Button type="submit" fullWidth disabled={saving || !file}>
                {saving ? 'Publicando...' : 'Publicar novo banner'}
              </Button>
            </form>
          </CardBody>
        </Card>
      </div>
    </section>
  );
}
