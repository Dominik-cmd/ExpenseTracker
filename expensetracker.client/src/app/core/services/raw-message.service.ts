import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { buildApiUrl } from '../config/api.config';
import { ManualParseResult, RawMessage } from '../models';

@Injectable({ providedIn: 'root' })
export class RawMessageService {
  private readonly http = inject(HttpClient);

  getRawMessages(status?: string | null): Observable<RawMessage[]> {
    const params = status ? new HttpParams().set('status', status) : undefined;
    return this.http.get<RawMessage[]>(buildApiUrl('/api/raw-messages'), { params });
  }

  reprocessRawMessage(id: string): Observable<void> {
    return this.http.post<void>(buildApiUrl(`/api/raw-messages/${id}/reprocess`), {});
  }

  parseRawMessageManually(text: string): Observable<ManualParseResult> {
    return this.http.post<ManualParseResult>(buildApiUrl('/api/diagnostic/parse-sms'), { text });
  }

  deleteRawMessage(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/api/raw-messages/${id}`));
  }
}
