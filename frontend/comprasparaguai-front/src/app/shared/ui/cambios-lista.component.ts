import { FechaPyPipe } from '../utils/fecha-py.pipe';
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Cambio } from '../services/cambios.service';
import { IconComponent } from './icon.component';

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
  reconciliacion: 'Baja (ya no está en tu ERP)',
  api: 'Panel',
  manual: 'Manual',
  historico: 'Histórico'
};

/** Qué significa cada origen (se muestra al pasar el mouse). */
const AYUDA_ORIGEN: Record<string, string> = {
  sync: 'Cambio detectado al leer tu sistema (ERP): precio, stock, nombre, etc.',
  reconciliacion: 'El producto ya no aparece en lo que entrega tu ERP (se dio de baja, se desactivó o cambió de código), por eso su stock pasó a 0 y dejó de estar disponible en los comparadores.',
  api: 'Cambio hecho desde este panel por un usuario.',
  manual: 'Cambio hecho directamente en la base de datos.',
  historico: 'Cambio anterior a que se registrara el origen.'
};

/** Tabla de cambios de productos: fecha, producto, origen y cada campo con su "antes → después". */
@Component({
  selector: 'app-cambios-lista',
  standalone: true,
  imports: [CommonModule, FechaPyPipe, IconComponent],
  template: `
    <div class="table-container" *ngIf="items.length > 0">
      <table class="data-table">
        <thead>
          <tr>
            <th scope="col" style="width: 150px;">Fecha</th>
            <th scope="col" *ngIf="mostrarProducto" style="width: 30%;">Producto</th>
            <th scope="col" style="width: 130px;">Origen</th>
            <th scope="col">Cambios</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let c of items">
            <td>{{ c.changedAt | fechaPy:'yyyy-MM-dd HH:mm' }}</td>
            <td *ngIf="mostrarProducto">
              <div class="text-bold">{{ c.code }}</div>
              <div class="text-small text-dim">{{ c.nombre }}<span *ngIf="c.marca"> · {{ c.marca }}</span></div>
            </td>
            <td [title]="ayudaOrigen(c.origen)">
              {{ origen(c.origen) }}
              <div class="text-small text-dim" *ngIf="c.usuario">{{ c.usuario }}</div>
            </td>
            <td class="td-wrap">
              <span *ngIf="c.operacion === 'Alta'" class="badge badge-success"><app-icon name="plus" size="1em"/> Alta del producto</span>
              <div *ngFor="let x of c.cambios" class="text-small">
                <span class="text-bold">{{ campo(x.campo) }}:</span>
                <span class="text-err">{{ x.antes ?? '—' }}</span>
                →
                <span class="text-ok">{{ x.despues ?? '—' }}</span>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <div *ngIf="!cargando && items.length === 0" class="empty-state">No hay cambios registrados</div>
  `
})
export class CambiosListaComponent {
  @Input() items: Cambio[] = [];
  @Input() mostrarProducto = true;
  /** Mientras es true no se muestra el mensaje de lista vacía. */
  @Input() cargando = false;

  campo(nombre: string): string {
    return CAMPOS[nombre] ?? nombre;
  }

  ayudaOrigen(valor?: string): string {
    return valor ? (AYUDA_ORIGEN[valor] ?? '') : '';
  }

  origen(valor?: string): string {
    return valor ? (ORIGENES[valor] ?? valor) : '—';
  }
}
