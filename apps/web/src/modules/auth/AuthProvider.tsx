import { createContext, useContext, useEffect, useMemo, useState } from 'react';
import type { PropsWithChildren } from 'react';
import * as authService from './authService';
import type { AuthenticatedUser, LoginInput, RegisterInput } from './types';

type AuthStatus = 'loading' | 'authenticated' | 'anonymous';

interface AuthContextValue {
  status: AuthStatus;
  user: AuthenticatedUser | null;
  login: (input: LoginInput) => Promise<void>;
  register: (input: RegisterInput) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: PropsWithChildren) {
  const [status, setStatus] = useState<AuthStatus>('loading');
  const [user, setUser] = useState<AuthenticatedUser | null>(null);

  useEffect(() => {
    let active = true;

    void authService
      .restoreSession()
      .then((session) => {
        if (!active) return;

        if (session) {
          setUser(session.user);
          setStatus('authenticated');
          return;
        }

        setUser(null);
        setStatus('anonymous');
      })
      .catch(() => {
        if (!active) return;
        setUser(null);
        setStatus('anonymous');
      });

    return () => {
      active = false;
    };
  }, []);

  const value = useMemo<AuthContextValue>(() => ({
    status,
    user,
    async login(input) {
      const session = await authService.login(input);
      setUser(session.user);
      setStatus('authenticated');
    },
    async register(input) {
      const session = await authService.register(input);
      setUser(session.user);
      setStatus('authenticated');
    },
    async logout() {
      await authService.logout();
      setUser(null);
      setStatus('anonymous');
    },
  }), [status, user]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used inside AuthProvider');
  }

  return context;
}
