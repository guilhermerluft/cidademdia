import type { AuthenticatedUser } from '../auth/types';
import { AdminPanel } from '../admin/AdminPanel';
import { PostFeed } from '../posts/PostFeed';
import { PostManagementPanel } from '../posts/PostManagementPanel';
import { MasterTeamPanel } from '../../app/dashboard/MasterTeamPanel';

interface HomeAccountModulesProps {
  user: AuthenticatedUser;
  permissions?: readonly string[];
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
    </section>
  );
}
