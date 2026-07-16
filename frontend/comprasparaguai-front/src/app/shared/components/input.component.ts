import { Component, Input, Output, EventEmitter, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, FormsModule } from '@angular/forms';

@Component({
  selector: 'app-input',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="input-group">
      <label *ngIf="label" [for]="inputId">{{ label }}</label>
      <input
        [id]="inputId"
        [type]="type"
        [(ngModel)]="value"
        [placeholder]="placeholder"
        [disabled]="disabled"
        (input)="onInput($event)"
        class="input-field" />
      <span *ngIf="hint" class="hint">{{ hint }}</span>
    </div>
  `,
  styles: [`
    .input-group {
      display: flex;
      flex-direction: column;
      gap: 6px;
      width: 100%;
    }
    label {
      font-size: 13px;
      font-weight: 500;
      color: var(--text-secondary);
    }
    .input-field {
      padding: 10px 12px;
      border: 0.5px solid var(--border);
      border-radius: var(--radius);
      font-size: 14px;
      font-family: inherit;
      transition: all 0.2s;
      background: var(--surface-2);
      color: var(--text-primary);
    }
    .input-field:focus {
      outline: none;
      border-color: var(--fill-accent);
      box-shadow: 0 0 0 2px var(--bg-accent);
    }
    .input-field:disabled {
      background: var(--surface-1);
      opacity: 0.6;
      cursor: not-allowed;
    }
    .hint {
      font-size: 12px;
      color: var(--text-muted);
    }
  `]
})
export class InputComponent {
  @Input() label: string = '';
  @Input() placeholder: string = '';
  @Input() type: string = 'text';
  @Input() disabled: boolean = false;
  @Input() hint: string = '';
  @Input() inputId: string = 'input-' + Math.random();
  @Input() value: string = '';
  @Output() inputValue = new EventEmitter<string>();

  onInput(event: Event): void {
    const target = event.target as HTMLInputElement;
    this.value = target.value;
    this.inputValue.emit(this.value);
  }
}
