import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClientModule } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ProductoService, Producto, Dashboard, TargetStatus, SendRun, VerifyRun, VerifyResult } from './shared/services/producto.service';
import { interval } from 'rxjs';
import { switchMap } from 'rxjs/operators';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, HttpClientModule, FormsModule],
  template: `
    <div class="app-container">
      <!-- HEADER -->
      <header class="app-header">
        <div class="header-left">
          <span class="logo-icon">📦</span>
          <h1>Compras Paraguai</h1>
        </div>
      </header>

      <!-- NAVIGATION TABS -->
      <nav class="app-nav">
        <button [class.active]="page === 'dashboard'" (click)="setPage('dashboard')" class="tab-btn">Dashboard</button>
        <button [class.active]="page === 'productos'" (click)="setPage('productos')" class="tab-btn">Productos</button>
        <button [class.active]="page === 'pendientes'" (click)="setPage('pendientes')" class="tab-btn">Pendientes</button>
        <button [class.active]="page === 'sync'" (click)="setPage('sync')" class="tab-btn">Sync</button>
        <button [class.active]="page === 'compras'" (click)="setPage('compras')" class="tab-btn">Compras</button>
        <button [class.active]="page === 'compara'" (click)="setPage('compara')" class="tab-btn">Compara</button>
        <button [class.active]="page === 'api'" (click)="setPage('api')" class="tab-btn">Probar API</button>
      </nav>

      <!-- ERROR MESSAGE -->
      <div *ngIf="errorMessage" class="alert alert-error">
        <span>⚠️ {{ errorMessage }}</span>
        <button (click)="errorMessage = ''" class="close-btn">&times;</button>
      </div>

      <!-- CONTENT AREA -->
      <main class="app-content">

        <!-- 1. DASHBOARD PAGE -->
        <div *ngIf="page === 'dashboard'" class="fade-in">
          <div class="section-card">
            <h3 class="section-title">🔄 Última Sincronización Oracle</h3>
            <div class="dashboard-grid">
              <div class="stat-card">
                <span class="stat-title">Fecha</span>
                <span class="stat-val">{{ dashboard?.lastSyncRun?.startedAt | date:'yyyy-MM-dd HH:mm:ss' }}</span>
              </div>
              <div class="stat-card">
                <span class="stat-title">🆕 Nuevos</span>
                <span class="stat-val">{{ dashboard?.lastSyncRun?.newCount ?? 0 }}</span>
              </div>
              <div class="stat-card">
                <span class="stat-title">🔄 Actualizados</span>
                <span class="stat-val">{{ dashboard?.lastSyncRun?.updatedCount ?? 0 }}</span>
              </div>
              <div class="stat-card">
                <span class="stat-title">📝 Notas</span>
                <span class="stat-val val-small">{{ dashboard?.lastSyncRun?.notes || '—' }}</span>
              </div>
            </div>
          </div>

          <!-- Targets Status -->
          <div *ngFor="let target of dashboard?.targets" class="section-card mt-6">
            <h3 class="section-title">{{ target.target }}</h3>
            <div class="dashboard-grid">
              <div class="stat-card">
                <span class="stat-title">⏳ Pendientes</span>
                <span class="stat-val">{{ target.pending }}</span>
              </div>
              <div class="stat-card">
                <span class="stat-title">✅ Enviados</span>
                <span class="stat-val" style="color: #4ade80;">{{ target.sent }}</span>
              </div>
              <div class="stat-card">
                <span class="stat-title">❌ Errores</span>
                <span class="stat-val" style="color: #ef4444;">{{ target.error }}</span>
              </div>
              <div class="stat-card">
                <span class="stat-title">📊 Total</span>
                <span class="stat-val">{{ target.total }}</span>
              </div>
              <div class="stat-card">
                <span class="stat-title">⏱️ Último envío</span>
                <span class="stat-val val-small">{{ target.lastSentAt | date:'yyyy-MM-dd HH:mm:ss' }}</span>
              </div>
            </div>
          </div>
        </div>

        <!-- 2. PRODUCTOS PAGE -->
        <div *ngIf="page === 'productos'" class="fade-in">
          <div class="search-bar">
            <input type="text" placeholder="Buscar código..." [(ngModel)]="filterCode" class="form-input" />
            <select [(ngModel)]="filterTarget" (ngModelChange)="searchProductos()" class="form-input" style="width: 120px;">
              <option value="">Destino: Todos</option>
              <option value="compras">Compras</option>
              <option value="compara">Compara</option>
            </select>
            <select [(ngModel)]="filterProductosRegimen" (ngModelChange)="searchProductos()" class="form-input" style="width: 140px;">
              <option value="">Régimen: Todos</option>
              <option value="true">🎫 Con régimen turismo</option>
              <option value="false">Normal (sin régimen)</option>
            </select>
            <select [(ngModel)]="filterProductosState" (ngModelChange)="searchProductos()" class="form-input" style="width: 140px;">
              <option value="">Estado: Todos</option>
              <option value="0">⏳ Pendiente</option>
              <option value="1">✅ Enviado</option>
              <option value="2">❌ Error</option>
            </select>
            <button (click)="searchProductos()" class="btn btn-primary">Buscar</button>
          </div>

          <div class="search-bar">
            <label style="color: #8f8f8f; font-size: 12px;">📅 Rango de fechas:</label>
            <input type="date" [(ngModel)]="filterProductosFechaDesde" (ngModelChange)="productosPage = 1; searchProductos()" class="form-input" style="width: 120px;" />
            <span style="color: #8f8f8f;">hasta</span>
            <input type="date" [(ngModel)]="filterProductosFechaHasta" (ngModelChange)="productosPage = 1; searchProductos()" class="form-input" style="width: 120px;" />
            <button (click)="setQuickDateRange('hoy', 'productos')" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12); font-size: 12px;">Hoy</button>
            <button (click)="setQuickDateRange('hora', 'productos')" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12); font-size: 12px;">Última hora</button>
            <button (click)="setQuickDateRange('semana', 'productos')" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12); font-size: 12px;">7 días</button>
            <button (click)="setQuickDateRange('mes', 'productos')" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12); font-size: 12px;">30 días</button>
            <button (click)="filterProductosFechaDesde = ''; filterProductosFechaHasta = ''; productosPage = 1; searchProductos();" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12);">Limpiar</button>
          </div>

          <div class="search-bar" *ngIf="productList && productList.items.length > 0">
            <select [(ngModel)]="productosPageSize" (ngModelChange)="productosPage = 1; searchProductos()" class="form-input" style="width: 140px;">
              <option value="10">10 por página</option>
              <option value="25">25 por página</option>
              <option value="50">50 por página</option>
              <option value="100">100 por página</option>
            </select>
          </div>

          <div class="table-container mt-6" *ngIf="productosPagedProducts.length > 0">
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
                <tr *ngFor="let p of productosPagedProducts">
                  <td class="text-bold">{{ p.code }}</td>
                  <td>{{ p.nameEs }}</td>
                  <td><span class="badge" [class.badge-success]="p.regimenTurismo" [class.badge-secondary]="!p.regimenTurismo">{{ p.regimenTurismo ? '🎫 Sí' : 'No' }}</span></td>
                  <td>{{ p.price | currency:'':'symbol-narrow' }}</td>
                  <td>{{ (p.precioExenta || 0) | currency:'':'symbol-narrow' }}</td>
                  <td class="text-center">{{ p.stock }}</td>
                  <td>
                    <span class="badge" [ngClass]="getStatusClass(p.syncStatus)">
                      {{ getStatusLabel(p.syncStatus) }}
                    </span>
                  </td>
                  <td>
                    <span class="badge" [ngClass]="getStatusClass(p.syncComparaStatus)">
                      {{ getStatusLabel(p.syncComparaStatus) }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <div *ngIf="productosTotalPages > 1" class="pagination">
            <button [disabled]="productosPage === 1" (click)="firstProductosPage()">«</button>
            <button [disabled]="productosPage === 1" (click)="prevProductosPage()">‹</button>
            <span class="page-info">Pág {{ productosPage }}/{{ productosTotalPages }}</span>
            <button [disabled]="productosPage === productosTotalPages" (click)="nextProductosPage()">›</button>
            <button [disabled]="productosPage === productosTotalPages" (click)="lastProductosPage()">»</button>
          </div>

          <div *ngIf="!productList || productList.items.length === 0" class="text-center py-8 text-dim">
            No hay productos
          </div>
        </div>

        <!-- 3. PENDIENTES PAGE -->
        <div *ngIf="page === 'pendientes'" class="fade-in">
          <div class="section-card">
            <div class="filter-tabs">
              <button [class.active]="pendientesTarget === 'compras'" (click)="switchPendientesTarget('compras')">Compras Paraguai</button>
              <button [class.active]="pendientesTarget === 'compara'" (click)="switchPendientesTarget('compara')">Compara Paraguai</button>
            </div>
          </div>

          <div class="section-card">
            <h3 class="section-title">Filtros</h3>
            <div class="filters-grid">
              <input type="text" placeholder="Código..." [(ngModel)]="pendientesFilterCode" (ngModelChange)="resetPendientesPage()" class="form-input" />
              <select [(ngModel)]="pendientesFilterRegimen" (ngModelChange)="resetPendientesPage()" class="form-input">
                <option value="">Régimen: Todos</option>
                <option value="true">🎫 Con régimen turismo</option>
                <option value="false">Normal (sin régimen)</option>
              </select>
              <select [(ngModel)]="pendientesFilterStatus" (ngModelChange)="resetPendientesPage()" class="form-input">
                <option value="">Estado: Todos</option>
                <option value="0">⏳ Pendiente</option>
                <option value="1">✅ Enviado</option>
                <option value="2">❌ Error</option>
              </select>
              <select [(ngModel)]="pendientesPageSize" (ngModelChange)="resetPendientesPage()" class="form-input">
                <option value="10">10 por página</option>
                <option value="25">25 por página</option>
                <option value="50">50 por página</option>
                <option value="100">100 por página</option>
              </select>
            </div>
            <div class="filters-grid" style="margin-top: 12px;">
              <label style="color: #8f8f8f; font-size: 12px; grid-column: 1 / -1;">📅 Rango de envíos:</label>
              <input type="date" [(ngModel)]="pendientesFilterFechaDesde" (ngModelChange)="resetPendientesPage()" class="form-input" />
              <input type="date" [(ngModel)]="pendientesFilterFechaHasta" (ngModelChange)="resetPendientesPage()" class="form-input" />
              <button (click)="setQuickDateRange('hoy', 'pendientes')" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12); font-size: 12px;">Hoy</button>
              <button (click)="setQuickDateRange('hora', 'pendientes')" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12); font-size: 12px;">Última hora</button>
              <button (click)="setQuickDateRange('semana', 'pendientes')" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12); font-size: 12px;">7 días</button>
              <button (click)="setQuickDateRange('mes', 'pendientes')" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12); font-size: 12px;">30 días</button>
              <button (click)="pendientesFilterFechaDesde = ''; pendientesFilterFechaHasta = ''; resetPendientesPage();" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12);">Limpiar</button>
            </div>
          </div>

          <div class="section-card">
            <h3 class="section-title">Productos ({{ pendientesFiltered.length }} resultados)</h3>
            <div class="table-container" *ngIf="pendientesPagedProducts.length > 0">
              <table class="data-table">
                <thead><tr><th>Código</th><th>Nombre</th><th>Régimen</th><th>Precio (IVA)</th><th>Precio Exenta</th><th>Stock</th><th>Estado</th></tr></thead>
                <tbody>
                  <tr *ngFor="let p of pendientesPagedProducts">
                    <td class="text-bold">{{ p.code }}</td>
                    <td>{{ p.namePt | slice:0:35 }}</td>
                    <td><span class="badge" [class.badge-success]="p.regimenTurismo" [class.badge-secondary]="!p.regimenTurismo">{{ p.regimenTurismo ? '🎫 Sí' : 'No' }}</span></td>
                    <td>{{ p.price | currency:'':'symbol-narrow' }}</td>
                    <td>{{ (p.precioExenta || 0) | currency:'':'symbol-narrow' }}</td>
                    <td class="text-center">{{ p.stock }}</td>
                    <td><span class="badge" [ngClass]="getStatusBadgeClassPendientes(pendientesTarget, p)">{{ getStatusLabelPendientes(pendientesTarget, p) }}</span></td>
                  </tr>
                </tbody>
              </table>
            </div>
            <div *ngIf="pendientesFiltered.length === 0" class="text-center py-8 text-dim">No hay productos que coincidan</div>

            <div *ngIf="pendientesTotalPages > 1" class="pagination">
              <button [disabled]="pendientesPage === 1" (click)="firstPendientesPage()">«</button>
              <button [disabled]="pendientesPage === 1" (click)="prevPendientesPage()">‹</button>
              <span class="page-info">Pág {{ pendientesPage }}/{{ pendientesTotalPages }}</span>
              <button [disabled]="pendientesPage === pendientesTotalPages" (click)="nextPendientesPage()">›</button>
              <button [disabled]="pendientesPage === pendientesTotalPages" (click)="lastPendientesPage()">»</button>
            </div>

            <div *ngIf="pendientesProducts.length > 0" class="summary-bar">
              <div class="summary-item"><span class="summary-label">🎫 Con régimen:</span><span class="summary-value">{{ countPendientesRegimen() }}</span></div>
              <div class="summary-item"><span class="summary-label">Normal:</span><span class="summary-value">{{ pendientesProducts.length - countPendientesRegimen() }}</span></div>
              <div class="summary-item"><span class="summary-label">Total:</span><span class="summary-value">{{ pendientesProducts.length }}</span></div>
            </div>
          </div>
        </div>

        <!-- 4. SYNC PAGE (Oracle → BD) -->
        <div *ngIf="page === 'sync'" class="fade-in">
          <div class="section-card">
            <h3 class="section-title">🔄 Sincronización Oracle → PostgreSQL</h3>
            <div class="search-bar">
              <label style="color: #8f8f8f; font-size: 12px;">📅 Rango de sincronizaciones:</label>
              <input type="date" [(ngModel)]="syncFilterFechaDesde" (ngModelChange)="applySyncFilters()" class="form-input" style="width: 120px;" />
              <span style="color: #8f8f8f;">hasta</span>
              <input type="date" [(ngModel)]="syncFilterFechaHasta" (ngModelChange)="applySyncFilters()" class="form-input" style="width: 120px;" />
              <button (click)="setQuickDateRange('hoy', 'sync')" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12); font-size: 12px;">Hoy</button>
              <button (click)="setQuickDateRange('hora', 'sync')" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12); font-size: 12px;">Última hora</button>
              <button (click)="setQuickDateRange('semana', 'sync')" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12); font-size: 12px;">7 días</button>
              <button (click)="setQuickDateRange('mes', 'sync')" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12); font-size: 12px;">30 días</button>
              <button (click)="syncFilterFechaDesde = ''; syncFilterFechaHasta = ''; loadSyncRuns();" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12);">Limpiar</button>
            </div>
            <div class="table-container">
              <table class="data-table">
                <thead>
                  <tr>
                    <th>Inicio</th>
                    <th>Fin</th>
                    <th>Duración</th>
                    <th>Filas</th>
                    <th>Nuevos</th>
                    <th>Actualizados</th>
                    <th>Estado</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let run of syncRuns">
                    <td>{{ run.startedAt | date:'yyyy-MM-dd HH:mm:ss' }}</td>
                    <td>{{ run.finishedAt | date:'yyyy-MM-dd HH:mm:ss' }}</td>
                    <td>{{ getDuration(run) }}</td>
                    <td>{{ run.rowsInView ?? 0 }}</td>
                    <td>{{ run.newCount }}</td>
                    <td>{{ run.updatedCount }}</td>
                    <td>
                      <span class="badge" [class.badge-success]="!run.notes?.startsWith('Error')" [class.badge-error]="run.notes?.startsWith('Error')">
                        {{ run.notes?.startsWith('Error') ? 'Con Error' : 'OK' }}
                      </span>
                    </td>
                  </tr>
                  <tr *ngIf="syncRuns.length === 0">
                    <td colspan="7" class="text-center py-8 text-dim">No hay corridas de sync</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <!-- 4. COMPRAS PAGE -->
        <div *ngIf="page === 'compras'" class="fade-in">
          <div class="section-card">
            <h3 class="section-title">📤 Envíos a Compras Paraguai</h3>
            <div class="search-bar">
              <label style="color: #8f8f8f; font-size: 12px;">📅 Rango de envíos:</label>
              <input type="date" [(ngModel)]="comprasFilterFechaDesde" (ngModelChange)="applyComprasFilters()" class="form-input" style="width: 120px;" />
              <span style="color: #8f8f8f;">hasta</span>
              <input type="date" [(ngModel)]="comprasFilterFechaHasta" (ngModelChange)="applyComprasFilters()" class="form-input" style="width: 120px;" />
              <button (click)="setQuickDateRange('hoy', 'compras')" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12); font-size: 12px;">Hoy</button>
              <button (click)="setQuickDateRange('hora', 'compras')" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12); font-size: 12px;">Última hora</button>
              <button (click)="setQuickDateRange('semana', 'compras')" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12); font-size: 12px;">7 días</button>
              <button (click)="setQuickDateRange('mes', 'compras')" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12); font-size: 12px;">30 días</button>
              <button (click)="comprasFilterFechaDesde = ''; comprasFilterFechaHasta = ''; loadComprasData();" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12);">Limpiar</button>
            </div>
            <div class="table-container">
              <table class="data-table">
                <thead>
                  <tr>
                    <th>Inicio</th>
                    <th>Fin</th>
                    <th>Modo</th>
                    <th>Total</th>
                    <th>✅ OK</th>
                    <th>❌ Fallidos</th>
                    <th>Error</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let run of sendRunsCompras">
                    <td>{{ run.startedAt | date:'yyyy-MM-dd HH:mm:ss' }}</td>
                    <td>{{ run.finishedAt | date:'yyyy-MM-dd HH:mm:ss' }}</td>
                    <td>{{ run.mode }}</td>
                    <td>{{ run.total }}</td>
                    <td style="color: #4ade80;">{{ run.sent }}</td>
                    <td style="color: #ef4444;">{{ run.failed }}</td>
                    <td class="text-small">{{ run.error || '—' }}</td>
                  </tr>
                  <tr *ngIf="sendRunsCompras.length === 0">
                    <td colspan="7" class="text-center py-8 text-dim">No hay envíos registrados</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <!-- Errores -->
          <div class="section-card mt-6">
            <h3 class="section-title">❌ Productos con Error</h3>
            <div class="table-container">
              <table class="data-table" *ngIf="errorProductsCompras.length > 0">
                <thead>
                  <tr>
                    <th>Código</th>
                    <th>Nombre</th>
                    <th>Error</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let e of errorProductsCompras">
                    <td>{{ e.code }}</td>
                    <td>{{ e.namePt }}</td>
                    <td class="text-small">{{ e.lastError }}</td>
                  </tr>
                </tbody>
              </table>
              <div *ngIf="errorProductsCompras.length === 0" class="text-center py-8 text-dim">
                Sin errores ✅
              </div>
            </div>
          </div>

          <!-- Verificaciones -->
          <div class="section-card mt-6">
            <h3 class="section-title">✔️ Verificaciones</h3>
            <div class="table-container">
              <table class="data-table" *ngIf="verifyRuns.length > 0">
                <thead>
                  <tr>
                    <th>Inicio</th>
                    <th>Muestra</th>
                    <th>Encontrados</th>
                    <th>Precio OK</th>
                    <th>Stock OK</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let v of verifyRuns" (click)="expandVerifyRun(v.id)" style="cursor: pointer;">
                    <td>{{ v.startedAt | date:'yyyy-MM-dd HH:mm:ss' }}</td>
                    <td>{{ v.sampleSize }}</td>
                    <td>{{ v.found }}/{{ v.sampleSize }}</td>
                    <td>{{ v.priceMatch }}</td>
                    <td>{{ v.stockMatch }}</td>
                  </tr>
                </tbody>
              </table>
              <div *ngIf="verifyRuns.length === 0" class="text-center py-8 text-dim">
                Sin verificaciones
              </div>
            </div>
          </div>
        </div>

        <!-- 5. COMPARA PAGE -->
        <div *ngIf="page === 'compara'" class="fade-in">
          <div class="section-card">
            <h3 class="section-title">📤 Envíos a Compara Paraguai</h3>
            <div class="search-bar">
              <label style="color: #8f8f8f; font-size: 12px;">📅 Rango de envíos:</label>
              <input type="date" [(ngModel)]="comparaFilterFechaDesde" (ngModelChange)="applyComparaFilters()" class="form-input" style="width: 120px;" />
              <span style="color: #8f8f8f;">hasta</span>
              <input type="date" [(ngModel)]="comparaFilterFechaHasta" (ngModelChange)="applyComparaFilters()" class="form-input" style="width: 120px;" />
              <button (click)="setQuickDateRange('hoy', 'compara')" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12); font-size: 12px;">Hoy</button>
              <button (click)="setQuickDateRange('hora', 'compara')" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12); font-size: 12px;">Última hora</button>
              <button (click)="setQuickDateRange('semana', 'compara')" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12); font-size: 12px;">7 días</button>
              <button (click)="setQuickDateRange('mes', 'compara')" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12); font-size: 12px;">30 días</button>
              <button (click)="comparaFilterFechaDesde = ''; comparaFilterFechaHasta = ''; loadComparaData();" class="btn" style="background: #333; color: #8f8f8f; border: 1px solid rgba(255,255,255,0.12);">Limpiar</button>
            </div>
            <div class="table-container">
              <table class="data-table">
                <thead>
                  <tr>
                    <th>Inicio</th>
                    <th>Fin</th>
                    <th>Modo</th>
                    <th>Total</th>
                    <th>✅ OK</th>
                    <th>❌ Fallidos</th>
                    <th>Error</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let run of sendRunsCompara">
                    <td>{{ run.startedAt | date:'yyyy-MM-dd HH:mm:ss' }}</td>
                    <td>{{ run.finishedAt | date:'yyyy-MM-dd HH:mm:ss' }}</td>
                    <td>{{ run.mode }}</td>
                    <td>{{ run.total }}</td>
                    <td style="color: #4ade80;">{{ run.sent }}</td>
                    <td style="color: #ef4444;">{{ run.failed }}</td>
                    <td class="text-small">{{ run.error || '—' }}</td>
                  </tr>
                  <tr *ngIf="sendRunsCompara.length === 0">
                    <td colspan="7" class="text-center py-8 text-dim">No hay envíos registrados</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <!-- Errores -->
          <div class="section-card mt-6">
            <h3 class="section-title">❌ Productos con Error</h3>
            <div class="table-container">
              <table class="data-table" *ngIf="errorProductsCompara.length > 0">
                <thead>
                  <tr>
                    <th>Código</th>
                    <th>Nombre</th>
                    <th>Error</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let e of errorProductsCompara">
                    <td>{{ e.code }}</td>
                    <td>{{ e.namePt }}</td>
                    <td class="text-small">{{ e.lastErrorCompara }}</td>
                  </tr>
                </tbody>
              </table>
              <div *ngIf="errorProductsCompara.length === 0" class="text-center py-8 text-dim">
                Sin errores ✅
              </div>
            </div>
          </div>
        </div>

        <!-- 6. PROBAR API PAGE -->
        <div *ngIf="page === 'api'" class="fade-in">
          <div class="section-card">
            <h3 class="section-title">🧪 Pruebas de API</h3>
            <p class="text-dim">Endpoints disponibles:</p>
            <ul class="text-dim" style="padding-left: 20px; margin: 12px 0;">
              <li><code>GET /api/dashboard</code> - Estado de ambos destinos</li>
              <li><code>GET /api/productos?target=compras</code> - Productos por destino</li>
              <li><code>GET /api/productos/pending?target=compras</code> - Pendientes</li>
              <li><code>GET /api/productos/errors?target=compras</code> - Errores</li>
              <li><code>GET /api/workers/send-runs?target=compras</code> - Historial de envíos</li>
              <li><code>GET /api/workers/verify-runs</code> - Historial de verificaciones</li>
            </ul>
          </div>
        </div>

      </main>
    </div>
  `,
  styles: [`
    :host {
      display: block;
      min-height: 100vh;
      background-color: #0c0c0c;
      color: #eaeaea;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
    }

    .app-container {
      max-width: 1300px;
      margin: 0 auto;
      padding: 16px 24px;
    }

    .app-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 12px 0 20px 0;
    }

    .header-left {
      display: flex;
      align-items: center;
      gap: 12px;
    }

    .logo-icon {
      font-size: 24px;
    }

    .app-header h1 {
      font-size: 22px;
      font-weight: 600;
      color: #ffffff;
      margin: 0;
    }

    .app-nav {
      display: flex;
      gap: 8px;
      margin-bottom: 24px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      padding-bottom: 8px;
    }

    .tab-btn {
      background: transparent;
      border: 1px solid transparent;
      color: #8f8f8f;
      padding: 8px 16px;
      font-size: 14px;
      font-weight: 500;
      border-radius: 6px;
      cursor: pointer;
      transition: all 0.2s ease;
    }

    .tab-btn:hover {
      color: #ffffff;
      background: rgba(255, 255, 255, 0.03);
    }

    .tab-btn.active {
      color: #ffffff;
      background: #181818;
      border-color: rgba(255, 255, 255, 0.12);
    }

    .app-content {
      animation: fadeIn 0.3s ease-in-out;
    }

    .fade-in {
      animation: fadeIn 0.3s ease-in-out;
    }

    @keyframes fadeIn {
      from { opacity: 0; transform: translateY(10px); }
      to { opacity: 1; transform: translateY(0); }
    }

    .section-card {
      background: #131313;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 8px;
      padding: 20px;
      margin-bottom: 20px;
    }

    .section-title {
      font-size: 16px;
      font-weight: 600;
      color: #ffffff;
      margin: 0 0 16px 0;
    }

    .dashboard-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 12px;
      margin-bottom: 16px;
    }

    .stat-card {
      background: rgba(255, 255, 255, 0.04);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 6px;
      padding: 12px;
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .stat-title {
      font-size: 12px;
      color: #8f8f8f;
      font-weight: 500;
    }

    .stat-val {
      font-size: 20px;
      font-weight: 600;
      color: #ffffff;
    }

    .stat-val.val-small {
      font-size: 14px;
    }

    .search-bar {
      display: flex;
      gap: 12px;
      margin-bottom: 20px;
      flex-wrap: wrap;
      align-items: center;
    }

    .form-input {
      flex: 1;
      min-width: 120px;
      background: #131313;
      border: 1px solid rgba(255, 255, 255, 0.12);
      color: #eaeaea;
      padding: 10px 14px;
      border-radius: 6px;
      font-size: 14px;
      transition: border-color 0.2s ease;
    }

    .form-input:focus {
      outline: none;
      border-color: rgba(255, 255, 255, 0.2);
      box-shadow: 0 0 0 2px rgba(255, 255, 255, 0.05);
    }

    .btn {
      padding: 8px 16px;
      border: none;
      border-radius: 6px;
      font-size: 14px;
      font-weight: 500;
      cursor: pointer;
      transition: all 0.2s ease;
    }

    .btn-primary {
      background: #2563eb;
      color: #ffffff;
    }

    .btn-primary:hover:not(:disabled) {
      background: #1d4ed8;
    }

    .table-container {
      overflow-x: auto;
      margin: 16px 0;
    }

    .data-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 13px;
      table-layout: fixed;
    }

    .data-table thead tr {
      border-bottom: 2px solid rgba(255, 255, 255, 0.12);
      background: rgba(255, 255, 255, 0.02);
    }

    .data-table th {
      text-align: left;
      padding: 10px 8px;
      font-weight: 600;
      color: #b0b0b0;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .data-table td {
      padding: 10px 8px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.05);
      color: #e0e0e0;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .data-table tbody tr:hover {
      background: rgba(255, 255, 255, 0.03);
    }

    .badge {
      display: inline-block;
      padding: 3px 8px;
      border-radius: 4px;
      font-size: 12px;
      font-weight: 500;
      background: rgba(100, 100, 100, 0.3);
      color: #c0c0c0;
    }

    .badge-success {
      background: rgba(74, 222, 128, 0.2);
      color: #4ade80;
    }

    .badge-error {
      background: rgba(239, 68, 68, 0.2);
      color: #ef4444;
    }

    .text-dim {
      color: #8f8f8f;
    }

    .text-small {
      font-size: 12px;
    }

    .text-bold {
      font-weight: 600;
    }

    .text-center {
      text-align: center;
    }

    .mt-6 {
      margin-top: 24px;
    }

    .alert {
      padding: 12px 16px;
      border-radius: 6px;
      margin-bottom: 16px;
      display: flex;
      justify-content: space-between;
      align-items: center;
    }

    .alert-error {
      background: rgba(239, 68, 68, 0.1);
      border: 1px solid rgba(239, 68, 68, 0.3);
      color: #fca5a5;
    }

    .close-btn {
      background: none;
      border: none;
      color: #fca5a5;
      cursor: pointer;
      font-size: 18px;
    }

    code {
      background: rgba(255, 255, 255, 0.05);
      padding: 2px 4px;
      border-radius: 3px;
      font-family: monospace;
      font-size: 12px;
    }

    ul {
      list-style: none;
    }

    li {
      margin: 4px 0;
    }

    .filter-tabs {
      display: flex;
      gap: 8px;
      margin-bottom: 16px;
    }

    .filter-tabs button {
      padding: 8px 16px;
      border: 1px solid rgba(255, 255, 255, 0.12);
      background: transparent;
      color: #8f8f8f;
      border-radius: 6px;
      cursor: pointer;
      font-size: 13px;
      transition: all 0.2s;
    }

    .filter-tabs button:hover {
      border-color: rgba(255, 255, 255, 0.2);
      color: #ffffff;
    }

    .filter-tabs button.active {
      background: #2563eb;
      color: #ffffff;
      border-color: #2563eb;
    }

    .filter-bar {
      display: flex;
      gap: 12px;
      align-items: flex-end;
    }

    .filter-bar .form-input {
      flex: 1;
    }

    .badge-secondary {
      background: rgba(100, 100, 100, 0.2);
      color: #8f8f8f;
    }

    .summary-bar {
      display: flex;
      gap: 24px;
      padding: 12px 0;
      border-top: 1px solid rgba(255, 255, 255, 0.08);
      margin-top: 16px;
    }

    .summary-item {
      display: flex;
      gap: 8px;
      align-items: center;
    }

    .summary-label {
      color: #8f8f8f;
      font-size: 12px;
    }

    .summary-value {
      font-weight: 600;
      color: #ffffff;
      font-size: 14px;
    }

    .py-8 {
      padding: 32px 0;
    }

    .filters-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
      gap: 12px;
    }

    .pagination {
      display: flex;
      justify-content: center;
      align-items: center;
      gap: 8px;
      margin-top: 20px;
      padding: 12px 0;
    }

    .pagination button {
      background: #181818;
      border: 1px solid rgba(255, 255, 255, 0.12);
      color: #8f8f8f;
      padding: 6px 12px;
      border-radius: 4px;
      cursor: pointer;
      font-size: 13px;
      transition: all 0.2s;
    }

    .pagination button:hover:not(:disabled) {
      background: #2563eb;
      border-color: #2563eb;
      color: #ffffff;
    }

    .pagination button:disabled {
      opacity: 0.3;
      cursor: not-allowed;
    }

    .page-info {
      font-size: 12px;
      color: #8f8f8f;
      min-width: 80px;
      text-align: center;
    }

    .text-center {
      text-align: center;
    }
  `]
})
export class AppComponent implements OnInit {
  page: string = 'dashboard';
  errorMessage: string = '';

  dashboard: Dashboard | null = null;
  syncRuns: any[] = [];
  syncRunsOriginal: any[] = [];
  sendRunsCompras: SendRun[] = [];
  sendRunsComprasOriginal: SendRun[] = [];
  sendRunsCompara: SendRun[] = [];
  sendRunsComparaOriginal: SendRun[] = [];
  verifyRuns: VerifyRun[] = [];
  errorProductsCompras: any[] = [];
  errorProductsCompara: any[] = [];
  productList: any = null;

  filterCode: string = '';
  filterTarget: string = '';
  filterProductosRegimen: string = '';
  filterProductosState: string = '';
  filterProductosFechaDesde: string = '';
  filterProductosFechaHasta: string = '';
  productosPageSize: number = 25;
  productosPage: number = 1;
  productosTotalPages: number = 1;
  productosPagedProducts: any[] = [];
  productosFiltered: any[] = [];

  // Pendientes page
  pendientesTarget: string = 'compras';
  pendientesProducts: any[] = [];
  pendientesFiltered: any[] = [];
  pendientesFilterCode: string = '';
  pendientesFilterRegimen: string = '';
  pendientesFilterStatus: string = '';
  pendientesFilterFechaDesde: string = '';
  pendientesFilterFechaHasta: string = '';
  pendientesPageSize: number = 50;
  pendientesPage: number = 1;
  pendientesTotalPages: number = 1;
  pendientesPagedProducts: any[] = [];

  // Sync, Compras, Compara filters
  syncFilterFechaDesde: string = '';
  syncFilterFechaHasta: string = '';
  comprasFilterFechaDesde: string = '';
  comprasFilterFechaHasta: string = '';
  comparaFilterFechaDesde: string = '';
  comparaFilterFechaHasta: string = '';

  // Sync pagination
  syncPageSize: number = 25;
  syncPage: number = 1;
  syncTotalPages: number = 1;
  syncRunsPaged: any[] = [];
  syncRunsFiltered: any[] = [];

  // Compras pagination
  comprasPageSize: number = 25;
  comprasPage: number = 1;
  comprasTotalPages: number = 1;
  sendRunsComprasPaged: SendRun[] = [];
  sendRunsComprasFiltered: SendRun[] = [];

  // Compara pagination
  comparaPageSize: number = 25;
  comparaPage: number = 1;
  comparaTotalPages: number = 1;
  sendRunsComparaPaged: SendRun[] = [];
  sendRunsComparaFiltered: SendRun[] = [];

  constructor(private productoService: ProductoService) {}

  ngOnInit() {
    this.startAutoRefresh();
    this.loadDashboard();
  }

  startAutoRefresh() {
    interval(30000).pipe(
      switchMap(() => {
        this.loadDashboard();
        if (this.page === 'sync') this.loadSyncRuns();
        if (this.page === 'compras') this.loadComprasData();
        if (this.page === 'compara') this.loadComparaData();
        return [];
      })
    ).subscribe();
  }

  setPage(p: string) {
    this.page = p;
    this.loadDashboard();
    if (p === 'sync') this.loadSyncRuns();
    if (p === 'compras') this.loadComprasData();
    if (p === 'compara') this.loadComparaData();
    if (p === 'productos') this.searchProductos();
    if (p === 'pendientes') this.loadPendientes();
  }

  loadDashboard() {
    this.productoService.getDashboard().subscribe(
      d => this.dashboard = d,
      e => this.errorMessage = 'Error cargando dashboard'
    );
  }

  loadSyncRuns() {
    this.productoService.getSyncRuns().subscribe(
      runs => {
        this.syncRunsOriginal = runs;
        this.applySyncFilters();
      },
      e => this.errorMessage = 'Error cargando sync runs'
    );
  }

  applySyncFilters() {
    this.syncRunsFiltered = this.syncRunsOriginal.filter(r =>
      this.isDateInRange(r.startedAt, this.syncFilterFechaDesde, this.syncFilterFechaHasta)
    );
    if (this.syncPage < 1) this.syncPage = 1;
    this.syncTotalPages = Math.ceil(this.syncRunsFiltered.length / this.syncPageSize) || 1;
    if (this.syncPage > this.syncTotalPages) this.syncPage = this.syncTotalPages;
    const start = (this.syncPage - 1) * this.syncPageSize;
    const end = start + this.syncPageSize;
    this.syncRunsPaged = this.syncRunsFiltered.slice(start, end);
    this.syncRuns = this.syncRunsPaged;
  }

  loadComprasData() {
    this.productoService.getSendRuns('compras').subscribe(
      runs => {
        this.sendRunsComprasOriginal = runs;
        this.applyComprasFilters();
      },
      e => this.errorMessage = 'Error cargando send runs'
    );
    this.productoService.getErrorProducts('compras').subscribe(
      errors => this.errorProductsCompras = errors
    );
    this.productoService.getVerifyRuns().subscribe(
      runs => this.verifyRuns = runs
    );
  }

  applyComprasFilters() {
    this.sendRunsComprasFiltered = this.sendRunsComprasOriginal.filter(r =>
      this.isDateInRange(r.startedAt, this.comprasFilterFechaDesde, this.comprasFilterFechaHasta)
    );
    if (this.comprasPage < 1) this.comprasPage = 1;
    this.comprasTotalPages = Math.ceil(this.sendRunsComprasFiltered.length / this.comprasPageSize) || 1;
    if (this.comprasPage > this.comprasTotalPages) this.comprasPage = this.comprasTotalPages;
    const start = (this.comprasPage - 1) * this.comprasPageSize;
    const end = start + this.comprasPageSize;
    this.sendRunsComprasPaged = this.sendRunsComprasFiltered.slice(start, end);
    this.sendRunsCompras = this.sendRunsComprasPaged;
  }

  loadComparaData() {
    this.productoService.getSendRuns('compara').subscribe(
      runs => {
        this.sendRunsComparaOriginal = runs;
        this.applyComparaFilters();
      },
      e => this.errorMessage = 'Error cargando send runs'
    );
    this.productoService.getErrorProducts('compara').subscribe(
      errors => this.errorProductsCompara = errors
    );
  }

  applyComparaFilters() {
    this.sendRunsComparaFiltered = this.sendRunsComparaOriginal.filter(r =>
      this.isDateInRange(r.startedAt, this.comparaFilterFechaDesde, this.comparaFilterFechaHasta)
    );
    if (this.comparaPage < 1) this.comparaPage = 1;
    this.comparaTotalPages = Math.ceil(this.sendRunsComparaFiltered.length / this.comparaPageSize) || 1;
    if (this.comparaPage > this.comparaTotalPages) this.comparaPage = this.comparaTotalPages;
    const start = (this.comparaPage - 1) * this.comparaPageSize;
    const end = start + this.comparaPageSize;
    this.sendRunsComparaPaged = this.sendRunsComparaFiltered.slice(start, end);
    this.sendRunsCompara = this.sendRunsComparaPaged;
  }

  searchProductos() {
    this.productoService.getProductos({
      code: this.filterCode,
      target: this.filterTarget,
      syncStatus: this.filterProductosState ? parseInt(this.filterProductosState) : undefined
    }).subscribe(
      list => {
        if (!list || !list.items) return;
        // Aplicar filtro de régimen turismo
        this.productosFiltered = list.items;
        if (this.filterProductosRegimen === 'true') {
          this.productosFiltered = this.productosFiltered.filter(p => p.regimenTurismo === true);
        } else if (this.filterProductosRegimen === 'false') {
          this.productosFiltered = this.productosFiltered.filter(p => !p.regimenTurismo);
        }
        // Aplicar filtro de estado
        if (this.filterProductosState) {
          const targetToUse = this.filterTarget || 'compras';
          const statusField = targetToUse === 'compras' ? 'syncStatus' : 'syncComparaStatus';
          this.productosFiltered = this.productosFiltered.filter(p =>
            p[statusField]?.toString() === this.filterProductosState
          );
        }
        // Aplicar filtro de fecha (usando lastSentAt de Compras como referencia)
        if (this.filterProductosFechaDesde || this.filterProductosFechaHasta) {
          this.productosFiltered = this.productosFiltered.filter(p =>
            this.isDateInRange(p.lastSentAt, this.filterProductosFechaDesde, this.filterProductosFechaHasta)
          );
        }
        // Aplicar paginación
        this.productosPage = 1;
        this.productosTotalPages = list.totalPages || Math.ceil(this.productosFiltered.length / this.productosPageSize) || 1;
        const start = 0;
        const end = this.productosPageSize;
        this.productosPagedProducts = this.productosFiltered.slice(start, end);
        this.productList = list;
      },
      e => this.errorMessage = 'Error cargando productos'
    );
  }

  resetProductosPage() {
    this.productosPage = 1;
    this.searchProductos();
  }

  firstProductosPage() {
    this.productosPage = 1;
    this.applyProductosPage();
  }

  prevProductosPage() {
    if (this.productosPage > 1) this.productosPage--;
    this.applyProductosPage();
  }

  nextProductosPage() {
    if (this.productosPage < this.productosTotalPages) this.productosPage++;
    this.applyProductosPage();
  }

  lastProductosPage() {
    this.productosPage = this.productosTotalPages;
    this.applyProductosPage();
  }

  applyProductosPage() {
    const start = (this.productosPage - 1) * this.productosPageSize;
    const end = start + this.productosPageSize;
    this.productosPagedProducts = this.productosFiltered.slice(start, end);
  }

  loadPendientes() {
    this.productoService.getPendingProducts(this.pendientesTarget).subscribe(
      products => {
        this.pendientesProducts = products;
        this.applyPendientesFilters();
      },
      e => this.errorMessage = 'Error cargando productos pendientes'
    );
  }

  applyPendientesFilters() {
    this.pendientesFiltered = this.pendientesProducts.filter(p => {
      const matchCode = !this.pendientesFilterCode ||
        p.code.toLowerCase().includes(this.pendientesFilterCode.toLowerCase());
      const matchRegimen = !this.pendientesFilterRegimen ||
        (this.pendientesFilterRegimen === 'true' && p.regimenTurismo) ||
        (this.pendientesFilterRegimen === 'false' && !p.regimenTurismo);
      const statusField = this.pendientesTarget === 'compras' ? 'syncStatus' : 'syncComparaStatus';
      const matchStatus = !this.pendientesFilterStatus || p[statusField]?.toString() === this.pendientesFilterStatus;
      const dateField = this.pendientesTarget === 'compras' ? 'lastSentAt' : 'lastSentComparaAt';
      const matchDate = this.isDateInRange(p[dateField], this.pendientesFilterFechaDesde, this.pendientesFilterFechaHasta);
      return matchCode && matchRegimen && matchStatus && matchDate;
    });

    // Aplicar paginación
    if (this.pendientesPage < 1) this.pendientesPage = 1;
    this.pendientesTotalPages = Math.ceil(this.pendientesFiltered.length / this.pendientesPageSize) || 1;
    if (this.pendientesPage > this.pendientesTotalPages) this.pendientesPage = this.pendientesTotalPages;

    const start = (this.pendientesPage - 1) * this.pendientesPageSize;
    const end = start + this.pendientesPageSize;
    this.pendientesPagedProducts = this.pendientesFiltered.slice(start, end);
  }

  resetPendientesPage() {
    this.pendientesPage = 1;
    this.applyPendientesFilters();
  }

  firstPendientesPage() {
    this.pendientesPage = 1;
    this.applyPendientesFilters();
  }

  prevPendientesPage() {
    if (this.pendientesPage > 1) this.pendientesPage--;
    this.applyPendientesFilters();
  }

  nextPendientesPage() {
    if (this.pendientesPage < this.pendientesTotalPages) this.pendientesPage++;
    this.applyPendientesFilters();
  }

  lastPendientesPage() {
    this.pendientesPage = this.pendientesTotalPages;
    this.applyPendientesFilters();
  }

  switchPendientesTarget(target: string) {
    this.pendientesTarget = target;
    this.pendientesPage = 1;
    this.loadPendientes();
  }

  countPendientesRegimen(): number {
    return this.pendientesProducts.filter(p => p.regimenTurismo).length;
  }

  getStatusLabelPendientes(target: string, p: any): string {
    const statusField = target === 'compras' ? 'syncStatus' : 'syncComparaStatus';
    const status = p[statusField];
    return status === 0 ? '⏳ Pendiente' : status === 1 ? '✅ Enviado' : '❌ Error';
  }

  getStatusBadgeClassPendientes(target: string, p: any): any {
    const statusField = target === 'compras' ? 'syncStatus' : 'syncComparaStatus';
    const status = p[statusField];
    return {
      'badge-success': status === 1,
      'badge-error': status === 2,
      'badge': status === 0
    };
  }

  getDuration(run: any): string {
    if (!run.startedAt || !run.finishedAt) return '—';
    const start = new Date(run.startedAt).getTime();
    const end = new Date(run.finishedAt).getTime();
    const secs = Math.round((end - start) / 1000);
    return secs < 60 ? `${secs}s` : `${Math.round(secs / 60)}m`;
  }

  getStatusLabel(status?: number): string {
    return status === 0 ? '⏳ Pendiente' : status === 1 ? '✅ Enviado' : '❌ Error';
  }

  getStatusClass(status?: number): any {
    return {
      'badge-success': status === 1,
      'badge-error': status === 2
    };
  }

  isDateInRange(date: string | Date | null | undefined, desde: string, hasta: string): boolean {
    if (!date) return true;
    if (!desde && !hasta) return true;

    const d = new Date(date);
    if (desde) {
      const desdeDate = new Date(desde);
      desdeDate.setHours(0, 0, 0, 0);
      if (d < desdeDate) return false;
    }
    if (hasta) {
      const hastaDate = new Date(hasta);
      hastaDate.setHours(23, 59, 59, 999);
      if (d > hastaDate) return false;
    }
    return true;
  }

  setQuickDateRange(range: string, filterType: 'productos' | 'pendientes' | 'sync' | 'compras' | 'compara') {
    const now = new Date();
    const today = now.toISOString().split('T')[0];
    let desde = '', hasta = '';

    switch (range) {
      case 'hoy':
        desde = today;
        hasta = today;
        break;
      case 'hora':
        // Última hora: desde hace 60 minutos
        const oneHourAgo = new Date(now.getTime() - 60 * 60 * 1000);
        desde = oneHourAgo.toISOString().split('T')[0];
        hasta = today;
        break;
      case 'semana':
        // Últimos 7 días
        const sevenDaysAgo = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
        desde = sevenDaysAgo.toISOString().split('T')[0];
        hasta = today;
        break;
      case 'mes':
        // Últimos 30 días
        const thirtyDaysAgo = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
        desde = thirtyDaysAgo.toISOString().split('T')[0];
        hasta = today;
        break;
      case 'todo':
        desde = '';
        hasta = '';
        break;
    }

    if (filterType === 'productos') {
      this.filterProductosFechaDesde = desde;
      this.filterProductosFechaHasta = hasta;
      this.resetProductosPage();
    } else if (filterType === 'pendientes') {
      this.pendientesFilterFechaDesde = desde;
      this.pendientesFilterFechaHasta = hasta;
      this.resetPendientesPage();
    } else if (filterType === 'sync') {
      this.syncFilterFechaDesde = desde;
      this.syncFilterFechaHasta = hasta;
      this.applySyncFilters();
    } else if (filterType === 'compras') {
      this.comprasFilterFechaDesde = desde;
      this.comprasFilterFechaHasta = hasta;
      this.applyComprasFilters();
    } else if (filterType === 'compara') {
      this.comparaFilterFechaDesde = desde;
      this.comparaFilterFechaHasta = hasta;
      this.applyComparaFilters();
    }
  }

  expandVerifyRun(id: number) {
    this.productoService.getVerifyResults(id).subscribe(
      results => console.log('Verify results:', results),
      e => this.errorMessage = 'Error cargando resultados de verificación'
    );
  }
}
