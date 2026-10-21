import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Cambio } from '../services/cambios.service';

const CAMPOS: Record<string, string> = {
  name_es: 'Nombre (ES)',
  name_pt: 'Nombre (PT)',
  marca: 'Marca',
  categoria: 'Categoría',
  price: 'Precio',
  precio_exenta: 'Precio exenta',
  regimen_turismo: 'Régimen turismo',
  stock: 'Stock',
  url_image: 'Imagen'
};

const ORIGENES: Record<string, string> = {
  sync: 'Sync',
  reconciliacion: 'Reconciliación',
  api: 'API',
  manual: 'Manual',
  historico: 'Histórico'
};

/** Tabla de cambios de productos: fecha, producto, origen y cada campo con su "antes → después". */
@Component({
  selector: 'app-cambios-lista',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="table-container" *ngIf="items.length > 0">
      <table class="data-table">
        <thead>
          <tr>
            <th style="width: 150px;">Fecha</th>
            <th *ngIf="mostrarProducto" style="width: 30%;">Producto</th>
            <th style="width: 130px;">Origen</th>
            <th>Cambios</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let c of items">
            <td>{{ c.changedAt | date:'yyyy-MM-dd HH:mm' }}</td>
            <td *ngIf="mostrarProducto">
              <div class="text-bold">{{ c.code }}</div>
              <div class="text-small text-dim">{{ c.nombre }}<span *ngIf="c.marca"> · {{ c.marca }}</span></div>
            </td>
            <td>
              {{ origen(c.origen) }}
              <div class="text-small text-dim" *ngIf="c.usuario">{{ c.usuario }}</div>
            </td>
            <td style="white-space: normal;">
              <span *ngIf="c.operacion === 'Alta'" class="badge badge-success">🆕 Alta del producto</span>
              <div *ngFor="let x of c.cambios" class="text-small">
                <span class="text-bold">{{ campo(x.campo) }}:</span>
                <span style="color: #ef4444;">{{ x.antes ?? '—' }}</span>
                →
                <span style="color: #4ade80;">{{ x.despues ?? '—' }}</span>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <div *ngIf="items.length === 0" class="text-center py-8 text-dim">No hay cambios registrados</div>
  `
})
export class CambiosListaComponent {
  @Input() items: Cambio[] = [];
  @Input() mostrarProducto = true;

  campo(nombre: string): string {
    return CAMPOS[nombre] ?? nombre;
  }

  origen(valor?: string): string {
    return valor ? (ORIGENES[valor] ?? valor) : '—';
  }
}
