import type { AxiosError, InternalAxiosRequestConfig } from 'axios';
import axios from 'axios';
import type { AuthSession } from '../modules/auth/types';

const baseURL = import.meta.env.VITE_API_BASE_URL ?? '/api/v1';

let accessToken: string | null = null;
let refreshPromise: Promise<AuthSession | null> | null = null;

export const api = axios.create({
  baseURL,
  withCredentials: true,
  timeout: 15_000,
});

const refreshClient = axios.create({
  baseURL,
  withCredentials: true,
  timeout: 15_000,
});

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function getAccessToken() {
  return accessToken;
}

function getJwtExpirationMs(token: string): number | null {
  try {
    const payloadPart = token.split('.')[1];
    if (!payloadPart) return null;

    const normalized = payloadPart
      .replace(/-/g, '+')
      .replace(/_/g, '/');
    const padded = normalized.padEnd(
      normalized.length + ((4 - (normalized.length % 4)) % 4),
      '=',
    );
    const payload = JSON.parse(atob(padded)) as { exp?: number };

    return typeof payload.exp === 'number'
      ? payload.exp * 1000
      : null;
  } catch {
    return null;
  }
}

export async function refreshSessionRequest(): Promise<AuthSession | null> {
  if (refreshPromise) {
    return refreshPromise;
  }

  refreshPromise = refreshClient
    .post<AuthSession>('/auth/refresh')
    .then(({ data }) => {
      setAccessToken(data.accessToken);
      return data;
    })
    .catch((error: AxiosError) => {
      if (error.response?.status === 401 || error.response?.status === 403) {
        setAccessToken(null);
        return null;
      }

      throw error;
    })
    .finally(() => {
      refreshPromise = null;
    });

  return refreshPromise;
}

export async function getRealtimeAccessToken(): Promise<string> {
  const current = accessToken;
  const expiresAt = current ? getJwtExpirationMs(current) : null;
  const hasEnoughLifetime =
    current !== null &&
    (expiresAt === null || expiresAt - Date.now() > 30_000);

  if (hasEnoughLifetime) {
    return current;
  }

  const session = await refreshSessionRequest();
  if (!session?.accessToken) {
    throw new Error('Authentication session is not available for realtime connection.');
  }

  return session.accessToken;
}

api.interceptors.request.use((config) => {
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`;
  }

  return config;
});

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const config = error.config as (InternalAxiosRequestConfig & { _retry?: boolean }) | undefined;
    const url = config?.url ?? '';
    const isAuthMutation =
      url.includes('/auth/login') ||
      url.includes('/auth/register') ||
      url.includes('/auth/refresh') ||
      url.includes('/auth/logout');

    if (error.response?.status !== 401 || !config || config._retry || isAuthMutation) {
      throw error;
    }

    config._retry = true;
    const session = await refreshSessionRequest();

    if (!session) {
      throw error;
    }

    config.headers.Authorization = `Bearer ${session.accessToken}`;
    return api.request(config);
  },
);
