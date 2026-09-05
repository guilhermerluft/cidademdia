import { useState } from 'react';
import { useLocation } from 'react-router-dom';

const DISMISSED_KEY = 'ced-mobile-app-coming-soon-dismissed';
const PUBLIC_PATHS = new Set(['/', '/ocorrencias', '/representantes', '/planos']);

export function MobileAppComingSoonNotice() {
  const { pathname } = useLocation();
  const [visible, setVisible] = useState(() => {
    try {
      return window.sessionStorage.getItem(DISMISSED_KEY) !== '1';
    } catch {
      return true;
    }
  });

  if (!visible || !PUBLIC_PATHS.has(pathname)) return null;

  function dismiss() {
    try {
      window.sessionStorage.setItem(DISMISSED_KEY, '1');
    } catch {
      // Keep the dismiss action functional even when browser storage is unavailable.
    }

    setVisible(false);
  }

  return (
    <aside className="mobile-app-coming-soon" aria-label="Aplicativos CidadeEmDia em breve">
      <div className="mobile-app-coming-soon__platforms" aria-hidden="true">
        <i className="fa-brands fa-android" />
        <i className="fa-brands fa-apple" />
      </div>

      <div className="mobile-app-coming-soon__copy">
        <strong>Em breve no Android e iOS</strong>
        <span>O CIDADEMDIA também estará no seu celular.</span>
      </div>

      <button
        className="mobile-app-coming-soon__close"
        type="button"
        aria-label="Fechar aviso dos aplicativos"
        onClick={dismiss}
      >
        <i className="fa-solid fa-xmark" aria-hidden="true" />
      </button>
    </aside>
  );
}
