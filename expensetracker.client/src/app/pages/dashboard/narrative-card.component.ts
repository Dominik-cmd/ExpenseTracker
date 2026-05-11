import { CommonModule } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { CardModule } from 'primeng/card';

import { NarrativeResponse } from '../../core/models';

@Component({
  standalone: true,
  selector: 'app-narrative-card',
  imports: [CommonModule, CardModule],
  template: `
    @if (showCard()) {
      <p-card>
        @if (title()) {
          <div class="widget-header">
            <span class="widget-title">{{ title() }}</span>
          </div>
        }

        @if (narrative()?.content) {
          <div class="narrative-content">{{ narrative()!.content }}</div>
        } @else if (loading() || narrative()?.isStale) {
          <div class="narrative-loading">Generating insight…</div>
        }

        @if (narrative()?.isStale) {
          <div class="narrative-stale">Updating…</div>
        }
      </p-card>
    }
  `,
  styles: [`
    :host {
      display: block;
    }

    .widget-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 1rem;
    }

    .widget-title {
      font-size: 1rem;
      font-weight: 600;
      color: var(--text-color);
    }

    .narrative-content {
      font-size: 0.95rem;
      font-style: italic;
      color: var(--text-color-secondary);
      line-height: 1.5;
      white-space: pre-line;
    }

    .narrative-loading {
      color: var(--text-color-secondary);
      font-size: 0.9rem;
    }

    .narrative-stale {
      font-size: 0.72rem;
      color: var(--yellow-500);
      margin-top: 0.5rem;
    }
  `]
})
export class NarrativeCardComponent {
  readonly narrative = input<NarrativeResponse | null>(null);
  readonly title = input('AI takeaway');
  readonly loading = input(false);

  protected readonly showCard = computed(() =>
    this.loading() || !!this.narrative()?.content || !!this.narrative()?.isStale
  );
}
