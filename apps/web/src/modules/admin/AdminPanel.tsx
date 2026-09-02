import { useCallback, useEffect, useMemo, useState } from 'react';
import { isAxiosError } from 'axios';
import { Badge, Button, Card, CardBody, SectionHeading } from '../../components/ui';
import {
  changeAdminUserStatus,
  getAdminBilling,
  getAdminOverview,
  listAdminAuditLogs,
  listAdminInstitutions,
  listAdminOccurrences,
  listAdminPosts,
  listAdminUsers,
} from './adminService';
import type {
  AdminAuditLog,
  AdminBillingSnapshot,
  AdminInstitution,
  AdminOccurrence,
  AdminOverview,
  AdminPage,
  AdminPost,
  AdminTab,
  AdminUser,
} from './types';
import './styles/admin.css';

const tabs: Array<{ key: AdminTab; label: string }> = [
  { key: 'users', label: 'Usuários' },
  { key: 'institutions', label: 'Instituições' },
  { key: 'occurrences', label: 'Ocorrências' },
  { key: 'posts', label: 'Conteúdo' },
  { key: 'billing', label: 'Planos e billing' },
  { key: 'audit', label: 'Auditoria' },
];

function formatDate(value: string | null | undefined) {
  return value ? new Date(value).toLocaleString('pt-BR') : '—';
}

function formatMoney(cents: number, currency = 'BRL') {
  return new Intl.NumberFormat('pt-BR', {
    style: 'currency',
    currency,
  }).format(cents / 100);
}

function statusVariant(status: string) {
  const normalized = status.toLowerCase();
  if (normalized.includes('active') || normalized.includes('published')) return 'success' as const;
  if (normalized.includes('suspend') || normalized.includes('pastdue') || normalized.includes('pending')) return 'warning' as const;
  if (normalized.includes('block') || normalized.includes('cancel') || normalized.includes('archiv')) return 'danger' as const;
  return 'neutral' as const;
}

function getRequestError(error: unknown) {
  if (isAxiosError(error)) {
    const data = error.response?.data as { error?: string; detail?: string } | undefined;
    switch (data?.error) {
      case 'admin_self_status_change_not_allowed':
        return 'O administrador não pode alterar o status da própria conta.';
      case 'admin_target_is_admin':
        return 'Contas administrativas não podem ter o status alterado por este painel.';
      case 'admin_reason_required':
        return 'Informe o motivo da alteração.';
      case 'admin_user_not_found':
        return 'Usuário não encontrado.';
    }
  }

  return 'Não foi possível concluir a operação administrativa.';
}

interface StatusAction {
  user: AdminUser;
  status: 'Active' | 'Suspended' | 'Blocked';
}

export function AdminPanel() {
  const [overview, setOverview] = useState<AdminOverview | null>(null);
  const [tab, setTab] = useState<AdminTab>('users');
  const [users, setUsers] = useState<AdminPage<AdminUser> | null>(null);
  const [institutions, setInstitutions] = useState<AdminPage<AdminInstitution> | null>(null);
  const [occurrences, setOccurrences] = useState<AdminPage<AdminOccurrence> | null>(null);
  const [posts, setPosts] = useState<AdminPage<AdminPost> | null>(null);
  const [billing, setBilling] = useState<AdminBillingSnapshot | null>(null);
  const [audit, setAudit] = useState<AdminPage<AdminAuditLog> | null>(null);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [action, setAction] = useState<StatusAction | null>(null);
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);

  const loadOverview = useCallback(async () => {
    setOverview(await getAdminOverview());
  }, []);

  const loadTab = useCallback(async (target: AdminTab, term = '') => {
    setLoading(true);
    setError(null);

    try {
      switch (target) {
        case 'users':
          setUsers(await listAdminUsers({ search: term || undefined, pageSize: 25 }));
          break;
        case 'institutions':
          setInstitutions(await listAdminInstitutions({ search: term || undefined, pageSize: 25 }));
          break;
        case 'occurrences':
          setOccurrences(await listAdminOccurrences({ search: term || undefined, pageSize: 25 }));
          break;
        case 'posts':
          setPosts(await listAdminPosts({ search: term || undefined, pageSize: 25 }));
          break;
        case 'billing':
          setBilling(await getAdminBilling(1, 25));
          break;
        case 'audit':
          setAudit(await listAdminAuditLogs(1, 25));
          break;
      }
    } catch (requestError) {
      setError(getRequestError(requestError));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void (async () => {
      try {
        await Promise.all([loadOverview(), loadTab('users', '')]);
      } catch (requestError) {
        setError(getRequestError(requestError));
        setLoading(false);
      }
    })();
  }, [loadOverview, loadTab]);

  const supportsSearch = tab !== 'billing' && tab !== 'audit';
  const summary = useMemo(() => overview ? [
    ['Usuários', overview.users],
    ['Masters', overview.masters],
    ['Subcontas', overview.subaccounts],
    ['Instituições', overview.institutions],
    ['Ocorrências', overview.occurrences],
    ['Publicações', overview.posts],
  ] : [], [overview]);

  async function handleSearch(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await loadTab(tab, search.trim());
  }

  async function submitStatusChange(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!action || !reason.trim()) return;

    setSaving(true);
    setError(null);
    try {
      await changeAdminUserStatus(action.user.id, action.status, reason.trim());
      setAction(null);
      setReason('');
      await Promise.all([loadOverview(), loadTab('users', search.trim())]);
      if (tab === 'audit') await loadTab('audit', '');
    } catch (requestError) {
      setError(getRequestError(requestError));
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="dashboard-section admin-panel" id="admin-panel" aria-labelledby="admin-panel-title">
      <SectionHeading
        title="Administração da plataforma"
        subtitle="Consulta operacional dos domínios essenciais. Alterações de plano e cota não são permitidas neste painel."
      />

      <div className="admin-overview" aria-label="Resumo administrativo">
        {summary.map(([label, value]) => (
          <Card className="admin-overview__card" key={label}>
            <CardBody>
              <span>{label}</span>
              <strong>{value}</strong>
            </CardBody>
          </Card>
        ))}
      </div>

      {overview ? (
        <div className="admin-account-status">
          <Badge variant="success">{overview.activeUsers} ativas</Badge>
          <Badge variant="warning">{overview.suspendedUsers} suspensas</Badge>
          <Badge variant="danger">{overview.blockedUsers} bloqueadas</Badge>
          <Badge variant="info">{overview.activeSubscriptions} assinaturas ativas</Badge>
        </div>
      ) : null}

      <div className="admin-tabs" role="tablist" aria-label="Áreas administrativas">
        {tabs.map((item) => (
          <button
            className={item.key === tab ? 'admin-tab admin-tab--active' : 'admin-tab'}
            key={item.key}
            onClick={() => {
              setTab(item.key);
              setSearch('');
              setAction(null);
              setReason('');
              void loadTab(item.key, '');
            }}
            role="tab"
            aria-selected={item.key === tab}
            type="button"
          >
            {item.label}
          </button>
        ))}
      </div>

      {supportsSearch ? (
        <form className="admin-search" onSubmit={handleSearch}>
          <label htmlFor="admin-search-input">Buscar</label>
          <div>
            <input
              id="admin-search-input"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Nome, e-mail, título ou endereço"
            />
            <Button type="submit" disabled={loading}>Buscar</Button>
          </div>
        </form>
      ) : null}

      {error ? <p className="admin-feedback admin-feedback--error" role="alert">{error}</p> : null}
      {loading ? <p className="admin-feedback" role="status">Carregando dados administrativos...</p> : null}

      {!loading && tab === 'users' && users ? (
        <div className="admin-list">
          <p className="admin-list__count">{users.totalItems} usuário(s)</p>
          {users.items.map((user) => (
            <Card className="admin-row" key={user.id}>
              <CardBody>
                <div className="admin-row__main">
                  <div>
                    <strong>{user.displayName}</strong>
                    <span>{user.email}</span>
                  </div>
                  <div className="admin-row__badges">
                    <Badge variant={statusVariant(user.status)}>{user.status}</Badge>
                    {user.roles.map((role) => <Badge key={role}>{role}</Badge>)}
                  </div>
                </div>
                <div className="admin-row__meta">
                  <span>E-mail {user.emailConfirmed ? 'confirmado' : 'não confirmado'}</span>
                  <span>Último acesso: {formatDate(user.lastLoginAt)}</span>
                </div>
                {!user.roles.includes('ADMIN') ? (
                  <div className="admin-row__actions">
                    {user.status !== 'Active' ? (
                      <Button size="sm" onClick={() => setAction({ user, status: 'Active' })}>Reativar</Button>
                    ) : null}
                    {user.status !== 'Suspended' ? (
                      <Button size="sm" variant="secondary" onClick={() => setAction({ user, status: 'Suspended' })}>Suspender</Button>
                    ) : null}
                    {user.status !== 'Blocked' ? (
                      <Button size="sm" variant="danger" onClick={() => setAction({ user, status: 'Blocked' })}>Bloquear</Button>
                    ) : null}
                  </div>
                ) : null}
              </CardBody>
            </Card>
          ))}
        </div>
      ) : null}

      {!loading && tab === 'institutions' && institutions ? (
        <div className="admin-list">
          <p className="admin-list__count">{institutions.totalItems} instituição(ões)</p>
          {institutions.items.map((institution) => (
            <Card className="admin-row" key={institution.id}>
              <CardBody>
                <div className="admin-row__main">
                  <div><strong>{institution.name}</strong><span>{institution.type} · {institution.scopeLevel}</span></div>
                  <Badge variant={statusVariant(institution.status)}>{institution.status}</Badge>
                </div>
                <div className="admin-row__meta">
                  <span>{institution.representatives} representantes</span>
                  <span>{institution.memberships} vínculos</span>
                  <span>{institution.stateCode ?? 'Sem UF'}</span>
                </div>
              </CardBody>
            </Card>
          ))}
        </div>
      ) : null}

      {!loading && tab === 'occurrences' && occurrences ? (
        <div className="admin-list">
          <p className="admin-list__count">{occurrences.totalItems} ocorrência(s)</p>
          {occurrences.items.map((occurrence) => (
            <Card className="admin-row" key={occurrence.id}>
              <CardBody>
                <div className="admin-row__main">
                  <div><strong>{occurrence.title}</strong><span>#{occurrence.publicCode}</span></div>
                  <Badge variant={statusVariant(occurrence.status)}>{occurrence.status}</Badge>
                </div>
                <div className="admin-row__meta">
                  <span>{occurrence.addressText}</span>
                  <span>{formatDate(occurrence.createdAt)}</span>
                </div>
              </CardBody>
            </Card>
          ))}
        </div>
      ) : null}

      {!loading && tab === 'posts' && posts ? (
        <div className="admin-list">
          <p className="admin-list__count">{posts.totalItems} publicação(ões)</p>
          {posts.items.map((post) => (
            <Card className="admin-row" key={post.id}>
              <CardBody>
                <div className="admin-row__main">
                  <div><strong>{post.title || 'Publicação sem título'}</strong><span>{post.type}</span></div>
                  <Badge variant={statusVariant(post.status)}>{post.status}</Badge>
                </div>
                <div className="admin-row__meta">
                  <span>{post.mediaCount} mídia(s)</span>
                  <span>{post.placementCount} placement(s)</span>
                  <span>{formatDate(post.createdAt)}</span>
                </div>
              </CardBody>
            </Card>
          ))}
        </div>
      ) : null}

      {!loading && tab === 'billing' && billing ? (
        <div className="admin-billing">
          <div className="admin-readonly-notice">
            <Badge variant="info">Somente consulta</Badge>
            <span>Planos, limites, assinaturas e pagamentos não podem ser alterados pelo Admin.</span>
          </div>
          <h3>Planos vigentes</h3>
          <div className="admin-plan-grid">
            {billing.plans.map((plan) => (
              <Card className="admin-plan" key={plan.planVersionId}>
                <CardBody>
                  <strong>{plan.planName}</strong>
                  <span>{plan.billingCategoryName} · {plan.billingIntervalMonths} mês(es)</span>
                  <b>{formatMoney(plan.priceCents)}</b>
                  <small>{plan.subaccountLimit} subcontas · {plan.monthlyPublicationLimit} publicações/mês</small>
                </CardBody>
              </Card>
            ))}
          </div>
          <h3>Assinaturas</h3>
          <div className="admin-list">
            {billing.subscriptions.items.length === 0 ? <p>Nenhuma assinatura registrada.</p> : null}
            {billing.subscriptions.items.map((subscription) => (
              <Card className="admin-row" key={subscription.id}>
                <CardBody>
                  <div className="admin-row__main">
                    <div><strong>{subscription.masterDisplayName}</strong><span>{subscription.masterEmail}</span></div>
                    <Badge variant={statusVariant(subscription.status)}>{subscription.status}</Badge>
                  </div>
                  <div className="admin-row__meta">
                    <span>{subscription.planName} · {subscription.offerKey}</span>
                    <span>Publicações: {subscription.currentPublicationCount}/{subscription.monthlyPublicationLimit}</span>
                    <span>Até {formatDate(subscription.currentPeriodEnd)}</span>
                  </div>
                </CardBody>
              </Card>
            ))}
          </div>
          <h3>Pagamentos</h3>
          <div className="admin-list">
            {billing.payments.items.length === 0 ? <p>Nenhum pagamento sincronizado.</p> : null}
            {billing.payments.items.map((payment) => (
              <Card className="admin-row" key={payment.id}>
                <CardBody>
                  <div className="admin-row__main">
                    <div><strong>{payment.masterEmail}</strong><span>{payment.provider}</span></div>
                    <Badge variant={statusVariant(payment.status)}>{payment.status}</Badge>
                  </div>
                  <div className="admin-row__meta">
                    <span>{formatMoney(payment.amountCents, payment.currency)}</span>
                    <span>{formatDate(payment.approvedAt || payment.createdAt)}</span>
                  </div>
                </CardBody>
              </Card>
            ))}
          </div>
        </div>
      ) : null}

      {!loading && tab === 'audit' && audit ? (
        <div className="admin-list">
          <p className="admin-list__count">{audit.totalItems} evento(s) auditado(s)</p>
          {audit.items.map((log) => (
            <Card className="admin-row" key={log.id}>
              <CardBody>
                <div className="admin-row__main">
                  <div><strong>{log.action}</strong><span>{log.actorDisplayName || log.actorEmail || log.actorUserId}</span></div>
                  <Badge>{log.entityType}</Badge>
                </div>
                <div className="admin-row__meta">
                  <span>{log.previousValue || '—'} → {log.newValue || '—'}</span>
                  <span>{log.reason}</span>
                  <span>{formatDate(log.occurredAt)}</span>
                </div>
              </CardBody>
            </Card>
          ))}
        </div>
      ) : null}

      {action ? (
        <form className="admin-status-form" onSubmit={submitStatusChange}>
          <div>
            <strong>Alterar status de {action.user.displayName}</strong>
            <p>Novo status: {action.status}. Esta ação será registrada na auditoria.</p>
          </div>
          <label>
            Motivo da alteração
            <textarea
              autoFocus
              maxLength={500}
              required
              rows={3}
              value={reason}
              onChange={(event) => setReason(event.target.value)}
            />
          </label>
          <div className="admin-status-form__actions">
            <Button type="button" variant="secondary" onClick={() => { setAction(null); setReason(''); }}>Cancelar</Button>
            <Button type="submit" disabled={saving || !reason.trim()}>{saving ? 'Salvando...' : 'Confirmar alteração'}</Button>
          </div>
        </form>
      ) : null}
    </section>
  );
}
