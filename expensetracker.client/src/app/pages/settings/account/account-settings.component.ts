import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { PasswordModule } from 'primeng/password';

import { AuthService } from '../../../core/services/auth.service';

@Component({
  standalone: true,
  selector: 'app-account-settings',
  imports: [CommonModule, FormsModule, ButtonModule, CardModule, PasswordModule],
  template: `
    <div class="grid">
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
  private readonly authService = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messageService = inject(MessageService);

  protected readonly changingPassword = signal(false);

  protected currentPassword = '';
  protected newPassword = '';
  protected confirmPassword = '';

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
      this.messageService.add({ severity: 'success', summary: 'Password changed', detail: 'Your password has been updated.' });
      this.currentPassword = '';
      this.newPassword = '';
      this.confirmPassword = '';
    });
  }
}
