import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { isAxiosError } from 'axios';
import { Badge, Button, Card, CardBody, SectionHeading } from '../../components/ui';
import { geocodeGoogleAddress } from '../../services/googleMaps';
import {
  createOccurrence,
  listEligibleMasters,
  listMyOccurrences,
  listOccurrenceCategories,
  prepareOccurrenceMedia,
} from './occurrenceService';
import { OccurrenceGeoFilter } from './OccurrenceGeoFilter';
import { OccurrenceLocationPicker } from './OccurrenceLocationPicker';
import { OccurrenceMediaGallery } from './OccurrenceMediaGallery';
import type { EligibleMaster, OccurrenceCategory, OccurrencePage } from './types';

const ACCEPTED_MEDIA_TYPES = 'image/jpeg,image/png,image/webp,video/mp4,video/webm';

interface OccurrenceFormState {
  categoryId: string;
  masterUserId: string;
  title: string;
  description: string;
  addressText: string;
  street: string;
  number: string;
  neighborhood: string;
  city: string;
  postalCode: string;
  stateCode: string;
  latitude: string;
  longitude: string;
  externalProtocolNumber: string;
  externalProtocolAgency: string;
}

const INITIAL_FORM: OccurrenceFormState = {
  categoryId: '',
  masterUserId: '',
  title: '',
  description: '',
  addressText: '',
  street: '',
  number: '',
  neighborhood: '',
  city: '',
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
      case 'master_not_eligible':
        return 'A conta Master selecionada não está disponível para receber esta ocorrência.';
      case 'photo_required':
        return 'Adicione pelo menos uma foto antes de publicar a ocorrência.';
      case 'media_not_ready_or_owned':
      case 'media_persistence_conflict':
      case 'target_persistence_conflict':
        return data.detail ?? 'Não foi possível concluir a criação da ocorrência com as mídias e a conta Master selecionada.';
      case 'invalid_media_selection':
        return data.detail ?? 'A seleção de mídias é inválida.';
      case 'invalid_geo_filter':
      case 'invalid_city':
        return data.detail ?? 'Os filtros geográficos informados são inválidos.';
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

function buildAddressQuery(form: OccurrenceFormState) {
  const cityState = [form.city.trim(), form.stateCode.trim().toUpperCase()]
    .filter(Boolean)
    .join(' - ');

  return [
    `${form.street.trim()}, ${form.number.trim()}`,
    form.neighborhood.trim(),
    cityState,
    form.postalCode.trim(),
    'Brasil',
  ].filter(Boolean).join(', ');
}

function validateOccurrenceForm(form: OccurrenceFormState, files: File[]) {
  const errors: string[] = [];

  if (!form.categoryId) errors.push('Selecione a categoria.');
  if (!form.masterUserId) errors.push('Selecione a conta Master que receberá a solicitação.');
  if (!form.title.trim()) errors.push('Informe o título da ocorrência.');
  if (!form.street.trim()) errors.push('Informe a rua.');
  if (!form.number.trim()) errors.push('Informe o número.');
  if (!form.neighborhood.trim()) errors.push('Informe o bairro.');
  if (!form.city.trim()) errors.push('Informe a cidade.');
  if (!form.externalProtocolNumber.trim()) errors.push('Informe o número do protocolo.');
  if (!files.some((file) => file.type.startsWith('image/'))) {
    errors.push('Adicione pelo menos uma foto da ocorrência.');
  }

  return errors;
}

export function OccurrenceCenter() {
  const [categories, setCategories] = useState<OccurrenceCategory[]>([]);
  const [masters, setMasters] = useState<EligibleMaster[]>([]);
  const [occurrences, setOccurrences] = useState<OccurrencePage | null>(null);
  const [form, setForm] = useState<OccurrenceFormState>(INITIAL_FORM);
  const [files, setFiles] = useState<File[]>([]);
  const [fileInputKey, setFileInputKey] = useState(0);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [progress, setProgress] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<string[]>([]);

  async function loadOccurrenceData() {
    const [nextCategories, nextMasters, nextOccurrences] = await Promise.all([
      listOccurrenceCategories(),
      listEligibleMasters(),
      listMyOccurrences(1, 10),
    ]);

    setCategories(nextCategories);
    setMasters(nextMasters);
    setOccurrences(nextOccurrences);
    setForm((current) => ({
      ...current,
      categoryId: current.categoryId || nextCategories[0]?.id || '',
      masterUserId: nextMasters.some((master) => master.id === current.masterUserId)
        ? current.masterUserId
        : '',
    }));
  }

  async function reloadOccurrenceList() {
    const nextOccurrences = await listMyOccurrences(1, 10);
    setOccurrences(nextOccurrences);
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
    setValidationErrors([]);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setMessage(null);
    setProgress(null);

    const requiredErrors = validateOccurrenceForm(form, files);
    if (requiredErrors.length > 0) {
      setValidationErrors(requiredErrors);
      return;
    }

    setValidationErrors([]);
    setSubmitting(true);

    try {
      let latitude = Number(form.latitude.replace(',', '.'));
      let longitude = Number(form.longitude.replace(',', '.'));

      if (!Number.isFinite(latitude) || latitude < -90 || latitude > 90
        || !Number.isFinite(longitude) || longitude < -180 || longitude > 180) {
        setProgress('Localizando o endereço no mapa...');
        const geocoded = await geocodeGoogleAddress(buildAddressQuery(form));
        latitude = geocoded.latitude;
        longitude = geocoded.longitude;
        setForm((current) => ({
          ...current,
          latitude: latitude.toFixed(6),
          longitude: longitude.toFixed(6),
        }));
      }

      const mediaIds: string[] = [];
      for (let index = 0; index < files.length; index += 1) {
        const file = files[index];
        setProgress(`Enviando mídia ${index + 1} de ${files.length}: ${file.name}`);
        const media = await prepareOccurrenceMedia(file);
        mediaIds.push(media.id);
      }

      setProgress('Registrando ocorrência e encaminhando para a conta Master...');
      const occurrence = await createOccurrence({
        categoryId: form.categoryId,
        masterUserId: form.masterUserId,
        title: form.title.trim(),
        description: form.description.trim() || null,
        street: form.street.trim(),
        number: form.number.trim(),
        neighborhood: form.neighborhood.trim(),
        city: form.city.trim(),
        latitude,
        longitude,
        postalCode: form.postalCode.trim() || null,
        cityId: null,
        stateCode: form.stateCode.trim().toUpperCase() || null,
        externalProtocolNumber: form.externalProtocolNumber.trim(),
        externalProtocolAgency: form.externalProtocolAgency.trim() || null,
        mediaIds,
      });

      setMessage(`Ocorrência ${occurrence.publicCode} registrada e encaminhada para aceite da conta Master.`);
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
        subtitle="Registre uma nova demanda, encaminhe para uma conta Master e acompanhe o andamento."
      />

      <div className="occurrence-center__grid">
        <Card className="occurrence-form-card">
          <CardBody>
            <div className="occurrence-form-card__header">
              <div>
                <span className="occurrence-eyebrow">Nova ocorrência</span>
                <h3 id="occurrence-center-title">Conte o que está acontecendo</h3>
                <p>Escolha a conta Master responsável pelo aceite. Endereço completo, protocolo e ao menos uma foto são obrigatórios.</p>
              </div>
            </div>

            <form className="occurrence-form" onSubmit={handleSubmit} noValidate>
              <label className="occurrence-form__protocol-field occurrence-form__full">
                Número do protocolo <span className="occurrence-required-marker" aria-hidden="true">*</span>
                <input
                  required
                  value={form.externalProtocolNumber}
                  onChange={(event) => updateField('externalProtocolNumber', event.target.value)}
                  placeholder="Ex.: 2026-000123"
                />
                <small>Esse protocolo identifica a solicitação junto ao órgão ou serviço relacionado.</small>
              </label>

              <label className="occurrence-form__paired-field">
                Categoria <span className="occurrence-required-marker" aria-hidden="true">*</span>
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

              <label className="occurrence-form__paired-field">
                Conta Master <span className="occurrence-required-marker" aria-hidden="true">*</span>
                <select
                  required
                  value={form.masterUserId}
                  onChange={(event) => updateField('masterUserId', event.target.value)}
                  disabled={loading || masters.length === 0}
                >
                  <option value="">Selecione quem receberá a ocorrência</option>
                  {masters.map((master) => (
                    <option key={master.id} value={master.id}>{master.displayName}</option>
                  ))}
                </select>
                <small>A ocorrência ficará aguardando o aceite da conta Master selecionada.</small>
              </label>

              <label className="occurrence-form__title-field occurrence-form__full">
                Título <span className="occurrence-required-marker" aria-hidden="true">*</span>
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

              <OccurrenceLocationPicker
                value={{
                  addressText: form.addressText,
                  street: form.street,
                  number: form.number,
                  neighborhood: form.neighborhood,
                  city: form.city,
                  postalCode: form.postalCode,
                  stateCode: form.stateCode,
                  latitude: form.latitude,
                  longitude: form.longitude,
                }}
                disabled={submitting}
                onChange={(field, value) => updateField(field, value)}
                onError={setError}
              />

              <label className="occurrence-form__full">
                Órgão do protocolo
                <input
                  value={form.externalProtocolAgency}
                  onChange={(event) => updateField('externalProtocolAgency', event.target.value)}
                  placeholder="Opcional — ex.: Prefeitura / Secretaria de Obras"
                />
              </label>

              <label className="occurrence-media-field occurrence-form__full">
                Fotos ou vídeos <span className="occurrence-required-marker" aria-hidden="true">*</span>
                <input
                  key={fileInputKey}
                  type="file"
                  multiple
                  accept={ACCEPTED_MEDIA_TYPES}
                  onChange={(event) => {
                    setFiles(Array.from(event.target.files ?? []));
                    setValidationErrors([]);
                  }}
                  disabled={submitting}
                />
                <small>Ao menos uma foto é obrigatória. Também podem ser enviados vídeos em MP4 ou WebM.</small>
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

              {validationErrors.length > 0 ? (
                <div className="occurrence-validation-summary" role="alert">
                  <strong>Preencha os campos obrigatórios antes de publicar:</strong>
                  <ul>
                    {validationErrors.map((validationError) => <li key={validationError}>{validationError}</li>)}
                  </ul>
                </div>
              ) : null}

              {masters.length === 0 && !loading ? (
                <p className="occurrence-error" role="alert">Nenhuma conta Master está disponível para receber ocorrências neste momento.</p>
              ) : null}
              {progress ? <p className="occurrence-progress" role="status">{progress}</p> : null}
              {message ? <p className="occurrence-success" role="status">{message}</p> : null}
              {error ? <p className="occurrence-error" role="alert">{error}</p> : null}

              <div className="occurrence-form__actions occurrence-form__full">
                <Button type="submit" size="lg" disabled={submitting || loading || !form.categoryId || masters.length === 0}>
                  {submitting ? 'Publicando...' : 'Publicar e encaminhar'}
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

            <OccurrenceGeoFilter
              disabled={loading}
              onResults={setOccurrences}
              onReset={reloadOccurrenceList}
              onError={setError}
            />

            {loading ? <p className="occurrence-empty" role="status">Carregando ocorrências...</p> : null}

            {!loading && occurrences?.items.length === 0 ? (
              <p className="occurrence-empty">Nenhuma ocorrência encontrada para os filtros informados.</p>
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
                    <OccurrenceMediaGallery
                      occurrenceId={occurrence.id}
                      occurrenceCode={occurrence.publicCode}
                    />
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
