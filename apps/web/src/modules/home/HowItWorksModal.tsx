import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';

interface HowItWorksModalProps {
  open: boolean;
  onClose: () => void;
}

const HOW_IT_WORKS_VIDEO_URL = '/media/como-funciona.mp4';

export function HowItWorksModal({ open, onClose }: HowItWorksModalProps) {
  const navigate = useNavigate();
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const [videoUnavailable, setVideoUnavailable] = useState(false);

  useEffect(() => {
    if (!open) return;

    const previousOverflow = document.body.style.overflow;
    const previouslyFocused = document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null;

    document.body.style.overflow = 'hidden';
    setVideoUnavailable(false);
    window.requestAnimationFrame(() => closeButtonRef.current?.focus());

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose();
    }

    document.addEventListener('keydown', handleKeyDown);

    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener('keydown', handleKeyDown);
      videoRef.current?.pause();
      previouslyFocused?.focus();
    };
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div
      className="how-it-works-modal"
      role="presentation"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}
    >
      <section
        className="how-it-works-modal__dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="how-it-works-modal-title"
      >
        <div className="how-it-works-modal__header">
          <div>
            <span>Conheça o CIDADEMDIA</span>
            <h2 id="how-it-works-modal-title">Como funciona</h2>
          </div>
          <button
            ref={closeButtonRef}
            className="how-it-works-modal__close"
            type="button"
            aria-label="Fechar vídeo Como funciona"
            onClick={onClose}
          >
            <i className="fa-solid fa-xmark" aria-hidden="true" />
          </button>
        </div>

        <div className="how-it-works-modal__player">
          {videoUnavailable ? (
            <div className="how-it-works-modal__unavailable" role="status">
              <i className="fa-solid fa-circle-exclamation" aria-hidden="true" />
              <strong>Vídeo temporariamente indisponível.</strong>
              <span>Tente novamente em instantes.</span>
            </div>
          ) : (
            <video
              ref={videoRef}
              src={HOW_IT_WORKS_VIDEO_URL}
              controls
              autoPlay
              playsInline
              preload="metadata"
              onError={() => setVideoUnavailable(true)}
              aria-label="Vídeo Como funciona do CIDADEMDIA"
            >
              Seu navegador não suporta reprodução de vídeo.
            </video>
          )}
        </div>

        <div className="how-it-works-modal__footer">
          <div>
            <strong>Prefere acompanhar cada etapa?</strong>
            <span>Abra o guia completo com vídeo e infográfico lado a lado.</span>
          </div>
          <button
            type="button"
            onClick={() => {
              onClose();
              navigate('/como-funciona');
            }}
          >
            Ver guia completo
            <i className="fa-solid fa-arrow-right" aria-hidden="true" />
          </button>
        </div>
      </section>
    </div>
  );
}
