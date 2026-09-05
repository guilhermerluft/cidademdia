import { useEffect, useState } from 'react';
import { CitizenOccurrenceChatButton } from '../chat/CitizenOccurrenceChatButton';
import { listOccurrenceMediaForPresentation } from './occurrenceService';
import type { OccurrenceMediaPresentation } from './types';

interface OccurrenceMediaGalleryProps {
  occurrenceId: string;
  occurrenceCode: string;
}

export function OccurrenceMediaGallery({ occurrenceId, occurrenceCode }: OccurrenceMediaGalleryProps) {
  const [media, setMedia] = useState<OccurrenceMediaPresentation[]>([]);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setFailed(false);

    void listOccurrenceMediaForPresentation(occurrenceId)
      .then((items) => {
        if (active) setMedia(items);
      })
      .catch(() => {
        if (active) {
          setMedia([]);
          setFailed(true);
        }
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [occurrenceId]);

  return (
    <>
      {loading ? (
        <div className="occurrence-media-gallery__loading" aria-label={`Carregando mídias de ${occurrenceCode}`} />
      ) : failed ? (
        <small className="occurrence-media-gallery__error">Não foi possível carregar as mídias desta ocorrência.</small>
      ) : media.length > 0 ? (
        <div className="occurrence-media-gallery" aria-label={`Mídias da ocorrência ${occurrenceCode}`}>
          {media.map((item) => {
            if (item.contentType.startsWith('image/')) {
              return (
                <a
                  className="occurrence-media-gallery__item"
                  href={item.readUrl}
                  target="_blank"
                  rel="noreferrer"
                  key={item.id}
                  aria-label={`Abrir imagem ${item.originalFileName}`}
                >
                  <img
                    src={item.readUrl}
                    alt={item.originalFileName || `Imagem da ocorrência ${occurrenceCode}`}
                    loading="lazy"
                  />
                </a>
              );
            }

            if (item.contentType.startsWith('video/')) {
              return (
                <div className="occurrence-media-gallery__item occurrence-media-gallery__item--video" key={item.id}>
                  <video
                    src={item.readUrl}
                    controls
                    preload="metadata"
                    playsInline
                    aria-label={item.originalFileName || `Vídeo da ocorrência ${occurrenceCode}`}
                  />
                </div>
              );
            }

            return null;
          })}
        </div>
      ) : null}

      <CitizenOccurrenceChatButton
        occurrenceId={occurrenceId}
        publicCode={occurrenceCode}
      />
    </>
  );
}
