import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { buildApiUrl } from '../config/api.config';
import { QueueStatus } from '../models';

@Injectable({ providedIn: 'root' })
export class QueueService {
  private readonly http = inject(HttpClient);

  getQueueStatus(): Observable<QueueStatus> {
    return this.http.get<QueueStatus>(buildApiUrl('/api/raw-messages/queue-status'));
  }
}
