import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';

import { AuthService } from '../services/auth.service';
import { AUTH_RETRY_CONTEXT, SKIP_AUTH_CONTEXT } from '../tokens/http-context.tokens';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  const shouldSkip = request.context.get(SKIP_AUTH_CONTEXT);
  const token = shouldSkip ? null : authService.getToken();
  const authorizedRequest = token
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;

  return next(authorizedRequest).pipe(
    catchError((error: HttpErrorResponse) => {
      if (shouldSkip || error.status !== 401 || request.context.get(AUTH_RETRY_CONTEXT)) {
        return throwError(() => error);
      }

      return authService.refresh().pipe(
        switchMap((session) => {
          if (!session?.token) {
            authService.clearSession();
            return throwError(() => error);
          }

          return next(request.clone({
            context: request.context.set(AUTH_RETRY_CONTEXT, true),
            setHeaders: { Authorization: `Bearer ${session.token}` }
          }));
        }),
        catchError((refreshError) => {
          authService.clearSession();
          return throwError(() => refreshError);
        })
      );
    })
  );
};
