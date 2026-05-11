import { DatePipe, JsonPipe } from '@angular/common';
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

import { EMPTY_RAW_MESSAGES } from '../../core/constants/fallbacks';
import { ManualParseResult, RawMessage } from '../../core/models';
import { RawMessageService } from '../../core/services/raw-message.service';

@Component({
  standalone: true,
  selector: 'app-parse-failures',
  imports: [DatePipe, JsonPipe, FormsModule, ButtonModule, CardModule, DialogModule, InputTextarea, TableModule, TagModule],
  template: `
    <p-card header="Parse failures" subheader="Inspect inbound SMS payloads, preview the raw body, and retry parsing workflows.">
      @if (messages().length) {
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
      } @else {
        <div class="empty-state empty-state--success text-center text-color-secondary">
          <i class="pi pi-check-circle"></i>
          <span>No parse failures. All recent SMS were processed cleanly.</span>
        </div>
      }
    </p-card>

    <p-dialog [(visible)]="previewVisible" [modal]="true" [style]="{ width: 'min(42rem, 96vw)' }" header="Raw message preview">
      @if (selectedMessage(); as message) {
        <div class="flex flex-column gap-3 mt-1">
          <div class="flex flex-wrap gap-2 align-items-center">
            <p-tag [value]="message.sender"></p-tag>
            <p-tag [value]="message.parseStatus" [severity]="message.parseStatus === 'Pending' ? 'warn' : message.parseStatus === 'Parsed' ? 'success' : 'danger'"></p-tag>
          </div>
          <pre class="preview-body">{{ message.body }}</pre>
          <div class="flex justify-content-end">
            <p-button label="Manual parse" icon="pi pi-sparkles" severity="secondary" [outlined]="true" (onClick)="openManualParse(message.body)"></p-button>
          </div>
        </div>
      }
    </p-dialog>

    <p-dialog [(visible)]="manualParseVisible" [modal]="true" [style]="{ width: 'min(40rem, 96vw)' }" header="Manual parser">
      <div class="p-fluid flex flex-column gap-3 mt-1">
        <div>
          <label class="field-label" for="manualParseText">SMS body</label>
          <textarea id="manualParseText" pInputTextarea rows="8" [(ngModel)]="manualParseDraft"></textarea>
        </div>

        @if (manualParseResult(); as result) {
          <div class="surface-ground border-round p-3">
            <div class="flex flex-wrap gap-2 align-items-center mb-3">
              <p-tag [value]="result.success ? 'Parsed' : 'Failed'" [severity]="result.success ? 'success' : 'danger'"></p-tag>
              @if (result.errorMessage) {
                <span class="text-sm text-color-secondary">{{ result.errorMessage }}</span>
              }
            </div>
            @if (result.parsedSms) {
              <pre class="preview-body">{{ result.parsedSms | json }}</pre>
            }
          </div>
        }
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

    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      padding: 2rem 0.75rem;
      color: var(--text-color-secondary);
    }

    .empty-state i {
      font-size: 1.1rem;
    }

    .empty-state--success i {
      color: var(--green-500);
    }
  `]
})
export class ParseFailuresComponent {
  private readonly rawMessageService = inject(RawMessageService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messageService = inject(MessageService);

  protected readonly messages = signal<RawMessage[]>(EMPTY_RAW_MESSAGES);
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
    this.rawMessageService.parseRawMessageManually(this.manualParseDraft).pipe(
      catchError(() => of({ success: false, parsedSms: null, errorMessage: 'Local diagnostic parser is not available.' })),
      finalize(() => this.parsing = false),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((result) => this.manualParseResult.set(result));
  }

  protected reprocess(message: RawMessage): void {
    this.rawMessageService.reprocessRawMessage(message.id).pipe(
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
        this.rawMessageService.deleteRawMessage(message.id).pipe(
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
    this.rawMessageService.getRawMessages().pipe(
      catchError(() => of(EMPTY_RAW_MESSAGES)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((messages) => this.messages.set(messages.filter((message) => `${message.parseStatus}`.toLowerCase() !== 'parsed')));
  }
}
