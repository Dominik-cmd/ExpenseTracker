import { Component, input, output } from '@angular/core';
import { ButtonModule } from 'primeng/button';

@Component({
  standalone: true,
  selector: 'app-error-state',
  imports: [ButtonModule],
  template: `
    <div class="error-state">
      <i class="pi pi-exclamation-circle error-icon"></i>
      <p class="error-message">{{ message() }}</p>
      <p-button
        label="Retry"
        icon="pi pi-refresh"
        severity="secondary"
        [outlined]="true"
        (onClick)="retry.emit()">
      </p-button>
    </div>
  `,
  styles: [`
    .error-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 3rem 1rem;
      gap: 0.75rem;
      color: var(--app-text-muted);
    }
    .error-icon {
      font-size: 2.5rem;
      color: var(--red-400);
    }
    .error-message {
      font-size: 0.875rem;
      margin: 0;
    }
  `]
})
export class ErrorStateComponent {
  message = input<string>('Something went wrong');
  retry = output<void>();
}
