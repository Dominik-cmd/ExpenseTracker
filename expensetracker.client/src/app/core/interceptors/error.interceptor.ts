import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MessageService } from 'primeng/api';
import { catchError, throwError } from 'rxjs';

import { SKIP_ERROR_TOAST_CONTEXT } from '../tokens/http-context.tokens';

export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const messageService = inject(MessageService);

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      if (!request.context.get(SKIP_ERROR_TOAST_CONTEXT)) {
        messageService.add({
          severity: 'error',
          summary: error.status === 0 ? 'Network error' : `Request failed (${error.status})`,
          detail: extractErrorMessage(error),
          life: 5000
        });
      }

      return throwError(() => error);
    })
  );
};

function extractErrorMessage(error: HttpErrorResponse): string {
  if (typeof error.error === 'string' && error.error.trim()) {
    return error.error;
  }

  if (typeof error.error?.message === 'string') {
    return error.error.message;
  }

  if (typeof error.error?.title === 'string') {
    return error.error.title;
  }

  return error.message || 'Something went wrong while talking to the API.';
}
