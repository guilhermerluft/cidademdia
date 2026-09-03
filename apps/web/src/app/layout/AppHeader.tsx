import { useEffect, useRef, useState } from 'react';
import { Brand, Button } from '../../components/ui';
import type { AuthenticatedUser } from '../../modules/auth/types';
import {
  AppNavigationIcon,
  getInitials,
  getPrimaryRoleLabel,
  getUserPanelAccess,
  getVisibleNavigation,
  type AppNavigationId,
} from './AppNavigation';

interface AppHeaderProps {
  active?: AppNavigationId;
  user?: AuthenticatedUser | null;
  permissions?: readonly string[];
  onLogin?: () => void;
  onRegister?: () => void;
  onLogout?: () => void | Promise<void>;
}

export function AppHeader({
  active,
  user,
  permissions = [],
  onLogin,
  onRegister,
  onLogout,
}: AppHeaderProps) {
  const navigation = getVisibleNavigation(user, permissions);
  const panelAccess = user ? getUserPanelAccess(user, permissions) : null;
  const [accountOpen, setAccountOpen] = useState(false);
  const accountMenuRef = useRef<HTMLDivElement>(null);
  const accountCloseTimerRef = useRef<number | null>(null);

  function cancelAccountClose() {
    if (accountCloseTimerRef.current !== null) {
      window.clearTimeout(accountCloseTimerRef.current);
      accountCloseTimerRef.current = null;
    }
  }

  function openAccountMenu() {
    cancelAccountClose();
    setAccountOpen(true);
  }

  function scheduleAccountClose() {
    cancelAccountClose();
    accountCloseTimerRef.current = window.setTimeout(() => {
      setAccountOpen(false);
      accountCloseTimerRef.current = null;
    }, 220);
  }

  useEffect(() => () => {
    if (accountCloseTimerRef.current !== null) {
      window.clearTimeout(accountCloseTimerRef.current);
    }
  }, []);

  useEffect(() => {
    if (!accountOpen) return;

    function handlePointerDown(event: PointerEvent) {
      if (!accountMenuRef.current?.contains(event.target as Node)) {
        cancelAccountClose();
        setAccountOpen(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        cancelAccountClose();
        setAccountOpen(false);
      }
    }

    document.addEventListener('pointerdown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('pointerdown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [accountOpen]);

  return (
    <header className="app-header">
      <div className="app-header__inner">
        <a className="app-header__brand" href="/" aria-label="Ir para o início">
          <Brand />
        </a>

        <nav className="app-header__nav" aria-label="Navegação principal">
          {navigation.map((item) => (
            <a
              className={item.id === active ? 'app-header__nav-item app-header__nav-item--active' : 'app-header__nav-item'}
              href={item.href}
              aria-current={item.id === active ? 'page' : undefined}
              key={item.id}
            >
              <span>{item.label}</span>
            </a>
          ))}
        </nav>

        <div className="app-header__actions">
          {user ? (
            <>
              <button className="app-header__notification" type="button" aria-label="Notificações">
                <AppNavigationIcon name="notifications" />
                <span className="app-header__notification-dot" aria-hidden="true" />
              </button>

              <div
                className={accountOpen ? 'app-header__account-menu app-header__account-menu--open' : 'app-header__account-menu'}
                ref={accountMenuRef}
                onMouseEnter={openAccountMenu}
                onMouseLeave={scheduleAccountClose}
              >
                <button
                  className="app-header__account"
                  type="button"
                  aria-haspopup="menu"
                  aria-expanded={accountOpen}
                  aria-label={`Abrir menu de ${user.displayName}`}
                  onClick={() => {
                    cancelAccountClose();
                    setAccountOpen((current) => !current);
                  }}
                >
                  <span className="app-header__avatar" aria-hidden="true">{getInitials(user.displayName)}</span>
                  <span className="app-header__account-copy">
                    <strong>{user.displayName}</strong>
                    <span>{getPrimaryRoleLabel(user.roles)}</span>
                  </span>
                  <span className="app-header__account-chevron" aria-hidden="true">
                    <AppNavigationIcon name="chevron" />
                  </span>
                </button>

                <div className="app-header__account-dropdown" role="menu" aria-label="Menu da conta">
                  <a className="app-header__account-menu-item" href="/perfil" role="menuitem">
                    <AppNavigationIcon name="profile" />
                    <span>
                      <strong>Perfil</strong>
                      <small>Dados da sua conta</small>
                    </span>
                  </a>

                  {panelAccess?.canAccessPanel && (
                    <a className="app-header__account-menu-item" href="/painel" role="menuitem">
                      <AppNavigationIcon name="panel" />
                      <span>
                        <strong>Painel</strong>
                        <small>Ocorrências e conversas</small>
                      </span>
                    </a>
                  )}

                  {onLogout && (
                    <button
                      className="app-header__account-menu-item app-header__account-menu-item--logout"
                      type="button"
                      role="menuitem"
                      onClick={() => {
                        cancelAccountClose();
                        setAccountOpen(false);
                        void onLogout();
                      }}
                    >
                      <AppNavigationIcon name="logout" />
                      <span>
                        <strong>Sair</strong>
                        <small>Encerrar sessão</small>
                      </span>
                    </button>
                  )}
                </div>
              </div>
            </>
          ) : (
            <>
              {onLogin && <Button variant="ghost" onClick={onLogin}>Entrar</Button>}
              {onRegister && <Button onClick={onRegister}>Criar conta</Button>}
            </>
          )}
        </div>
      </div>
    </header>
  );
}

interface AppBottomNavigationProps extends AppHeaderProps {}

export function AppBottomNavigation({
  active,
  user,
  permissions = [],
  onLogin,
  onRegister,
}: AppBottomNavigationProps) {
  const navigation = getVisibleNavigation(user, permissions);
  const visibleNavigation = user ? navigation.slice(0, 5) : navigation;

  return (
    <nav className="app-bottom-nav" aria-label="Navegação mobile">
      {visibleNavigation.map((item) => (
        <a
          className={item.id === active ? 'app-bottom-nav__item app-bottom-nav__item--active' : 'app-bottom-nav__item'}
          href={item.href}
          aria-current={item.id === active ? 'page' : undefined}
          key={item.id}
        >
          <AppNavigationIcon name={item.icon} />
          <span>{item.label}</span>
        </a>
      ))}

      {!user && onLogin && (
        <button className="app-bottom-nav__item" type="button" onClick={onLogin}>
          <AppNavigationIcon name="login" />
          <span>Entrar</span>
        </button>
      )}

      {!user && onRegister && (
        <button className="app-bottom-nav__item" type="button" onClick={onRegister}>
          <AppNavigationIcon name="register" />
          <span>Criar conta</span>
        </button>
      )}
    </nav>
  );
}
