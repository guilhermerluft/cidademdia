import type { AuthenticatedUser } from '../auth/types';
import { AdminPanel } from '../admin/AdminPanel';
import { InstitutionDirectory } from '../institutions/InstitutionDirectory';
import { PostFeed } from '../posts/PostFeed';
import { PostManagementPanel } from '../posts/PostManagementPanel';
import { MasterTeamPanel } from '../../app/dashboard/MasterTeamPanel';
import { getPrimaryRoleLabel } from '../../app/layout/AppNavigation';

interface HomeAccountModulesProps {
  user: AuthenticatedUser;
}

export function HomeAccountModules({ user }: HomeAccountModulesProps) {
  const isMaster = user.roles.includes('MASTER');
  const isAdmin = user.roles.includes('ADMIN');

  return (
    <section className="public-home__account-zone" aria-label="Recursos da sua conta">
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
