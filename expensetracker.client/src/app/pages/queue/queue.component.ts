import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval, startWith, switchMap } from 'rxjs';
import { MessageService } from 'primeng/api';
import { BadgeModule } from 'primeng/badge';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';

import { EMPTY_QUEUE_STATUS } from '../../core/constants/fallbacks';
import { QueueStatus } from '../../core/models';
import { QueueService } from '../../core/services/queue.service';
const POLL_INTERVAL_MS = 4000;

@Component({
  standalone: true,
  selector: 'app-queue',
  imports: [CommonModule, BadgeModule, ButtonModule, CardModule, TableModule, TagModule],
  template: `
    <div class="grid">
      <!-- Summary row -->
      <div class="col-12">
        <div class="flex align-items-center justify-content-between mb-3">
          <div class="flex align-items-center gap-3">
            <h2 class="m-0">Processing queue</h2>
            <span class="text-color-secondary text-sm">Auto-refreshes every 4 s</span>
          </div>
          <p-button icon="pi pi-refresh" severity="secondary" [outlined]="true" (onClick)="refresh()"></p-button>
        </div>
      </div>

      <!-- Pending card -->
      <div class="col-12 lg:col-6">
        <p-card>
          <ng-template pTemplate="title">
            <div class="flex align-items-center gap-2">
              <span>Pending</span>
              <p-badge
                [value]="status().pendingCount.toString()"
                [severity]="status().pendingCount > 0 ? 'warn' : 'success'">
              </p-badge>
            </div>
          </ng-template>
          <ng-template pTemplate="subtitle">Messages waiting in the background queue</ng-template>

          <p-table [value]="status().pending" responsiveLayout="scroll" styleClass="p-datatable-sm" [rows]="20" [paginator]="status().pending.length > 20">
            <ng-template pTemplate="header">
              <tr>
                <th>Message preview</th>
                <th style="width: 9rem">Queued at</th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-item>
              <tr>
                <td class="text-sm font-mono">{{ item.preview }}</td>
                <td class="text-sm text-color-secondary">{{ item.createdAt | date:'HH:mm:ss' }}</td>
              </tr>
            </ng-template>
            <ng-template pTemplate="emptymessage">
              <tr>
                <td colspan="2" class="text-center p-4">
                  <i class="pi pi-check-circle text-green-500 text-2xl mb-2" style="display:block"></i>
                  Queue is empty
                </td>
              </tr>
            </ng-template>
          </p-table>
        </p-card>
      </div>

      <!-- Recently processed card -->
      <div class="col-12 lg:col-6">
        <p-card>
          <ng-template pTemplate="title">Recently processed</ng-template>
          <ng-template pTemplate="subtitle">Last 20 messages handled by the background service</ng-template>

          <p-table [value]="status().recentlyProcessed" responsiveLayout="scroll" styleClass="p-datatable-sm">
            <ng-template pTemplate="header">
              <tr>
                <th>Message preview</th>
                <th style="width: 6rem">Status</th>
                <th style="width: 9rem">Processed</th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-item>
              <tr>
                <td class="text-sm font-mono" [title]="item.failureReason ?? ''">{{ item.preview }}</td>
                <td>
                  <p-tag
                    [value]="item.status"
                    [severity]="item.status === 'Parsed' ? 'success' : item.status === 'Failed' ? 'danger' : 'secondary'">
                  </p-tag>
                </td>
                <td class="text-sm text-color-secondary">{{ item.processedAt | date:'HH:mm:ss' }}</td>
              </tr>
            </ng-template>
            <ng-template pTemplate="emptymessage">
              <tr><td colspan="3" class="text-center text-color-secondary p-4">No messages processed yet.</td></tr>
            </ng-template>
          </p-table>
        </p-card>
      </div>
    </div>
  `
})
export class QueueComponent implements OnInit {
  private readonly queueService = inject(QueueService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messageService = inject(MessageService);

  protected readonly status = signal<QueueStatus>(EMPTY_QUEUE_STATUS);

  ngOnInit(): void {
    interval(POLL_INTERVAL_MS).pipe(
      startWith(0),
      switchMap(() => this.queueService.getQueueStatus()),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: (data) => this.status.set(data),
      error: () => {}
    });
  }

  protected refresh(): void {
    this.queueService.getQueueStatus().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (data) => this.status.set(data),
      error: () => this.messageService.add({ severity: 'error', summary: 'Error', detail: 'Failed to refresh queue.' })
    });
  }
}
