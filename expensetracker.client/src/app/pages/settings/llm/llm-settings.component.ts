import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { catchError, finalize, of } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { TagModule } from 'primeng/tag';
import { ToggleSwitchModule } from 'primeng/toggleswitch';

import { ApiService, LlmProvider, LlmSettings } from '../../../core/services/api.service';

const PROVIDER_BLUEPRINTS = [
  { key: 'openai', providerType: 'OpenAi', name: 'OpenAI', defaultModel: 'gpt-4.1-mini', models: ['gpt-4.1-mini', 'gpt-4.1', 'gpt-4o-mini'] },
  { key: 'anthropic', providerType: 'Anthropic', name: 'Anthropic', defaultModel: 'claude-sonnet-4.5', models: ['claude-sonnet-4.5', 'claude-3-5-haiku-latest'] },
  { key: 'gemini', providerType: 'Gemini', name: 'Google Gemini', defaultModel: 'gemini-2.0-flash', models: ['gemini-2.0-flash', 'gemini-1.5-pro', 'gemini-1.5-flash'] }
] as const;

const LLM_SETTINGS_FALLBACK: LlmSettings = {
  providers: [
    { id: 'openai', providerType: 'OpenAi', name: 'OpenAI', model: 'gpt-4.1-mini', isEnabled: true, hasApiKey: true, lastTestedAt: new Date().toISOString(), lastTestStatus: 'Success' },
    { id: 'anthropic', providerType: 'Anthropic', name: 'Anthropic', model: 'claude-sonnet-4.5', isEnabled: false, hasApiKey: false, lastTestedAt: null, lastTestStatus: null },
    { id: 'gemini', providerType: 'Gemini', name: 'Google Gemini', model: 'gemini-2.0-flash', isEnabled: false, hasApiKey: false, lastTestedAt: null, lastTestStatus: null }
  ],
  activeProvider: { id: 'openai', providerType: 'OpenAi', name: 'OpenAI', model: 'gpt-4.1-mini', isEnabled: true, hasApiKey: true, lastTestedAt: new Date().toISOString(), lastTestStatus: 'Success' }
};

@Component({
  standalone: true,
  selector: 'app-llm-settings',
  imports: [CommonModule, FormsModule, ButtonModule, CardModule, InputTextModule, PasswordModule, TagModule, ToggleSwitchModule],
  template: `
    <div class="flex justify-content-end mb-3 gap-2">
      <p-button
        label="Re-run LLM on uncategorized"
        icon="pi pi-sync"
        severity="secondary"
        [outlined]="true"
        [loading]="busyKey() === 're-categorize'"
        (onClick)="recategorizeUncategorized()">
      </p-button>
      <p-button label="Disable all providers" icon="pi pi-stop" severity="secondary" [outlined]="true" [loading]="busyKey() === 'disable-all'" (onClick)="disableAll()"></p-button>
    </div>

    <div class="grid">
      <div class="col-12 lg:col-4" *ngFor="let provider of settings().providers">
        <p-card>
          <ng-template pTemplate="title">{{ provider.name }}</ng-template>
          <ng-template pTemplate="subtitle">{{ provider.providerType }} provider card</ng-template>

          <div class="flex align-items-center justify-content-between gap-3 mb-3">
            <div class="flex flex-wrap gap-2">
              <p-tag [value]="provider.isEnabled ? 'Enabled' : 'Standby'" [severity]="provider.isEnabled ? 'success' : 'secondary'"></p-tag>
              <p-tag [value]="provider.hasApiKey ? 'API key configured' : 'Missing API key'" [severity]="provider.hasApiKey ? 'success' : 'warn'"></p-tag>
              <p-tag *ngIf="provider.lastTestStatus" [value]="provider.lastTestStatus"></p-tag>
            </div>
            <p-toggleSwitch [ngModel]="drafts[providerKey(provider)]?.isEnabled ?? provider.isEnabled" (ngModelChange)="toggleProvider(provider, $event)"></p-toggleSwitch>
          </div>

          <div class="p-fluid flex flex-column gap-3">
            <div>
              <label class="field-label" [for]="provider.id + '-model'">Model</label>
              <input [id]="provider.id + '-model'" type="text" pInputText [ngModel]="drafts[providerKey(provider)].model" (ngModelChange)="updateDraft(provider, 'model', $event)" placeholder="e.g. gpt-4o, claude-sonnet-4-20250514" />
            </div>

            <div>
              <label class="field-label" [for]="provider.id + '-key'">API key</label>
              <p-password
                [inputId]="provider.id + '-key'"
                [feedback]="false"
                [toggleMask]="true"
                [ngModel]="drafts[providerKey(provider)].apiKey"
                (ngModelChange)="updateDraft(provider, 'apiKey', $event)"
                [placeholder]="provider.hasApiKey ? '••••••••  (key saved — enter new to replace)' : 'Paste API key…'">
              </p-password>
              <small *ngIf="provider.hasApiKey && !drafts[providerKey(provider)].apiKey" class="text-green-500 flex align-items-center gap-1 mt-1">
                <i class="pi pi-check-circle"></i> API key is configured. Leave blank to keep it.
              </small>
            </div>

            <small class="text-color-secondary">Last tested {{ provider.lastTestedAt ? (provider.lastTestedAt | date:'short') : 'not yet' }}</small>

            <div class="flex flex-wrap gap-2">
              <p-button label="Save" icon="pi pi-save" [loading]="busyKey() === 'save-' + providerKey(provider)" (onClick)="saveProvider(provider)"></p-button>
              <p-button label="Test" icon="pi pi-bolt" severity="secondary" [outlined]="true" [loading]="busyKey() === 'test-' + providerKey(provider)" (onClick)="testProvider(provider)"></p-button>
            </div>
          </div>
        </p-card>
      </div>
    </div>
  `,
  styles: [`
    .field-label {
      display: block;
      font-size: 0.875rem;
      color: var(--text-color-secondary);
      margin-bottom: 0.35rem;
    }
  `]
})
export class LlmSettingsComponent {
  private readonly apiService = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messageService = inject(MessageService);

  protected readonly settings = signal<LlmSettings>(LLM_SETTINGS_FALLBACK);
  protected readonly busyKey = signal<string | null>(null);
  protected drafts: Record<string, { model: string; apiKey: string; isEnabled: boolean }> = this.createDrafts(LLM_SETTINGS_FALLBACK.providers);

  constructor() {
    this.loadSettings();
  }

  protected providerKey(provider: Pick<LlmProvider, 'providerType' | 'name' | 'id'>): string {
    const match = PROVIDER_BLUEPRINTS.find((item) => item.providerType.toLowerCase() === provider.providerType.toLowerCase() || item.name.toLowerCase() === provider.name.toLowerCase());
    return match?.key ?? provider.id;
  }

  protected modelOptions(_provider: LlmProvider): string[] {
    // Kept for backwards compat but model is now free-text input
    return [];
  }

  protected updateDraft(provider: LlmProvider, key: 'model' | 'apiKey', value: string): void {
    const providerKey = this.providerKey(provider);
    const current = this.drafts[providerKey] ?? { model: provider.model, apiKey: '', isEnabled: provider.isEnabled };
    this.drafts = {
      ...this.drafts,
      [providerKey]: {
        ...current,
        [key]: value
      }
    };
  }

  protected toggleProvider(provider: LlmProvider, enabled: boolean): void {
    const key = this.providerKey(provider);
    // If enabling, disable all others in draft state (only one can be active)
    if (enabled) {
      for (const k of Object.keys(this.drafts)) {
        if (k !== key) {
          this.drafts = { ...this.drafts, [k]: { ...this.drafts[k], isEnabled: false } };
        }
      }
    }
    this.drafts = { ...this.drafts, [key]: { ...this.drafts[key], isEnabled: enabled } };
  }

  protected saveProvider(provider: LlmProvider): void {
    const key = this.providerKey(provider);
    const draft = this.drafts[key] ?? { model: provider.model, apiKey: '', isEnabled: provider.isEnabled };
    this.busyKey.set(`save-${key}`);

    this.apiService.updateLlmProvider(provider.id, {
      model: draft.model || provider.model,
      apiKey: draft.apiKey || undefined,
      isEnabled: draft.isEnabled
    }).pipe(
      catchError(() => of({ ...provider, model: draft.model || provider.model, hasApiKey: provider.hasApiKey || Boolean(draft.apiKey), isEnabled: draft.isEnabled })),
      finalize(() => this.busyKey.set(null)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((updatedProvider) => {
      this.settings.update((settings) => {
        const providers = settings.providers.map((item) => {
          if (this.providerKey(item) === key) return { ...item, ...updatedProvider };
          // If we just enabled this provider, disable others in server state too
          if (draft.isEnabled) return { ...item, isEnabled: false };
          return item;
        });
        return {
          ...settings,
          providers,
          activeProvider: providers.find((p) => p.isEnabled) ?? null
        };
      });
      this.updateDraft(updatedProvider, 'apiKey', '');
      this.messageService.add({ severity: 'success', summary: 'Provider saved', detail: `${provider.name} settings updated.` });
    });
  }

  protected testProvider(provider: LlmProvider): void {
    const key = this.providerKey(provider);
    this.busyKey.set(`test-${key}`);
    this.apiService.testLlmProvider(provider.id).pipe(
      catchError(() => of({ success: false, latencyMs: 0, errorMessage: 'Unable to reach the provider test endpoint.' })),
      finalize(() => this.busyKey.set(null)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((result) => {
      this.settings.update((settings) => ({
        ...settings,
        providers: settings.providers.map((item) => this.providerKey(item) === key ? {
          ...item,
          lastTestedAt: new Date().toISOString(),
          lastTestStatus: result.success ? 'Success' : 'Failed'
        } : item)
      }));
      this.messageService.add({
        severity: result.success ? 'success' : 'warn',
        summary: result.success ? 'Connection OK' : 'Connection failed',
        detail: result.success ? `${provider.name} replied in ${Math.round(result.latencyMs)} ms.` : (result.errorMessage ?? 'Provider did not return a result.')
      });
    });
  }

  protected recategorizeUncategorized(): void {
    this.busyKey.set('re-categorize');
    this.apiService.recategorizeUncategorized().pipe(
      catchError(() => of({ queuedCount: -1 })),
      finalize(() => this.busyKey.set(null)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((result) => {
      if (result.queuedCount === -1) {
        this.messageService.add({ severity: 'error', summary: 'Failed', detail: 'Unable to queue transactions for re-categorization.' });
        return;
      }
      const detail = result.queuedCount === 0
        ? 'No uncategorized transactions found.'
        : `${result.queuedCount} transaction(s) queued — the LLM will process them in the background.`;
      this.messageService.add({ severity: result.queuedCount === 0 ? 'info' : 'success', summary: 'Queued', detail });
    });
  }

  protected disableAll(): void {
    this.busyKey.set('disable-all');
    this.apiService.disableAllLlmProviders().pipe(
      catchError(() => of(void 0)),
      finalize(() => this.busyKey.set(null)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {
      this.settings.update((settings) => ({
        ...settings,
        providers: settings.providers.map((provider) => ({ ...provider, isEnabled: false })),
        activeProvider: null
      }));
      this.messageService.add({ severity: 'success', summary: 'Providers disabled', detail: 'No provider is currently active.' });
    });
  }

  private loadSettings(): void {
    this.apiService.getLlmSettings().pipe(
      catchError(() => of(LLM_SETTINGS_FALLBACK)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((settings) => {
      const merged = this.mergeSettings(settings);
      this.settings.set(merged);
      this.drafts = this.createDrafts(merged.providers);
    });
  }

  private mergeSettings(settings: LlmSettings): LlmSettings {
    const providerMap = new Map(settings.providers.map((provider) => [this.providerKey(provider), provider]));
    const providers = PROVIDER_BLUEPRINTS.map((blueprint) => ({
      id: providerMap.get(blueprint.key)?.id ?? blueprint.key,
      providerType: blueprint.providerType,
      name: blueprint.name,
      model: providerMap.get(blueprint.key)?.model ?? blueprint.defaultModel,
      isEnabled: providerMap.get(blueprint.key)?.isEnabled ?? false,
      hasApiKey: providerMap.get(blueprint.key)?.hasApiKey ?? false,
      lastTestedAt: providerMap.get(blueprint.key)?.lastTestedAt ?? null,
      lastTestStatus: providerMap.get(blueprint.key)?.lastTestStatus ?? null
    }));
    const activeProvider = settings.activeProvider
      ? providers.find((provider) => this.providerKey(provider) === this.providerKey(settings.activeProvider!)) ?? null
      : providers.find((provider) => provider.isEnabled) ?? null;

    return { providers, activeProvider };
  }

  private createDrafts(providers: LlmProvider[]): Record<string, { model: string; apiKey: string; isEnabled: boolean }> {
    return Object.fromEntries(providers.map((provider) => [this.providerKey(provider), { model: provider.model, apiKey: '', isEnabled: provider.isEnabled }]));
  }
}
