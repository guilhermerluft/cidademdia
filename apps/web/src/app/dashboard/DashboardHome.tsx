import { Badge, Button, Card, CardBody, SectionHeading } from '../../components/ui';
import { AdminPanel } from '../../modules/admin/AdminPanel';
import type { AuthenticatedUser } from '../../modules/auth/types';
import { ChatInbox } from '../../modules/chat/ChatInbox';
import { InstitutionDirectory } from '../../modules/institutions/InstitutionDirectory';
import { OccurrenceAssignmentPanel } from '../../modules/occurrenceAssignments/OccurrenceAssignmentPanel';
import { OccurrenceCenter } from '../../modules/occurrences/OccurrenceCenter';
import { PostFeed } from '../../modules/posts/PostFeed';
import { PostManagementPanel } from '../../modules/posts/PostManagementPanel';
import { MasterTeamPanel } from './MasterTeamPanel';

interface DashboardHomeProps {
  user: AuthenticatedUser;
}

function getRoleCopy(roles: string[]) {
  if (roles.includes('ADMIN')) {
    return {
      title: 'Administração da plataforma',
      description: 'Acompanhe a operação e acesse recursos administrativos conforme suas permissões.',
    };
  }

  if (roles.includes('MASTER')) {
    return {
      title: 'Gestão da sua conta Master',
      description: 'Acompanhe ocorrências recebidas, equipe, mídias e os recursos vinculados ao seu plano.',
    };
  }

  if (roles.includes('SUBACCOUNT')) {
    return {
      title: 'Painel da sua subconta',
      description: 'Acesse somente os recursos liberados pela conta Master à qual você está vinculado.',
    };
  }

  return {
    title: 'Sua cidade, mais conectada',
    description: 'Registre ocorrências, acompanhe o andamento e fique por dentro das informações da sua cidade.',
  };
}

export function DashboardHome({ user }: DashboardHomeProps) {
  const roleCopy = getRoleCopy(user.roles);
  const firstName = user.displayName.trim().split(/\s+/)[0] || user.displayName;
  const isMaster = user.roles.includes('MASTER');
  const isAdmin = user.roles.includes('ADMIN');
  const isSubaccount = user.roles.includes('SUBACCOUNT');
  const isCitizen = !isMaster && !isAdmin && !isSubaccount;

  function scrollToActions() {
    document.getElementById('dashboard-actions')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  return (
    <div className="dashboard-home" id="dashboard-home">
      <section className="dashboard-hero">
        <div className="dashboard-hero__copy">
          <Badge variant="primary">Visão geral</Badge>
          <p className="dashboard-hero__welcome">Olá, {firstName}</p>
          <h1>{roleCopy.title}</h1>
          <p>{roleCopy.description}</p>
          <div className="dashboard-hero__actions">
            <Button size="lg" onClick={scrollToActions}>Ver ações rápidas</Button>
            <Button size="lg" variant="secondary" onClick={() => document.getElementById('dashboard-profile')?.scrollIntoView({ behavior: 'smooth' })}>
              Ver meu perfil
            </Button>
          </div>
        </div>

        <div className="dashboard-hero__visual" aria-hidden="true">
          <div className="dashboard-hero__orbit dashboard-hero__orbit--one" />
          <div className="dashboard-hero__orbit dashboard-hero__orbit--two" />
          <div className="dashboard-hero__status-card">
            <span className="dashboard-hero__status-dot" />
            <strong>Conta ativa</strong>
            <small>Ambiente seguro e conectado</small>
          </div>
        </div>
      </section>

      <section className="dashboard-section" aria-labelledby="dashboard-summary-title">
        <SectionHeading
          title="Resumo da sua conta"
          subtitle="Informações rápidas sobre o acesso atual."
        />
        <div className="dashboard-summary-grid">
          <Card className="dashboard-summary-card">
            <CardBody>
              <span className="dashboard-summary-card__label">Perfil</span>
              <strong>{user.roles.includes('MASTER') ? 'Master' : user.roles.includes('ADMIN') ? 'Administrador' : user.roles.includes('SUBACCOUNT') ? 'Subconta' : 'Cidadão'}</strong>
              <small>Permissões aplicadas conforme sua conta.</small>
            </CardBody>
          </Card>

          <Card className="dashboard-summary-card">
            <CardBody>
              <span className="dashboard-summary-card__label">Sessão</span>
              <strong className="dashboard-summary-card__success">Protegida</strong>
              <small>Autenticação ativa nesta sessão.</small>
            </CardBody>
          </Card>

          <Card className="dashboard-summary-card">
            <CardBody>
              <span className="dashboard-summary-card__label">Conta</span>
              <strong>{user.displayName}</strong>
              <small>{user.email}</small>
            </CardBody>
          </Card>
        </div>
      </section>

      <section className="dashboard-section" id="dashboard-actions" aria-labelledby="dashboard-actions-title">
        <SectionHeading
          title="Ações rápidas"
          subtitle="Acesse os principais recursos disponíveis para o seu perfil."
        />

        <div className="dashboard-action-grid">
          <Card className="dashboard-action-card" interactive>
            <CardBody>
              <span className="dashboard-action-card__icon dashboard-action-card__icon--blue" aria-hidden="true">01</span>
              <div>
                <h3>Ocorrências</h3>
                <p>Registrar, acompanhar e consultar o histórico das demandas.</p>
              </div>
              <span className="dashboard-action-card__arrow" aria-hidden="true">→</span>
            </CardBody>
          </Card>

          <Card className="dashboard-action-card" interactive id="dashboard-media">
            <CardBody>
              <span className="dashboard-action-card__icon dashboard-action-card__icon--green" aria-hidden="true">02</span>
              <div>
                <h3>Mídias</h3>
                <p>{isMaster || isAdmin ? 'Criar, publicar e acompanhar conteúdos da plataforma.' : 'Acompanhar conteúdos e informações publicadas na plataforma.'}</p>
              </div>
              <span className="dashboard-action-card__arrow" aria-hidden="true">→</span>
            </CardBody>
          </Card>

          {(isMaster || isAdmin) && (
            <Card
              className="dashboard-action-card"
              interactive
              id={isAdmin ? 'dashboard-admin' : undefined}
              onClick={isAdmin ? () => document.getElementById('admin-panel')?.scrollIntoView({ behavior: 'smooth', block: 'start' }) : undefined}
            >
              <CardBody>
                <span className="dashboard-action-card__icon dashboard-action-card__icon--yellow" aria-hidden="true">03</span>
                <div>
                  <h3>{isAdmin ? 'Administração' : 'Equipe e permissões'}</h3>
                  <p>{isAdmin ? 'Acessar controles administrativos da plataforma.' : 'Gerenciar subcontas e permissões da conta Master.'}</p>
                </div>
                <span className="dashboard-action-card__arrow" aria-hidden="true">→</span>
              </CardBody>
            </Card>
          )}
        </div>
      </section>

      {isCitizen ? <OccurrenceCenter /> : null}
      {isMaster ? <OccurrenceAssignmentPanel mode="master" /> : null}
      {isMaster ? <MasterTeamPanel /> : null}
      {isSubaccount ? <OccurrenceAssignmentPanel mode="subaccount" /> : null}
      {isCitizen ? <ChatInbox mode="citizen" /> : null}
      {isMaster ? <ChatInbox mode="master" /> : null}
      {isSubaccount ? <ChatInbox mode="subaccount" /> : null}
      {isAdmin ? <AdminPanel /> : null}
      {(isMaster || isAdmin) ? <PostManagementPanel /> : null}
      <PostFeed />
      <InstitutionDirectory />

      <section className="dashboard-section dashboard-profile-section" id="dashboard-profile">
        <div>
          <span className="dashboard-profile-section__eyebrow">Seu perfil</span>
          <h2>Dados da conta sempre acessíveis</h2>
          <p>O shell concentra perfil, notificações e navegação sem perder a identidade visual do CidadeEmDia.</p>
        </div>
        <Badge variant="success">Sessão autenticada</Badge>
      </section>
    </div>
  );
}