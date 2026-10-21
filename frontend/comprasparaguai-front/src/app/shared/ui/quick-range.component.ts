import { Component, EventEmitter, Output } from '@angular/core';

/** Botones de rango rápido de fechas (Hoy / Última hora / 7 días / 30 días / Limpiar). */
@Component({
  selector: 'app-quick-range',
  standalone: true,
  template: `
    <button (click)="pick.emit('hoy')" class="btn btn-ghost btn-sm">Hoy</button>
    <button (click)="pick.emit('hora')" class="btn btn-ghost btn-sm">Última hora</button>
    <button (click)="pick.emit('semana')" class="btn btn-ghost btn-sm">7 días</button>
    <button (click)="pick.emit('mes')" class="btn btn-ghost btn-sm">30 días</button>
    <button (click)="clear.emit()" class="btn btn-ghost">Limpiar</button>
  `,
  styles: [`:host { display: contents; }`]
})
export class QuickRangeComponent {
  @Output() pick = new EventEmitter<string>();
  @Output() clear = new EventEmitter<void>();
}
