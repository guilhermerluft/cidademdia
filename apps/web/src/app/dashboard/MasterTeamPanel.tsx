import { useCallback, useEffect, useMemo, useState } from 'react';
import { isAxiosError } from 'axios';
import { Badge, Button, Card, CardBody, SectionHeading } from '../../components/ui';
import {
  createMasterSubaccount,
  inviteMasterSubaccount,
  listMasterSubaccounts,
  revokeMasterSubaccount,
  updateMasterSubaccountPermissions,
} from '../../modules/subaccounts/subaccountService';
import {
  SUBACCOUNT_PERMISSION_OPTIONS,
  type MasterSubaccountMember,
  type MasterSubaccountTeam,
} from '../../modules/subaccounts/types';

function getErrorCode(error: unknown) {
  if (!isAxiosError(error)) return null;
  const data = error.response?.data as { error?: string } | undefined;
  return data?.error ?? null;
}

function getErrorMessage(error: unknown) {
  switch (getErrorCode(error)) {
    case 'subaccount_user_not_found':
      return 'A conta ainda não existe. Não foi possível enviar o convite automaticamente.';
    case 'subaccount_limit_reached':
      return 'O limite de subcontas e convites pendentes da sua conta foi atingido.';
    case 'subaccount_already_linked':
      return 'Esta pessoa já está vinculada à sua conta Master.';
    case 'subaccount_user_already_registered':
      return 'A conta foi criada enquanto o convite era preparado. Tente adicionar novamente.';
    case 'incompatible_account_role':
      return 'Esta conta possui um perfil incompatível com o vínculo de subconta.';
    case 'subaccount_user_unavailable':
      return 'Esta conta não está disponível para ser vinculada no momento.';
    case 'invalid_permissions':
      return 'Uma ou mais permissões selecionadas não são válidas.';
    case 'subaccount_revoked':
      return 'Esta subconta já foi revogada.';
    case 'invitation_delivery_failed':
      return 'O convite foi preparado, mas o e-mail não pôde ser enviado. Tente novamente em instantes.';
    default:
      return 'Não foi possível concluir a operação agora. Tente novamente.';
  }
}

function initials(displayName: string) {
  const parts = displayName.trim().split(/\s+/).filter(Boolean);
  return (parts.slice(0, 2).map((part) => part[0]?.toUpperCase()).join('') || 'SC');
}

function permissionLabel(key: string) {
  return SUBACCOUNT_PERMISSION_OPTIONS.find((item) => item.key === key)?.label ?? key;
}

function formatExpiry(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 'prazo indisponível';
  return date.toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
}

function PermissionSelector({
  selected,
  onChange,
  disabled = false,
}: {
  selected: string[];
  onChange: (permissions: string[]) => void;
  disabled?: boolean;
}) {
  function toggle(permission: string) {
    onChange(selected.includes(permission)
      ? selected.filter((item) => item !== permission)
      : [...selected, permission]);
  }

  return (
    <div className="subaccount-permission-grid">
      {SUBACCOUNT_PERMISSION_OPTIONS.map((permission) => (
        <label className="subaccount-permission-option" key={permission.key}>
          <input
            type="checkbox"
            checked={selected.includes(permission.key)}
            disabled={disabled}
            onChange={() => toggle(permission.key)}
          />
          <span>
            <strong>{permission.label}</strong>
            <small>{permission.description}</small>
          </span>
        </label>
      ))}
    </div>
  );
}

export function MasterTeamPanel() {
  const [team, setTeam] = useState<MasterSubaccountTeam | null>(null);
  const [loading, setLoading] = useState(true);
  const [busyAction, setBusyAction] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [email, setEmail] = useState('');
  const [newPermissions, setNewPermissions] = useState<string[]>([]);
  const [editingLinkId, setEditingLinkId] = useState<string | null>(null);
  const [editingPermissions, setEditingPermissions] = useState<string[]>([]);

  const loadTeam = useCallback(async (showLoading = true) => {
    if (showLoading) setLoading(true);
    setError(null);

    try {
      setTeam(await listMasterSubaccounts());
    } catch (requestError) {
      setError(getErrorMessage(requestError));
    } finally {
      if (showLoading) setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadTeam();
  }, [loadTeam]);

  const usedCapacity = team ? team.activeCount + team.pendingInvitationCount : 0;

  const capacityLabel = useMemo(() => {
    if (!team) return 'Carregando equipe';
    if (team.limit === null) {
      return `${team.activeCount} ativa${team.activeCount === 1 ? '' : 's'} · ${team.pendingInvitationCount} convite${team.pendingInvitationCount === 1 ? '' : 's'}`;
    }
    return `${usedCapacity} de ${team.limit} vagas reservadas`;
  }, [team, usedCapacity]);

  async function handleCreate(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSuccess(null);
    setBusyAction('create');

    const input = { email: email.trim(), permissions: newPermissions };

    try {
      await createMasterSubaccount(input);
      setSuccess('Subconta vinculada com sucesso.');
      setEmail('');
      setNewPermissions([]);
      await loadTeam(false);
    } catch (requestError) {
      if (getErrorCode(requestError) !== 'subaccount_user_not_found') {
        setError(getErrorMessage(requestError));
      } else {
        try {
          await inviteMasterSubaccount(input);
          setSuccess('A pessoa ainda não possuía conta. Enviamos um convite para criar a conta e entrar na sua equipe.');
          setEmail('');
          setNewPermissions([]);
          await loadTeam(false);
        } catch (invitationError) {
          setError(getErrorMessage(invitationError));
        }
      }
    } finally {
      setBusyAction(null);
    }
  }

  function beginEditing(member: MasterSubaccountMember) {
    setEditingLinkId(member.linkId);
    setEditingPermissions([...member.permissions]);
    setError(null);
    setSuccess(null);
  }

  async function savePermissions(member: MasterSubaccountMember) {
    setBusyAction(`permissions:${member.linkId}`);
    setError(null);
    setSuccess(null);

    try {
      await updateMasterSubaccountPermissions(member.linkId, { permissions: editingPermissions });
      setEditingLinkId(null);
      setEditingPermissions([]);
      setSuccess(`Permissões de ${member.displayName} atualizadas.`);
      await loadTeam(false);
    } catch (requestError) {
      setError(getErrorMessage(requestError));
    } finally {
      setBusyAction(null);
    }
  }

  async function revoke(member: MasterSubaccountMember) {
    const confirmed = window.confirm(`Revogar o acesso de ${member.displayName}? A alteração entra em vigor imediatamente.`);
    if (!confirmed) return;

    setBusyAction(`revoke:${member.linkId}`);
    setError(null);
    setSuccess(null);

    try {
      await revokeMasterSubaccount(member.linkId);
      setEditingLinkId(null);
      setEditingPermissions([]);
      setSuccess(`Acesso de ${member.displayName} revogado.`);
      await loadTeam(false);
    } catch (requestError) {
      setError(getErrorMessage(requestError));
    } finally {
      setBusyAction(null);
    }
  }

  return (
    <section className="dashboard-section master-team" id="dashboard-team" aria-labelledby="master-team-title">
      <SectionHeading
        title="Equipe e permissões"
        subtitle="Defina exatamente o que cada subconta pode fazer em nome da sua conta Master."
        action={<Badge variant={team?.limit !== null && usedCapacity >= (team?.limit ?? Number.MAX_SAFE_INTEGER) ? 'warning' : 'primary'}>{capacityLabel}</Badge>}
      />

      {error && <p className="subaccount-feedback subaccount-feedback--error" role="alert">{error}</p>}
      {success && <p className="subaccount-feedback subaccount-feedback--success" role="status">{success}</p>}

      <div className="master-team__layout">
        <Card className="master-team__create" elevated>
          <CardBody>
            <div className="master-team__card-heading">
              <span className="master-team__step">Nova subconta</span>
              <h3>Adicionar pessoa à equipe</h3>
              <p>Informe o e-mail e escolha as permissões. Se a pessoa ainda não tiver conta, o CidadeEmDia enviará um convite automaticamente.</p>
            </div>

            <form className="subaccount-form" onSubmit={handleCreate}>
              <label className="subaccount-field">
                E-mail da pessoa
                <input
                  type="email"
                  autoComplete="email"
                  required
                  placeholder="nome@empresa.com.br"
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                />
              </label>

              <fieldset className="subaccount-fieldset">
                <legend>Permissões</legend>
                <PermissionSelector
                  selected={newPermissions}
                  onChange={setNewPermissions}
                  disabled={busyAction === 'create'}
                />
              </fieldset>

              <Button type="submit" size="lg" fullWidth disabled={busyAction === 'create'}>
                {busyAction === 'create' ? 'Processando...' : 'Adicionar ou convidar'}
              </Button>
            </form>
          </CardBody>
        </Card>

        <Card className="master-team__members" elevated>
          <CardBody>
            <div className="master-team__card-heading master-team__card-heading--row">
              <div>
                <span className="master-team__step">Equipe atual</span>
                <h3>Subcontas e convites</h3>
              </div>
              <button className="subaccount-refresh" type="button" onClick={() => void loadTeam()} disabled={loading}>
                Atualizar
              </button>
            </div>

            {loading ? (
              <div className="subaccount-state" aria-busy="true">
                <span className="subaccount-spinner" aria-hidden="true" />
                <strong>Carregando equipe</strong>
                <small>Buscando os vínculos, convites e permissões mais recentes.</small>
              </div>
            ) : !team || (team.members.length === 0 && team.invitations.length === 0) ? (
              <div className="subaccount-state">
                <span className="subaccount-empty-icon" aria-hidden="true">+</span>
                <strong>Nenhuma subconta ou convite</strong>
                <small>Adicione a primeira pessoa usando o formulário ao lado.</small>
              </div>
            ) : (
              <div className="subaccount-list">
                {team?.invitations.map((invitation) => (
                  <article className="subaccount-member" key={invitation.invitationId}>
                    <div className="subaccount-member__summary">
                      <div className="subaccount-member__avatar" aria-hidden="true">✉</div>
                      <div className="subaccount-member__identity">
                        <strong>Convite enviado</strong>
                        <span>{invitation.email}</span>
                      </div>
                      <Badge variant="warning">Pendente</Badge>
                    </div>

                    <div className="subaccount-member__permissions" aria-label="Permissões do convite">
                      {invitation.permissions.length > 0
                        ? invitation.permissions.map((permission) => <span key={permission}>{permissionLabel(permission)}</span>)
                        : <span className="subaccount-member__no-permission">Sem permissões operacionais</span>}
                    </div>
                    <small>Expira em {formatExpiry(invitation.expiresAt)}</small>
                  </article>
                ))}

                {team?.members.map((member) => {
                  const active = member.status === 'ACTIVE';
                  const editing = editingLinkId === member.linkId;
                  const saving = busyAction === `permissions:${member.linkId}`;
                  const revoking = busyAction === `revoke:${member.linkId}`;

                  return (
                    <article className={`subaccount-member${active ? '' : ' subaccount-member--revoked'}`} key={member.linkId}>
                      <div className="subaccount-member__summary">
                        <div className="subaccount-member__avatar" aria-hidden="true">{initials(member.displayName)}</div>
                        <div className="subaccount-member__identity">
                          <strong>{member.displayName}</strong>
                          <span>{member.email}</span>
                        </div>
                        <Badge variant={active ? 'success' : 'neutral'}>{active ? 'Ativa' : 'Revogada'}</Badge>
                      </div>

                      <div className="subaccount-member__permissions" aria-label="Permissões atuais">
                        {member.permissions.length > 0
                          ? member.permissions.map((permission) => <span key={permission}>{permissionLabel(permission)}</span>)
                          : <span className="subaccount-member__no-permission">Sem permissões operacionais</span>}
                      </div>

                      {active && !editing && (
                        <div className="subaccount-member__actions">
                          <Button size="sm" variant="soft" onClick={() => beginEditing(member)}>Editar permissões</Button>
                          <Button size="sm" variant="ghost" onClick={() => void revoke(member)} disabled={revoking}>
                            {revoking ? 'Revogando...' : 'Revogar acesso'}
                          </Button>
                        </div>
                      )}

                      {active && editing && (
                        <div className="subaccount-member__editor">
                          <PermissionSelector
                            selected={editingPermissions}
                            onChange={setEditingPermissions}
                            disabled={saving || revoking}
                          />
                          <div className="subaccount-member__actions">
                            <Button size="sm" onClick={() => void savePermissions(member)} disabled={saving}>
                              {saving ? 'Salvando...' : 'Salvar permissões'}
                            </Button>
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() => {
                                setEditingLinkId(null);
                                setEditingPermissions([]);
                              }}
                              disabled={saving}
                            >
                              Cancelar
                            </Button>
                          </div>
                        </div>
                      )}
                    </article>
                  );
                })}
              </div>
            )}
          </CardBody>
        </Card>
      </div>
    </section>
  );
}
