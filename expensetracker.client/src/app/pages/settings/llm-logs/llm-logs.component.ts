import { CommonModule, DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { catchError, of } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { DialogModule } from 'primeng/dialog';
import { TableModule } from 'primeng/table';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

interface LlmLogEntry {
  id: string;
  providerType: string;
  model: string;
  merchantRaw: string | null;
  merchantNormalized: string | null;
  amount: number | null;
  systemPrompt: string;
  userPrompt: string;
  responseRaw: string | null;
  parsedCategory: string | null;
  parsedSubcategory: string | null;
  parsedConfidence: number | null;
  parsedReasoning: string | null;
  latencyMs: number;
  success: boolean;
  errorMessage: string | null;
  createdAt: string;
}

interface PagedResult {
  items: LlmLogEntry[];
  totalCount: number;
  page: number;
  pageSize: number;
}

@Component({
  standalone: true,
  selector: 'app-llm-logs',
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    ButtonModule,
    CardModule,
    SelectModule,
    TagModule,
    DialogModule,
    TableModule,
    ConfirmDialogModule
  ],
  providers: [ConfirmationService],
  template: `
    <div class="flex align-items-center justify-content-between mb-4 gap-3 flex-wrap">
      <div>
        <h2 class="m-0 text-xl font-semibold">LLM Call Logs</h2>
        <p class="mt-1 mb-0 text-color-secondary">Every request sent to an LLM provider, with full prompts and raw responses.</p>
      </div>
      <div class="flex gap-2 align-items-center flex-wrap">
        <p-select
          [options]="providerOptions"
          [(ngModel)]="filterProvider"
          optionLabel="label"
          optionValue="value"
          placeholder="All providers"
          (onChange)="reload()"
          styleClass="w-10rem">
        </p-select>
        <p-select
          [options]="successOptions"
          [(ngModel)]="filterSuccess"
          optionLabel="label"
          optionValue="value"
          placeholder="All results"
          (onChange)="reload()"
          styleClass="w-9rem">
        </p-select>
        <p-button icon="pi pi-refresh" severity="secondary" [outlined]="true" (onClick)="reload()" [loading]="loading()"></p-button>
        <p-button icon="pi pi-trash" severity="danger" [outlined]="true" label="Clear all" (onClick)="confirmClear()"></p-button>
      </div>
    </div>

    <p-card>
      <p-table
        [value]="logs()"
        [lazy]="true"
        [paginator]="true"
        [rows]="pageSize"
        [totalRecords]="totalCount()"
        [loading]="loading()"
        (onPage)="onPage($event)"
        [rowHover]="true"
        styleClass="p-datatable-sm"
        responsiveLayout="scroll">

        <ng-template pTemplate="header">
          <tr>
            <th style="width:9rem">Time</th>
            <th style="width:7rem">Provider</th>
            <th style="width:8rem">Model</th>
            <th>Merchant</th>
            <th style="width:6rem">Amount</th>
            <th style="width:5rem">Latency</th>
            <th style="width:5rem">Result</th>
            <th>Category</th>
            <th style="width:5rem">Actions</th>
          </tr>
        </ng-template>

        <ng-template pTemplate="body" let-log>
          <tr>
            <td class="text-sm text-color-secondary">{{ log.createdAt | date:'dd.MM.yy HH:mm:ss' }}</td>
            <td><p-tag [value]="log.providerType" severity="info"></p-tag></td>
            <td class="text-sm">{{ log.model }}</td>
            <td>
              <div class="font-medium">{{ log.merchantNormalized || log.merchantRaw || '—' }}</div>
              <div class="text-xs text-color-secondary" *ngIf="log.merchantRaw && log.merchantRaw !== log.merchantNormalized">{{ log.merchantRaw }}</div>
            </td>
            <td class="text-sm">{{ log.amount != null ? (log.amount | number:'1.2-2') + ' EUR' : '—' }}</td>
            <td class="text-sm">{{ log.latencyMs | number:'1.0-0' }} ms</td>
            <td>
              <p-tag [value]="log.success ? 'OK' : 'Failed'" [severity]="log.success ? 'success' : 'danger'"></p-tag>
            </td>
            <td>
              <span *ngIf="log.parsedCategory">
                {{ log.parsedCategory }}<span *ngIf="log.parsedSubcategory"> › {{ log.parsedSubcategory }}</span>
              </span>
              <span *ngIf="!log.parsedCategory && log.errorMessage" class="text-xs text-red-400">{{ log.errorMessage | slice:0:60 }}…</span>
              <span *ngIf="log.parsedConfidence != null" class="text-xs text-color-secondary ml-1">({{ (log.parsedConfidence * 100) | number:'1.0-0' }}%)</span>
            </td>
            <td>
              <p-button icon="pi pi-eye" [text]="true" severity="secondary" size="small" (onClick)="openDetail(log)"></p-button>
            </td>
          </tr>
        </ng-template>

        <ng-template pTemplate="emptymessage">
          <tr>
            <td colspan="9" class="text-center text-color-secondary py-5">
              No LLM calls logged yet. They appear here once a transaction is categorized via LLM.
            </td>
          </tr>
        </ng-template>
      </p-table>
    </p-card>

    <!-- Detail dialog -->
    <p-dialog
      [(visible)]="detailVisible"
      [modal]="true"
      [style]="{ width: 'min(860px, 96vw)' }"
      header="LLM Call Detail"
      [dismissableMask]="true">

      <div *ngIf="selected() as log" class="flex flex-column gap-4">
        <!-- Meta -->
        <div class="grid">
          <div class="col-6 md:col-3"><div class="text-xs text-color-secondary mb-1">Provider</div><strong>{{ log.providerType }}</strong></div>
          <div class="col-6 md:col-3"><div class="text-xs text-color-secondary mb-1">Model</div><strong>{{ log.model }}</strong></div>
          <div class="col-6 md:col-3"><div class="text-xs text-color-secondary mb-1">Latency</div><strong>{{ log.latencyMs }} ms</strong></div>
          <div class="col-6 md:col-3"><div class="text-xs text-color-secondary mb-1">Status</div>
            <p-tag [value]="log.success ? 'Success' : 'Failed'" [severity]="log.success ? 'success' : 'danger'"></p-tag>
          </div>
        </div>

        <!-- Merchant + result -->
        <div class="grid">
          <div class="col-12 md:col-6">
            <div class="text-xs text-color-secondary mb-1">Merchant (raw / normalized)</div>
            <div>{{ log.merchantRaw || '—' }}</div>
            <div class="text-color-secondary text-sm" *ngIf="log.merchantNormalized">→ {{ log.merchantNormalized }}</div>
          </div>
          <div class="col-12 md:col-6">
            <div class="text-xs text-color-secondary mb-1">Categorization result</div>
            <div *ngIf="log.parsedCategory">
              <strong>{{ log.parsedCategory }}</strong><span *ngIf="log.parsedSubcategory"> › {{ log.parsedSubcategory }}</span>
              <span class="text-color-secondary ml-2" *ngIf="log.parsedConfidence != null">({{ (log.parsedConfidence * 100) | number:'1.0-0' }}% confidence)</span>
            </div>
            <div class="text-sm text-color-secondary" *ngIf="log.parsedReasoning">{{ log.parsedReasoning }}</div>
            <div class="text-red-400 text-sm" *ngIf="log.errorMessage">{{ log.errorMessage }}</div>
          </div>
        </div>

        <!-- Prompts -->
        <div>
          <div class="text-xs text-color-secondary mb-1">System prompt</div>
          <pre class="raw-block">{{ log.systemPrompt }}</pre>
        </div>
        <div>
          <div class="text-xs text-color-secondary mb-1">User prompt</div>
          <pre class="raw-block">{{ log.userPrompt }}</pre>
        </div>

        <!-- Raw response -->
        <div *ngIf="log.responseRaw">
          <div class="text-xs text-color-secondary mb-1">Raw response</div>
          <pre class="raw-block">{{ formatJson(log.responseRaw) }}</pre>
        </div>
      </div>
    </p-dialog>

    <p-confirmDialog></p-confirmDialog>
  `,
  styles: [`
    .raw-block {
      background: var(--app-surface-0, #f5f7fb);
      border: 1px solid var(--app-surface-border, rgba(0,0,0,.08));
      border-radius: 0.5rem;
      padding: 0.75rem 1rem;
      font-size: 0.78rem;
      line-height: 1.5;
      white-space: pre-wrap;
      word-break: break-word;
      max-height: 18rem;
      overflow-y: auto;
      margin: 0;
      font-family: 'JetBrains Mono', 'Fira Code', monospace;
    }
  `]
})
export class LlmLogsComponent {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);

  protected readonly logs = signal<LlmLogEntry[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(false);
  protected readonly selected = signal<LlmLogEntry | null>(null);
  protected detailVisible = false;
  protected pageSize = 20;
  protected currentPage = 1;
  protected filterProvider: string | null = null;
  protected filterSuccess: boolean | null = null;

  protected readonly providerOptions = [
    { label: 'All providers', value: null },
    { label: 'OpenAI', value: 'OpenAi' },
    { label: 'Anthropic', value: 'Anthropic' },
    { label: 'Gemini', value: 'Gemini' }
  ];

  protected readonly successOptions = [
    { label: 'All results', value: null },
    { label: 'Successful only', value: true },
    { label: 'Failed only', value: false }
  ];

  constructor() {
    this.reload();
  }

  protected reload(page = 1): void {
    this.currentPage = page;
    this.loading.set(true);
    const params: Record<string, string> = {
      page: String(page),
      pageSize: String(this.pageSize)
    };
    if (this.filterProvider) params['provider'] = this.filterProvider;
    if (this.filterSuccess !== null) params['successOnly'] = String(this.filterSuccess);

    const qs = new URLSearchParams(params).toString();
    this.http.get<PagedResult>(`/api/llm-logs?${qs}`).pipe(
      catchError(() => of({ items: [], totalCount: 0, page, pageSize: this.pageSize })),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((result) => {
      this.logs.set(result.items);
      this.totalCount.set(result.totalCount);
      this.loading.set(false);
    });
  }

  protected onPage(event: { first: number; rows: number }): void {
    this.pageSize = event.rows;
    this.reload(Math.floor(event.first / event.rows) + 1);
  }

  protected openDetail(log: LlmLogEntry): void {
    this.selected.set(log);
    this.detailVisible = true;
  }

  protected confirmClear(): void {
    this.confirmationService.confirm({
      message: 'Delete all LLM call logs? This cannot be undone.',
      header: 'Clear logs',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.http.delete('/api/llm-logs').pipe(
          catchError(() => of(null)),
          takeUntilDestroyed(this.destroyRef)
        ).subscribe(() => {
          this.messageService.add({ severity: 'success', summary: 'Logs cleared' });
          this.reload();
        });
      }
    });
  }

  protected formatJson(raw: string): string {
    try {
      return JSON.stringify(JSON.parse(raw), null, 2);
    } catch {
      return raw;
    }
  }
}
