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
            <p-chips [(ngModel)]="senders" [separator]="','" [allowDuplicate]="false" placeholder="Add trusted SMS senders"></p-chips>
            <div class="flex flex-wrap gap-2">
              <p-tag *ngFor="let sender of senders" [value]="sender"></p-tag>
            </div>
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

  constructor() {
    this.loadSettings();
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
