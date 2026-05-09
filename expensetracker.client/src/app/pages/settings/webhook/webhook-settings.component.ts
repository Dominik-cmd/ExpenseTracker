import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { catchError, finalize, of } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ChipsModule } from 'primeng/chips';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';

import { ApiService, WebhookSettings } from '../../../core/services/api.service';

const WEBHOOK_SETTINGS_FALLBACK: WebhookSettings = {
  secret: 'replace-me-at-runtime',
  senders: ['OTP banka', 'OTPBanka', 'OTP BANKA']
};

@Component({
  standalone: true,
  selector: 'app-webhook-settings',
  imports: [CommonModule, FormsModule, ButtonModule, CardModule, ChipsModule, InputTextModule, TagModule],
  template: `
    <div class="grid">
      <div class="col-12 xl:col-5">
        <p-card header="Webhook secret" subheader="Use this secret in the X-Webhook-Secret header for inbound SMS pushes.">
          <div class="flex flex-column gap-3">
            <div class="secret-shell">{{ settings().secret }}</div>
            <div class="flex flex-wrap gap-2">
              <p-button label="Refresh" icon="pi pi-sync" severity="secondary" [outlined]="true" (onClick)="loadSettings()"></p-button>
              <p-button label="Rotate secret" icon="pi pi-refresh" [loading]="rotatingSecret()" (onClick)="rotateSecret()"></p-button>
            </div>
            <small class="text-color-secondary">Rotate only after updating any upstream SMS forwarders.</small>
          </div>
        </p-card>
      </div>

      <div class="col-12 xl:col-7">
        <p-card header="Allowed SMS senders" subheader="Trusted sender aliases used by the SMS webhook and parser.">
          <div class="p-fluid flex flex-column gap-3">
            <div class="flex gap-2">
              <input type="text" pInputText [(ngModel)]="newSender" placeholder="Enter sender name" (keydown.enter)="addSender()" class="flex-1" />
              <p-button icon="pi pi-plus" label="Add" [disabled]="!newSender.trim()" (onClick)="addSender()"></p-button>
            </div>
            <div class="flex flex-wrap gap-2" *ngIf="senders.length > 0">
              <p-tag *ngFor="let sender of senders; let i = index" [value]="sender" [rounded]="true">
                <span class="flex align-items-center gap-1">
                  {{ sender }}
                  <i class="pi pi-times" style="cursor: pointer; font-size: 0.75rem; margin-left: 0.25rem;" (click)="removeSender(i)"></i>
                </span>
              </p-tag>
            </div>
            <small *ngIf="senders.length === 0" class="text-color-secondary">No senders configured. Add at least one trusted sender.</small>
            <div class="flex flex-wrap gap-2 justify-content-end">
              <p-button label="Reset" severity="secondary" [outlined]="true" (onClick)="resetSenders()"></p-button>
              <p-button label="Save senders" icon="pi pi-save" [loading]="savingSenders()" (onClick)="saveSenders()"></p-button>
            </div>
          </div>
        </p-card>
      </div>
    </div>
  `,
  styles: [`
    .secret-shell {
      font-family: var(--font-family-monospace, ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace);
      padding: 1rem;
      border-radius: var(--border-radius);
      background: var(--surface-100);
      word-break: break-all;
    }
  `]
})
export class WebhookSettingsComponent {
  private readonly apiService = inject(ApiService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messageService = inject(MessageService);

  protected readonly settings = signal<WebhookSettings>(WEBHOOK_SETTINGS_FALLBACK);
  protected readonly rotatingSecret = signal(false);
  protected readonly savingSenders = signal(false);
  protected senders: string[] = [...WEBHOOK_SETTINGS_FALLBACK.senders];
  protected newSender = '';

  constructor() {
    this.loadSettings();
  }

  protected addSender(): void {
    const trimmed = this.newSender.trim();
    if (trimmed && !this.senders.includes(trimmed)) {
      this.senders = [...this.senders, trimmed];
    }
    this.newSender = '';
  }

  protected removeSender(index: number): void {
    this.senders = this.senders.filter((_, i) => i !== index);
  }

  protected rotateSecret(): void {
    this.confirmationService.confirm({
      header: 'Rotate webhook secret',
      message: 'Existing integrations will need the new secret. Continue?',
      accept: () => {
        this.rotatingSecret.set(true);
        this.apiService.rotateWebhookSecret().pipe(
          finalize(() => this.rotatingSecret.set(false)),
          takeUntilDestroyed(this.destroyRef)
        ).subscribe((result) => {
          this.settings.update((settings) => ({ ...settings, secret: result.secret }));
          this.messageService.add({ severity: 'success', summary: 'Secret rotated', detail: 'Update inbound webhook integrations with the new secret.' });
        });
      }
    });
  }

  protected saveSenders(): void {
    this.savingSenders.set(true);
    this.apiService.updateSmsSenders(this.senders).pipe(
      finalize(() => this.savingSenders.set(false)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((savedSenders) => {
      this.senders = [...savedSenders];
      this.settings.update((settings) => ({ ...settings, senders: [...savedSenders] }));
      this.messageService.add({ severity: 'success', summary: 'Senders saved', detail: 'Trusted sender list updated.' });
    });
  }

  protected resetSenders(): void {
    this.senders = [...this.settings().senders];
  }

  protected loadSettings(): void {
    this.apiService.getWebhookSettings().pipe(
      catchError(() => of(WEBHOOK_SETTINGS_FALLBACK)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((settings) => {
      this.settings.set(settings);
      this.senders = [...settings.senders];
    });
  }
}
