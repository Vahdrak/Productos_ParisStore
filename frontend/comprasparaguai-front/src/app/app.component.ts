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
            <select [(ngModel)]="filterTarget" class="form-input" style="width: 120px;">
              <option value="">Todos</option>
              <option value="compras">Compras</option>
              <option value="compara">Compara</option>
            </select>
            <button (click)="searchProductos()" class="btn btn-primary">Buscar</button>
          </div>

          <div class="table-container mt-6" *ngIf="productList && productList.items.length > 0">
            <table class="data-table">
              <thead>
                <tr>
                  <th>Código</th>
                  <th>Nombre (ES)</th>
                  <th>Compras</th>
                  <th>Compara</th>
                  <th>Precio</th>
                  <th>Stock</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let p of productList.items">
                  <td class="text-bold">{{ p.code }}</td>
                  <td>{{ p.nameEs }}</td>
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
                  <td>{{ p.price | currency }}</td>
                  <td>{{ p.stock }}</td>
                </tr>
              </tbody>
            </table>
          </div>
          <div *ngIf="!productList || productList.items.length === 0" class="text-center py-8 text-dim">
            No hay productos
          </div>
        </div>

        <!-- 3. SYNC PAGE (Oracle → BD) -->
        <div *ngIf="page === 'sync'" class="fade-in">
          <div class="section-card">
            <h3 class="section-title">🔄 Sincronización Oracle → PostgreSQL</h3>
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
      gap: 8px;
      margin-bottom: 16px;
    }

    .form-input {
      flex: 1;
      background: #131313;
      border: 1px solid rgba(255, 255, 255, 0.08);
      color: #eaeaea;
      padding: 8px 12px;
      border-radius: 6px;
      font-size: 14px;
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
    }

    .data-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 13px;
    }

    .data-table thead tr {
      border-bottom: 1px solid rgba(255, 255, 255, 0.12);
    }

    .data-table th {
      text-align: left;
      padding: 10px;
      font-weight: 600;
      color: #8f8f8f;
    }

    .data-table td {
      padding: 10px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.05);
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
  `]
})
export class AppComponent implements OnInit {
  page: string = 'dashboard';
  errorMessage: string = '';

  dashboard: Dashboard | null = null;
  syncRuns: any[] = [];
  sendRunsCompras: SendRun[] = [];
  sendRunsCompara: SendRun[] = [];
  verifyRuns: VerifyRun[] = [];
  errorProductsCompras: any[] = [];
  errorProductsCompara: any[] = [];
  productList: any = null;

  filterCode: string = '';
  filterTarget: string = '';

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
  }

  loadDashboard() {
    this.productoService.getDashboard().subscribe(
      d => this.dashboard = d,
      e => this.errorMessage = 'Error cargando dashboard'
    );
  }

  loadSyncRuns() {
    this.productoService.getSyncRuns().subscribe(
      runs => this.syncRuns = runs,
      e => this.errorMessage = 'Error cargando sync runs'
    );
  }

  loadComprasData() {
    this.productoService.getSendRuns('compras').subscribe(
      runs => this.sendRunsCompras = runs,
      e => this.errorMessage = 'Error cargando send runs'
    );
    this.productoService.getErrorProducts('compras').subscribe(
      errors => this.errorProductsCompras = errors
    );
    this.productoService.getVerifyRuns().subscribe(
      runs => this.verifyRuns = runs
    );
  }

  loadComparaData() {
    this.productoService.getSendRuns('compara').subscribe(
      runs => this.sendRunsCompara = runs,
      e => this.errorMessage = 'Error cargando send runs'
    );
    this.productoService.getErrorProducts('compara').subscribe(
      errors => this.errorProductsCompara = errors
    );
  }

  searchProductos() {
    this.productoService.getProductos({ code: this.filterCode, target: this.filterTarget }).subscribe(
      list => this.productList = list,
      e => this.errorMessage = 'Error cargando productos'
    );
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

  expandVerifyRun(id: number) {
    this.productoService.getVerifyResults(id).subscribe(
      results => console.log('Verify results:', results),
      e => this.errorMessage = 'Error cargando resultados de verificación'
    );
  }
}
