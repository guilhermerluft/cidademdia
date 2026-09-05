import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from './ui';
import {
  subscribeCommercialSignup,
  type CommercialSignupIntent,
} from './commercialSignup';

export function CommercialSignupModal() {
  const navigate = useNavigate();
  const [intent, setIntent] = useState<CommercialSignupIntent | null>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);

  useEffect(() => subscribeCommercialSignup((nextIntent) => setIntent(nextIntent)), []);

  useEffect(() => {
    if (!intent) return;

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    closeButtonRef.current?.focus();

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setIntent(null);
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      document.body.style.overflow = previousOverflow;
    };
  }, [intent]);

  if (!intent) return null;

  const actionCopy = intent === 'support'
    ? 'apoiar ocorrências e participar das interações'
    : 'visualizar os detalhes completos das ocorrências';

  function goToRegistration() {
    setIntent(null);
    navigate('/?auth=register');
  }

  return (
    <div
      className="commercial-signup-modal"
      role="presentation"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) setIntent(null);
      }}
    >
      <section
        className="commercial-signup-modal__dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="commercial-signup-title"
        aria-describedby="commercial-signup-description"
      >
        <button
          ref={closeButtonRef}
          className="commercial-signup-modal__close"
          type="button"
          aria-label="Fechar convite para cadastro"
          onClick={() => setIntent(null)}
        >
          ×
        </button>

        <div className="commercial-signup-modal__icon" aria-hidden="true">◎</div>
        <span className="commercial-signup-modal__eyebrow">Participe da sua cidade</span>
        <h2 id="commercial-signup-title">Crie sua conta gratuita para interagir</h2>
        <p id="commercial-signup-description">
          Para {actionCopy}, crie sua conta no CidadeEmDia. O cadastro é gratuito e também permite publicar suas próprias ocorrências e acompanhar o andamento delas.
        </p>

        <ul className="commercial-signup-modal__benefits" aria-label="Benefícios da conta gratuita">
          <li><span aria-hidden="true">✓</span> Apoie ocorrências da sua região</li>
          <li><span aria-hidden="true">✓</span> Publique ocorrências gratuitamente</li>
          <li><span aria-hidden="true">✓</span> Acompanhe detalhes e atualizações</li>
        </ul>

        <Button type="button" size="lg" fullWidth onClick={goToRegistration}>Cadastre-se</Button>
      </section>
    </div>
  );
}
