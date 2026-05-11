import { HttpClient, HttpContext } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, map, Observable, of, tap } from 'rxjs';

import { buildApiUrl } from '../config/api.config';
import { SKIP_AUTH_CONTEXT, SKIP_ERROR_TOAST_CONTEXT } from '../tokens/http-context.tokens';

export const AUTH_STORAGE_KEY = 'expense-tracker.auth';

export interface LoginCredentials {
  username: string;
  password: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface AuthSession {
  token: string;
  refreshToken: string;
  expiresAt: string;
}

interface LoginResponse {
  token: string;
  refreshToken: string;
  expiresAt: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly sessionState = signal<AuthSession | null>(this.restoreSession());

  readonly session = computed(() => this.sessionState());
  readonly authenticated = computed(() => {
    const session = this.sessionState();
    return !!session && !this.isTokenExpired(session.token);
  });

  login(credentials: LoginCredentials): Observable<AuthSession> {
    const context = new HttpContext()
      .set(SKIP_AUTH_CONTEXT, true)
      .set(SKIP_ERROR_TOAST_CONTEXT, true);

    return this.http.post<LoginResponse>(buildApiUrl('/api/auth/login'), credentials, { context }).pipe(
      map((response) => this.storeSession(response))
    );
  }

  refresh(): Observable<AuthSession | null> {
    const refreshToken = this.sessionState()?.refreshToken;
    if (!refreshToken) {
      return of(null);
    }

    const context = new HttpContext()
      .set(SKIP_AUTH_CONTEXT, true)
      .set(SKIP_ERROR_TOAST_CONTEXT, true);

    return this.http.post<LoginResponse>(buildApiUrl('/api/auth/refresh'), { refreshToken }, { context }).pipe(
      map((response) => this.storeSession(response)),
      catchError(() => of(null))
    );
  }

  logout(options: { server?: boolean; redirect?: boolean } = {}): Observable<void> {
    const shouldNotifyServer = options.server ?? true;
    const shouldRedirect = options.redirect ?? true;

    if (!shouldNotifyServer || !this.getToken()) {
      this.clearSession(shouldRedirect);
      return of(void 0);
    }

    return this.http.post<void>(buildApiUrl('/api/auth/logout'), {}, {
      context: new HttpContext().set(SKIP_ERROR_TOAST_CONTEXT, true)
    }).pipe(
      catchError(() => of(void 0)),
      tap(() => this.clearSession(shouldRedirect)),
      map(() => void 0)
    );
  }

  changePassword(request: ChangePasswordRequest): Observable<AuthSession> {
    return this.http.post<LoginResponse>(buildApiUrl('/api/auth/change-password'), request).pipe(
      map(response => this.storeSession(response))
    );
  }

  isAuthenticated(): boolean {
    return this.authenticated();
  }

  getToken(): string | null {
    const currentSession = this.sessionState();
    if (!currentSession) {
      return null;
    }

    if (this.isTokenExpired(currentSession.token)) {
      this.clearSession(false);
      return null;
    }

    return currentSession.token;
  }

  getUsername(): string | null {
    const payload = this.decodeToken(this.getToken());
    const username = payload?.['unique_name'] ?? payload?.['name'] ?? payload?.['sub'];
    return typeof username === 'string' ? username : null;
  }

  isAdmin(): boolean {
    const payload = this.decodeToken(this.getToken());
    const role = payload?.['role'] ?? payload?.['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
    return role === 'Admin' || (Array.isArray(role) && role.includes('Admin'));
  }

  clearSession(redirect = true): void {
    this.sessionState.set(null);
    localStorage.removeItem(AUTH_STORAGE_KEY);

    if (redirect) {
      void this.router.navigate(['/login']);
    }
  }

  private storeSession(response: LoginResponse): AuthSession {
    const session: AuthSession = {
      token: response.token,
      refreshToken: response.refreshToken,
      expiresAt: response.expiresAt
    };

    this.sessionState.set(session);
    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(session));
    return session;
  }

  private restoreSession(): AuthSession | null {
    const storedValue = localStorage.getItem(AUTH_STORAGE_KEY);
    if (!storedValue) {
      return null;
    }

    try {
      const session = JSON.parse(storedValue) as AuthSession;
      if (!session.token || this.isTokenExpired(session.token)) {
        localStorage.removeItem(AUTH_STORAGE_KEY);
        return null;
      }

      return session;
    } catch {
      localStorage.removeItem(AUTH_STORAGE_KEY);
      return null;
    }
  }

  private decodeToken(token: string | null): Record<string, unknown> | null {
    if (!token) {
      return null;
    }

    const parts = token.split('.');
    if (parts.length < 2) {
      return null;
    }

    try {
      const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
      const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
      return JSON.parse(atob(padded)) as Record<string, unknown>;
    } catch {
      return null;
    }
  }

  private isTokenExpired(token: string, skewSeconds = 30): boolean {
    const payload = this.decodeToken(token);
    const expiration = payload?.['exp'];
    return typeof expiration !== 'number' || expiration <= Math.floor(Date.now() / 1000) + skewSeconds;
  }
}
