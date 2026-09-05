import { useEffect, useState } from 'react';
import { subscribeToToasts } from './toast';
import type { ToastEventDetail } from './toast';

const MAX_VISIBLE_TOASTS = 4;

function toneIcon(tone: ToastEventDetail['tone']) {
  switch (tone) {
    case 'success': return '✓';
    case 'warning': return '!';
    case 'error': return '×';
    default: return 'i';
  }
}

export function ToastViewport() {
  const [items, setItems] = useState<ToastEventDetail[]>([]);

  useEffect(() => subscribeToToasts((item) => {
    setItems((current) => [...current, item].slice(-MAX_VISIBLE_TOASTS));

    if (item.durationMs > 0) {
      window.setTimeout(() => {
        setItems((current) => current.filter((toast) => toast.id !== item.id));
      }, item.durationMs);
    }
  }), []);

  function dismiss(id: string) {
    setItems((current) => current.filter((toast) => toast.id !== id));
  }

  return (
    <div className="ced-toast-viewport" aria-live="polite" aria-atomic="false">
      {items.map((item) => (
        <div
          key={item.id}
          className={`ced-toast ced-toast--${item.tone}`}
          role={item.tone === 'error' ? 'alert' : 'status'}
          data-toast-id={item.id}
        >
          <span className="ced-toast__icon" aria-hidden="true">{toneIcon(item.tone)}</span>
          <span className="ced-toast__message">{item.message}</span>
          <button
            type="button"
            className="ced-toast__close"
            aria-label="Fechar aviso"
            onClick={() => dismiss(item.id)}
          >
            ×
          </button>
        </div>
      ))}
    </div>
  );
}
