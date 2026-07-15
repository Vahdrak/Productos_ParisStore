import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="card" [class.card-elevated]="elevated">
      <div *ngIf="title" class="card-header">
        <h3>{{ title }}</h3>
      </div>
      <div class="card-body">
        <ng-content></ng-content>
      </div>
      <div *ngIf="showFooter" class="card-footer">
        <ng-content select="[card-footer]"></ng-content>
      </div>
    </div>
  `,
  styles: [`
    .card {
      background: var(--surface-2);
      border: 0.5px solid var(--border);
      border-radius: 12px;
      overflow: hidden;
    }
    .card-elevated {
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
    }
    .card-header {
      border-bottom: 0.5px solid var(--border);
      padding: 16px;
    }
    .card-header h3 {
      margin: 0;
      font-size: 16px;
      font-weight: 500;
      color: var(--text-primary);
    }
    .card-body {
      padding: 16px;
    }
    .card-footer {
      border-top: 0.5px solid var(--border);
      padding: 12px 16px;
      background: var(--surface-1);
    }
  `]
})
export class CardComponent {
  @Input() title: string = '';
  @Input() elevated: boolean = false;
  @Input() showFooter: boolean = false;
}
