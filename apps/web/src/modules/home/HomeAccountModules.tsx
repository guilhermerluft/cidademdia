import type { AuthenticatedUser } from '../auth/types';
import { AdminPanel } from '../admin/AdminPanel';
import { ChatInbox } from '../chat/ChatInbox';
import { InstitutionDirectory } from '../institutions/InstitutionDirectory';
import { OccurrenceAssignmentPanel } from '../occurrenceAssignments/OccurrenceAssignmentPanel';
import { OccurrenceCenter } from '../occurrences/OccurrenceCenter';
import { PostFeed } from '../posts/PostFeed';
import { PostManagementPanel } from '../posts/PostManagementPanel';
import { MasterTeamPanel } from '../../app/dashboard/MasterTeamPanel';
import { getPrimaryRoleLabel } from '../../app/layout/AppNavigation';

interface HomeAccountModulesProps {
  user: AuthenticatedUser;
  permissions: readonly string[];
}

export function HomeAccountModules({ user, permissions }: HomeAccountModulesProps) {
  const isMaster = user.roles.includes('MASTER');
  const isAdmin = user.roles.includes('ADMIN');
  const isSubaccount = user.roles.includes('SUBACCOUNT') && !isMaster && !isAdmin;
  const isCitizen = !isMaster && !isAdmin && !isSubaccount;
  const canReadTargetedOccurrences = !isSubaccount || permissions.includes('occurrence.read.targeted');
  const canReadChat = !isSubaccount || permissions.includes('chat.read');

  return (
    <section className="public-home__account-zone" aria-label="Recursos da sua conta">
      {(isCitizen || isMaster || (isSubaccount && canReadTargetedOccurrences)) && (
        <div className="public-home__account-module" id="conta-ocorrencias">
          {isCitizen && <OccurrenceCenter />}
          {isMaster && <OccurrenceAssignmentPanel mode="master" />}
          {isSubaccount && canReadTargetedOccurrences && <OccurrenceAssignmentPanel mode="subaccount" />}
        </div>
      )}

      {isCitizen && (
        <div className="public-home__account-module">
          <ChatInbox mode="citizen" />
        </div>
      )}

      {isMaster && (
        <div className="public-home__account-module">
          <ChatInbox mode="master" />
        </div>
      )}

      {isSubaccount && canReadChat && (
        <div className="public-home__account-module">
          <ChatInbox mode="subaccount" />
        </div>
      )}

      {(isMaster || isAdmin) && (
        <div className="public-home__account-module" id="gestao-midias">
          <PostManagementPanel />
        </div>
      )}

      {isMaster && (
        <div className="public-home__account-module" id="equipe">
          <MasterTeamPanel />
        </div>
      )}

      {isAdmin && (
        <div className="public-home__account-module" id="admin">
          <AdminPanel />
        </div>
      )}

      <div className="public-home__account-module">
        <PostFeed />
      </div>

      <div className="public-home__account-module">
        <InstitutionDirectory />
      </div>

      <section className="dashboard-section dashboard-profile-section public-home__account-profile" id="perfil">
        <div>
          <span className="dashboard-profile-section__eyebrow">Seu perfil</span>
          <h2>{user.displayName}</h2>
          <p>{user.email}</p>
        </div>
        <span className="public-home__account-role">{getPrimaryRoleLabel(user.roles)}</span>
      </section>
    </section>
  );
}
