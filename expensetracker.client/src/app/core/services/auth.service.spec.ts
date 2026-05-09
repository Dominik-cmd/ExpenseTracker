import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AUTH_STORAGE_KEY, AuthService } from './auth.service';
import { SKIP_AUTH_CONTEXT, SKIP_ERROR_TOAST_CONTEXT } from '../tokens/http-context.tokens';

describe('AuthService', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), AuthService]
    });
  });

  afterEach(() => {
    localStorage.clear();
    TestBed.inject(HttpTestingController).verify();
  });

  it('stores the session after login', () => {
    const service = TestBed.inject(AuthService);
    const httpTesting = TestBed.inject(HttpTestingController);
    let storedToken = '';

    service.login({ username: 'demo', password: 'secret' }).subscribe((session) => {
      storedToken = session.token;
    });

    const request = httpTesting.expectOne('/api/auth/login');
    expect(request.request.context.get(SKIP_AUTH_CONTEXT)).toBeTrue();
    expect(request.request.context.get(SKIP_ERROR_TOAST_CONTEXT)).toBeTrue();
    request.flush({
      token: createJwt(3600),
      refreshToken: 'refresh-token',
      expiresAt: new Date(Date.now() + 3600_000).toISOString()
    });

    expect(storedToken).toContain('.');
    expect(service.isAuthenticated()).toBeTrue();
    expect(service.getUsername()).toBe('demo');
  });

  it('removes expired sessions from storage', () => {
    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify({
      token: createJwt(-60),
      refreshToken: 'refresh-token',
      expiresAt: new Date(Date.now() - 3600_000).toISOString()
    }));

    const service = TestBed.inject(AuthService);

    expect(service.isAuthenticated()).toBeFalse();
    expect(service.getToken()).toBeNull();
    expect(localStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
  });
});

function createJwt(expiresInSeconds: number): string {
  const header = base64UrlEncode({ alg: 'HS256', typ: 'JWT' });
  const payload = base64UrlEncode({ sub: 'demo', unique_name: 'demo', exp: Math.floor(Date.now() / 1000) + expiresInSeconds });
  return `${header}.${payload}.signature`;
}

function base64UrlEncode(value: unknown): string {
  return btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');
}
