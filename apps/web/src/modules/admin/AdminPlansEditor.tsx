import { useEffect, useMemo, useState } from 'react';
import { isAxiosError } from 'axios';
import { Badge, Button, Card, CardBody, SectionHeading } from '../../components/ui';
import { getAdminBilling } from './adminService';
import { updateAdminPlan } from './adminManagementService';
import type { AdminPlan } from './types';

interface PlanFormState {
  price: string;
  signupFee: string;
  subaccountLimit: string;
  monthlyPublicationLimit: string;
  reason: string;
}

function moneyFromCents(cents: number) {
  return (cents / 100).toFixed(2).replace('.', ',');
}

function centsFromMoney(value: string) {
  const normalized = value.trim().replace(/\./g, '').replace(',', '.');
  const parsed = Number(normalized);
  return Number.isFinite(parsed) && parsed >= 0 ? Math.round(parsed * 100) : null;
}

function formatMoney(cents: number) {
  return new Intl.NumberFormat('pt-BR', {
    style: 'currency',
    currency: 'BRL',
  }).format(cents / 100);
}

function intervalLabel(months: number) {
  if (months === 1) return 'Mensal';
  if (months === 3) return 'Trimestral';
  if (months === 6) return 'Semestral';
  if (months === 12) return 'Anual';
  return `${months} meses`;
}

function requestErrorMessage(error: unknown) {
  if (isAxiosError(error)) {
    const data = error.response?.data as { error?: string } | undefined;
    switch (data?.error) {
      case 'admin_plan_version_not_current':
        return 'Este plano foi alterado por outra operação. Atualize a lista e tente novamente.';
      case 'admin_plan_values_invalid':
        return 'Revise os valores e limites informados.';
      case 'admin_reason_required':
        return 'Informe um motivo com pelo menos 3 caracteres.';
      case 'admin_plan_version_not_found':
        return 'A versão do plano não foi encontrada.';
    }
  }

  return 'Não foi possível atualizar o plano agora.';
}

function createForm(plan: AdminPlan): PlanFormState {
  return {
    price: moneyFromCents(plan.priceCents),
    signupFee: moneyFromCents(plan.signupFeeCents),
    subaccountLimit: String(plan.subaccountLimit),
    monthlyPublicationLimit: String(plan.monthlyPublicationLimit),
    reason: '',
  };
}

export function AdminPlansEditor() {
  const [plans, setPlans] = useState<AdminPlan[]>([]);
  const [editing, setEditing] = useState<AdminPlan | null>(null);
  const [form, setForm] = useState<PlanFormState | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  async function refresh() {
    setLoading(true);
    setError(null);
    try {
      const snapshot = await getAdminBilling(1, 1);
      setPlans(snapshot.plans);
    } catch (requestError) {
      setError(requestErrorMessage(requestError));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void refresh();
  }, []);

  const groupedPlans = useMemo(() => {
    const groups = new Map<string, AdminPlan[]>();
    for (const plan of plans) {
      const current = groups.get(plan.planKey) ?? [];
      current.push(plan);
      groups.set(plan.planKey, current);
    }

    return Array.from(groups.entries()).map(([key, items]) => ({
      key,
      name: items[0]?.planName ?? key,
      items: items.slice().sort((left, right) => left.billingIntervalMonths - right.billingIntervalMonths),
    }));
  }, [plans]);

  function beginEdit(plan: AdminPlan) {
    setEditing(plan);
    setForm(createForm(plan));
    setError(null);
    setMessage(null);
  }

  async function save(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editing || !form) return;

    const priceCents = centsFromMoney(form.price);
    const signupFeeCents = centsFromMoney(form.signupFee);
    const subaccountLimit = Number(form.subaccountLimit);
    const monthlyPublicationLimit = Number(form.monthlyPublicationLimit);

    if (
      priceCents === null
      || signupFeeCents === null
      || !Number.isInteger(subaccountLimit)
      || subaccountLimit < 0
      || !Number.isInteger(monthlyPublicationLimit)
      || monthlyPublicationLimit < 0
    ) {
      setError('Revise os valores monetários e os limites do plano.');
      return;
    }

    if (form.reason.trim().length < 3) {
      setError('Informe o motivo da alteração.');
      return;
    }

    setSaving(true);
    setError(null);
    setMessage(null);

    try {
      const result = await updateAdminPlan(editing.planVersionId, {
        priceCents,
        signupFeeCents,
        subaccountLimit,
        monthlyPublicationLimit,
        reason: form.reason.trim(),
      });

      setMessage(result.changed
        ? `${editing.planName} (${intervalLabel(editing.billingIntervalMonths)}) atualizado. A versão anterior foi preservada para assinaturas existentes.`
        : 'Nenhuma alteração de valores foi necessária.');
      setEditing(null);
      setForm(null);
      await refresh();
    } catch (requestError) {
      setError(requestErrorMessage(requestError));
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="admin-management-section" aria-labelledby="admin-plans-editor-title">
      <SectionHeading
        title="Planos"
        subtitle="Altere preço, taxa de adesão e limites do catálogo público. Cada alteração cria uma nova versão sem reescrever contratos já existentes."
      />

      {message ? <p className="admin-management-feedback admin-management-feedback--success" role="status">{message}</p> : null}
      {error ? <p className="admin-management-feedback admin-management-feedback--error" role="alert">{error}</p> : null}
      {loading ? <p className="admin-management-feedback" role="status">Carregando planos...</p> : null}

      {!loading && (
        <div className="admin-plan-editor-groups">
          {groupedPlans.map((group) => (
            <div className="admin-plan-editor-group" key={group.key}>
              <div className="admin-plan-editor-group__heading">
                <div>
                  <span>Plano</span>
                  <h3>{group.name}</h3>
                </div>
                <Badge variant="info">{group.items.length} modalidade(s)</Badge>
              </div>

              <div className="admin-plan-editor-grid">
                {group.items.map((plan) => (
                  <Card className="admin-plan-editor-card" key={plan.planVersionId}>
                    <CardBody>
                      <div className="admin-plan-editor-card__heading">
                        <strong>{intervalLabel(plan.billingIntervalMonths)}</strong>
                        <Badge>v{plan.version}</Badge>
                      </div>
                      <dl>
                        <div><dt>Valor</dt><dd>{formatMoney(plan.priceCents)}</dd></div>
                        <div><dt>Adesão</dt><dd>{formatMoney(plan.signupFeeCents)}</dd></div>
                        <div><dt>Subcontas</dt><dd>{plan.subaccountLimit}</dd></div>
                        <div><dt>Publicações/mês</dt><dd>{plan.monthlyPublicationLimit}</dd></div>
                      </dl>
                      <Button size="sm" onClick={() => beginEdit(plan)}>Alterar plano</Button>
                    </CardBody>
                  </Card>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}

      {editing && form ? (
        <div className="admin-management-dialog-backdrop" role="presentation" onMouseDown={() => !saving && setEditing(null)}>
          <div className="admin-management-dialog" role="dialog" aria-modal="true" aria-labelledby="admin-plan-dialog-title" onMouseDown={(event) => event.stopPropagation()}>
            <div className="admin-management-dialog__heading">
              <div>
                <span>Alterar catálogo</span>
                <h3 id="admin-plan-dialog-title">{editing.planName} · {intervalLabel(editing.billingIntervalMonths)}</h3>
              </div>
              <button type="button" aria-label="Fechar" onClick={() => !saving && setEditing(null)}>×</button>
            </div>

            <form className="admin-plan-edit-form" onSubmit={save}>
              <div className="admin-plan-edit-form__grid">
                <label>
                  Valor do plano (R$)
                  <input value={form.price} onChange={(event) => setForm({ ...form, price: event.target.value })} inputMode="decimal" required />
                </label>
                <label>
                  Taxa de adesão (R$)
                  <input value={form.signupFee} onChange={(event) => setForm({ ...form, signupFee: event.target.value })} inputMode="decimal" required />
                </label>
                <label>
                  Limite de subcontas
                  <input type="number" min={0} step={1} value={form.subaccountLimit} onChange={(event) => setForm({ ...form, subaccountLimit: event.target.value })} required />
                </label>
                <label>
                  Publicações por mês
                  <input type="number" min={0} step={1} value={form.monthlyPublicationLimit} onChange={(event) => setForm({ ...form, monthlyPublicationLimit: event.target.value })} required />
                </label>
              </div>

              <label>
                Motivo da alteração
                <textarea rows={3} maxLength={500} value={form.reason} onChange={(event) => setForm({ ...form, reason: event.target.value })} placeholder="Ex.: reajuste comercial aprovado" required />
              </label>

              <div className="admin-management-dialog__actions">
                <Button type="button" variant="ghost" onClick={() => setEditing(null)} disabled={saving}>Cancelar</Button>
                <Button type="submit" disabled={saving}>{saving ? 'Salvando...' : 'Salvar nova versão'}</Button>
              </div>
            </form>
          </div>
        </div>
      ) : null}
    </section>
  );
}
