import { useEffect, useMemo, useState } from 'react';
import { isAxiosError } from 'axios';
import { Badge, Button, Card, CardBody, SectionHeading } from '../../components/ui';
import { api } from '../../services/api';
import { ChatPanel } from './ChatPanel';

export type ChatInboxMode = 'citizen' | 'master' | 'subaccount';

interface ChatInboxProps {
  mode: ChatInboxMode;
}

interface ChatTargetSummary {
  targetId: string;
  occurrenceId: string;
  publicCode: string;
  title: string;
  counterpart: string;
}

interface CitizenOccurrencePage {
  items: Array<{
    id: string;
    publicCode: string;
    title: string;
  }>;
}

interface CitizenOccurrenceTarget {
  id: string;
  occurrenceId: string;
  masterDisplayName: string;
  status: string;
}

interface MasterOccurrenceTarget {
  targetId: string;
  occurrenceId: string;
  publicCode: string;
  title: string;
  targetStatus: string;
}

interface AssignedOccurrence {
  targetId: string;
  occurrenceId: string;
  publicCode: string;
  title: string;
  targetStatus: string;
}

function errorMessage(error: unknown) {
  if (isAxiosError(error)) {
    const payload = error.response?.data as { detail?: string } | undefined;
    if (payload?.detail) return payload.detail;
  }

  return 'Não foi possível carregar as conversas agora.';
}

async function loadCitizenTargets(): Promise<ChatTargetSummary[]> {
  const { data: page } = await api.get<CitizenOccurrencePage>('/occurrences', {
    params: { page: 1, pageSize: 50 },
  });

  const groups = await Promise.all(
    page.items.map(async (occurrence) => {
      const { data: targets } = await api.get<CitizenOccurrenceTarget[]>(
        `/occurrences/${occurrence.id}/targets`,
      );

      return targets
        .filter((target) => target.status === 'ACCEPTED')
        .map((target) => ({
          targetId: target.id,
          occurrenceId: target.occurrenceId,
          publicCode: occurrence.publicCode,
          title: occurrence.title,
          counterpart: target.masterDisplayName || 'Conta Master',
        }));
    }),
  );

  return groups.flat();
}

async function loadMasterTargets(): Promise<ChatTargetSummary[]> {
  const { data } = await api.get<MasterOccurrenceTarget[]>('/master/occurrence-targets');

  return data
    .filter((target) => target.targetStatus === 'ACCEPTED')
    .map((target) => ({
      targetId: target.targetId,
      occurrenceId: target.occurrenceId,
      publicCode: target.publicCode,
      title: target.title,
      counterpart: 'Cidadão responsável',
    }));
}

async function loadSubaccountTargets(): Promise<ChatTargetSummary[]> {
  const { data } = await api.get<AssignedOccurrence[]>('/subaccount/occurrence-assignments');

  return data
    .filter((target) => target.targetStatus === 'ACCEPTED')
    .map((target) => ({
      targetId: target.targetId,
      occurrenceId: target.occurrenceId,
      publicCode: target.publicCode,
      title: target.title,
      counterpart: 'Conversa da ocorrência',
    }));
}

export function ChatInbox({ mode }: ChatInboxProps) {
  const [targets, setTargets] = useState<ChatTargetSummary[]>([]);
  const [selectedTargetId, setSelectedTargetId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    const nextTargets = mode === 'citizen'
      ? await loadCitizenTargets()
      : mode === 'master'
        ? await loadMasterTargets()
        : await loadSubaccountTargets();

    setTargets(nextTargets);
    setSelectedTargetId((current) => (
      current && nextTargets.some((target) => target.targetId === current)
        ? current
        : null
    ));
  }

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);

    void load()
      .catch((loadError) => {
        if (active) setError(errorMessage(loadError));
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [mode]);

  const selectedTarget = useMemo(
    () => targets.find((target) => target.targetId === selectedTargetId) ?? null,
    [selectedTargetId, targets],
  );

  return (
    <section className="dashboard-section" id="dashboard-chat" aria-labelledby="dashboard-chat-title">
      <SectionHeading
        title="Conversas"
        subtitle="Acesse o chat das ocorrências aceitas, com mensagens de texto e áudio privados."
      />

      {error ? <p className="chat-panel__notice" role="alert">{error}</p> : null}
      {loading ? <Card><CardBody>Carregando conversas...</CardBody></Card> : null}
      {!loading && targets.length === 0 ? (
        <Card>
          <CardBody>Nenhuma conversa disponível. O chat é aberto quando uma ocorrência compartilhada é aceita.</CardBody>
        </Card>
      ) : null}

      {!loading && targets.length > 0 ? (
        <div className="dashboard-summary-grid">
          {targets.map((target) => {
            const selected = target.targetId === selectedTargetId;
            return (
              <Card key={target.targetId} className="dashboard-summary-card">
                <CardBody>
                  <span className="dashboard-summary-card__label">{target.publicCode}</span>
                  <strong>{target.title}</strong>
                  <small>{target.counterpart}</small>
                  <Badge variant="success">Chat ativo</Badge>
                  <Button
                    type="button"
                    variant={selected ? 'secondary' : 'primary'}
                    onClick={() => setSelectedTargetId(selected ? null : target.targetId)}
                  >
                    {selected ? 'Fechar conversa' : 'Abrir conversa'}
                  </Button>
                </CardBody>
              </Card>
            );
          })}
        </div>
      ) : null}

      {selectedTarget ? (
        <ChatPanel
          key={selectedTarget.targetId}
          targetId={selectedTarget.targetId}
          title={`Conversa ${selectedTarget.publicCode}`}
        />
      ) : null}
    </section>
  );
}
