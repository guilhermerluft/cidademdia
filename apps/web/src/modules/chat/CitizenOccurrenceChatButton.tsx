import { useEffect, useState } from 'react';
import { Button } from '../../components/ui';
import { api } from '../../services/api';
import { OccurrenceChatModal } from './OccurrenceChatModal';

interface CitizenOccurrenceTarget {
  id: string;
  occurrenceId: string;
  masterDisplayName: string;
  status: string;
}

interface CitizenOccurrenceChatButtonProps {
  occurrenceId: string;
  publicCode: string;
  occurrenceTitle?: string;
}

export function CitizenOccurrenceChatButton({
  occurrenceId,
  publicCode,
  occurrenceTitle,
}: CitizenOccurrenceChatButtonProps) {
  const [targets, setTargets] = useState<CitizenOccurrenceTarget[]>([]);
  const [selectedTarget, setSelectedTarget] = useState<CitizenOccurrenceTarget | null>(null);

  useEffect(() => {
    let active = true;

    void api.get<CitizenOccurrenceTarget[]>(`/occurrences/${occurrenceId}/targets`)
      .then(({ data }) => {
        if (!active) return;
        setTargets(data.filter((target) => target.status === 'ACCEPTED'));
      })
      .catch(() => {
        if (active) setTargets([]);
      });

    return () => {
      active = false;
    };
  }, [occurrenceId]);

  if (targets.length === 0) return null;

  return (
    <>
      <div className="occurrence-context-chat-actions" aria-label={`Conversas da ocorrência ${publicCode}`}>
        {targets.map((target) => (
          <Button
            key={target.id}
            type="button"
            variant="secondary"
            size="sm"
            onClick={() => setSelectedTarget(target)}
          >
            <i className="fa-regular fa-comments" aria-hidden="true" />
            Conversar{target.masterDisplayName ? ` com ${target.masterDisplayName}` : ''}
          </Button>
        ))}
      </div>

      {selectedTarget ? (
        <OccurrenceChatModal
          targetId={selectedTarget.id}
          publicCode={publicCode}
          title={occurrenceTitle ?? `Ocorrência ${publicCode}`}
          onClose={() => setSelectedTarget(null)}
        />
      ) : null}
    </>
  );
}
