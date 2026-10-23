import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { quickRange } from '../utils/fechas';
import { QuickRangeComponent } from './quick-range.component';

/** Rango de fechas (desde/hasta) con atajos. Soporta [(desde)] y [(hasta)]; `changed` avisa de cualquier cambio. */
@Component({
  selector: 'app-date-range',
  standalone: true,
  imports: [FormsModule, QuickRangeComponent],
  template: `
    <div class="search-bar">
      <label class="label-dim">{{ label }}</label>
      <input type="date" class="form-input date-input" aria-label="Desde"
             [ngModel]="desde" (ngModelChange)="onDesde($event)" />
      <span class="hasta-sep">hasta</span>
      <input type="date" class="form-input date-input" aria-label="Hasta"
             [ngModel]="hasta" (ngModelChange)="onHasta($event)" />
      <app-quick-range (pick)="onPick($event)" (clear)="onClear()"></app-quick-range>
    </div>
  `,
  styles: [`.hasta-sep { color: var(--text-dim); }`]
})
export class DateRangeComponent {
  @Input() desde = '';
  @Input() hasta = '';
  @Input() label = 'Rango de fechas:';
  @Output() desdeChange = new EventEmitter<string>();
  @Output() hastaChange = new EventEmitter<string>();
  @Output() changed = new EventEmitter<void>();
  @Output() cleared = new EventEmitter<void>();

  onDesde(valor: string): void {
    this.desde = valor ?? '';
    this.desdeChange.emit(this.desde);
    this.changed.emit();
  }

  onHasta(valor: string): void {
    this.hasta = valor ?? '';
    this.hastaChange.emit(this.hasta);
    this.changed.emit();
  }

  onPick(rango: string): void {
    const r = quickRange(rango);
    this.desde = r.desde;
    this.hasta = r.hasta;
    this.desdeChange.emit(this.desde);
    this.hastaChange.emit(this.hasta);
    this.changed.emit();
  }

  onClear(): void {
    this.desde = '';
    this.hasta = '';
    this.desdeChange.emit('');
    this.hastaChange.emit('');
    this.cleared.emit();
  }
}
