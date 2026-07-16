import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClientModule } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { BtnComponent, InputComponent, CardComponent, BadgeComponent } from './shared/components';
import { ProductoService } from './shared/services/producto.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, HttpClientModule, FormsModule, BtnComponent, InputComponent, CardComponent, BadgeComponent],
  template: `
    <div class="app-layout">
      <!-- Sidebar -->
      <aside class="sidebar">
        <div class="sidebar-header">
          <h1>📦 Compras Paraguai</h1>
        </div>

        <nav class="sidebar-nav">
          <button [class.active]="page === 'dashboard'" (click)="page = 'dashboard'" class="nav-btn">
            📊 Dashboard
          </button>
          <button [class.active]="page === 'productos'" (click)="page = 'productos'" class="nav-btn">
            📋 Productos
          </button>
        </nav>
      </aside>

      <!-- Content -->
      <main class="content">
        <!-- DASHBOARD PAGE -->
        <div *ngIf="page === 'dashboard'">
          <h2>Dashboard Operativo</h2>
          <p class="subtitle">Resumen de sincronización en tiempo real</p>

          <!-- Stats Grid -->
          <div *ngIf="dashboard" class="stats-grid">
            <app-card title="Pendientes" [elevated]="true">
              <div class="big-number">{{ dashboard.pendingCount }}</div>
              <p class="desc">Sin enviar</p>
            </app-card>
            <app-card title="Nuevos" [elevated]="true">
              <div class="big-number">{{ dashboard.newCount }}</div>
              <p class="desc">Última sync</p>
            </app-card>
            <app-card title="Actualizados" [elevated]="true">
              <div class="big-number">{{ dashboard.updatedCount }}</div>
              <p class="desc">Cambios</p>
            </app-card>
            <app-card title="Enviados" [elevated]="true">
              <div class="big-number">{{ dashboard.sentCount }}</div>
              <p class="desc">API</p>
            </app-card>
            <app-card title="Errores" [elevated]="true">
              <div class="big-number error">{{ dashboard.errorCount }}</div>
              <p class="desc">Con error</p>
            </app-card>
            <app-card title="Estado" [elevated]="true">
              <div class="big-number" [class.ok]="dashboard.status === 'OK'">{{ dashboard.status }}</div>
              <p class="desc" *ngIf="dashboard.lastSyncAt">{{ dashboard.lastSyncAt | date:'short' }}</p>
              <p class="desc" *ngIf="!dashboard.lastSyncAt">Sin sincronización</p>
            </app-card>
          </div>

          <div *ngIf="!dashboard" class="loading">Cargando dashboard...</div>

          <!-- Tabla de Últimas Corridas -->
          <div *ngIf="syncRuns" class="section">
            <h3>Últimas Corridas</h3>
            <table class="runs-table">
              <thead>
                <tr>
                  <th>Inicio</th>
                  <th>Fin</th>
                  <th>Nuevos</th>
                  <th>Actualizados</th>
                  <th>Enviados</th>
                  <th>Errores</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let run of syncRuns">
                  <td>{{ run.startedAt | date:'short' }}</td>
                  <td>{{ run.finishedAt | date:'short' }}</td>
                  <td>{{ run.newCount }}</td>
                  <td>{{ run.updatedCount }}</td>
                  <td>{{ run.sentCount }}</td>
                  <td>{{ run.errorCount }}</td>
                </tr>
              </tbody>
            </table>
            <div *ngIf="syncRuns.length === 0" class="empty-state">Sin corridas registradas</div>
          </div>
        </div>

        <!-- PRODUCTOS PAGE -->
        <div *ngIf="page === 'productos'">
          <h2>Productos</h2>
          <p class="subtitle">Organizados por categoría</p>

          <div *ngIf="!categoriesWithProducts" class="loading">Cargando productos...</div>

          <!-- Categorías con Acordeón -->
          <div *ngIf="categoriesWithProducts && categoriesWithProducts.length > 0" class="categories-container">
            <div *ngFor="let category of categoriesWithProducts" class="category-section">
              <button (click)="toggleCategory(category.category)" class="category-header">
                <span class="category-icon">{{ expandedCategories[category.category] ? '▼' : '▶' }}</span>
                <span class="category-name">{{ category.category }}</span>
                <span class="category-count">({{ category.totalCount }} productos)</span>
              </button>

              <!-- Productos dentro de la categoría -->
              <div *ngIf="expandedCategories[category.category]" class="category-content">
                <table class="productos-table">
                  <thead>
                    <tr>
                      <th>Código</th>
                      <th>Nombre</th>
                      <th>Precio</th>
                      <th>Stock</th>
                      <th>Estado</th>
                      <th>Acción</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr *ngFor="let prod of category.products">
                      <td class="code">{{ prod.code }}</td>
                      <td>{{ prod.namePt }}</td>
                      <td class="price">{{ prod.price | currency }}</td>
                      <td class="stock">{{ prod.stock }}</td>
                      <td>
                        <app-badge [variant]="getStatusVariant(prod.syncStatus)">
                          {{ getStatusLabel(prod.syncStatus) }}
                        </app-badge>
                      </td>
                      <td>
                        <app-btn label="Ver" (onClick)="viewProductDetail(prod.code)" />
                      </td>
                    </tr>
                  </tbody>
                </table>

                <!-- Paginación por categoría (placeholder para futuro) -->
                <div class="category-pagination">
                  <p>{{ category.products.length }} de {{ category.totalCount }} productos mostrados</p>
                </div>
              </div>
            </div>
          </div>

          <div *ngIf="categoriesWithProducts && categoriesWithProducts.length === 0" class="empty-state">
            No hay productos
          </div>
        </div>

        <!-- DETALLE PAGE -->
        <div *ngIf="page === 'detalle' && productDetail">
          <div class="detalle-header">
            <app-btn label="← Volver" (onClick)="page = 'productos'" />
            <h2>{{ productDetail.code }}</h2>
          </div>

          <div class="detalle-grid">
            <!-- Datos Actuales -->
            <app-card title="Datos Actuales" [elevated]="true">
              <div class="detail-row"><strong>Código:</strong> {{ productDetail.code }}</div>
              <div class="detail-row"><strong>Nombre (PT):</strong> {{ productDetail.namePt }}</div>
              <div class="detail-row"><strong>Nombre (ES):</strong> {{ productDetail.nameEs }}</div>
              <div class="detail-row"><strong>Precio:</strong> {{ productDetail.price | currency }}</div>
              <div class="detail-row"><strong>Stock:</strong> {{ productDetail.stock }}</div>
              <div class="detail-row"><strong>Marca:</strong> {{ productDetail.brand || 'N/A' }}</div>
              <div class="detail-row">
                <strong>Estado:</strong>
                <app-badge [variant]="getStatusVariant(productDetail.syncStatus)">
                  {{ getStatusLabel(productDetail.syncStatus) }}
                </app-badge>
              </div>
            </app-card>

            <!-- Diff (Antes/Después) -->
            <app-card *ngIf="productDiff" title="Últimos Cambios" [elevated]="true">
              <div *ngIf="productDiff.hasChanges" class="diff-view">
                <div class="diff-col">
                  <h4>Antes</h4>
                  <div *ngIf="productDiff.previous">
                    <div>Precio: {{ productDiff.previous.price | currency }}</div>
                    <div>Stock: {{ productDiff.previous.stock }}</div>
                    <div>Fecha: {{ productDiff.previous.changedAt | date:'short' }}</div>
                  </div>
                </div>
                <div class="diff-col">
                  <h4>Ahora</h4>
                  <div>Precio: {{ productDiff.current.price | currency }}</div>
                  <div>Stock: {{ productDiff.current.stock }}</div>
                  <div>Fecha: {{ productDiff.current.changedAt | date:'short' }}</div>
                </div>
              </div>
              <div *ngIf="!productDiff.hasChanges" class="empty-state">Sin cambios registrados</div>
            </app-card>
          </div>

          <!-- Timeline del Historial -->
          <app-card *ngIf="productHistory" title="Historial de Cambios" [elevated]="true">
            <div *ngIf="productHistory.length > 0" class="timeline">
              <div *ngFor="let h of productHistory" class="timeline-item">
                <div class="timeline-date">{{ h.changedAt | date:'short' }}</div>
                <div class="timeline-content">
                  <div>Precio: {{ h.price | currency }} → Stock: {{ h.stock }}</div>
                  <div class="timeline-note">Cambio detectado en sincronización</div>
                </div>
              </div>
            </div>
            <div *ngIf="productHistory.length === 0" class="empty-state">Sin historial de cambios</div>
          </app-card>
        </div>
      </main>
    </div>
  `,
  styles: [`
    .app-layout { display: flex; height: 100vh; background: var(--surface-0); }
    .sidebar { width: 240px; background: var(--surface-1); border-right: 0.5px solid var(--border); display: flex; flex-direction: column; overflow-y: auto; }
    .sidebar-header { padding: 20px; border-bottom: 0.5px solid var(--border); background: var(--surface-2); }
    .sidebar-header h1 { margin: 0; font-size: 18px; font-weight: 500; }
    .sidebar-nav { display: flex; flex-direction: column; padding: 12px; gap: 4px; }
    .nav-btn { padding: 12px 16px; background: transparent; border: 0.5px solid transparent; border-radius: var(--radius); cursor: pointer; font-size: 14px; color: var(--text-secondary); text-align: left; transition: all 0.2s; font-family: inherit; }
    .nav-btn:hover { background: var(--surface-2); color: var(--text-primary); }
    .nav-btn.active { background: var(--fill-accent); color: white; border-color: var(--fill-accent); }

    .content { flex: 1; overflow-y: auto; padding: 24px; max-width: 1400px; }
    h2 { margin: 0 0 4px 0; font-size: 24px; font-weight: 500; }
    h3 { margin: 0 0 12px 0; font-size: 18px; font-weight: 500; }
    .subtitle { margin: 0 0 20px 0; color: var(--text-secondary); font-size: 14px; }
    .section { margin-top: 32px; }

    .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 12px; margin-bottom: 32px; }
    .big-number { font-size: 32px; font-weight: 500; color: var(--fill-accent); margin: 12px 0; }
    .big-number.error { color: var(--fill-danger); }
    .big-number.ok { color: var(--fill-success); }
    .desc { font-size: 12px; color: var(--text-secondary); margin: 8px 0 0 0; }

    .filters-bar { display: flex; gap: 12px; align-items: flex-end; margin-bottom: 20px; padding: 12px; background: var(--surface-1); border-radius: var(--radius); }
    .filter-group { display: flex; flex-direction: column; gap: 6px; }
    .filter-group label { font-size: 12px; font-weight: 500; color: var(--text-secondary); }
    .filter-input { padding: 8px 12px; border: 0.5px solid var(--border); border-radius: var(--radius); font-size: 13px; font-family: inherit; background: var(--surface-2); color: var(--text-primary); }

    .runs-table, .productos-table { width: 100%; border-collapse: collapse; font-size: 13px; }
    .runs-table th, .productos-table th { background: var(--surface-1); padding: 12px; text-align: left; font-weight: 500; border-bottom: 0.5px solid var(--border); }
    .runs-table td, .productos-table td { padding: 12px; border-bottom: 0.5px solid var(--border); }
    .runs-table tr:hover, .productos-table tr:hover { background: var(--surface-1); }
    .code { font-family: monospace; font-size: 12px; }
    .price { text-align: right; }
    .stock { text-align: center; }

    .pagination { margin-top: 20px; display: flex; justify-content: space-between; align-items: center; padding: 12px; background: var(--surface-1); border-radius: var(--radius); }
    .pagination p { margin: 0; font-size: 13px; }
    .pagination-controls { display: flex; gap: 8px; }

    .detalle-header { display: flex; gap: 16px; align-items: center; margin-bottom: 20px; }
    .detalle-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 20px; }
    .detail-row { padding: 8px 0; font-size: 13px; border-bottom: 0.5px solid var(--border); }
    .detail-row strong { font-weight: 500; }
    .diff-view { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }
    .diff-col { font-size: 13px; }
    .diff-col h4 { margin: 0 0 8px 0; color: var(--text-secondary); font-size: 12px; }
    .diff-col div { margin: 4px 0; }

    .timeline { display: flex; flex-direction: column; gap: 12px; }
    .timeline-item { padding: 12px; border-left: 3px solid var(--fill-accent); background: var(--surface-1); border-radius: var(--radius); }
    .timeline-date { font-size: 12px; color: var(--text-secondary); margin-bottom: 4px; font-weight: 500; }
    .timeline-content { font-size: 13px; }
    .timeline-note { font-size: 11px; color: var(--text-secondary); margin-top: 4px; }

    .empty-state { padding: 32px; text-align: center; color: var(--text-secondary); }
    .loading { padding: 32px; text-align: center; color: var(--text-secondary); }

    .categories-container { display: flex; flex-direction: column; gap: 12px; }
    .category-section { border: 0.5px solid var(--border); border-radius: var(--radius); overflow: hidden; }
    .category-header { width: 100%; padding: 16px; background: var(--surface-1); border: none; cursor: pointer; display: flex; align-items: center; gap: 12px; font-size: 14px; font-weight: 500; color: var(--text-primary); transition: all 0.2s; }
    .category-header:hover { background: var(--surface-2); }
    .category-icon { display: inline-block; width: 20px; text-align: center; }
    .category-name { flex: 1; text-align: left; }
    .category-count { font-size: 12px; color: var(--text-secondary); }
    .category-content { padding: 12px; background: var(--surface-0); }
    .category-pagination { margin-top: 12px; padding-top: 12px; border-top: 0.5px solid var(--border); font-size: 12px; color: var(--text-secondary); text-align: center; }
  `],
  providers: [ProductoService]
})
export class AppComponent implements OnInit {
  page: 'dashboard' | 'productos' | 'detalle' = 'dashboard';

  // Dashboard
  dashboard: any = null;
  syncRuns: any[] = [];

  // Productos
  categoriesWithProducts: any[] = [];
  expandedCategories: { [key: string]: boolean } = {};

  // Detalle
  productDetail: any = null;
  productDiff: any = null;
  productHistory: any[] = [];

  constructor(private service: ProductoService) {}

  ngOnInit() {
    this.loadDashboard();
    this.loadCategories();
    setInterval(() => this.loadDashboard(), 30000);
  }

  loadDashboard() {
    this.service.getDashboard().subscribe({
      next: (d) => { this.dashboard = d; },
      error: (e) => console.error(e)
    });
    this.loadSyncRuns();
  }

  loadSyncRuns() {
    this.service.getSyncRuns().subscribe({
      next: (r) => { this.syncRuns = r; },
      error: (e) => console.error(e)
    });
  }

  loadCategories() {
    this.service.getProductsByCategories().subscribe({
      next: (categories) => {
        this.categoriesWithProducts = categories;
        // Expandir la primera categoría por defecto
        if (categories.length > 0) {
          this.expandedCategories[categories[0].category] = true;
        }
      },
      error: (e) => console.error(e)
    });
  }

  toggleCategory(categoryName: string) {
    this.expandedCategories[categoryName] = !this.expandedCategories[categoryName];
  }

  viewProductDetail(code: string) {
    this.page = 'detalle';
    this.service.getProductByCode(code).subscribe({
      next: (p) => { this.productDetail = p; },
      error: (e) => console.error(e)
    });
    this.service.getProductDiff(code).subscribe({
      next: (d) => { this.productDiff = d; },
      error: (e) => console.error(e)
    });
    this.service.getProductHistorial(code).subscribe({
      next: (h) => { this.productHistory = h; },
      error: (e) => console.error(e)
    });
  }

  getStatusLabel(status: number): string {
    const labels = { 0: 'Pendiente', 1: 'Enviado', 2: 'Error' };
    return labels[status as keyof typeof labels] || 'Desconocido';
  }

  getStatusVariant(status: number): 'pending' | 'sent' | 'error' | 'info' {
    const variants: Record<number, 'pending' | 'sent' | 'error' | 'info'> = { 0: 'pending', 1: 'sent', 2: 'error' };
    return variants[status] || 'info';
  }
}
