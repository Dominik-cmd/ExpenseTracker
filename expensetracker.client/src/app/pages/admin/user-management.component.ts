import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

import { ApiService, UserInfo } from '../../core/services/api.service';

@Component({
  standalone: true,
  selector: 'app-user-management',
  imports: [
    CommonModule, FormsModule, ButtonModule, CardModule, CheckboxModule,
    ConfirmDialogModule, DialogModule, InputTextModule, PasswordModule, TableModule, TagModule
  ],
  template: `
    <div class="grid">
      <div class="col-12">
        <p-card>
          <ng-template pTemplate="title">
            <div class="flex align-items-center justify-content-between">
              <span>User management</span>
              <p-button label="Add user" icon="pi pi-user-plus" (onClick)="showCreateDialog()"></p-button>
            </div>
          </ng-template>
          <ng-template pTemplate="subtitle">Manage users who can access the expense tracker.</ng-template>

          <p-table [value]="users()" [loading]="loading()" responsiveLayout="scroll" styleClass="p-datatable-sm">
            <ng-template pTemplate="header">
              <tr>
                <th>Username</th>
                <th>Role</th>
                <th>Created</th>
                <th style="width: 6rem"></th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-user>
              <tr>
                <td>{{ user.username }}</td>
                <td>
                  <p-tag *ngIf="user.isAdmin" severity="warn" value="Admin"></p-tag>
                  <p-tag *ngIf="!user.isAdmin" severity="info" value="User"></p-tag>
                </td>
                <td>{{ user.createdAt | date:'mediumDate' }}</td>
                <td>
                  <p-button
                    *ngIf="!user.isAdmin"
                    icon="pi pi-trash"
                    severity="danger"
                    [text]="true"
                    [rounded]="true"
                    (onClick)="confirmDelete(user)"></p-button>
                </td>
              </tr>
            </ng-template>
            <ng-template pTemplate="emptymessage">
              <tr><td colspan="4" class="text-center text-color-secondary p-4">No users found.</td></tr>
            </ng-template>
          </p-table>
        </p-card>
      </div>
    </div>

    <p-dialog header="Create user" [(visible)]="dialogVisible" [modal]="true" [style]="{ width: '24rem' }">
      <div class="flex flex-column gap-3 mt-2">
        <div class="flex flex-column gap-2">
          <label for="new-username">Username</label>
          <input id="new-username" type="text" pInputText [(ngModel)]="newUsername" style="width:100%" />
        </div>
        <div class="flex flex-column gap-2">
          <label for="new-password">Password</label>
          <p-password
            inputId="new-password"
            [(ngModel)]="newPassword"
            [feedback]="true"
            [toggleMask]="true"
            styleClass="w-full"
            [inputStyle]="{ width: '100%' }"></p-password>
        </div>
        <div class="flex align-items-center gap-2">
          <p-checkbox inputId="new-admin" [(ngModel)]="newIsAdmin" [binary]="true"></p-checkbox>
          <label for="new-admin">Admin</label>
        </div>
      </div>
      <ng-template pTemplate="footer">
        <p-button label="Cancel" [text]="true" (onClick)="dialogVisible = false"></p-button>
        <p-button
          label="Create"
          icon="pi pi-check"
          [loading]="creating()"
          [disabled]="!newUsername || !newPassword || newUsername.length < 3 || newPassword.length < 6"
          (onClick)="createUser()"></p-button>
      </ng-template>
    </p-dialog>

    <p-confirmDialog></p-confirmDialog>
  `
})
export class UserManagementComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly users = signal<UserInfo[]>([]);
  protected readonly loading = signal(false);
  protected readonly creating = signal(false);
  protected dialogVisible = false;
  protected newUsername = '';
  protected newPassword = '';
  protected newIsAdmin = false;

  ngOnInit(): void {
    this.loadUsers();
  }

  protected showCreateDialog(): void {
    this.newUsername = '';
    this.newPassword = '';
    this.newIsAdmin = false;
    this.dialogVisible = true;
  }

  protected createUser(): void {
    this.creating.set(true);
    this.api.createUser({ username: this.newUsername, password: this.newPassword, isAdmin: this.newIsAdmin }).pipe(
      finalize(() => this.creating.set(false)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'User created', detail: `${this.newUsername} has been created.` });
        this.dialogVisible = false;
        this.loadUsers();
      },
      error: (err: { error?: { message?: string } }) => {
        this.messageService.add({ severity: 'error', summary: 'Failed', detail: err.error?.message ?? 'Could not create user.' });
      }
    });
  }

  protected confirmDelete(user: UserInfo): void {
    this.confirmationService.confirm({
      message: `Delete user "${user.username}"? All their data will be permanently removed.`,
      header: 'Confirm deletion',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.deleteUser(user)
    });
  }

  private deleteUser(user: UserInfo): void {
    this.api.deleteUser(user.id).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: 'Deleted', detail: `${user.username} has been removed.` });
        this.loadUsers();
      },
      error: (err: { error?: { message?: string } }) => {
        this.messageService.add({ severity: 'error', summary: 'Failed', detail: err.error?.message ?? 'Could not delete user.' });
      }
    });
  }

  private loadUsers(): void {
    this.loading.set(true);
    this.api.getUsers().pipe(
      finalize(() => this.loading.set(false)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: (users) => this.users.set(users),
      error: () => this.messageService.add({ severity: 'error', summary: 'Error', detail: 'Failed to load users.' })
    });
  }
}
