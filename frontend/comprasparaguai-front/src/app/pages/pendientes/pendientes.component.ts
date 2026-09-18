import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BtnComponent, CardComponent, InputComponent } from '../../shared/components';
import { ProductoService, Producto } from '../../shared/services/producto.service';

@Component({
  selector: 'app-pendientes-page',
  standalone: true,
  imports: [CommonModule, FormsModule, BtnComponent, CardComponent, InputComponent],
  template: `
    <div class="pendientes">
      <h2>Productos Pendientes de Envío</h2>
      <p class="subtitle">Monitoreo de envíos a APIs</p>

      <div class="tabs">
        <button
          [class.active]="selectedTarget === 'compras'"
          (click)="selectedTarget = 'compras'; loadProducts()">
          Compras Paraguai
        </button>
        <button
          [class.active]="selectedTarget === 'compara'"
          (click)="selectedTarget = 'compara'; loadProducts()">
          Compara Paraguai
        </button>
      </div>

      <app-card title="Filtros" [elevated]="true">
        <div class="filters">
          <app-input
            label="Buscar por código"
            placeholder="PROD-001"
            [(ngModel)]="filterCode"
            (ngModelChange)="applyFilters()" />

          <div class="filter-group">
            <label>Régimen Turismo</label>
            <select [(ngModel)]="filterRegimen" (ngModelChange)="applyFilters()">
              <option value="">Todos</option>
              <option value="true">Con régimen turismo (exenta)</option>
              <option value="false">Sin régimen turismo (normal)</option>
            </select>
          </div>
        </div>
      </app-card>

      <div *ngIf="loading" class="msg loading">Cargando productos...</div>
      <div *ngIf="error" class="msg error">⚠️ {{ error }}</div>

      <app-card title="Productos" [elevated]="true" *ngIf="!loading">
        <div *ngIf="filteredProducts.length > 0; else noProducts" class="table">
          <div class="table-header">
            <div class="col code">Código</div>
            <div class="col name">Nombre</div>
            <div class="col price">Precio</div>
            <div class="col regimen">Régimen</div>
            <div class="col exenta">Precio Exenta</div>
            <div class="col stock">Stock</div>
          </div>
          <div *ngFor="let product of filteredProducts" class="table-row">
            <div class="col code">{{ product.code }}</div>
            <div class="col name">{{ product.namePt }}</div>
            <div class="col price">{{ product.price | currency }}</div>
            <div class="col regimen">
              <span *ngIf="product.regimenTurismo" class="badge turismo">🎫 Sí</span>
              <span *ngIf="!product.regimenTurismo" class="badge normal">No</span>
            </div>
            <div class="col exenta">
              {{ product.precioExenta | currency }}
            </div>
            <div class="col stock">{{ product.stock }}</div>
          </div>
        </div>
        <ng-template #noProducts>
          <p class="info">No hay productos pendientes</p>
        </ng-template>

        <div class="summary">
          <div class="summary-item">
            <strong>Total:</strong> {{ filteredProducts.length }}
          </div>
          <div class="summary-item">
            <strong>Con régimen turismo:</strong> {{ countRegimen() }}
          </div>
          <div class="summary-item">
            <strong>Sin régimen turismo:</strong> {{ filteredProducts.length - countRegimen() }}
          </div>
        </div>
      </app-card>
    </div>
  `,
  styles: [`
    .pendientes {
      max-width: 1400px;
    }

    h2 {
      margin: 0 0 4px 0;
      font-size: 24px;
      font-weight: 500;
    }

    .subtitle {
      margin: 0 0 20px 0;
      color: var(--text-secondary);
      font-size: 14px;
    }

    .tabs {
      display: flex;
      gap: 8px;
      margin-bottom: 20px;
    }

    .tabs button {
      padding: 10px 16px;
      border: 1px solid var(--border);
      background: transparent;
      border-radius: var(--radius);
      cursor: pointer;
      font-size: 13px;
      transition: all 0.2s;
      color: var(--text-secondary);
    }

    .tabs button:hover {
      border-color: var(--fill-accent);
      color: var(--text-accent);
    }

    .tabs button.active {
      background: var(--fill-accent);
      color: white;
      border-color: var(--fill-accent);
    }

    .filters {
      display: grid;
      grid-template-columns: 1fr 200px;
      gap: 16px;
      align-items: flex-end;
    }

    .filter-group {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }

    .filter-group label {
      font-size: 12px;
      font-weight: 500;
      color: var(--text-secondary);
    }

    .filter-group select {
      padding: 8px 12px;
      border: 1px solid var(--border);
      border-radius: var(--radius);
      background: var(--surface-1);
      color: var(--text-primary);
      font-size: 13px;
      cursor: pointer;
    }

    .table {
      margin-top: 16px;
    }

    .table-header {
      display: grid;
      grid-template-columns: 120px 1fr 100px 100px 120px 80px;
      gap: 12px;
      padding: 12px;
      background: var(--surface-2);
      border-radius: var(--radius);
      font-weight: 500;
      font-size: 13px;
      color: var(--text-secondary);
      border-bottom: 1px solid var(--border);
    }

    .table-row {
      display: grid;
      grid-template-columns: 120px 1fr 100px 100px 120px 80px;
      gap: 12px;
      padding: 12px;
      border-bottom: 1px solid var(--border);
      align-items: center;
      font-size: 13px;
    }

    .table-row:hover {
      background: var(--surface-1);
    }

    .col {
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .col.code {
      font-weight: 500;
      color: var(--fill-accent);
    }

    .col.name {
      white-space: normal;
    }

    .col.price {
      text-align: right;
    }

    .col.exenta {
      text-align: right;
    }

    .col.stock {
      text-align: center;
    }

    .badge {
      display: inline-block;
      padding: 4px 8px;
      border-radius: 4px;
      font-size: 11px;
      font-weight: 500;
    }

    .badge.turismo {
      background: var(--bg-accent);
      color: var(--text-accent);
    }

    .badge.normal {
      background: var(--surface-2);
      color: var(--text-secondary);
    }

    .msg {
      padding: 16px;
      border-radius: var(--radius);
      font-size: 13px;
      text-align: center;
    }

    .loading {
      background: var(--bg-accent);
      color: var(--text-accent);
    }

    .error {
      background: var(--bg-danger);
      color: var(--text-danger);
    }

    .info {
      padding: 24px;
      text-align: center;
      color: var(--text-secondary);
      font-size: 13px;
    }

    .summary {
      display: flex;
      gap: 24px;
      padding: 16px;
      background: var(--surface-1);
      border-radius: var(--radius);
      margin-top: 16px;
      font-size: 13px;
    }

    .summary-item {
      display: flex;
      gap: 8px;
    }

    .summary-item strong {
      color: var(--text-secondary);
    }
  `],
  providers: [ProductoService]
})
export class PendientesPageComponent implements OnInit {
  selectedTarget = 'compras';
  products: Producto[] = [];
  filteredProducts: Producto[] = [];
  filterCode = '';
  filterRegimen = '';
  loading = false;
  error = '';

  constructor(private productoService: ProductoService) {}

  ngOnInit() {
    this.loadProducts();
  }

  loadProducts() {
    this.loading = true;
    this.error = '';
    this.productoService.getPendingProducts(this.selectedTarget).subscribe({
      next: (products) => {
        this.products = products;
        this.applyFilters();
        this.loading = false;
      },
      error: (e) => {
        this.error = 'Error cargando productos';
        this.loading = false;
      }
    });
  }

  applyFilters() {
    this.filteredProducts = this.products.filter(p => {
      const matchCode = !this.filterCode || p.code.toLowerCase().includes(this.filterCode.toLowerCase());
      const matchRegimen = !this.filterRegimen ||
        (this.filterRegimen === 'true' && p.regimenTurismo) ||
        (this.filterRegimen === 'false' && !p.regimenTurismo);
      return matchCode && matchRegimen;
    });
  }

  countRegimen(): number {
    return this.filteredProducts.filter(p => p.regimenTurismo).length;
  }
}
