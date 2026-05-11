import { HttpContextToken } from '@angular/common/http';

export const SKIP_AUTH_CONTEXT = new HttpContextToken<boolean>(() => false);
export const SKIP_ERROR_TOAST_CONTEXT = new HttpContextToken<boolean>(() => false);
export const AUTH_RETRY_CONTEXT = new HttpContextToken<boolean>(() => false);
