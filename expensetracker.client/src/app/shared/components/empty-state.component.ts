import { Component, input } from '@angular/core';

@Component({
  standalone: true,
  selector: 'app-empty-state',
  template: `
    <div class="empty-state">
      <i [class]="icon()" class="empty-icon"></i>
      <p class="empty-message">{{ message() }}</p>
    </div>
  `,
  styles: [`
    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 3rem 1rem;
      gap: 0.75rem;
      color: var(--app-text-muted);
    }
    .empty-icon {
      font-size: 2.5rem;
      opacity: 0.5;
    }
    .empty-message {
      font-size: 0.875rem;
      margin: 0;
    }
  `]
})
export class EmptyStateComponent {
  message = input<string>('No data available');
  icon = input<string>('pi pi-inbox');
}
