import { api, refreshSessionRequest, setAccessToken } from '../../services/api';
import type { AuthSession, ForgotPasswordInput, LoginInput, RegisterInput, ResetPasswordInput } from './types';

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

export async function requestPasswordReset(input: ForgotPasswordInput): Promise<void> {
  await api.post('/auth/password/forgot', input);
}

export async function resetPassword(input: ResetPasswordInput): Promise<void> {
  await api.post('/auth/password/reset', input);
  setAccessToken(null);
}
