import { useEffect, useMemo, useState } from 'react';
import { isAxiosError } from 'axios';
import { Badge, Button, Card, CardBody, SectionHeading } from '../../components/ui';
import { listMasterSubaccounts } from '../subaccounts/subaccountService';
import type { MasterSubaccountMember } from '../subaccounts/types';
import {
  assignOccurrenceTarget,
  changeAssignedOccurrenceStatus,
  listAssignedOccurrences,
  listMasterOccurrenceTargets,
  unassignOccurrenceTarget,
  type AssignedOccurrence,
  type MasterOccurrenceTarget,
} from './occurrenceAssignmentService';

interface OccurrenceAssignmentPanelProps {
  mode: 'master' | 'subaccount';
}

const STATUS_LABELS: Record<string, string> = {
  NOVA: 'Nova',
  RECEBIDA: 'Recebida',
  EM_ANALISE: 'Em análise',
  EM_ANDAMENTO: 'Em andamento',
  AGUARDANDO_INFORMACAO: 'Aguardando informação',
  RESOLVIDA: 'Resolvida',
  ENCERRADA: 'Encerrada',
  CANCELADA: 'Cancelada',
};

const NEXT_STATUS: Record<string, string> = {
  RECEBIDA: 'EM_ANALISE',
  EM_ANALISE: 'EM_ANDAMENTO',
  EM_ANDAMENTO: 'AGUARDANDO_INFORMACAO',
  AGUARDANDO_INFORMACAO: 'RESOLVIDA',
};

function messageFromError(error: unknown) {
  if (isAxiosError(error)) {
    const payload = error.response?.data as { code?: string; detail?: string } | undefined;
    if (payload?.code === 'target_not_accepted') return 'A Master precisa aceitar a ocorrência antes de atribuí-la.';
    if (payload?.code === 'subaccount_link_not_found') return 'A subconta selecionada não está mais ativa nesta Master.';
    if (payload?.code === 'accepted_master_or_assigned_subaccount_required') return 'Seu assignment ou sua permissão não está mais ativo.';
    if (payload?.detail) return payload.detail;
  }

  return 'Não foi possível concluir a operação agora.';
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value));
}

export function OccurrenceAssignmentPanel({ mode }: OccurrenceAssignmentPanelProps) {
  const [masterTargets, setMasterTargets] = useState<MasterOccurrenceTarget[]>([]);
  const [assignedOccurrences, setAssignedOccurrences] = useState<AssignedOccurrence[]>([]);
  const [members, setMembers] = useState<MasterSubaccountMember[]>([]);
  const [loading, setLoading] = useState(true);
  const [savingId, setSavingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const activeMembers = useMemo(
    () => members.filter((member) => member.status.toUpperCase() === 'ACTIVE'),
    [members],
  );

  async function load() {
    if (mode === 'master') {
      const [targets, team] = await Promise.all([listMasterOccurrenceTargets(), listMasterSubaccounts()]);
      setMasterTargets(targets);
      setMembers(team.members);
      return;
    }

    setAssignedOccurrences(await listAssignedOccurrences());
  }

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);

    void load()
      .catch((requestError) => {
        if (active) setError(messageFromError(requestError));
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [mode]);

  async function handleAssignment(target: MasterOccurrenceTarget, linkId: string) {
    setSavingId(target.targetId);
    setError(null);
    setMessage(null);

    try {
      if (linkId) {
        const assignment = await assignOccurrenceTarget(target.targetId, linkId);
        setMessage(`Ocorrência ${target.publicCode} atribuída a ${assignment.subaccountDisplayName}.`);
      } else {
        await unassignOccurrenceTarget(target.targetId);
        setMessage(`Assignment da ocorrência ${target.publicCode} removido.`);
      }
      await load();
    } catch (requestError) {
      setError(messageFromError(requestError));
    } finally {
      setSavingId(null);
    }
  }

  async function advanceStatus(item: AssignedOccurrence) {
    const nextStatus = NEXT_STATUS[item.occurrenceStatus];
    if (!nextStatus) return;

    setSavingId(item.occurrenceId);
    setError(null);
    setMessage(null);

    try {
      await changeAssignedOccurrenceStatus(item.occurrenceId, nextStatus);
      setMessage(`Ocorrência ${item.publicCode} avançada para ${STATUS_LABELS[nextStatus] ?? nextStatus}.`);
      await load();
    } catch (requestError) {
      setError(messageFromError(requestError));
    } finally {
      setSavingId(null);
    }
  }

  if (mode === 'master') {
    return (
      <section className="dashboard-section assignment-panel" id="dashboard-assignments">
        <SectionHeading
          title="Distribuição de ocorrências"
          subtitle="Atribua cada ocorrência aceita a uma subconta específica. Permissões continuam sendo obrigatórias."
        />
        {error ? <div className="assignment-feedback assignment-feedback--error">{error}</div> : null}
        {message ? <div className="assignment-feedback assignment-feedback--success">{message}</div> : null}
        <div className="assignment-list">
          {loading ? <Card><CardBody>Carregando ocorrências...</CardBody></Card> : null}
          {!loading && masterTargets.length === 0 ? <Card><CardBody>Nenhuma ocorrência direcionada a esta Master.</CardBody></Card> : null}
          {masterTargets.map((target) => {
            const accepted = target.targetStatus === 'ACCEPTED';
            return (
              <Card key={target.targetId} className="assignment-card">
                <CardBody>
                  <div className="assignment-card__main">
                    <div>
                      <span className="assignment-card__code">{target.publicCode}</span>
                      <h3>{target.title}</h3>
                      <p>{target.addressText}</p>
                    </div>
                    <div className="assignment-card__badges">
                      <Badge variant={accepted ? 'success' : 'neutral'}>{target.targetStatus}</Badge>
                      <Badge variant="primary">{STATUS_LABELS[target.occurrenceStatus] ?? target.occurrenceStatus}</Badge>
                    </div>
                  </div>
                  <label className="assignment-card__select">
                    Subconta responsável
                    <select
                      value={target.assignment?.masterSubaccountId ?? ''}
                      disabled={!accepted || savingId === target.targetId}
                      onChange={(event) => void handleAssignment(target, event.target.value)}
                    >
                      <option value="">Sem assignment</option>
                      {activeMembers.map((member) => (
                        <option key={member.linkId} value={member.linkId}>{member.displayName} — {member.email}</option>
                      ))}
                    </select>
                  </label>
                  {!accepted ? <small>A ocorrência só pode ser distribuída depois do aceite da Master.</small> : null}
                  {target.assignment ? <small>Atribuída em {formatDate(target.assignment.assignedAt)}.</small> : null}
                </CardBody>
              </Card>
            );
          })}
        </div>
      </section>
    );
  }

  return (
    <section className="dashboard-section assignment-panel" id="dashboard-assigned-occurrences">
      <SectionHeading
        title="Ocorrências atribuídas a você"
        subtitle="Somente assignments explícitos e permitidos pela sua Master aparecem aqui."
      />
      {error ? <div className="assignment-feedback assignment-feedback--error">{error}</div> : null}
      {message ? <div className="assignment-feedback assignment-feedback--success">{message}</div> : null}
      <div className="assignment-list">
        {loading ? <Card><CardBody>Carregando assignments...</CardBody></Card> : null}
        {!loading && assignedOccurrences.length === 0 ? <Card><CardBody>Nenhuma ocorrência foi atribuída à sua subconta.</CardBody></Card> : null}
        {assignedOccurrences.map((item) => {
          const nextStatus = NEXT_STATUS[item.occurrenceStatus];
          return (
            <Card key={item.assignmentId} className="assignment-card">
              <CardBody>
                <div className="assignment-card__main">
                  <div>
                    <span className="assignment-card__code">{item.publicCode}</span>
                    <h3>{item.title}</h3>
                    <p>{item.addressText}</p>
                  </div>
                  <Badge variant="primary">{STATUS_LABELS[item.occurrenceStatus] ?? item.occurrenceStatus}</Badge>
                </div>
                <small>Atribuída em {formatDate(item.assignedAt)}.</small>
                {item.canChangeStatus && nextStatus ? (
                  <Button
                    variant="secondary"
                    disabled={savingId === item.occurrenceId}
                    onClick={() => void advanceStatus(item)}
                  >
                    {savingId === item.occurrenceId ? 'Atualizando...' : `Avançar para ${STATUS_LABELS[nextStatus] ?? nextStatus}`}
                  </Button>
                ) : null}
              </CardBody>
            </Card>
          );
        })}
      </div>
    </section>
  );
}
