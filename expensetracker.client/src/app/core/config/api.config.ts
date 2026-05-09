import { environment } from '../../../environments/environment';

export interface RuntimeEnvironment {
  apiBaseUrl?: string;
}

declare global {
  interface Window {
    __env?: RuntimeEnvironment;
  }
}

const trimTrailingSlash = (value: string) => value.replace(/\/+$/, '');

export function getApiBaseUrl(): string {
  const runtimeBaseUrl = typeof window !== 'undefined' ? window.__env?.apiBaseUrl : undefined;
  return trimTrailingSlash(runtimeBaseUrl ?? environment.apiBaseUrl ?? '');
}

export function buildApiUrl(path: string): string {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  return `${getApiBaseUrl()}${normalizedPath}`;
}
