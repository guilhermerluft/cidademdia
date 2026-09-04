import type { KeyboardEvent } from 'react';
import { requestCommercialSignup } from '../../components/commercialSignup';
import { getAccessToken } from '../../services/api';
import type { PublicOccurrenceItem } from '../home/homeService';
import { OccurrenceSupportButton } from './OccurrenceSupportButton';
import './public-occurrence-card.css';

function formatTime(value: string) {
  return new Intl.DateTimeFormat('pt-BR', {
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value));
}

function getStatusLabel(status: string) {
  switch (status) {
    case 'NOVA': return 'Nova';
    case 'RECEBIDA': return 'Recebida';
    case 'EM_ANALISE': return 'Em análise';
    case 'EM_ANDAMENTO': return 'Em andamento';
    case 'AGUARDANDO_INFORMACAO': return 'Aguardando informação';
    case 'RESOLVIDA': return 'Resolvida';
    default: return status.replaceAll('_', ' ').toLowerCase();
  }
}

function getStatusClass(status: string) {
  switch (status) {
    case 'RESOLVIDA': return 'resolved';
    case 'EM_ANDAMENTO': return 'progress';
    case 'EM_ANALISE': return 'analysis';
    case 'AGUARDANDO_INFORMACAO': return 'waiting';
    case 'RECEBIDA': return 'received';
    default: return 'new';
  }
}

function getCategoryTone(slug: string) {
  const normalized = slug.toLowerCase();
  if (normalized.includes('ilumin')) return 'blue';
  if (normalized.includes('limpeza') || normalized.includes('lixo')) return 'green';
  if (normalized.includes('transito') || normalized.includes('trânsito')) return 'orange';
  if (normalized.includes('segur')) return 'purple';
  return 'red';
}

function getCategorySymbol(slug: string) {
  const normalized = slug.toLowerCase();
  if (normalized.includes('ilumin')) return '☀';
  if (normalized.includes('limpeza') || normalized.includes('lixo')) return '♻';
  if (normalized.includes('transito') || normalized.includes('trânsito')) return '↔';
  if (normalized.includes('segur')) return '◆';
  if (normalized.includes('buraco') || normalized.includes('infra')) return '△';
  return '●';
}

interface PublicOccurrenceCardProps {
  occurrence: PublicOccurrenceItem;
  onOpen?: (occurrence: PublicOccurrenceItem) => void;
}

export function PublicOccurrenceCard({ occurrence, onOpen }: PublicOccurrenceCardProps) {
  const tone = getCategoryTone(occurrence.categorySlug);
  const interactive = Boolean(onOpen);

  function handleOpen() {
    if (!onOpen) return;

    if (!getAccessToken()) {
      requestCommercialSignup('details');
      return;
    }

    onOpen(occurrence);
  }

  function handleCardKeyDown(event: KeyboardEvent<HTMLElement>) {
    if (!onOpen || event.target !== event.currentTarget) return;
    if (event.key !== 'Enter' && event.key !== ' ') return;

    event.preventDefault();
    handleOpen();
  }

  return (
    <article
      className={`public-home__occurrence-card public-occurrences__card${interactive ? ' public-occurrences__card--interactive' : ''}`}
      data-occurrence-id={occurrence.id}
      tabIndex={interactive ? 0 : undefined}
      aria-label={interactive ? `Abrir ocorrência ${occurrence.publicCode}: ${occurrence.title}` : undefined}
      onClick={onOpen ? handleOpen : undefined}
      onKeyDown={interactive ? handleCardKeyDown : undefined}
    >
      {occurrence.coverMedia ? (
        <div className="public-home__occurrence-thumb public-occurrences__card-cover">
          <img
            src={occurrence.coverMedia.readUrl}
            alt=""
            loading="lazy"
            decoding="async"
          />
        </div>
      ) : (
        <div className={`public-home__occurrence-thumb public-home__occurrence-thumb--${tone}`} aria-hidden="true">
          <span>{getCategorySymbol(occurrence.categorySlug)}</span>
        </div>
      )}

      <div className="public-home__occurrence-main">
        <OccurrenceSupportButton
          occurrenceId={occurrence.id}
          initialCount={occurrence.supportCount}
          className="public-occurrence-support--card"
        />
        <span className={`public-home__category public-home__category--${tone}`}>
          {getCategorySymbol(occurrence.categorySlug)} {occurrence.categoryName || 'Ocorrência urbana'}
        </span>
        <div className="public-occurrence-breadcrumb" aria-label="Identificação da ocorrência">
          <span>{occurrence.publicCode}</span>
          {occurrence.externalProtocolNumber ? (
            <>
              <span aria-hidden="true">/</span>
              <strong>Protocolo {occurrence.externalProtocolNumber}</strong>
            </>
          ) : null}
        </div>
        <h3>{occurrence.title}</h3>
        <p className="public-home__occurrence-location">
          <span aria-hidden="true">●</span> {occurrence.addressText}
        </p>
        {occurrence.description && <p className="public-home__occurrence-description">{occurrence.description}</p>}
      </div>

      <div className="public-home__occurrence-meta">
        <span className={`public-home__occurrence-status public-home__occurrence-status--${getStatusClass(occurrence.status)}`}>
          {getStatusLabel(occurrence.status)}
        </span>
        <span className="public-home__occurrence-time"><span aria-hidden="true">◷</span> {formatTime(occurrence.updatedAt)}</span>
      </div>
    </article>
  );
}
