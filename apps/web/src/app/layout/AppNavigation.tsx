import { useEffect, useState } from 'react';
import type { AuthenticatedUser } from '../../modules/auth/types';
import { listSubaccountContexts } from '../../modules/subaccounts/subaccountService';

export type AppNavigationId =
  | 'home'
  | 'plans'
  | 'occurrences'
  | 'media'
  | 'team'
  | 'admin'
  | 'profile';

export type AppNavigationIconName = AppNavigationId | 'notifications' | 'login' | 'register';

export interface AppNavigationItem {
  id: AppNavigationId;
  label: string;
  icon: AppNavigationIconName;
  href: string;
  public: boolean;
  authenticated: boolean;
  roles?: string[];
  restrictedSubaccountPermission?: string;
}

export interface NavigationAccessState {
  permissions: string[];
  subaccountContextActive: boolean | null;
}

export const APP_NAVIGATION: readonly AppNavigationItem[] = [
  {
    id: 'home',
    label: 'Início',
    icon: 'home',
    href: '/',
    public: true,
    authenticated: true,
  },
  {
    id: 'plans',
    label: 'Planos',
    icon: 'plans',
    href: '/planos',
    public: true,
    authenticated: true,
  },
  {
    id: 'occurrences',
    label: 'Ocorrências',
    icon: 'occurrences',
    href: '/ocorrencias',
    public: true,
    authenticated: true,
  },
  {
    id: 'media',
    label: 'Mídias',
    icon: 'media',
    href: '/#midias',
    public: false,
    authenticated: true,
  },
  {
    id: 'team',
    label: 'Equipe',
    icon: 'team',
    href: '/#equipe',
    public: false,
    authenticated: true,
    roles: ['MASTER'],
  },
  {
    id: 'admin',
    label: 'Admin',
    icon: 'admin',
    href: '/#admin',
    public: false,
    authenticated: true,
    roles: ['ADMIN'],
  },
  {
    id: 'profile',
    label: 'Perfil',
    icon: 'profile',
    href: '/#perfil',
    public: false,
    authenticated: true,
  },
] as const;

export function isRestrictedSubaccount(user: AuthenticatedUser) {
  return user.roles.includes('SUBACCOUNT')
    && !user.roles.includes('MASTER')
    && !user.roles.includes('ADMIN');
}

export function getVisibleNavigation(
  user?: AuthenticatedUser | null,
  permissions: readonly string[] = [],
): AppNavigationItem[] {
  if (!user) return APP_NAVIGATION.filter((item) => item.public);

  const restrictedSubaccount = isRestrictedSubaccount(user);

  return APP_NAVIGATION.filter((item) => {
    if (!item.authenticated) return false;

    if (item.roles && !item.roles.some((role) => user.roles.includes(role))) {
      return false;
    }

    if (
      restrictedSubaccount
      && item.restrictedSubaccountPermission
      && !permissions.includes(item.restrictedSubaccountPermission)
    ) {
      return false;
    }

    return true;
  });
}

export function useNavigationAccess(user?: AuthenticatedUser | null): NavigationAccessState {
  const [state, setState] = useState<NavigationAccessState>({
    permissions: [],
    subaccountContextActive: null,
  });

  useEffect(() => {
    if (!user || !isRestrictedSubaccount(user)) {
      setState({ permissions: [], subaccountContextActive: null });
      return;
    }

    let active = true;

    async function verifyAccess() {
      try {
        const contexts = await listSubaccountContexts();
        if (!active) return;

        setState({
          permissions: Array.from(new Set(contexts.flatMap((context) => context.permissions))),
          subaccountContextActive: contexts.length > 0,
        });
      } catch {
        // Falha de rede não deve presumir revogação nem derrubar a sessão.
      }
    }

    const handleFocus = () => void verifyAccess();
    void verifyAccess();
    const intervalId = window.setInterval(() => void verifyAccess(), 15000);
    window.addEventListener('focus', handleFocus);

    return () => {
      active = false;
      window.clearInterval(intervalId);
      window.removeEventListener('focus', handleFocus);
    };
  }, [user]);

  return state;
}

export function useNavigationPermissions(user?: AuthenticatedUser | null) {
  return useNavigationAccess(user).permissions;
}

export function getPrimaryRoleLabel(roles: readonly string[]) {
  if (roles.includes('ADMIN')) return 'Administrador';
  if (roles.includes('MASTER')) return 'Conta Master';
  if (roles.includes('SUBACCOUNT')) return 'Subconta';
  return 'Cidadão';
}

export function getInitials(displayName: string) {
  const parts = displayName.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return 'CD';
  return parts.slice(0, 2).map((part) => part[0]?.toUpperCase()).join('');
}

export function AppNavigationIcon({ name }: { name: AppNavigationIconName }) {
  const common = {
    width: 21,
    height: 21,
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
    case 'admin':
      return <svg {...common}><path d="M12 3 4 7v5c0 5 3.4 8.5 8 9 4.6-.5 8-4 8-9V7Z"/><path d="m9.5 12 1.7 1.7 3.7-4"/></svg>;
    case 'profile':
      return <svg {...common}><circle cx="12" cy="8" r="4"/><path d="M4 21a8 8 0 0 1 16 0"/></svg>;
    case 'notifications':
      return <svg {...common}><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9"/><path d="M10 21h4"/></svg>;
    case 'login':
      return <svg {...common}><path d="M10 17l5-5-5-5"/><path d="M15 12H3"/><path d="M14 3h5a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-5"/></svg>;
    case 'register':
      return <svg {...common}><circle cx="9" cy="8" r="4"/><path d="M2 21a7 7 0 0 1 14 0"/><path d="M19 8v6"/><path d="M16 11h6"/></svg>;
  }
}
