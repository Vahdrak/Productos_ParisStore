import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClientModule } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { BtnComponent, InputComponent, CardComponent, BadgeComponent } from '../../shared/components';
import { ProductoService, Producto, ProductDiff, ProductHistorial, Dashboard } from '../../shared/services/producto.service';

@Component({
  selector: 'app-productos-test',
  standalone: true,
  imports: [CommonModule, HttpClientModule, FormsModule, BtnComponent, InputComponent, CardComponent, BadgeComponent],
  template: `
    <div class="container">
      <header class="header">
        <h1>📦 Compras Paraguai</h1>
        <p>Monitoreo de sincronización</p>
      </header>

      <!-- Navegación por tabs -->
      <div class="nav-tabs">
        <button [class.active]="activeTab === 'dashboard'" (click)="activeTab = 'dashboard'" class="tab-btn">
          📊 Dashboard
        </button>
        <button [class.active]="activeTab === 'prueba'" (click)="activeTab = 'prueba'" class="tab-btn">
          🔍 Buscar
        </button>
      </div>

      <!-- TAB 1: DASHBOARD (datos reales) -->
      <div *ngIf="activeTab === 'dashboard'" class="tab-content">
        <div *ngIf="dashboard" class="stats-grid">
          <app-card title="Pendientes" [elevated]="true">
            <div class="stat-big">{{ dashboard.pendingCount }}</div>
          </app-card>
          <app-card title="Nuevos" [elevated]="true">
            <div class="stat-big">{{ dashboard.newCount }}</div>
          </app-card>
          <app-card title="Actualizados" [elevated]="true">
            <div class="stat-big">{{ dashboard.updatedCount }}</div>
          </app-card>
          <app-card title="Enviados" [elevated]="true">
            <div class="stat-big">{{ dashboard.sentCount }}</div>
          </app-card>
          <app-card title="Errores" [elevated]="true">
            <div class="stat-big error-text">{{ dashboard.errorCount }}</div>
          </app-card>
          <app-card title="Estado" [elevated]="true">
            <div class="stat-big">{{ dashboard.status }}</div>
            <div class="stat-date">{{ dashboard.lastSyncAt | date:'short' }}</div>
          </app-card>
        </div>

        <div *ngIf="!dashboard" class="loading">Cargando dashboard...</div>
      </div>

      <!-- TAB 2: BUSCAR (datos reales) -->
      <div *ngIf="activeTab === 'prueba'" class="tab-content">
        <app-card title="Buscar Producto" [elevated]="true">
          <div class="search-row">
            <app-input
              label="Código"
              placeholder="Ej: PROD-001847"
              [(ngModel)]="searchCode"
              hint="Código del producto" />
            <app-btn label="Buscar" variant="primary" (onClick)="searchProduct()" />
          </div>
          <div *ngIf="loading" class="loading">Cargando...</div>
          <div *ngIf="error" class="error">⚠️ {{ error }}</div>
        </app-card>

        <!-- Resultado -->
        <div *ngIf="currentProduct" class="results">
          <app-card [title]="currentProduct.code" [elevated]="true">
            <div class="detail">
              <div><strong>Nombre:</strong> {{ currentProduct.namePt }}</div>
              <div><strong>Precio:</strong> {{ currentProduct.price | currency }}</div>
              <div><strong>Stock:</strong> {{ currentProduct.stock }}</div>
              <div><strong>Marca:</strong> {{ currentProduct.brand || 'N/A' }}</div>
            </div>

            <div class="card-footer">
              <app-btn label="Editar" (onClick)="toggleEditMode()" />
              <app-btn label="Ver Diff" variant="primary" (onClick)="loadDiff()" />
              <app-btn label="Historial" (onClick)="loadHistorial()" />
            </div>
          </app-card>
        </div>

        <!-- Edición -->
        <div *ngIf="editMode && currentProduct" class="edit-section">
          <app-card title="Editar" [elevated]="true">
            <div class="form-grid">
              <app-input label="Nombre" [(ngModel)]="editedProduct.namePt" />
              <app-input label="Precio" type="number" [(ngModel)]="editedProduct.price" />
              <app-input label="Stock" type="number" [(ngModel)]="editedProduct.stock" />
            </div>

            <div class="card-footer">
              <app-btn label="Guardar" variant="primary" (onClick)="saveProduct()" />
              <app-btn label="Cancelar" (onClick)="toggleEditMode()" />
            </div>
          </app-card>
        </div>

        <!-- Diff -->
        <div *ngIf="productDiff && !historialMode">
          <app-card [title]="'Cambios: ' + productDiff.code" [elevated]="true">
            <div *ngIf="productDiff.hasChanges" class="diff">
              <div class="diff-col">
                <h4>Antes</h4>
                <div *ngIf="productDiff.previous">
                  <div>Precio: {{ productDiff.previous.price | currency }}</div>
                  <div>Stock: {{ productDiff.previous.stock }}</div>
                </div>
              </div>
              <div class="diff-col">
                <h4>Ahora</h4>
                <div>Precio: {{ productDiff.current.price | currency }}</div>
                <div>Stock: {{ productDiff.current.stock }}</div>
              </div>
            </div>
            <div *ngIf="!productDiff.hasChanges" class="empty">Sin cambios</div>
          </app-card>
        </div>

        <!-- Historial -->
        <div *ngIf="historialMode">
          <app-card title="Historial" [elevated]="true">
            <div *ngIf="productHistorial.length > 0">
              <div *ngFor="let h of productHistorial" class="hist-item">
                <div class="hist-date">{{ h.changedAt | date:'short' }}</div>
                <div>Precio: {{ h.price | currency }} | Stock: {{ h.stock }}</div>
              </div>
            </div>
            <div *ngIf="productHistorial.length === 0" class="empty">Sin historial</div>
            <div class="card-footer">
              <app-btn label="Volver" (onClick)="historialMode = false" />
            </div>
          </app-card>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .container {
      max-width: 100%;
      margin: 0;
      padding: 24px;
      font-family: var(--font-sans);
      color: var(--text-primary);
    }

    .header {
      margin-bottom: 24px;
      text-align: center;
    }

    .header h1 {
      font-size: 24px;
      font-weight: 500;
      margin: 0;
    }

    .header p {
      color: var(--text-secondary);
      margin: 4px 0 0 0;
      font-size: 14px;
    }

    .nav-tabs {
      display: flex;
      gap: 8px;
      margin-bottom: 20px;
      border-bottom: 1px solid var(--border);
      padding-bottom: 0;
    }

    .tab-btn {
      padding: 12px 16px;
      background: transparent;
      border: none;
      border-bottom: 2px solid transparent;
      cursor: pointer;
      font-size: 14px;
      font-weight: 500;
      color: var(--text-secondary);
      transition: all 0.2s;
    }

    .tab-btn:hover {
      color: var(--text-primary);
    }

    .tab-btn.active {
      color: var(--fill-accent);
      border-bottom-color: var(--fill-accent);
    }

    .tab-content {
      animation: fadeIn 0.2s;
    }

    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    .stats-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));
      gap: 12px;
    }

    .stat-big {
      font-size: 28px;
      font-weight: 500;
      color: var(--fill-accent);
      margin: 8px 0;
    }

    .error-text {
      color: var(--fill-danger);
    }

    .stat-date {
      font-size: 12px;
      color: var(--text-secondary);
    }

    .search-row {
      display: flex;
      gap: 12px;
      align-items: flex-end;
    }

    .search-row app-input {
      flex: 1;
    }

    .results {
      margin-top: 20px;
    }

    .detail {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding: 12px 0;
    }

    .detail div {
      font-size: 14px;
    }

    .card-footer {
      display: flex;
      gap: 8px;
      margin-top: 16px;
      padding-top: 12px;
      border-top: 0.5px solid var(--border);
    }

    .edit-section {
      margin-top: 20px;
    }

    .form-grid {
      display: flex;
      flex-direction: column;
      gap: 12px;
      margin-bottom: 12px;
    }

    .diff {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 20px;
      padding: 12px 0;
    }

    .diff-col h4 {
      margin: 0 0 8px 0;
      font-size: 13px;
      color: var(--text-secondary);
    }

    .diff-col {
      font-size: 13px;
    }

    .diff-col div {
      margin: 4px 0;
    }

    .hist-item {
      padding: 12px;
      border-left: 2px solid var(--fill-accent);
      background: var(--surface-1);
      border-radius: var(--radius);
      margin-bottom: 8px;
      font-size: 13px;
    }

    .hist-date {
      font-size: 12px;
      color: var(--text-secondary);
      margin-bottom: 4px;
    }

    .empty {
      padding: 12px;
      text-align: center;
      color: var(--text-secondary);
      font-size: 13px;
    }

    .loading {
      padding: 12px;
      background: var(--bg-accent);
      color: var(--text-accent);
      border-radius: var(--radius);
      font-size: 13px;
      margin-top: 12px;
    }

    .error {
      padding: 12px;
      background: var(--bg-danger);
      color: var(--text-danger);
      border-radius: var(--radius);
      font-size: 13px;
      margin-top: 12px;
    }
  `],
  providers: [ProductoService]
})
export class ProductosTestComponent implements OnInit {
  activeTab: 'dashboard' | 'prueba' = 'dashboard';
  dashboard: Dashboard | null = null;
  searchCode: string = '';
  currentProduct: Producto | null = null;
  editedProduct: Partial<Producto> = {};
  editMode = false;
  loading = false;
  error: string | null = null;
  productDiff: ProductDiff | null = null;
  productHistorial: ProductHistorial[] = [];
  historialMode = false;

  constructor(private productoService: ProductoService) {}

  ngOnInit(): void {
    this.loadDashboard();
  }

  loadDashboard(): void {
    this.productoService.getDashboard().subscribe({
      next: (data) => {
        this.dashboard = data;
      },
      error: (err) => {
        console.error(err);
      }
    });
  }

  searchProduct(): void {
    if (!this.searchCode.trim()) {
      this.error = 'Ingresa código';
      return;
    }

    this.loading = true;
    this.error = null;
    this.currentProduct = null;

    this.productoService.getProductByCode(this.searchCode).subscribe({
      next: (product) => {
        this.currentProduct = product;
        this.editedProduct = { ...product };
        this.loading = false;
      },
      error: (err) => {
        this.error = err.message;
        this.loading = false;
      }
    });
  }

  loadDiff(): void {
    if (!this.currentProduct) return;
    this.productoService.getProductDiff(this.currentProduct.code).subscribe({
      next: (diff) => {
        this.productDiff = diff;
        this.historialMode = false;
      },
      error: (err) => {
        this.error = err.message;
      }
    });
  }

  loadHistorial(): void {
    if (!this.currentProduct) return;
    this.historialMode = true;
    this.productoService.getProductHistorial(this.currentProduct.code).subscribe({
      next: (historial) => {
        this.productHistorial = historial;
      },
      error: (err) => {
        this.error = err.message;
      }
    });
  }

  toggleEditMode(): void {
    this.editMode = !this.editMode;
    if (!this.editMode) {
      this.editedProduct = { ...this.currentProduct };
    }
  }

  saveProduct(): void {
    if (!this.currentProduct) return;
    this.loading = true;
    this.productoService.updateProduct(this.currentProduct.code, this.editedProduct).subscribe({
      next: (updated) => {
        this.currentProduct = updated;
        this.editMode = false;
        this.loading = false;
      },
      error: (err) => {
        this.error = err.message;
        this.loading = false;
      }
    });
  }
}
