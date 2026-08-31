import { useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import { isAxiosError } from 'axios';
import { Badge, Button, Card, CardBody, SectionHeading } from '../../components/ui';
import {
  createOccurrence,
  listMyOccurrences,
  listOccurrenceCategories,
  prepareOccurrenceMedia,
} from './occurrenceService';
import type { OccurrenceCategory, OccurrencePage } from './types';

const ACCEPTED_MEDIA_TYPES = 'image/jpeg,image/png,image/webp,video/mp4,video/webm';

interface OccurrenceFormState {
  categoryId: string;
  title: string;
  description: string;
  addressText: string;
  postalCode: string;
  stateCode: string;
  latitude: string;
  longitude: string;
  externalProtocolNumber: string;
  externalProtocolAgency: string;
}

const INITIAL_FORM: OccurrenceFormState = {
  categoryId: '',
  title: '',
  description: '',
  addressText: '',
  postalCode: '',
  stateCode: '',
  latitude: '',
  longitude: '',
  externalProtocolNumber: '',
  externalProtocolAgency: '',
};

const STATUS_LABELS: Record<string, string> = {
  NOVA: 'Nova',
  RECEBIDA: 'Recebida',
  EM_ANALISE: 'Em análise',
  EM_ANDAMENTO: 'Em andamento',
  AGUARDANDO_INFORMACAO: 'Aguardando informação',
  RESOLVIDA: 'Resolvida',
  ENCERRADA: 'Encerrada',
  CANCELADA: 'Cancelada',
};

function statusVariant(status: string) {
  switch (status) {
    case 'NOVA':
      return 'info' as const;
    case 'RECEBIDA':
      return 'primary' as const;
    case 'EM_ANALISE':
    case 'AGUARDANDO_INFORMACAO':
      return 'warning' as const;
    case 'EM_ANDAMENTO':
      return 'progress' as const;
    case 'RESOLVIDA':
      return 'resolved' as const;
    case 'CANCELADA':
      return 'cancelled' as const;
    default:
      return 'neutral' as const;
  }
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('pt-BR', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(value));
}

function formatBytes(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function getErrorMessage(error: unknown) {
  if (isAxiosError(error)) {
    const data = error.response?.data as { code?: string; detail?: string } | undefined;

    switch (data?.code) {
      case 'media_extension_not_allowed':
      case 'media_type_not_allowed':
        return 'Uma das mídias selecionadas não possui formato permitido.';
      case 'media_size_not_allowed':
        return data.detail ?? 'Uma das mídias ultrapassa o limite permitido.';
      case 'media_signature_invalid':
      case 'media_verification_failed':
        return 'Uma das mídias não passou pela validação de segurança.';
      case 'storage_not_configured':
      case 'storage_verification_failed':
        return 'O armazenamento de mídias está temporariamente indisponível.';
      case 'category_inactive':
      case 'category_not_found':
        return 'A categoria selecionada não está mais disponível.';
      case 'media_not_ready_or_owned':
      case 'media_persistence_conflict':
        return 'Não foi possível vincular as mídias à ocorrência. Tente novamente.';
      case 'invalid_media_selection':
        return data.detail ?? 'A seleção de mídias é inválida.';
      default:
        if (data?.detail) return data.detail;
    }
  }

  if (error instanceof Error && error.message) {
    if (error.message === 'Failed to fetch') {
      return 'O navegador não conseguiu enviar a mídia ao armazenamento. Tente novamente em instantes.';
    }

    return error.message;
  }

  return 'Não foi possível concluir a operação. Tente novamente.';
}

export function OccurrenceCenter() {
  const [categories, setCategories] = useState<OccurrenceCategory[]>([]);
  const [occurrences, setOccurrences] = useState<OccurrencePage | null>(null);
  const [form, setForm] = useState<OccurrenceFormState>(INITIAL_FORM);
  const [files, setFiles] = useState<File[]>([]);
  const [fileInputKey, setFileInputKey] = useState(0);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [locating, setLocating] = useState(false);
  const [progress, setProgress] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const selectedCategory = useMemo(
    () => categories.find((category) => category.id === form.categoryId) ?? null,
    [categories, form.categoryId],
  );

  async function loadOccurrenceData() {
    const [nextCategories, nextOccurrences] = await Promise.all([
      listOccurrenceCategories(),
      listMyOccurrences(1, 10),
    ]);

    setCategories(nextCategories);
    setOccurrences(nextOccurrences);
    setForm((current) => ({
      ...current,
      categoryId: current.categoryId || nextCategories[0]?.id || '',
    }));
  }

  useEffect(() => {
    let active = true;
    setLoading(true);

    void loadOccurrenceData()
      .catch((requestError) => {
        if (active) setError(getErrorMessage(requestError));
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, []);

  function updateField<K extends keyof OccurrenceFormState>(field: K, value: OccurrenceFormState[K]) {
    setForm((current) => ({ ...current, [field]: value }));
  }

  function useCurrentLocation() {
    setError(null);

    if (!navigator.geolocation) {
      setError('Este navegador não disponibiliza geolocalização. Informe latitude e longitude manualmente.');
      return;
    }

    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      (position) => {
        updateField('latitude', position.coords.latitude.toFixed(6));
        updateField('longitude', position.coords.longitude.toFixed(6));
        setLocating(false);
      },
      () => {
        setError('Não foi possível obter sua localização. Autorize o navegador ou informe as coordenadas manualmente.');
        setLocating(false);
      },
      {
        enableHighAccuracy: true,
        timeout: 10_000,
        maximumAge: 30_000,
      },
    );
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    setMessage(null);
    setProgress(null);

    try {
      const latitude = Number(form.latitude.replace(',', '.'));
      const longitude = Number(form.longitude.replace(',', '.'));

      if (!Number.isFinite(latitude) || latitude < -90 || latitude > 90) {
        throw new Error('Informe uma latitude válida entre -90 e 90.');
      }

      if (!Number.isFinite(longitude) || longitude < -180 || longitude > 180) {
        throw new Error('Informe uma longitude válida entre -180 e 180.');
      }

      const mediaIds: string[] = [];
      for (let index = 0; index < files.length; index += 1) {
        const file = files[index];
        setProgress(`Enviando mídia ${index + 1} de ${files.length}: ${file.name}`);
        const media = await prepareOccurrenceMedia(file);
        mediaIds.push(media.id);
      }

      setProgress('Registrando ocorrência...');
      const occurrence = await createOccurrence({
        categoryId: form.categoryId,
        title: form.title.trim(),
        description: form.description.trim() || null,
        addressText: form.addressText.trim(),
        latitude,
        longitude,
        postalCode: form.postalCode.trim() || null,
        cityId: null,
        stateCode: form.stateCode.trim().toUpperCase() || null,
        externalProtocolNumber: form.externalProtocolNumber.trim() || null,
        externalProtocolAgency: form.externalProtocolAgency.trim() || null,
        mediaIds,
      });

      setMessage(`Ocorrência ${occurrence.publicCode} registrada com sucesso.`);
      setForm({
        ...INITIAL_FORM,
        categoryId: categories[0]?.id || '',
      });
      setFiles([]);
      setFileInputKey((value) => value + 1);
      await loadOccurrenceData();
    } catch (requestError) {
      setError(getErrorMessage(requestError));
    } finally {
      setSubmitting(false);
      setProgress(null);
    }
  }

  return (
    <section className="dashboard-section occurrence-center" id="dashboard-occurrences" aria-labelledby="occurrence-center-title">
      <SectionHeading
        title="Minhas ocorrências"
        subtitle="Registre uma nova demanda e acompanhe as ocorrências publicadas pela sua conta."
      />

      <div className="occurrence-center__grid">
        <Card className="occurrence-form-card">
          <CardBody>
            <div className="occurrence-form-card__header">
              <div>
                <span className="occurrence-eyebrow">Nova ocorrência</span>
                <h3 id="occurrence-center-title">Conte o que está acontecendo</h3>
                <p>O conteúdo publicado fica preservado. Novas informações serão adicionadas depois como complementos.</p>
              </div>
              {selectedCategory ? <Badge variant="primary">{selectedCategory.name}</Badge> : null}
            </div>

            <form className="occurrence-form" onSubmit={handleSubmit}>
              <label>
                Categoria
                <select
                  required
                  value={form.categoryId}
                  onChange={(event) => updateField('categoryId', event.target.value)}
                  disabled={loading || categories.length === 0}
                >
                  {categories.length === 0 ? <option value="">Nenhuma categoria disponível</option> : null}
                  {categories.map((category) => (
                    <option key={category.id} value={category.id}>{category.name}</option>
                  ))}
                </select>
              </label>

              <label>
                Título
                <input
                  required
                  value={form.title}
                  onChange={(event) => updateField('title', event.target.value)}
                  placeholder="Ex.: Buraco grande na via"
                />
              </label>

              <label className="occurrence-form__full">
                Descrição
                <textarea
                  value={form.description}
                  onChange={(event) => updateField('description', event.target.value)}
                  rows={4}
                  placeholder="Descreva a situação com informações objetivas."
                />
              </label>

              <label className="occurrence-form__full">
                Endereço
                <input
                  required
                  value={form.addressText}
                  onChange={(event) => updateField('addressText', event.target.value)}
                  placeholder="Rua, número, bairro e cidade"
                />
              </label>

              <label>
                CEP
                <input
                  value={form.postalCode}
                  onChange={(event) => updateField('postalCode', event.target.value)}
                  inputMode="numeric"
                  placeholder="00000-000"
                />
              </label>

              <label>
                UF
                <input
                  value={form.stateCode}
                  onChange={(event) => updateField('stateCode', event.target.value.slice(0, 2).toUpperCase())}
                  maxLength={2}
                  placeholder="RS"
                />
              </label>

              <div className="occurrence-location occurrence-form__full">
                <div className="occurrence-location__header">
                  <div>
                    <span>Localização</span>
                    <small>Enquanto o mapa não está ativo, use a localização do dispositivo ou informe as coordenadas.</small>
                  </div>
                  <Button type="button" variant="soft" size="sm" onClick={useCurrentLocation} disabled={locating || submitting}>
                    {locating ? 'Obtendo...' : 'Usar minha localização'}
                  </Button>
                </div>

                <div className="occurrence-location__fields">
                  <label>
                    Latitude
                    <input
                      required
                      inputMode="decimal"
                      value={form.latitude}
                      onChange={(event) => updateField('latitude', event.target.value)}
                      placeholder="-30.034600"
                    />
                  </label>
                  <label>
                    Longitude
                    <input
                      required
                      inputMode="decimal"
                      value={form.longitude}
                      onChange={(event) => updateField('longitude', event.target.value)}
                      placeholder="-51.217700"
                    />
                  </label>
                </div>
              </div>

              <label>
                Protocolo externo
                <input
                  value={form.externalProtocolNumber}
                  onChange={(event) => updateField('externalProtocolNumber', event.target.value)}
                  placeholder="Opcional"
                />
              </label>

              <label>
                Órgão do protocolo
                <input
                  value={form.externalProtocolAgency}
                  onChange={(event) => updateField('externalProtocolAgency', event.target.value)}
                  placeholder="Opcional"
                />
              </label>

              <label className="occurrence-media-field occurrence-form__full">
                Fotos ou vídeos
                <input
                  key={fileInputKey}
                  type="file"
                  multiple
                  accept={ACCEPTED_MEDIA_TYPES}
                  onChange={(event) => setFiles(Array.from(event.target.files ?? []))}
                  disabled={submitting}
                />
                <small>Formatos aceitos: JPEG, PNG, WebP, MP4 e WebM. Os arquivos são enviados diretamente ao armazenamento privado.</small>
              </label>

              {files.length > 0 ? (
                <ul className="occurrence-file-list" aria-label="Mídias selecionadas">
                  {files.map((file, index) => (
                    <li key={`${file.name}-${file.size}-${index}`}>
                      <span>{file.name}</span>
                      <small>{formatBytes(file.size)}</small>
                    </li>
                  ))}
                </ul>
              ) : null}

              {progress ? <p className="occurrence-progress" role="status">{progress}</p> : null}
              {message ? <p className="occurrence-success" role="status">{message}</p> : null}
              {error ? <p className="occurrence-error" role="alert">{error}</p> : null}

              <div className="occurrence-form__actions occurrence-form__full">
                <Button type="submit" size="lg" disabled={submitting || loading || !form.categoryId}>
                  {submitting ? 'Publicando...' : 'Publicar ocorrência'}
                </Button>
              </div>
            </form>
          </CardBody>
        </Card>

        <Card className="occurrence-list-card">
          <CardBody>
            <div className="occurrence-list-card__header">
              <div>
                <span className="occurrence-eyebrow">Acompanhamento</span>
                <h3>Publicadas por você</h3>
              </div>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                disabled={loading}
                onClick={() => {
                  setLoading(true);
                  setError(null);
                  void loadOccurrenceData()
                    .catch((requestError) => setError(getErrorMessage(requestError)))
                    .finally(() => setLoading(false));
                }}
              >
                Atualizar
              </Button>
            </div>

            {loading ? <p className="occurrence-empty" role="status">Carregando ocorrências...</p> : null}

            {!loading && occurrences?.items.length === 0 ? (
              <p className="occurrence-empty">Você ainda não publicou nenhuma ocorrência.</p>
            ) : null}

            {!loading && occurrences && occurrences.items.length > 0 ? (
              <div className="occurrence-list">
                {occurrences.items.map((occurrence) => (
                  <article className="occurrence-list-item" key={occurrence.id}>
                    <div className="occurrence-list-item__topline">
                      <span className="occurrence-list-item__code">{occurrence.publicCode}</span>
                      <Badge variant={statusVariant(occurrence.status)}>
                        {STATUS_LABELS[occurrence.status] ?? occurrence.status}
                      </Badge>
                    </div>
                    <h4>{occurrence.title}</h4>
                    <p>{occurrence.categoryName}</p>
                    <small>{occurrence.addressText}</small>
                    <time dateTime={occurrence.createdAt}>Publicada em {formatDate(occurrence.createdAt)}</time>
                  </article>
                ))}
              </div>
            ) : null}

            {occurrences && occurrences.totalItems > occurrences.items.length ? (
              <small className="occurrence-list-card__footer">
                Exibindo {occurrences.items.length} de {occurrences.totalItems} ocorrências mais recentes.
              </small>
            ) : null}
          </CardBody>
        </Card>
      </div>
    </section>
  );
}
