import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { buildApiUrl } from '../config/api.config';
import { MerchantRule, UpdateMerchantRuleRequest } from '../models';

@Injectable({ providedIn: 'root' })
export class MerchantRuleService {
  private readonly http = inject(HttpClient);

  getMerchantRules(): Observable<MerchantRule[]> {
    return this.http.get<MerchantRule[]>(buildApiUrl('/api/merchant-rules'));
  }

  updateMerchantRule(id: string, payload: UpdateMerchantRuleRequest): Observable<MerchantRule> {
    return this.http.patch<MerchantRule>(buildApiUrl(`/api/merchant-rules/${id}`), payload);
  }

  deleteMerchantRule(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/api/merchant-rules/${id}`));
  }
}
