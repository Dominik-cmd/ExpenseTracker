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
import { ToggleSwitchModule } from 'primeng/toggleswitch';

import { ApiService, AccountSettings } from '../../../core/services/api.service';
import { AuthService } from '../../../core/services/auth.service';

const ACCOUNT_SETTINGS_FALLBACK: AccountSettings = {
  username: 'expense-user',
  defaultCurrency: 'EUR',
  emailNotifications: true,
  webhookNotifications: false
};

@Component({
  standalone: true,
  selector: 'app-account-settings',
  imports: [CommonModule, FormsModule, ButtonModule, CardModule, InputTextModule, PasswordModule, ToggleSwitchModule],
  template: `
    <div class="grid">
      <div class="col-12 xl:col-6">
        <p-card header="Account preferences" subheader="Local UI preferences with optional backend persistence.">
          <div class="p-fluid flex flex-column gap-3">
            <div class="flex flex-column gap-2">
              <label for="username">Username</label>
              <input id="username" pInputText [(ngModel)]="settingsModel.username" />
            </div>
            <div class="flex flex-column gap-2">
              <label for="currency">Default currency</label>
              <input id="currency" pInputText maxlength="3" [(ngModel)]="settingsModel.defaultCurrency" />
            </div>
            <div class="flex align-items-center justify-content-between gap-3">
              <div>
                <div class="font-medium">Email notifications</div>
                <small class="text-color-secondary">Receive alerts for new parsing issues.</small>
              </div>
              <p-toggleSwitch [(ngModel)]="settingsModel.emailNotifications"></p-toggleSwitch>
            </div>
            <div class="flex align-items-center justify-content-between gap-3">
              <div>
                <div class="font-medium">Webhook notifications</div>
                <small class="text-color-secondary">Notify this account about sender list changes.</small>
              </div>
              <p-toggleSwitch [(ngModel)]="settingsModel.webhookNotifications"></p-toggleSwitch>
            </div>
            <p-button label="Save preferences" icon="pi pi-save" [loading]="savingPreferences()" (onClick)="savePreferences()"></p-button>
          </div>
        </p-card>
      </div>

      <div class="col-12 xl:col-6">
        <p-card header="Change password" subheader="Changing the password invalidates refresh tokens and signs you out.">
          <div class="p-fluid flex flex-column gap-3">
            <div class="flex flex-column gap-2">
              <label for="currentPassword">Current password</label>
              <p-password inputId="currentPassword" [feedback]="false" [toggleMask]="true" [(ngModel)]="currentPassword"></p-password>
            </div>
            <div class="flex flex-column gap-2">
              <label for="newPassword">New password</label>
              <p-password inputId="newPassword" [feedback]="true" [toggleMask]="true" [(ngModel)]="newPassword"></p-password>
            </div>
            <div class="flex flex-column gap-2">
              <label for="confirmPassword">Confirm password</label>
              <p-password inputId="confirmPassword" [feedback]="false" [toggleMask]="true" [(ngModel)]="confirmPassword"></p-password>
            </div>
            <p-button label="Update password" icon="pi pi-lock" [loading]="changingPassword()" (onClick)="changePassword()"></p-button>
          </div>
        </p-card>
      </div>
    </div>
  `
})
export class AccountSettingsComponent {
  private readonly apiService = inject(ApiService);
  private readonly authService = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messageService = inject(MessageService);

  protected readonly savingPreferences = signal(false);
  protected readonly changingPassword = signal(false);

  protected settingsModel: AccountSettings = { ...ACCOUNT_SETTINGS_FALLBACK };
  protected currentPassword = '';
  protected newPassword = '';
  protected confirmPassword = '';

  constructor() {
    this.apiService.getAccountSettings().pipe(
      catchError(() => of(ACCOUNT_SETTINGS_FALLBACK)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((settings) => {
      this.settingsModel = settings;
    });
  }

  protected savePreferences(): void {
    this.savingPreferences.set(true);
    this.apiService.updateAccountSettings(this.settingsModel).pipe(
      finalize(() => this.savingPreferences.set(false)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe((settings) => {
      this.settingsModel = settings;
      this.messageService.add({ severity: 'success', summary: 'Preferences saved', detail: 'Account preferences are up to date.' });
    });
  }

  protected changePassword(): void {
    if (!this.currentPassword || !this.newPassword) {
      this.messageService.add({ severity: 'warn', summary: 'Missing values', detail: 'Please fill out the password form.' });
      return;
    }

    if (this.newPassword !== this.confirmPassword) {
      this.messageService.add({ severity: 'warn', summary: 'Passwords do not match', detail: 'Confirm the new password before saving.' });
      return;
    }

    this.changingPassword.set(true);
    this.authService.changePassword({
      currentPassword: this.currentPassword,
      newPassword: this.newPassword
    }).pipe(
      finalize(() => this.changingPassword.set(false)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {
      this.messageService.add({ severity: 'success', summary: 'Password changed', detail: 'Please sign in again with your new password.' });
      this.authService.clearSession();
    });
  }
}


