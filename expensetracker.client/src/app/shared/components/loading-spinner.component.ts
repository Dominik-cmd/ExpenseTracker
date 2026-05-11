import { Component, input } from '@angular/core';

@Component({
  standalone: true,
  selector: 'app-loading-spinner',
  template: `
    <div class="loading-container">
      <i class="pi pi-spin pi-spinner" [style.fontSize]="size()"></i>
      @if (message()) {
        <span class="loading-message">{{ message() }}</span>
      }
    </div>
  `,
  styles: [`
    .loading-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 2rem;
      gap: 0.75rem;
      color: var(--app-text-muted);
    }
    .loading-message {
      font-size: 0.875rem;
    }
  `]
})
export class LoadingSpinnerComponent {
  message = input<string>('');
  size = input<string>('2rem');
}
