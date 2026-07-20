import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-badge',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span [class]="variant"><ng-content></ng-content>{{ label }}</span>
  `,
  styles: [`
    span {
      display: inline-block;
      padding: 4px 10px;
      border-radius: calc(var(--radius) / 2);
      font-size: 12px;
      font-weight: 500;
    }
    span.pending {
      background: var(--bg-warning);
      color: var(--text-warning);
    }
    span.sent {
      background: var(--bg-success);
      color: var(--text-success);
    }
    span.error {
      background: var(--bg-danger);
      color: var(--text-danger);
    }
    span.info {
      background: var(--bg-accent);
      color: var(--text-accent);
    }
  `]
})
export class BadgeComponent {
  @Input() label: string = '';
  @Input() variant: 'pending' | 'sent' | 'error' | 'info' = 'info';

  get badgeClass(): string {
    return `badge badge-${this.variant}`;
  }
}
