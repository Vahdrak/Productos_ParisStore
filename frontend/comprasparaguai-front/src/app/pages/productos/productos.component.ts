import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProductoService } from '../../shared/services/producto.service';
import { ErrorService } from '../../shared/services/error.service';
import { isDateInRange, quickRange } from '../../shared/utils/fechas';
import { statusClass, statusLabel, statusField } from '../../shared/utils/estado';
import { QuickRangeComponent } from '../../shared/ui/quick-range.component';

@Component({
  selector: 'app-productos',
  standalone: true,
  imports: [CommonModule, FormsModule, QuickRangeComponent],
  template: `
    <div class="fade-in">
      <div class="search-bar">
        <input type="text" placeholder="Buscar código..." [(ngModel)]="filterCode" class="form-input" />
        <select [(ngModel)]="filterTarget" (ngModelChange)="search()" class="form-input" style="width: 120px;">
          <option value="">Destino: Todos</option>
          <option value="compras">Compras</option>
          <option value="compara">Compara</option>
        </select>
        <select [(ngModel)]="filterRegimen" (ngModelChange)="search()" class="form-input" style="width: 140px;">
          <option value="">Régimen: Todos</option>
          <option value="true">🎫 Con régimen turismo</option>
          <option value="false">Normal (sin régimen)</option>
        </select>
        <select [(ngModel)]="filterState" (ngModelChange)="search()" class="form-input" style="width: 140px;">
          <option value="">Estado: Todos</option>
          <option value="0">⏳ Pendiente</option>
          <option value="1">✅ Enviado</option>
          <option value="2">❌ Error</option>
        </select>
        <button (click)="search()" class="btn btn-primary">Buscar</button>
      </div>

      <div class="search-bar">
        <label class="label-dim">📅 Rango de fechas:</label>
        <input type="date" [(ngModel)]="fechaDesde" (ngModelChange)="search()" class="form-input" style="width: 120px;" />
        <span style="color: #8f8f8f;">hasta</span>
        <input type="date" [(ngModel)]="fechaHasta" (ngModelChange)="search()" class="form-input" style="width: 120px;" />
        <app-quick-range (pick)="setQuickRange($event)" (clear)="fechaDesde = ''; fechaHasta = ''; search();"></app-quick-range>
      </div>

      <div class="search-bar" *ngIf="productList && productList.items.length > 0">
        <select [(ngModel)]="pageSize" (ngModelChange)="search()" class="form-input" style="width: 140px;">
          <option [ngValue]="10">10 por página</option>
          <option [ngValue]="25">25 por página</option>
          <option [ngValue]="50">50 por página</option>
          <option [ngValue]="100">100 por página</option>
        </select>
      </div>

      <div class="table-container mt-6" *ngIf="pagedProducts.length > 0">
        <table class="data-table">
          <thead>
            <tr>
              <th>Código</th>
              <th>Nombre (ES)</th>
              <th>Régimen</th>
              <th>Precio (IVA)</th>
              <th>Precio Exenta</th>
              <th>Stock</th>
              <th>Compras</th>
              <th>Compara</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let p of pagedProducts">
              <td class="text-bold">{{ p.code }}</td>
              <td>{{ p.nameEs }}</td>
              <td><span class="badge" [class.badge-success]="p.regimenTurismo" [class.badge-secondary]="!p.regimenTurismo">{{ p.regimenTurismo ? '🎫 Sí' : 'No' }}</span></td>
              <td>{{ p.price | currency:'':'symbol-narrow' }}</td>
              <td>{{ (p.precioExenta || 0) | currency:'':'symbol-narrow' }}</td>
              <td class="text-center">{{ p.stock }}</td>
              <td>
                <span class="badge" [ngClass]="statusClass(p.syncStatus)">
                  {{ statusLabel(p.syncStatus) }}
                </span>
              </td>
              <td>
                <span class="badge" [ngClass]="statusClass(p.syncComparaStatus)">
                  {{ statusLabel(p.syncComparaStatus) }}
                </span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div *ngIf="totalPages > 1" class="pagination">
        <button [disabled]="page === 1" (click)="goToPage(1)">«</button>
        <button [disabled]="page === 1" (click)="goToPage(page - 1)">‹</button>
        <span class="page-info">Pág {{ page }}/{{ totalPages }}</span>
        <button [disabled]="page === totalPages" (click)="goToPage(page + 1)">›</button>
        <button [disabled]="page === totalPages" (click)="goToPage(totalPages)">»</button>
      </div>

      <div *ngIf="!productList || productList.items.length === 0" class="text-center py-8 text-dim">
        No hay productos
      </div>
    </div>
  `
})
export class ProductosComponent implements OnInit {
  readonly statusClass = statusClass;
  readonly statusLabel = statusLabel;

  productList: any = null;
  pagedProducts: any[] = [];

  filterCode = '';
  filterTarget = '';
  filterRegimen = '';
  filterState = '';
  fechaDesde = '';
  fechaHasta = '';

  pageSize = 25;
  page = 1;
  totalPages = 1;

  constructor(private productoService: ProductoService, private errors: ErrorService) {}

  ngOnInit() {
    this.search();
  }

  /** Nueva búsqueda (cambió un filtro): vuelve a la página 1. */
  search() {
    this.goToPage(1);
  }

  /** Pide la página al servidor; el régimen y las fechas se filtran sobre la página recibida. */
  goToPage(page: number) {
    this.page = Math.max(1, page);
    this.productoService.getProductos({
      code: this.filterCode,
      target: this.filterTarget,
      syncStatus: this.filterState ? parseInt(this.filterState) : undefined
    }, this.page, this.pageSize).subscribe(
      list => {
        if (!list || !list.items) return;
        let items: any[] = list.items;
        if (this.filterRegimen === 'true') {
          items = items.filter(p => p.regimenTurismo === true);
        } else if (this.filterRegimen === 'false') {
          items = items.filter(p => !p.regimenTurismo);
        }
        if (this.filterState) {
          const field = statusField(this.filterTarget || 'compras');
          items = items.filter(p => p[field]?.toString() === this.filterState);
        }
        // Fecha: usa lastSentAt de Compras como referencia
        if (this.fechaDesde || this.fechaHasta) {
          items = items.filter(p => isDateInRange(p.lastSentAt, this.fechaDesde, this.fechaHasta));
        }
        this.totalPages = list.totalPages || 1;
        this.pagedProducts = items;
        this.productList = list;
      },
      () => this.errors.show('Error cargando productos')
    );
  }

  setQuickRange(range: string) {
    const { desde, hasta } = quickRange(range);
    this.fechaDesde = desde;
    this.fechaHasta = hasta;
    this.search();
  }
}
