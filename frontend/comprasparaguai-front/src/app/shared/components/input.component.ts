import { Component, Input, Output, EventEmitter, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

@Component({
  selector: 'app-input',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="input-group">
      <label *ngIf="label" [for]="inputId">{{ label }}</label>
      <input
        [id]="inputId"
        [type]="type"
        [value]="value"
        [placeholder]="placeholder"
        [disabled]="disabled"
        (input)="onInput($event)"
        (change)="onChange($event)"
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
export class InputComponent implements ControlValueAccessor {
  @Input() label: string = '';
  @Input() placeholder: string = '';
  @Input() type: string = 'text';
  @Input() disabled: boolean = false;
  @Input() hint: string = '';
  @Input() inputId: string = 'input-' + Math.random();
  @Output() inputValue = new EventEmitter<string>();

  value: string = '';
  private onChange = (value: string) => {};
  private onTouched = () => {};

  onInput(event: Event): void {
    const target = event.target as HTMLInputElement;
    this.value = target.value;
    this.inputValue.emit(this.value);
    this.onChange(this.value);
  }

  onChange(event: Event): void {
    const target = event.target as HTMLInputElement;
    this.onTouched();
  }

  writeValue(value: any): void {
    this.value = value || '';
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
  }
}
