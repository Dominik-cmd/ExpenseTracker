import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { buildApiUrl } from '../config/api.config';
import { CreateUserRequest, UserInfo } from '../models';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly http = inject(HttpClient);

  getUsers(): Observable<UserInfo[]> {
    return this.http.get<UserInfo[]>(buildApiUrl('/api/admin/users'));
  }

  createUser(request: CreateUserRequest): Observable<UserInfo> {
    return this.http.post<UserInfo>(buildApiUrl('/api/admin/users'), request);
  }

  deleteUser(id: string): Observable<void> {
    return this.http.delete<void>(buildApiUrl(`/api/admin/users/${id}`));
  }
}
