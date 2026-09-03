import { Brand, Button } from '../../components/ui';
import type { AuthenticatedUser } from '../../modules/auth/types';
import {
  AppNavigationIcon,
  getInitials,
  getPrimaryRoleLabel,
  getVisibleNavigation,
  type AppNavigationId,
} from './AppNavigation';

interface AppHeaderProps {
  active: AppNavigationId;
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

              <div className="app-header__account">
                <div className="app-header__avatar" aria-hidden="true">{getInitials(user.displayName)}</div>
                <div className="app-header__account-copy">
                  <strong>{user.displayName}</strong>
                  <span>{getPrimaryRoleLabel(user.roles)}</span>
                </div>
                {onLogout && (
                  <button className="app-header__logout" type="button" onClick={() => void onLogout()}>
                    Sair
                  </button>
                )}
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
