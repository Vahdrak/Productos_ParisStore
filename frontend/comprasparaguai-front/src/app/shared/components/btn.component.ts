import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-btn',
  standalone: true,
  imports: [CommonModule],
  template: `
    <button
      [class]="btnClass"
      [disabled]="disabled"
      (click)="onClick.emit()"
      type="button">
      {{ label }}
    </button>
  `,
  styles: [`
    button {
      padding: 10px 16px;
      border: 0.5px solid var(--border-strong);
      border-radius: var(--radius);
      font-size: 14px;
      font-weight: 500;
      cursor: pointer;
      transition: all 0.2s;
      background: transparent;
      color: var(--text-primary);
    }
    button:hover:not(:disabled) {
      background: var(--surface-1);
      border-color: var(--border-stronger);
    }
    button:active:not(:disabled) {
      transform: scale(0.98);
    }
    button:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
    button.primary {
      background: var(--fill-accent);
      color: var(--on-accent);
      border-color: var(--fill-accent);
    }
    button.primary:hover:not(:disabled) {
      background: var(--fill-accent-hover);
      border-color: var(--fill-accent-hover);
    }
    button.danger {
      background: var(--fill-danger);
      color: var(--on-danger);
      border-color: var(--fill-danger);
    }
    button.danger:hover:not(:disabled) {
      background: var(--fill-danger-hover);
      border-color: var(--fill-danger-hover);
    }
  `]
})
export class BtnComponent {
  @Input() label: string = 'Button';
  @Input() variant: 'default' | 'primary' | 'danger' = 'default';
  @Input() disabled: boolean = false;
  @Output() onClick = new EventEmitter<void>();

  get btnClass(): string {
    return `btn btn-${this.variant}`;
  }
}
