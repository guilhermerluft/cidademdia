import type { PropsWithChildren } from 'react';
import { Brand } from '../../components/ui';
import type { AuthenticatedUser } from '../../modules/auth/types';

type DashboardIconName =
  | 'home'
  | 'plans'
  | 'occurrences'
  | 'media'
  | 'team'
  | 'notifications'
  | 'profile'
  | 'admin';

interface DashboardShellProps extends PropsWithChildren {
  user: AuthenticatedUser;
  onLogout: () => Promise<void>;
}

interface NavigationItem {
  label: string;
  icon: DashboardIconName;
  href: string;
}

function DashboardIcon({ name }: { name: DashboardIconName }) {
  const common = {
    width: 22,
    height: 22,
    viewBox: '0 0 24 24',
    fill: 'none',
    stroke: 'currentColor',
    strokeWidth: 1.9,
    strokeLinecap: 'round' as const,
    strokeLinejoin: 'round' as const,
    'aria-hidden': true,
  };

  switch (name) {
    case 'home':
      return <svg {...common}><path d="M3 11.5 12 4l9 7.5"/><path d="M5.5 10.5V20h13v-9.5"/><path d="M9.5 20v-5.5h5V20"/></svg>;
    case 'plans':
      return <svg {...common}><rect x="3" y="5" width="18" height="14" rx="2"/><path d="M7 9h10"/><path d="M7 13h6"/></svg>;
    case 'occurrences':
      return <svg {...common}><path d="M8 6h12"/><path d="M8 12h12"/><path d="M8 18h12"/><path d="M4 6h.01"/><path d="M4 12h.01"/><path d="M4 18h.01"/></svg>;
    case 'media':
      return <svg {...common}><rect x="3" y="5" width="18" height="14" rx="2"/><path d="m10 9 5 3-5 3Z"/></svg>;
    case 'team':
      return <svg {...common}><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>;
    case 'notifications':
      return <svg {...common}><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9"/><path d="M10 21h4"/></svg>;
    case 'profile':
      return <svg {...common}><circle cx="12" cy="8" r="4"/><path d="M4 21a8 8 0 0 1 16 0"/></svg>;
    case 'admin':
      return <svg {...common}><path d="M12 3 4 7v5c0 5 3.4 8.5 8 9 4.6-.5 8-4 8-9V7Z"/><path d="m9.5 12 1.7 1.7 3.7-4"/></svg>;
  }
}

function getPrimaryRole(roles: string[]) {
  if (roles.includes('ADMIN')) return 'Administrador';
  if (roles.includes('MASTER')) return 'Conta Master';
  if (roles.includes('SUBACCOUNT')) return 'Subconta';
  return 'Cidadão';
}

function getNavigation(roles: string[]): NavigationItem[] {
  const items: NavigationItem[] = [
    { label: 'Início', icon: 'home', href: '#dashboard-home' },
    { label: 'Planos', icon: 'plans', href: '/planos' },
    { label: 'Ocorrências', icon: 'occurrences', href: '#dashboard-actions' },
    { label: 'Mídias', icon: 'media', href: '#dashboard-media' },
  ];

  if (roles.includes('MASTER')) {
    items.push({ label: 'Equipe', icon: 'team', href: '#dashboard-team' });
  }

  if (roles.includes('ADMIN')) {
    items.push({ label: 'Admin', icon: 'admin', href: '#dashboard-admin' });
  }

  items.push({ label: 'Perfil', icon: 'profile', href: '#dashboard-profile' });
  return items;
}

function initials(displayName: string) {
  const parts = displayName.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return 'CD';
  return parts.slice(0, 2).map((part) => part[0]?.toUpperCase()).join('');
}

export function DashboardShell({ user, onLogout, children }: DashboardShellProps) {
  const navigation = getNavigation(user.roles);
  const roleLabel = getPrimaryRole(user.roles);
  const isPlansPage = window.location.pathname.replace(/\/+$/, '') === '/planos';

  function navigationHref(item: NavigationItem) {
    if (isPlansPage && item.href.startsWith('#')) return `/${item.href}`;
    return item.href;
  }

  function navigationClass(item: NavigationItem, index: number, mobile = false) {
    const base = mobile ? 'dashboard-bottom-nav__item' : 'dashboard-nav__item';
    const active = item.href === '/planos' ? isPlansPage : index === 0 && !isPlansPage;
    return active ? `${base} ${base}--active` : base;
  }

  return (
    <div className="dashboard-shell">
      <header className="dashboard-header">
        <div className="dashboard-header__inner ced-container">
          <Brand className="dashboard-header__brand" />

          <nav className="dashboard-nav dashboard-nav--desktop" aria-label="Navegação principal">
            {navigation.map((item, index) => (
              <a
                className={navigationClass(item, index)}
                href={navigationHref(item)}
                key={item.label}
              >
                <DashboardIcon name={item.icon} />
                <span>{item.label}</span>
              </a>
            ))}
          </nav>

          <div className="dashboard-header__actions">
            <button className="dashboard-icon-button" type="button" aria-label="Notificações">
              <DashboardIcon name="notifications" />
              <span className="dashboard-icon-button__dot" aria-hidden="true" />
            </button>

            <div className="dashboard-account">
              <div className="dashboard-account__avatar" aria-hidden="true">{initials(user.displayName)}</div>
              <div className="dashboard-account__copy">
                <strong>{user.displayName}</strong>
                <span>{roleLabel}</span>
              </div>
              <button className="dashboard-account__logout" type="button" onClick={() => void onLogout()}>
                Sair
              </button>
            </div>
          </div>
        </div>
      </header>

      <main className="dashboard-main ced-container">{children}</main>

      <nav className="dashboard-bottom-nav" aria-label="Navegação mobile">
        {navigation.slice(0, 5).map((item, index) => (
          <a
            className={navigationClass(item, index, true)}
            href={navigationHref(item)}
            key={item.label}
          >
            <DashboardIcon name={item.icon} />
            <span>{item.label}</span>
          </a>
        ))}
      </nav>
    </div>
  );
}
