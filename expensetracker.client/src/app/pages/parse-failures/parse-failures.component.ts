import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { catchError, finalize, of } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DialogModule } from 'primeng/dialog';
import { InputTextarea } from 'primeng/inputtextarea';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';

import { ApiService, ManualParseResult, RawMessage } from '../../core/services/api.service';

const PARSE_FAILURES_FALLBACK: RawMessage[] = [
  {
    id: '1', sender: 'OTP banka', body: 'POS NAKUP ???', receivedAt: new Date().toISOString(), parseStatus: 'Failed',
    errorMessage: 'Unable to parse merchant name.', idempotencyHash: 'hash-1', transactionId: null, createdAt: new Date().toISOString()
  },
  {
    id: '2', sender: 'OTP banka', body: 'CARD PAYMENT 12.34 EUR BOOKSTORE', receivedAt: new Date(Date.now() - 86400000).toISOString(), parseStatus: 'Pending',
    errorMessage: 'Waiting for background parser.', idempotencyHash: 'hash-2', transactionId: null, createdAt: new Date().toISOString()
  }
];

@Component({
  standalone: true,
  selector: 'app-parse-failures',
  imports: [CommonModule, FormsModule, ButtonModule, CardModule, DialogModule, InputTextarea, TableModule, TagModule],
  template: `
    <p-card header="Parse failures" subheader="Inspect inbound SMS payloads, preview the raw body, and retry parsing workflows.">
      <p-table [value]="messages()" responsiveLayout="scroll">
        <ng-template pTemplate="header">
          <tr>
            <th>Received</th>
            <th>Sender</th>
            <th>Body</th>
            <th>Status</th>
            <th>Error</th>
            <th style="width: 14rem">Actions</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-message>
          <tr>
            <td>{{ message.receivedAt | date:'short' }}</td>
            <td>{{ message.sender }}</td>
            <td class="truncate-column">{{ message.body }}</td>
            <td><p-tag [value]="message.parseStatus" [severity]="message.parseStatus === 'Pending' ? 'warn' : message.parseStatus === 'Parsed' ? 'success' : 'danger'"></p-tag></td>
            <td>{{ message.errorMessage || '-' }}</td>
            <td>
              <div class="flex flex-wrap gap-2">
                <p-button label="Preview" icon="pi pi-eye" size="small" [text]="true" (onClick)="openPreview(message)"></p-button>
                <p-button label="Reprocess" icon="pi pi-refresh" size="small" [text]="true" (onClick)="reprocess(message)"></p-button>
                <p-button icon="pi pi-trash" size="small" [text]="true" severity="danger" (onClick)="deleteMessage(message)"></p-button>
              </div>
            </td>
          </tr>
        </ng-template>
      </p-table>
    </p-card>

    <p-dialog [(visible)]="previewVisible" [modal]="true" [style]="{ width: 'min(42rem, 96vw)' }" header="Raw message preview">
      <div *ngIf="selectedMessage() as message" class="flex flex-column gap-3 mt-1">
        <div class="flex flex-wrap gap-2 align-items-center">
          <p-tag [value]="message.sender"></p-tag>
          <p-tag [value]="message.parseStatus" [severity]="message.parseStatus === 'Pending' ? 'warn' : message.parseStatus === 'Parsed' ? 'success' : 'danger'"></p-tag>
        </div>
        <pre class="preview-body">{{ message.body }}</pre>
        <div class="flex justify-content-end">
          <p-button label="Manual parse" icon="pi pi-sparkles" severity="secondary" [outlined]="true" (onClick)="openManualParse(message.body)"></p-button>
        </div>
      </div>
    </p-dialog>

    <p-dialog [(visible)]="manualParseVisible" [modal]="true" [style]="{ width: 'min(40rem, 96vw)' }" header="Manual parser">
      <div class="p-fluid flex flex-column gap-3 mt-1">
        <div>
          <label class="field-label" for="manualParseText">SMS body</label>
          <textarea id="manualParseText" pInputTextarea rows="8" [(ngModel)]="manualParseDraft"></textarea>
        </div>

        <div class="surface-ground border-round p-3" *ngIf="manualParseResult() as result">
          <div class="flex flex-wrap gap-2 align-items-center mb-3">
            <p-tag [value]="result.success ? 'Parsed' : 'Failed'" [severity]="result.success ? 'success' : 'danger'"></p-tag>
            <span class="text-sm text-color-secondary" *ngIf="result.errorMessage">{{ result.errorMessage }}</span>
          </div>
          <pre class="preview-body" *ngIf="result.parsedSms">{{ result.parsedSms | json }}</pre>
        </div>
      </div>

      <ng-template pTemplate="footer">
        <div class="flex justify-content-end gap-2 w-full">
          <p-button label="Close" severity="secondary" [outlined]="true" (onClick)="manualParseVisible = false"></p-button>
          <p-button label="Run parser" icon="pi pi-play" [loading]="parsing" (onClick)="runManualParse()"></p-button>
        </div>
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .truncate-column {
      max-width: 24rem;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .preview-body {
      margin: 0;
      padding: 1rem;
      border-radius: var(--border-radius);
      background: var(--surface-100);
      white-space: pre-wrap;
      word-break: break-word;
      max-height: 24rem;
      overflow: auto;
    }

    .field-label {
      display: block;
      font-size: 0.875rem;
      color: var(--text-color-secondary);
      margin-bottom: 0.35rem;
    }
  `]
})
export class ParseFailuresComponent {
  private readonly apiService = inject(ApiService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messageService = inject(MessageService);

  protected readonly messages = signal<RawMessage[]>(PARSE_FAILURES_FALLBACK);
  protected readonly selectedMessage = signal<RawMessage | null>(null);
  protected readonly manualParseResult = signal<ManualParseResult | null>(null);

  protected previewVisible = false;
  protected manualParseVisible = false;
  protected parsing = false;
  protected manualParseDraft = '';

  constructor() {
    this.loadMessages();
  }

  protected openPreview(message: RawMessage): void {
    this.selectedMessage.set(message);
    this.previewVisible = true;
  }

  protected openManualParse(text: string): void {
    this.manualParseDraft = text;
    this.manualParseResult.set(null);
    this.manualParseVisible = true;
  }

  protected runManualParse(): void {
    if (!this.manualParseDraft.trim()) {
      return;
    }

    this.parsing = true;
    this.apiService.parseRawMessageManually(this.manualParseDraft).pipe(
      catchError(() => of({ success: false, parsedSms: null, errorMessage: 'Local diagnostic parser is not available.' })),
      finalize(() => this.parsing = false),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((result) => this.manualParseResult.set(result));
  }

  protected reprocess(message: RawMessage): void {
    this.apiService.reprocessRawMessage(message.id).pipe(
      catchError(() => of(void 0)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {
      this.messages.update((messages) => messages.map((item) => item.id === message.id ? { ...item, parseStatus: 'Pending', errorMessage: null } : item));
      this.messageService.add({ severity: 'success', summary: 'Queued', detail: 'Message queued for reprocessing.' });
    });
  }

  protected deleteMessage(message: RawMessage): void {
    this.confirmationService.confirm({
      header: 'Delete raw message',
      message: `Delete the raw payload from ${message.sender}?`,
      accept: () => {
        this.apiService.deleteRawMessage(message.id).pipe(
          catchError(() => of(void 0)),
          takeUntilDestroyed(this.destroyRef)
        ).subscribe(() => {
          this.messages.update((messages) => messages.filter((item) => item.id !== message.id));
          this.messageService.add({ severity: 'success', summary: 'Message deleted', detail: 'The raw payload was removed.' });
        });
      }
    });
  }

  private loadMessages(): void {
    this.apiService.getRawMessages().pipe(
      catchError(() => of(PARSE_FAILURES_FALLBACK)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((messages) => this.messages.set(messages.filter((message) => `${message.parseStatus}`.toLowerCase() !== 'parsed')));
  }
}
