import type { PropsWithChildren } from 'react';
import type { AuthenticatedUser } from '../../modules/auth/types';
import { AppBottomNavigation, AppHeader } from './AppHeader';
import { useNavigationPermissions, type AppNavigationId } from './AppNavigation';

interface DashboardShellProps extends PropsWithChildren {
  user: AuthenticatedUser;
  onLogout: () => Promise<void>;
}

export function DashboardShell({ user, onLogout, children }: DashboardShellProps) {
  const permissions = useNavigationPermissions(user);
  const active: AppNavigationId = window.location.pathname.replace(/\/+$/, '') === '/planos'
    ? 'plans'
    : 'home';

  return (
    <div className="dashboard-shell">
      <AppHeader
        active={active}
        user={user}
        permissions={permissions}
        onLogout={onLogout}
      />

      <main className="dashboard-main ced-container">{children}</main>

      <AppBottomNavigation
        active={active}
        user={user}
        permissions={permissions}
      />
    </div>
  );
}
