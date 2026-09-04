import { useEffect, useState } from 'react';
import { toast } from '../../components/toast';
import { getAccessToken } from '../../services/api';
import { supportOccurrence } from '../home/homeService';

interface OccurrenceSupportButtonProps {
  occurrenceId: string;
  initialCount: number;
  className?: string;
}

export function OccurrenceSupportButton({
  occurrenceId,
  initialCount,
  className = '',
}: OccurrenceSupportButtonProps) {
  const [count, setCount] = useState(initialCount);
  const [supported, setSupported] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    setCount(initialCount);
  }, [initialCount]);

  async function handleSupport() {
    const authenticated = Boolean(getAccessToken());
    if (!authenticated) {
      toast.info('Entre ou crie sua conta para apoiar esta ocorrência.');
      return;
    }

    if (submitting) return;
    setSubmitting(true);

    try {
      const result = await supportOccurrence(occurrenceId);
      setCount(result.supportCount);
      setSupported(result.supportedByRequester);
    } finally {
      setSubmitting(false);
    }
  }

  const authenticated = Boolean(getAccessToken());

  return (
    <button
      type="button"
      className={`public-occurrence-support${supported ? ' is-supported' : ''}${className ? ` ${className}` : ''}`}
      aria-label={authenticated ? `Apoiar ocorrência. ${count} apoios` : `Entrar para apoiar ocorrência. ${count} apoios`}
      aria-pressed={authenticated ? supported : undefined}
      title={authenticated ? 'Apoiar esta ocorrência' : 'Entre para apoiar esta ocorrência'}
      disabled={submitting}
      onClick={(event) => {
        event.stopPropagation();
        void handleSupport();
      }}
      onKeyDown={(event) => event.stopPropagation()}
    >
      <span className="public-occurrence-support__icon" aria-hidden="true">↑</span>
      <span className="public-occurrence-support__count">{count}</span>
    </button>
  );
}
