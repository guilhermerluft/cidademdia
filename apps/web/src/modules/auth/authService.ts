import { api, refreshSessionRequest, setAccessToken } from '../../services/api';
import type { AuthSession, LoginInput, RegisterInput } from './types';

export async function register(input: RegisterInput): Promise<AuthSession> {
  const { data } = await api.post<AuthSession>('/auth/register', input);
  setAccessToken(data.accessToken);
  return data;
}

export async function login(input: LoginInput): Promise<AuthSession> {
  const { data } = await api.post<AuthSession>('/auth/login', input);
  setAccessToken(data.accessToken);
  return data;
}

export async function restoreSession(): Promise<AuthSession | null> {
  return refreshSessionRequest();
}

export async function logout(): Promise<void> {
  try {
    await api.post('/auth/logout');
  } finally {
    setAccessToken(null);
  }
}
