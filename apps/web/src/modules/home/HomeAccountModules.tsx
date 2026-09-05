import type { AuthenticatedUser } from '../auth/types';
import { PostFeed } from '../posts/PostFeed';

interface HomeAccountModulesProps {
  user: AuthenticatedUser;
  permissions?: readonly string[];
}

export function HomeAccountModules({ user }: HomeAccountModulesProps) {
  const isMaster = user.roles.includes('MASTER');
  const isAdmin = user.roles.includes('ADMIN');

  if (isMaster) return null;
  if (isAdmin) return null;

  return (
    <section className="public-home__account-zone" aria-label="Recursos da sua conta">
      <div className="public-home__account-module">
        <PostFeed />
      </div>
    </section>
  );
}
