import { useEffect, useRef } from 'react';
import type { PublicOccurrenceDetails } from '../home/homeService';
import { OccurrenceSupportButton } from './OccurrenceSupportButton';

interface PublicOccurrenceDetailsModalProps {
  occurrence: PublicOccurrenceDetails;
  onClose: () => void;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('pt-BR', {
    dateStyle: 'medium',
    timeStyle: 'short',
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
    case 'ENCERRADA': return 'Encerrada';
    case 'CANCELADA': return 'Cancelada';
    default: return status.replaceAll('_', ' ').toLowerCase();
  }
}

export function PublicOccurrenceDetailsModal({ occurrence, onClose }: PublicOccurrenceDetailsModalProps) {
  const dialogRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    dialogRef.current?.focus();

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose();
    }

    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('keydown', handleKeyDown);
      document.body.style.overflow = previousOverflow;
    };
  }, [onClose]);

  return (
    <div
      className="public-occurrence-details__backdrop"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}
    >
      <div
        ref={dialogRef}
        className="public-occurrence-details"
        role="dialog"
        aria-modal="true"
        aria-labelledby="public-occurrence-details-title"
        tabIndex={-1}
      >
        <header className="public-occurrence-details__header">
          <div>
            <div className="public-occurrence-breadcrumb public-occurrence-breadcrumb--details" aria-label="Identificação da ocorrência">
              <span>{occurrence.publicCode}</span>
              {occurrence.externalProtocolNumber ? (
                <>
                  <span aria-hidden="true">/</span>
                  <strong>Protocolo {occurrence.externalProtocolNumber}</strong>
                </>
              ) : null}
            </div>
            <h2 id="public-occurrence-details-title">{occurrence.title}</h2>
            <p>{occurrence.categoryName || 'Ocorrência urbana'}</p>
          </div>
          <div className="public-occurrence-details__header-actions">
            <OccurrenceSupportButton
              occurrenceId={occurrence.id}
              initialCount={occurrence.supportCount}
              className="public-occurrence-support--details"
            />
            <button
              type="button"
              className="public-occurrence-details__close"
              aria-label="Fechar detalhes da ocorrência"
              onClick={onClose}
            >
              <i className="fa-solid fa-xmark" aria-hidden="true" />
            </button>
          </div>
        </header>

        {occurrence.media.length > 0 ? (
          <section className="public-occurrence-details__media" aria-label="Fotos e vídeos da ocorrência">
            {occurrence.media.map((media) => (
              <div className="public-occurrence-details__media-item" key={media.id}>
                {media.contentType.startsWith('image/') ? (
                  <a href={media.readUrl} target="_blank" rel="noreferrer" aria-label={`Abrir ${media.originalFileName}`}>
                    <img
                      src={media.readUrl}
                      alt={media.originalFileName}
                      loading="lazy"
                      decoding="async"
                    />
                  </a>
                ) : media.contentType.startsWith('video/') ? (
                  <video controls preload="metadata" src={media.readUrl} aria-label={media.originalFileName} />
                ) : null}
              </div>
            ))}
          </section>
        ) : null}

        <div className="public-occurrence-details__content">
          <section className="public-occurrence-details__summary">
            <div>
              <span>Status</span>
              <strong>{getStatusLabel(occurrence.status)}</strong>
            </div>
            <div>
              <span>Publicada em</span>
              <strong>{formatDate(occurrence.createdAt)}</strong>
            </div>
            <div>
              <span>Última atualização</span>
              <strong>{formatDate(occurrence.updatedAt)}</strong>
            </div>
          </section>

          <section className="public-occurrence-details__section">
            <h3>Informações da ocorrência</h3>
            {occurrence.description ? <p>{occurrence.description}</p> : <p>Sem descrição adicional.</p>}
          </section>

          <section className="public-occurrence-details__section public-occurrence-details__location">
            <h3>Localização</h3>
            <p>{occurrence.addressText}</p>
          </section>
        </div>
      </div>
    </div>
  );
}
