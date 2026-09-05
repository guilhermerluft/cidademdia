import { useEffect, useRef } from 'react';
import { ChatPanel } from './ChatPanel';

interface OccurrenceChatModalProps {
  targetId: string;
  publicCode: string;
  title: string;
  onClose: () => void;
}

export function OccurrenceChatModal({ targetId, publicCode, title, onClose }: OccurrenceChatModalProps) {
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
      className="occurrence-chat-modal__backdrop"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}
    >
      <div
        ref={dialogRef}
        className="occurrence-chat-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="occurrence-chat-modal-title"
        tabIndex={-1}
      >
        <header className="occurrence-chat-modal__header">
          <div>
            <span>{publicCode}</span>
            <h2 id="occurrence-chat-modal-title">{title}</h2>
          </div>
          <button
            type="button"
            className="occurrence-chat-modal__close"
            aria-label="Fechar conversa"
            onClick={onClose}
          >
            <i className="fa-solid fa-xmark" aria-hidden="true" />
          </button>
        </header>

        <ChatPanel targetId={targetId} title={`Conversa ${publicCode}`} />
      </div>
    </div>
  );
}
