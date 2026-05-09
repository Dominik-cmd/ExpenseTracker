import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';

import { AuthService } from '../../core/services/auth.service';

@Component({
  standalone: true,
  selector: 'app-login',
  imports: [CommonModule, ReactiveFormsModule, ButtonModule, CardModule, InputTextModule, PasswordModule],
  template: `
    <div class="login-page">
      <p-card styleClass="login-card">
        <ng-template pTemplate="title">Welcome back</ng-template>
        <ng-template pTemplate="subtitle">Sign in to manage transactions, categories, rules, and reports.</ng-template>

        <form class="p-fluid flex flex-column gap-3 mt-3" [formGroup]="form" (ngSubmit)="submit()">
          <div class="flex flex-column gap-2">
            <label for="username">Username</label>
            <input id="username" type="text" pInputText formControlName="username" autocomplete="username" style="width:100%" />
          </div>

          <div class="flex flex-column gap-2">
            <label for="password">Password</label>
            <p-password
              inputId="password"
              formControlName="password"
              [feedback]="false"
              [toggleMask]="true"
              autocomplete="current-password"
              styleClass="w-full"
              [inputStyle]="{ width: '100%' }"></p-password>
          </div>

          <small class="text-red-400" *ngIf="errorMessage()">{{ errorMessage() }}</small>

          <p-button type="submit" label="Login" icon="pi pi-sign-in" [loading]="loading()" [disabled]="form.invalid"></p-button>
        </form>
      </p-card>
    </div>
  `,
  styles: [`
    .login-page {
      min-height: calc(100vh - 9rem);
      display: grid;
      place-items: center;
      padding: 1rem;
    }

    .login-card {
      width: min(28rem, 100%);
      border-radius: 1.25rem;
    }
  `]
})
export class LoginComponent {
  private readonly authService = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly formBuilder = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly loading = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly form = this.formBuilder.nonNullable.group({
    username: ['dominik', Validators.required],
    password: ['', Validators.required]
  });

  constructor() {
    if (this.authService.isAuthenticated()) {
      void this.router.navigate(['/dashboard']);
    }
  }

  protected submit(): void {
    if (this.form.invalid || this.loading()) {
      return;
    }

    this.errorMessage.set('');
    this.loading.set(true);

    this.authService.login(this.form.getRawValue()).pipe(
      finalize(() => this.loading.set(false)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/dashboard';
        void this.router.navigateByUrl(returnUrl);
      },
      error: (error: { error?: { message?: string } }) => {
        const detail = error.error?.message ?? 'Unable to sign in with the provided credentials.';
        this.errorMessage.set(detail);
        this.messageService.add({ severity: 'error', summary: 'Login failed', detail });
      }
    });
  }
}


