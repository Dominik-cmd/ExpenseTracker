import { CurrencyPipe } from '@angular/common';
import { inject, Pipe, PipeTransform } from '@angular/core';

import { CurrencyService } from '../../core/services/currency.service';

/**
 * Formats a number as currency using the user's configured default currency.
 * Usage: {{ amount | appCurrency }} or {{ amount | appCurrency:'1.0-0' }}
 */
@Pipe({
  name: 'appCurrency',
  standalone: true,
  pure: false
})
export class AppCurrencyPipe implements PipeTransform {
  private readonly currencyService = inject(CurrencyService);
  private readonly currencyPipe = new CurrencyPipe('en');

  transform(value: number | null | undefined, digitsInfo: string = '1.2-2', display: string = 'symbol'): string | null {
    if (value == null) return null;
    return this.currencyPipe.transform(value, this.currencyService.currency(), display, digitsInfo);
  }
}
