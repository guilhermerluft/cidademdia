export type ToastTone = 'info' | 'success' | 'warning' | 'error';

export interface ToastOptions {
  tone?: ToastTone;
  durationMs?: number;
}

export interface ToastEventDetail {
  id: string;
  message: string;
  tone: ToastTone;
  durationMs: number;
}

const TOAST_EVENT = 'ced:toast';
const DEFAULT_DURATION_MS = 4200;
let nextToastId = 1;
let nativeAlertBridgeInstalled = false;

function emitToast(message: string, options: ToastOptions = {}) {
  if (typeof window === 'undefined') return;

  const detail: ToastEventDetail = {
    id: `toast-${nextToastId++}`,
    message,
    tone: options.tone ?? 'info',
    durationMs: options.durationMs ?? DEFAULT_DURATION_MS,
  };

  window.dispatchEvent(new CustomEvent<ToastEventDetail>(TOAST_EVENT, { detail }));
}

export const toast = {
  info(message: string, durationMs?: number) {
    emitToast(message, { tone: 'info', durationMs });
  },
  success(message: string, durationMs?: number) {
    emitToast(message, { tone: 'success', durationMs });
  },
  warning(message: string, durationMs?: number) {
    emitToast(message, { tone: 'warning', durationMs });
  },
  error(message: string, durationMs?: number) {
    emitToast(message, { tone: 'error', durationMs });
  },
};

export function subscribeToToasts(listener: (toast: ToastEventDetail) => void) {
  const handler = (event: Event) => {
    listener((event as CustomEvent<ToastEventDetail>).detail);
  };

  window.addEventListener(TOAST_EVENT, handler);
  return () => window.removeEventListener(TOAST_EVENT, handler);
}

export function installNativeAlertToastBridge() {
  if (typeof window === 'undefined' || nativeAlertBridgeInstalled) return;
  nativeAlertBridgeInstalled = true;

  window.alert = (message?: unknown) => {
    toast.info(message == null ? '' : String(message));
  };
}
