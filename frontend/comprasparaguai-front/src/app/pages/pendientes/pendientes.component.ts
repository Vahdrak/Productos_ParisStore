import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProductoService } from '../../shared/services/producto.service';
import { ErrorService } from '../../shared/services/error.service';
import { isDateInRange, quickRange } from '../../shared/utils/fechas';
import { statusField, statusLabel } from '../../shared/utils/estado';
import { QuickRangeComponent } from '../../shared/ui/quick-range.component';

@Component({
  selector: 'app-pendientes',
  standalone: true,
  imports: [CommonModule, FormsModule, QuickRangeComponent],
  template: `
    <div class="fade-in">
      <div class="section-card">
        <div class="filter-tabs">
          <button [class.active]="target === 'compras'" (click)="switchTarget('compras')">Compras Paraguai</button>
          <button [class.active]="target === 'compara'" (click)="switchTarget('compara')">Compara Paraguai</button>
        </div>
      </div>

      <div class="section-card">
        <h3 class="section-title">Filtros</h3>
        <div class="filters-grid">
          <input type="text" placeholder="Código..." [(ngModel)]="filterCode" (ngModelChange)="resetPage()" class="form-input" />
          <select [(ngModel)]="filterRegimen" (ngModelChange)="resetPage()" class="form-input">
            <option value="">Régimen: Todos</option>
            <option value="true">🎫 Con régimen turismo</option>
            <option value="false">Normal (sin régimen)</option>
          </select>
          <select [(ngModel)]="filterStatus" (ngModelChange)="resetPage()" class="form-input">
            <option value="">Estado: Todos</option>
            <option value="0">⏳ Pendiente</option>
            <option value="1">✅ Enviado</option>
            <option value="2">❌ Error</option>
          </select>
          <select [(ngModel)]="pageSize" (ngModelChange)="resetPage()" class="form-input">
            <option [ngValue]="10">10 por página</option>
            <option [ngValue]="25">25 por página</option>
            <option [ngValue]="50">50 por página</option>
            <option [ngValue]="100">100 por página</option>
          </select>
        </div>
        <div class="filters-grid" style="margin-top: 12px;">
          <label class="label-dim" style="grid-column: 1 / -1;">📅 Rango de envíos:</label>
          <input type="date" [(ngModel)]="fechaDesde" (ngModelChange)="resetPage()" class="form-input" />
          <input type="date" [(ngModel)]="fechaHasta" (ngModelChange)="resetPage()" class="form-input" />
          <app-quick-range (pick)="setQuickRange($event)" (clear)="fechaDesde = ''; fechaHasta = ''; resetPage();"></app-quick-range>
        </div>
      </div>

      <div class="section-card">
        <h3 class="section-title">Productos ({{ filtered.length }} resultados)</h3>
        <div class="table-container" *ngIf="pagedProducts.length > 0">
          <table class="data-table">
            <thead><tr><th>Código</th><th>Nombre</th><th>Régimen</th><th>Precio (IVA)</th><th>Precio Exenta</th><th>Stock</th><th>Estado</th></tr></thead>
            <tbody>
              <tr *ngFor="let p of pagedProducts">
                <td class="text-bold">{{ p.code }}</td>
                <td>{{ p.namePt | slice:0:35 }}</td>
                <td><span class="badge" [class.badge-success]="p.regimenTurismo" [class.badge-secondary]="!p.regimenTurismo">{{ p.regimenTurismo ? '🎫 Sí' : 'No' }}</span></td>
                <td>{{ p.price | currency:'':'symbol-narrow' }}</td>
                <td>{{ (p.precioExenta || 0) | currency:'':'symbol-narrow' }}</td>
                <td class="text-center">{{ p.stock }}</td>
                <td><span class="badge" [ngClass]="getStatusBadgeClass(p)">{{ getStatusLabel(p) }}</span></td>
              </tr>
            </tbody>
          </table>
        </div>
        <div *ngIf="filtered.length === 0" class="text-center py-8 text-dim">No hay productos que coincidan</div>

        <div *ngIf="totalPages > 1" class="pagination">
          <button [disabled]="page === 1" (click)="goToPage(1)">«</button>
          <button [disabled]="page === 1" (click)="goToPage(page - 1)">‹</button>
          <span class="page-info">Pág {{ page }}/{{ totalPages }}</span>
          <button [disabled]="page === totalPages" (click)="goToPage(page + 1)">›</button>
          <button [disabled]="page === totalPages" (click)="goToPage(totalPages)">»</button>
        </div>

        <div *ngIf="products.length > 0" class="summary-bar">
          <div class="summary-item"><span class="summary-label">🎫 Con régimen:</span><span class="summary-value">{{ countRegimen() }}</span></div>
          <div class="summary-item"><span class="summary-label">Normal:</span><span class="summary-value">{{ products.length - countRegimen() }}</span></div>
          <div class="summary-item"><span class="summary-label">Total:</span><span class="summary-value">{{ products.length }}</span></div>
        </div>
      </div>
    </div>
  `
})
export class PendientesComponent implements OnInit {
  target = 'compras';
  products: any[] = [];
  filtered: any[] = [];
  pagedProducts: any[] = [];

  filterCode = '';
  filterRegimen = '';
  filterStatus = '';
  fechaDesde = '';
  fechaHasta = '';

  pageSize = 50;
  page = 1;
  totalPages = 1;

  constructor(private productoService: ProductoService, private errors: ErrorService) {}

  ngOnInit() {
    this.load();
  }

  load() {
    this.productoService.getPendingProducts(this.target).subscribe(
      products => {
        this.products = products;
        this.applyFilters();
      },
      () => this.errors.show('Error cargando productos pendientes')
    );
  }

  applyFilters() {
    const field = statusField(this.target);
    const dateField = this.target === 'compras' ? 'lastSentAt' : 'lastSentComparaAt';

    this.filtered = this.products.filter((p: any) => {
      const matchCode = !this.filterCode || p.code.toLowerCase().includes(this.filterCode.toLowerCase());
      const matchRegimen = !this.filterRegimen ||
        (this.filterRegimen === 'true' && p.regimenTurismo) ||
        (this.filterRegimen === 'false' && !p.regimenTurismo);
      const matchStatus = !this.filterStatus || p[field]?.toString() === this.filterStatus;
      const matchDate = isDateInRange(p[dateField], this.fechaDesde, this.fechaHasta);
      return matchCode && matchRegimen && matchStatus && matchDate;
    });

    if (this.page < 1) this.page = 1;
    this.totalPages = Math.ceil(this.filtered.length / this.pageSize) || 1;
    if (this.page > this.totalPages) this.page = this.totalPages;

    const start = (this.page - 1) * this.pageSize;
    this.pagedProducts = this.filtered.slice(start, start + this.pageSize);
  }

  resetPage() {
    this.goToPage(1);
  }

  goToPage(page: number) {
    this.page = Math.min(Math.max(1, page), this.totalPages);
    this.applyFilters();
  }

  switchTarget(target: string) {
    this.target = target;
    this.page = 1;
    this.load();
  }

  setQuickRange(range: string) {
    const { desde, hasta } = quickRange(range);
    this.fechaDesde = desde;
    this.fechaHasta = hasta;
    this.resetPage();
  }

  countRegimen(): number {
    return this.products.filter(p => p.regimenTurismo).length;
  }

  getStatusLabel(p: any): string {
    return statusLabel(p[statusField(this.target)]);
  }

  getStatusBadgeClass(p: any): any {
    const status = p[statusField(this.target)];
    return {
      'badge-success': status === 1,
      'badge-error': status === 2,
      'badge': status === 0
    };
  }
}
