import { inject, Injectable, signal } from '@angular/core';

import { SettingsService } from './settings.service';

const CURRENCY_STORAGE_KEY = 'expense-tracker.currency';

@Injectable({ providedIn: 'root' })
export class CurrencyService {
  private readonly settingsService = inject(SettingsService);
  private readonly currencySignal = signal<string>(this.readStoredCurrency());

  readonly currency = this.currencySignal.asReadonly();

  /** Load currency from backend (call once on app init or login) */
  load(): void {
    this.settingsService.getAccountSettings().subscribe({
      next: (settings) => {
        const currency = settings.defaultCurrency || 'EUR';
        this.currencySignal.set(currency);
        localStorage.setItem(CURRENCY_STORAGE_KEY, currency);
      }
    });
  }

  /** Update the cached currency (called when user changes settings) */
  set(currency: string): void {
    this.currencySignal.set(currency);
    localStorage.setItem(CURRENCY_STORAGE_KEY, currency);
  }

  private readStoredCurrency(): string {
    if (typeof localStorage === 'undefined') return 'EUR';
    return localStorage.getItem(CURRENCY_STORAGE_KEY) || 'EUR';
  }
}
