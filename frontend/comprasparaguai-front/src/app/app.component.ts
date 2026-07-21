import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClientModule } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ProductoService, Producto, ProductDiff, ProductHistorial, Dashboard, SyncRun } from './shared/services/producto.service';

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
          <span class="status-badge-live">Sincronizando</span>
        </div>
      </header>

      <!-- NAVIGATION TABS -->
      <nav class="app-nav">
        <button [class.active]="page === 'dashboard'" (click)="setPage('dashboard')" class="tab-btn">
          Dashboard
        </button>
        <button [class.active]="page === 'productos'" (click)="setPage('productos')" class="tab-btn">
          Productos
        </button>
        <button [class.active]="page === 'historial'" (click)="setPage('historial')" class="tab-btn">
          Historial
        </button>
        <button [class.active]="page === 'comprasparaguai'" (click)="setPage('comprasparaguai')" class="tab-btn font-accent">
          API ComprasParaguai
        </button>
      </nav>

      <!-- CONTENT AREA -->
      <main class="app-content">

        <!-- ERROR MESSAGE -->
        <div *ngIf="errorMessage" class="alert alert-error">
          <span>⚠️ {{ errorMessage }}</span>
          <button (click)="errorMessage = ''" class="close-btn">&times;</button>
        </div>

        <!-- 1. DASHBOARD PAGE -->
        <div *ngIf="page === 'dashboard'" class="fade-in">
          <!-- Summary Cards -->
          <div class="dashboard-grid">
            <div class="stat-card">
              <span class="stat-title">Pendientes</span>
              <span class="stat-val">{{ dashboard?.pendingCount ?? 0 }}</span>
            </div>
            <div class="stat-card">
              <span class="stat-title">Enviados hoy</span>
              <span class="stat-val">{{ dashboard?.sentCount ?? 0 }}</span>
            </div>
            <div class="stat-card">
              <span class="stat-title">Errores</span>
              <span class="stat-val text-red">{{ dashboard?.errorCount ?? 0 }}</span>
            </div>
            <div class="stat-card">
              <span class="stat-title">Ú. corrida</span>
              <span class="stat-val val-small">{{ lastRunTimeAgo }}</span>
            </div>
          </div>

          <!-- Runs Table -->
          <div class="section-card mt-6">
            <h3 class="section-title">Historial de Ejecuciones</h3>
            <div class="table-container">
              <table class="data-table">
                <thead>
                  <tr>
                    <th>Inicio</th>
                    <th>Duración</th>
                    <th>Procesados</th>
                    <th>Nuevos</th>
                    <th>Actualizados</th>
                    <th>Enviados</th>
                    <th>Errores</th>
                    <th>Estado</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let run of syncRuns">
                    <td>{{ run.startedAt | date:'yyyy-MM-dd HH:mm:ss' }}</td>
                    <td>{{ getDuration(run) }}</td>
                    <td>{{ getProcesados(run) }}</td>
                    <td>{{ run.newCount }}</td>
                    <td>{{ run.updatedCount }}</td>
                    <td>{{ run.sentCount }}</td>
                    <td>{{ run.errorCount }}</td>
                    <td>
                      <span class="badge" [class.badge-success]="!run.errorCount" [class.badge-error]="run.errorCount > 0">
                        {{ run.errorCount > 0 ? 'Con Errores' : 'Completado' }}
                      </span>
                    </td>
                  </tr>
                  <tr *ngIf="syncRuns.length === 0">
                    <td colspan="8" class="text-center py-8 text-dim">No hay corridas de sincronización registradas.</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <!-- 2. PRODUCTOS PAGE -->
        <div *ngIf="page === 'productos'" class="fade-in">
          <!-- Search & Filter Bar -->
          <div class="search-bar">
            <div class="search-input-wrapper">
              <span class="search-icon">🔍</span>
              <input
                type="text"
                placeholder="Buscar código o nombre..."
                [(ngModel)]="searchText"
                (ngModelChange)="onSearchChange()"
                class="form-input search-input" />
            </div>
          </div>

          <!-- Products Table -->
          <div class="section-card">
            <div class="table-container">
              <table class="data-table">
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
                  <tr *ngFor="let prod of products">
                    <td class="text-bold">{{ prod.code }}</td>
                    <td>{{ prod.namePt || prod.nameEs }}</td>
                    <td class="text-right">{{ (prod.price ?? 0) | currency:'PYG':'symbol':'1.0-0' }}₲</td>
                    <td class="text-right">{{ prod.stock | number }}</td>
                    <td>
                      <span class="badge"
                        [class.badge-pending]="prod.syncStatus === 0"
                        [class.badge-success]="prod.syncStatus === 1"
                        [class.badge-error]="prod.syncStatus === 2">
                        {{ getStatusLabel(prod.syncStatus) }}
                      </span>
                    </td>
                    <td>
                      <div class="action-cell">
                        <button (click)="openProductDetails(prod.code)" class="link-btn" style="color: #8b5cf6;">👁️ Ver</button>
                        <span class="divider">|</span>
                        <button (click)="viewProductDiff(prod.code)" class="link-btn">Ver diff</button>
                        <span class="divider">|</span>
                        <button (click)="editProduct(prod.code)" class="link-btn text-accent">Editar</button>
                        <span class="divider">|</span>
                        <button (click)="useForCPTest(prod)" class="link-btn text-accent" style="color: #60a5fa;">Probar API</button>
                      </div>
                    </td>
                  </tr>
                  <tr *ngIf="products.length === 0">
                    <td colspan="6" class="text-center py-8 text-dim">No se encontraron productos correspondientes a la búsqueda.</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <!-- Pagination Footer -->
            <div *ngIf="totalCount > 0" class="pagination-footer">
              <span class="text-dim">Mostrando {{ (pageIndex-1)*pageSize + 1 }} - {{ Math.min(pageIndex*pageSize, totalCount) }} de {{ totalCount }}</span>
              <div class="pagination-controls">
                <button [disabled]="pageIndex === 1" (click)="loadPage(pageIndex - 1)" class="btn-icon">◀</button>
                <span class="page-num">Pág. {{ pageIndex }} / {{ totalPages }}</span>
                <button [disabled]="pageIndex === totalPages" (click)="loadPage(pageIndex + 1)" class="btn-icon">▶</button>
              </div>
            </div>
          </div>

          <!-- COMPARACIÓN DE CAMBIOS (DIFF VIEW) -->
          <div *ngIf="showDiffModal && productDiff" class="section-card diff-modal fade-in mt-6">
            <div class="modal-header">
              <h3>Comparar cambios: {{ productDiff.code }}</h3>
              <button (click)="showDiffModal = false" class="close-modal-btn">&times;</button>
            </div>
            
            <div *ngIf="productDiff.hasChanges; else noDiffChanges" class="diff-grid">
              <!-- Precio -->
              <div class="diff-row-wrapper" *ngIf="productDiff.previous?.price !== productDiff.current.price">
                <span class="diff-field-label">Precio</span>
                <div class="diff-boxes">
                  <div class="diff-box box-before">
                    <span class="box-label">Precio (antes)</span>
                    <span class="box-value">{{ (productDiff.previous?.price ?? 0) | currency:'PYG':'symbol':'1.0-0' }}₲</span>
                  </div>
                  <div class="diff-box box-after">
                    <span class="box-label">Precio (después)</span>
                    <span class="box-value">{{ (productDiff.current.price ?? 0) | currency:'PYG':'symbol':'1.0-0' }}₲</span>
                  </div>
                </div>
              </div>

              <!-- Stock -->
              <div class="diff-row-wrapper" *ngIf="productDiff.previous?.stock !== productDiff.current.stock">
                <span class="diff-field-label">Stock</span>
                <div class="diff-boxes">
                  <div class="diff-box box-before">
                    <span class="box-label">Stock (antes)</span>
                    <span class="box-value">{{ (productDiff.previous?.stock ?? 0) | number }}</span>
                  </div>
                  <div class="diff-box box-after">
                    <span class="box-label">Stock (después)</span>
                    <span class="box-value">{{ productDiff.current.stock | number }}</span>
                  </div>
                </div>
              </div>

              <!-- Descripción -->
              <div class="diff-row-wrapper" *ngIf="productDiff.previous?.namePt !== productDiff.current.namePt">
                <span class="diff-field-label">Nombre (PT)</span>
                <div class="diff-boxes">
                  <div class="diff-box box-before">
                    <span class="box-label">Nombre (antes)</span>
                    <span class="box-value text-small">{{ productDiff.previous?.namePt || 'N/A' }}</span>
                  </div>
                  <div class="diff-box box-after">
                    <span class="box-label">Nombre (después)</span>
                    <span class="box-value text-small">{{ productDiff.current.namePt }}</span>
                  </div>
                </div>
              </div>

              <!-- Marca -->
              <div class="diff-row-wrapper" *ngIf="productDiff.previous?.brand !== productDiff.current.brand">
                <span class="diff-field-label">Marca</span>
                <div class="diff-boxes">
                  <div class="diff-box box-before">
                    <span class="box-label">Marca (antes)</span>
                    <span class="box-value">{{ productDiff.previous?.brand || 'N/A' }}</span>
                  </div>
                  <div class="diff-box box-after">
                    <span class="box-label">Marca (después)</span>
                    <span class="box-value">{{ productDiff.current.brand || 'N/A' }}</span>
                  </div>
                </div>
              </div>
            </div>
            <ng-template #noDiffChanges>
              <div class="empty-state-inner py-8 text-center text-dim">
                El producto no registra cambios pendientes respecto al último historial.
              </div>
            </ng-template>
          </div>

          <!-- EDIT PRODUCT VIEW -->
          <div *ngIf="showEditModal" class="section-card edit-modal fade-in mt-6">
            <div class="modal-header">
              <h3>Editar Producto: {{ editedProduct.code }}</h3>
              <button (click)="showEditModal = false" class="close-modal-btn">&times;</button>
            </div>
            
            <form (submit)="saveProduct()" class="edit-form">
              <div class="form-grid">
                <div class="form-group">
                  <label>Nombre Portugués</label>
                  <input type="text" [(ngModel)]="editedProduct.namePt" name="namePt" class="form-input" required />
                </div>
                <div class="form-group">
                  <label>Precio (PYG)</label>
                  <input type="number" [(ngModel)]="editedProduct.price" name="price" class="form-input" required />
                </div>
                <div class="form-group">
                  <label>Stock</label>
                  <input type="number" [(ngModel)]="editedProduct.stock" name="stock" class="form-input" required />
                </div>
                <div class="form-group">
                  <label>Marca</label>
                  <input type="text" [(ngModel)]="editedProduct.brand" name="brand" class="form-input" />
                </div>
                <div class="form-group span-2">
                  <label>Descripción Portugués</label>
                  <textarea [(ngModel)]="editedProduct.descriptionPt" name="descriptionPt" class="form-input" rows="3"></textarea>
                </div>
                <div class="form-group span-2">
                  <label>URL Imagen</label>
                  <input type="text" [(ngModel)]="editedProduct.urlImage" name="urlImage" class="form-input" />
                </div>
              </div>
              <div class="form-actions mt-4">
                <button type="submit" class="btn btn-primary" [disabled]="loading">
                  {{ loading ? 'Guardando...' : 'Guardar y Sincronizar' }}
                </button>
                <button type="button" (click)="showEditModal = false" class="btn btn-secondary">Cancelar</button>
              </div>
            </form>
          </div>
        </div>

        <!-- MODAL: Product Details & Image Preview -->
        <div *ngIf="showDetailsModal && selectedProductForDetails" class="modal-overlay" (click)="showDetailsModal = false">
          <div class="modal-content" (click)="$event.stopPropagation()">
            <div class="modal-header">
              <h3>📦 Detalles del Producto: {{ selectedProductForDetails.code }}</h3>
              <button (click)="showDetailsModal = false" class="close-modal-btn">&times;</button>
            </div>

            <div class="modal-body">
              <div class="details-grid">
                <!-- Información del Producto -->
                <div class="details-section">
                  <h4>Información</h4>
                  <div class="detail-item">
                    <label>Código:</label>
                    <span>{{ selectedProductForDetails.code }}</span>
                  </div>
                  <div class="detail-item">
                    <label>Nombre (PT):</label>
                    <span>{{ selectedProductForDetails.namePt }}</span>
                  </div>
                  <div class="detail-item">
                    <label>Nombre (ES):</label>
                    <span>{{ selectedProductForDetails.nameEs || 'N/A' }}</span>
                  </div>
                  <div class="detail-item">
                    <label>Marca:</label>
                    <span>{{ selectedProductForDetails.brand || 'N/A' }}</span>
                  </div>
                  <div class="detail-item">
                    <label>Precio:</label>
                    <span>{{ selectedProductForDetails.price | currency:'PYG':'symbol':'1.0-0' }}</span>
                  </div>
                  <div class="detail-item">
                    <label>Precio con IVA:</label>
                    <span>{{ selectedProductForDetails.priceIva | currency:'PYG':'symbol':'1.0-0' }}</span>
                  </div>
                  <div class="detail-item">
                    <label>Stock:</label>
                    <span>{{ selectedProductForDetails.stock }}</span>
                  </div>
                  <div class="detail-item">
                    <label>Descripción (PT):</label>
                    <span class="desc">{{ selectedProductForDetails.descriptionPt || 'N/A' }}</span>
                  </div>
                </div>

                <!-- Vista Previa de Imagen -->
                <div class="image-preview-section">
                  <h4>🖼️ Vista Previa de Imagen</h4>
                  <div class="image-container">
                    <div *ngIf="imageLoading" class="image-loading">
                      Verificando imagen...
                    </div>
                    <div *ngIf="!imageLoading && selectedProductForDetails.urlImage">
                      <img
                        *ngIf="imageExists"
                        [src]="selectedProductForDetails.urlImage"
                        [alt]="selectedProductForDetails.code"
                        class="product-image"
                        (error)="imageExists = false" />
                      <div *ngIf="!imageExists" class="image-not-found">
                        ❌ Imagen no encontrada
                        <p class="text-sm">URL: {{ selectedProductForDetails.urlImage }}</p>
                      </div>
                    </div>
                    <div *ngIf="!selectedProductForDetails.urlImage" class="no-image">
                      Sin imagen
                    </div>
                  </div>
                  <div class="image-status">
                    <span *ngIf="imageExists" class="status-badge status-success">✅ Imagen disponible</span>
                    <span *ngIf="!imageExists && !imageLoading" class="status-badge status-error">❌ Imagen no disponible</span>
                  </div>
                </div>
              </div>
            </div>

            <div class="modal-footer">
              <button (click)="editProduct(selectedProductForDetails.code)" class="btn btn-primary">
                ✏️ Editar
              </button>
              <button (click)="showDetailsModal = false" class="btn btn-secondary">
                Cerrar
              </button>
            </div>
          </div>
        </div>

        <!-- 3. HISTORIAL RUNS PAGE -->
        <div *ngIf="page === 'historial'" class="fade-in">
          <div class="section-card">
            <h3 class="section-title">Corridas de Sincronización (Detallado)</h3>
            <div class="table-container">
              <table class="data-table">
                <thead>
                  <tr>
                    <th>ID</th>
                    <th>Inicio</th>
                    <th>Fin</th>
                    <th>Duración</th>
                    <th>Filas Leídas</th>
                    <th>Nuevos</th>
                    <th>Actualizados</th>
                    <th>Enviados</th>
                    <th>Errores</th>
                    <th>Notas</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let run of syncRuns">
                    <td class="text-dim">#{{ run.id }}</td>
                    <td>{{ run.startedAt | date:'yyyy-MM-dd HH:mm:ss' }}</td>
                    <td>{{ run.finishedAt ? (run.finishedAt | date:'yyyy-MM-dd HH:mm:ss') : 'En curso' }}</td>
                    <td>{{ getDuration(run) }}</td>
                    <td>{{ run.rowsInView ?? '—' }}</td>
                    <td class="text-bold">{{ run.newCount }}</td>
                    <td class="text-bold">{{ run.updatedCount }}</td>
                    <td class="text-bold text-success">{{ run.sentCount }}</td>
                    <td class="text-bold" [class.text-red]="run.errorCount > 0">{{ run.errorCount }}</td>
                    <td class="text-dim text-small">{{ run.notes || '—' }}</td>
                  </tr>
                  <tr *ngIf="syncRuns.length === 0">
                    <td colspan="10" class="text-center py-8 text-dim">No hay corridas registradas.</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <!-- 4. COMPRAS PARAGUAI API PROXY PAGE -->
        <div *ngIf="page === 'comprasparaguai'" class="fade-in">
          <div class="section-card">
            <h3 class="section-title">Pruebas directas de API Compras Paraguai</h3>
            <p class="text-dim text-small mb-4">Esta pestaña te permite interactuar directamente con la API externa de Compras Paraguai a través del proxy del Backend, sin simulaciones.</p>
            
            <div class="form-group mb-6" style="max-width: 500px;">
              <label>Token de Acceso Compras Paraguai</label>
              <input type="password" [(ngModel)]="cpToken" placeholder="Ingresa tu token de API aquí..." class="form-input" />
              <span class="text-dim" style="font-size: 11px; margin-top: 4px; display: block;">El token no se guardará en ningún lugar al recargar la página.</span>
            </div>

            <div class="api-grid mt-6">
              <!-- GET Operation -->
              <div class="api-card">
                <h4>Listar Productos (GET /api/products/list/)</h4>
                <p class="text-dim text-small mb-4">Realiza una consulta GET para obtener la lista de productos cargados y validados. Puedes filtrar por código específico.</p>
                
                <div class="form-group mb-4">
                  <label>Código del Producto (Opcional)</label>
                  <input type="text" [(ngModel)]="cpSearchCode" placeholder="Ej: 8806097656784" class="form-input" />
                </div>

                <button (click)="testCPGet()" class="btn btn-primary" [disabled]="loadingCP || !cpToken">
                  {{ loadingCP ? 'Consultando...' : 'Obtener Catálogo' }}
                </button>
                
                <div *ngIf="cpGetResponse" class="response-box mt-4">
                  <span class="box-label">Respuesta JSON (Compras Paraguai)</span>
                  <pre class="json-code"><code>{{ cpGetResponse | json }}</code></pre>
                </div>
              </div>

              <!-- POST Operation -->
              <div class="api-card">
                <h4>Enviar Lote de Productos (POST /api/products/import/)</h4>
                <p class="text-dim text-small mb-4">Envía un payload de productos directamente para su validación y procesamiento de importación en Compras Paraguai.</p>
                
                <div class="form-group mb-4">
                  <label>Payload JSON (Array de productos)</label>
                  <textarea [(ngModel)]="cpPostPayload" class="form-input text-small" rows="10" style="font-family: monospace;" placeholder="[ { ... } ]"></textarea>
                  <button (click)="loadSamplePayload()" class="link-btn mt-2" style="font-size: 12px; display: inline-block;">Cargar plantilla de prueba</button>
                </div>

                <button (click)="testCPPost()" class="btn btn-primary" [disabled]="loadingCP || !cpToken || !cpPostPayload">
                  {{ loadingCP ? 'Enviando Lote...' : 'Enviar Lote' }}
                </button>
                
                <div *ngIf="cpPostResponse" class="response-box mt-4">
                  <span class="box-label">Respuesta JSON (Compras Paraguai)</span>
                  <pre class="json-code"><code>{{ cpPostResponse | json }}</code></pre>
                </div>
              </div>
            </div>
          </div>
        </div>

      </main>
    </div>
  `,
  styles: [`
    /* PREMIUM DARK SYSTEM THEME */
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

    /* HEADER */
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
      letter-spacing: -0.5px;
      margin: 0;
    }

    .status-badge-live {
      background-color: #0b1f13;
      color: #4ade80;
      border: 1px solid #144627;
      padding: 4px 10px;
      border-radius: 9999px;
      font-size: 11px;
      font-weight: 500;
      letter-spacing: 0.3px;
    }

    /* NAVIGATION TABS */
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
      box-shadow: 0 4px 12px rgba(0,0,0,0.5);
    }

    .tab-btn.font-accent {
      color: #3b82f6;
    }
    .tab-btn.font-accent.active {
      color: #ffffff;
      background: #101e36;
      border-color: rgba(59, 130, 246, 0.2);
    }

    /* CONTENT LAYOUT */
    .app-content {
      padding-bottom: 60px;
    }

    .mt-6 { margin-top: 24px; }
    .mt-4 { margin-top: 16px; }

    /* DASHBOARD CARDS */
    .dashboard-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 16px;
    }

    .stat-card {
      background: #121212;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 12px;
      padding: 20px;
      display: flex;
      flex-direction: column;
      gap: 8px;
      box-shadow: 0 4px 20px rgba(0, 0, 0, 0.2);
    }

    .stat-title {
      font-size: 13px;
      color: #8f8f8f;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }

    .stat-val {
      font-size: 32px;
      font-weight: 600;
      color: #ffffff;
    }

    .stat-val.val-small {
      font-size: 22px;
      color: #eaeaea;
      padding-top: 8px;
    }

    .text-red { color: #fca5a5; }

    /* SECTION CARD */
    .section-card {
      background: #121212;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 12px;
      padding: 24px;
      box-shadow: 0 4px 30px rgba(0, 0, 0, 0.3);
    }

    .section-title {
      font-size: 16px;
      font-weight: 500;
      color: #ffffff;
      margin: 0 0 16px 0;
    }

    /* SEARCH BAR */
    .search-bar {
      margin-bottom: 16px;
    }

    .search-input-wrapper {
      position: relative;
      max-width: 500px;
    }

    .search-icon {
      position: absolute;
      left: 14px;
      top: 50%;
      transform: translateY(-50%);
      color: #8f8f8f;
      font-size: 14px;
    }

    .search-input {
      width: 100%;
      padding: 12px 12px 12px 42px;
      background: #181818;
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 8px;
      color: #ffffff;
      font-size: 14px;
      transition: all 0.2s;
    }

    .search-input:focus {
      outline: none;
      border-color: rgba(255, 255, 255, 0.3);
      background: #1c1c1c;
      box-shadow: 0 0 0 2px rgba(255, 255, 255, 0.05);
    }

    /* DATA TABLE */
    .table-container {
      overflow-x: auto;
    }

    .data-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 14px;
      text-align: left;
    }

    .data-table th {
      color: #8f8f8f;
      font-weight: 500;
      padding: 12px 16px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      background: rgba(255, 255, 255, 0.01);
    }

    .data-table td {
      padding: 14px 16px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.05);
      color: #eaeaea;
    }

    .data-table tr:hover td {
      background: rgba(255, 255, 255, 0.02);
    }

    .text-bold { font-weight: 500; color: #ffffff; }
    .text-right { text-align: right; }
    .text-center { text-align: center; }
    .text-dim { color: #8f8f8f; }
    .text-success { color: #4ade80; }
    .text-small { font-size: 12px; }

    /* BADGES */
    .badge {
      display: inline-block;
      padding: 3px 8px;
      border-radius: 4px;
      font-size: 11px;
      font-weight: 500;
    }

    .badge-pending {
      background-color: #2b1f0c;
      color: #fbbf24;
      border: 1px solid #78350f;
    }

    .badge-success {
      background-color: #0c2615;
      color: #34d399;
      border: 1px solid #064e3b;
    }

    .badge-error {
      background-color: #2e1010;
      color: #fca5a5;
      border: 1px solid #7f1d1d;
    }

    /* ACTION BUTTONS */
    .action-cell {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .link-btn {
      background: transparent;
      border: none;
      color: #3b82f6;
      cursor: pointer;
      font-size: 13px;
      padding: 2px 4px;
      text-decoration: underline;
      transition: color 0.2s;
    }

    .link-btn:hover {
      color: #60a5fa;
    }

    .link-btn.text-accent {
      color: #34d399;
    }

    .link-btn.text-accent:hover {
      color: #6ee7b7;
    }

    .divider {
      color: rgba(255, 255, 255, 0.15);
      font-size: 12px;
    }

    /* PAGINATION FOOTER */
    .pagination-footer {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding-top: 16px;
      border-top: 1px solid rgba(255, 255, 255, 0.08);
      margin-top: 8px;
      font-size: 13px;
    }

    .pagination-controls {
      display: flex;
      align-items: center;
      gap: 12px;
    }

    .btn-icon {
      background: #181818;
      border: 1px solid rgba(255, 255, 255, 0.1);
      color: #ffffff;
      padding: 6px 12px;
      border-radius: 6px;
      cursor: pointer;
    }

    .btn-icon:disabled {
      opacity: 0.3;
      cursor: not-allowed;
    }

    .page-num {
      color: #ffffff;
    }

    /* DIFF SECTION / MODAL PANEL */
    .diff-modal, .edit-modal {
      border: 1px solid rgba(255, 255, 255, 0.12);
      background: #161616;
    }

    .modal-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 20px;
      padding-bottom: 12px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
    }

    .modal-header h3 {
      font-size: 16px;
      font-weight: 500;
      color: #ffffff;
      margin: 0;
    }

    .close-modal-btn {
      background: transparent;
      border: none;
      color: #8f8f8f;
      font-size: 24px;
      cursor: pointer;
      line-height: 1;
      padding: 0 4px;
    }

    .close-modal-btn:hover {
      color: #ffffff;
    }

    .diff-grid {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .diff-row-wrapper {
      display: grid;
      grid-template-columns: 100px 1fr;
      align-items: center;
      gap: 12px;
    }

    .diff-field-label {
      font-size: 13px;
      color: #8f8f8f;
      font-weight: 500;
    }

    .diff-boxes {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 12px;
    }

    .diff-box {
      border-radius: 6px;
      padding: 10px 14px;
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .box-before {
      background: #270d0d;
      border: 1px solid #571313;
      color: #fca5a5;
    }

    .box-after {
      background: #092113;
      border: 1px solid #144927;
      color: #4ade80;
    }

    .box-label {
      font-size: 11px;
      opacity: 0.7;
      text-transform: uppercase;
      letter-spacing: 0.3px;
    }

    .box-value {
      font-size: 14px;
      font-weight: 500;
    }

    /* EDIT FORM */
    .edit-form {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .form-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
    }

    .form-group {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }

    .form-group.span-2 {
      grid-column: span 2;
    }

    .form-group label {
      font-size: 12px;
      color: #8f8f8f;
      font-weight: 500;
    }

    .form-input {
      background: #1c1c1c;
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 6px;
      padding: 10px 12px;
      color: #ffffff;
      font-size: 14px;
      font-family: inherit;
    }

    .form-input:focus {
      outline: none;
      border-color: rgba(255, 255, 255, 0.3);
    }

    .form-actions {
      display: flex;
      gap: 12px;
    }

    .btn {
      padding: 10px 20px;
      font-size: 14px;
      font-weight: 500;
      border-radius: 6px;
      cursor: pointer;
      transition: all 0.2s;
    }

    .btn-primary {
      background: #10b981;
      border: 1px solid #047857;
      color: #ffffff;
    }

    .btn-primary:hover {
      background: #059669;
    }

    .btn-primary:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .btn-secondary {
      background: transparent;
      border: 1px solid rgba(255, 255, 255, 0.12);
      color: #eaeaea;
    }

    .btn-secondary:hover {
      background: rgba(255, 255, 255, 0.05);
    }

    /* ALERTS */
    .alert {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 12px 16px;
      border-radius: 8px;
      margin-bottom: 20px;
      font-size: 14px;
    }

    .alert-error {
      background: #2e1010;
      border: 1px solid #7f1d1d;
      color: #fca5a5;
    }

    .close-btn {
      background: transparent;
      border: none;
      color: inherit;
      font-size: 20px;
      cursor: pointer;
      line-height: 1;
    }

    /* COMPRAS PARAGUAI PROXY API SPECIFICS */
    .api-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 20px;
    }

    .api-card {
      background: #181818;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 8px;
      padding: 20px;
      display: flex;
      flex-direction: column;
    }

    .api-card h4 {
      margin: 0 0 10px 0;
      color: #ffffff;
      font-size: 15px;
      font-weight: 500;
    }

    .mb-6 { margin-bottom: 24px; }
    .mb-4 { margin-bottom: 16px; }

    .response-box {
      background: #0c0c0c;
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 6px;
      padding: 12px;
      margin-top: 16px;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }

    .json-code {
      max-height: 280px;
      overflow-y: auto;
      font-family: monospace;
      font-size: 12px;
      color: #34d399;
      white-space: pre-wrap;
      margin: 0;
      background: transparent;
      border: none;
      padding: 0;
    }

    /* ANIMATIONS & UTILS */
    .fade-in {
      animation: fadeIn 0.25s ease-out;
    }

    @keyframes fadeIn {
      from { opacity: 0; transform: translateY(4px); }
      to { opacity: 1; transform: translateY(0); }
    }

    .py-8 { padding-top: 32px; padding-bottom: 32px; }
    .mt-4 { margin-top: 16px; }
    .mt-6 { margin-top: 24px; }
    .text-sm { font-size: 12px; }
    .text-dim { color: var(--text-secondary); }

    /* MODAL OVERLAY */
    .modal-overlay {
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background: rgba(0, 0, 0, 0.5);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 1000;
    }

    .modal-content {
      background: var(--surface-1);
      border-radius: var(--radius);
      box-shadow: 0 20px 25px rgba(0, 0, 0, 0.3);
      max-width: 900px;
      width: 90%;
      max-height: 90vh;
      display: flex;
      flex-direction: column;
      animation: fadeIn 0.2s ease-out;
    }

    .modal-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 20px;
      border-bottom: 0.5px solid var(--border);
    }

    .modal-header h3 {
      margin: 0;
      font-size: 18px;
      font-weight: 500;
    }

    .close-modal-btn {
      background: none;
      border: none;
      font-size: 28px;
      cursor: pointer;
      color: var(--text-secondary);
      padding: 0;
      width: 32px;
      height: 32px;
      display: flex;
      align-items: center;
      justify-content: center;
      border-radius: var(--radius);
      transition: all 0.2s;
    }

    .close-modal-btn:hover {
      background: var(--surface-2);
      color: var(--text-primary);
    }

    .modal-body {
      flex: 1;
      padding: 24px;
      overflow-y: auto;
    }

    .details-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 32px;
    }

    .details-section {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .details-section h4 {
      margin: 0 0 8px 0;
      font-size: 14px;
      font-weight: 600;
      color: var(--fill-accent);
      text-transform: uppercase;
      letter-spacing: 0.3px;
    }

    .detail-item {
      display: grid;
      grid-template-columns: 120px 1fr;
      gap: 8px;
      font-size: 13px;
      padding: 8px;
      background: var(--surface-0);
      border-radius: calc(var(--radius) - 2px);
    }

    .detail-item label {
      font-weight: 500;
      color: var(--text-secondary);
    }

    .detail-item span {
      color: var(--text-primary);
      word-break: break-word;
    }

    .detail-item span.desc {
      grid-column: 2;
      font-size: 12px;
      line-height: 1.4;
    }

    .image-preview-section {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .image-preview-section h4 {
      margin: 0;
      font-size: 14px;
      font-weight: 600;
      color: var(--fill-accent);
      text-transform: uppercase;
      letter-spacing: 0.3px;
    }

    .image-container {
      background: var(--surface-0);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      padding: 16px;
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 300px;
      overflow: hidden;
    }

    .product-image {
      max-width: 100%;
      max-height: 300px;
      border-radius: calc(var(--radius) - 2px);
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
    }

    .image-loading,
    .image-not-found,
    .no-image {
      text-align: center;
      color: var(--text-secondary);
      font-size: 14px;
    }

    .image-not-found p {
      margin: 8px 0 0 0;
      font-size: 12px;
      font-family: monospace;
      color: var(--text-secondary);
    }

    .image-status {
      display: flex;
      gap: 8px;
      justify-content: center;
    }

    .status-badge {
      font-size: 12px;
      padding: 6px 12px;
      border-radius: calc(var(--radius) - 2px);
      font-weight: 500;
    }

    .status-success {
      background: rgba(74, 222, 128, 0.1);
      color: #4ade80;
      border: 0.5px solid #4ade80;
    }

    .status-error {
      background: rgba(239, 68, 68, 0.1);
      color: #ef4444;
      border: 0.5px solid #ef4444;
    }

    .modal-footer {
      display: flex;
      gap: 12px;
      justify-content: flex-end;
      padding: 16px 24px;
      border-top: 0.5px solid var(--border);
      background: var(--surface-0);
    }

    .btn {
      padding: 8px 16px;
      border-radius: var(--radius);
      border: none;
      cursor: pointer;
      font-size: 13px;
      font-weight: 500;
      transition: all 0.2s;
      font-family: inherit;
    }

    .btn-primary {
      background: var(--fill-accent);
      color: white;
    }

    .btn-primary:hover {
      opacity: 0.9;
    }

    .btn-secondary {
      background: var(--surface-2);
      color: var(--text-primary);
    }

    .btn-secondary:hover {
      background: var(--surface-3);
    }

    @media (max-width: 768px) {
      .details-grid {
        grid-template-columns: 1fr;
        gap: 24px;
      }

      .detail-item {
        grid-template-columns: 1fr;
      }

      .detail-item label {
        font-weight: 600;
        margin-bottom: 4px;
      }
    }
  `]
})
export class AppComponent implements OnInit {
  page: 'dashboard' | 'productos' | 'historial' | 'comprasparaguai' = 'dashboard';
  Math = Math;

  // Real-time states
  dashboard: Dashboard | null = null;
  syncRuns: SyncRun[] = [];
  lastRunTimeAgo = 'N/A';

  // Products state (Search / Pagination)
  products: Producto[] = [];
  totalCount = 0;
  pageIndex = 1;
  pageSize = 15;
  searchText = '';
  totalPages = 1;

  // Comparison State (Diff View)
  showDiffModal = false;
  productDiff: ProductDiff | null = null;

  // Editing State
  showEditModal = false;
  editedProduct: Partial<Producto> & { code: string } = { code: '' };

  // Details Modal State (Image Preview)
  showDetailsModal = false;
  selectedProductForDetails: Producto | null = null;
  imageExists = false;
  imageLoading = false;

  // Compras Paraguai Proxy API State
  cpToken = '';
  cpSearchCode = '';
  cpGetResponse: any = null;
  cpPostResponse: any = null;
  cpPostPayload = '';
  loadingCP = false;

  // UI state variables
  loading = false;
  errorMessage = '';

  constructor(private service: ProductoService) {}

  ngOnInit() {
    this.loadDashboard();
    this.loadSyncRuns();
    this.loadProducts();

    // Auto-refresh stats every 30 seconds
    setInterval(() => {
      this.loadDashboard();
      this.loadSyncRuns();
    }, 30000);
  }

  setPage(targetPage: 'dashboard' | 'productos' | 'historial' | 'comprasparaguai') {
    this.page = targetPage;
    this.showDiffModal = false;
    this.showEditModal = false;
    if (targetPage === 'dashboard') {
      this.loadDashboard();
      this.loadSyncRuns();
    } else if (targetPage === 'productos') {
      this.loadProducts();
    } else if (targetPage === 'historial') {
      this.loadSyncRuns();
    }
  }

  loadDashboard() {
    this.service.getDashboard().subscribe({
      next: (data) => {
        this.dashboard = data;
      },
      error: (err) => {
        console.error('Error fetching dashboard', err);
        this.errorMessage = 'No se pudo conectar a la base de datos o al backend API.';
      }
    });
  }

  loadSyncRuns() {
    this.service.getSyncRuns().subscribe({
      next: (runs) => {
        this.syncRuns = runs;
        this.calculateLastRunTime();
      },
      error: (err) => {
        console.error('Error fetching sync runs', err);
      }
    });
  }

  loadProducts() {
    this.loading = true;
    this.service.getProductos({ name: this.searchText, code: this.searchText }, this.pageIndex, this.pageSize).subscribe({
      next: (res) => {
        this.products = res.items;
        this.totalCount = res.totalCount;
        this.pageIndex = res.page;
        this.pageSize = res.pageSize;
        this.totalPages = res.totalPages;
        this.loading = false;
      },
      error: (err) => {
        console.error('Error fetching products', err);
        this.loading = false;
        this.errorMessage = 'Error al cargar listado de productos.';
      }
    });
  }

  onSearchChange() {
    this.pageIndex = 1;
    this.loadProducts();
  }

  loadPage(page: number) {
    if (page < 1 || page > this.totalPages) return;
    this.pageIndex = page;
    this.loadProducts();
  }

  viewProductDiff(code: string) {
    this.showDiffModal = false;
    this.showEditModal = false;
    this.service.getProductDiff(code).subscribe({
      next: (diff) => {
        this.productDiff = diff;
        this.showDiffModal = true;
      },
      error: (err) => {
        console.error('Error loading product diff', err);
        this.errorMessage = `Error al generar diff para el producto ${code}.`;
      }
    });
  }

  editProduct(code: string) {
    this.showDiffModal = false;
    this.showEditModal = false;
    this.service.getProductByCode(code).subscribe({
      next: (prod) => {
        this.editedProduct = {
          code: prod.code,
          namePt: prod.namePt,
          price: prod.price,
          stock: prod.stock,
          brand: prod.brand,
          descriptionPt: prod.descriptionPt,
          urlImage: prod.urlImage
        };
        this.showEditModal = true;
      },
      error: (err) => {
        console.error('Error loading product detail', err);
        this.errorMessage = `Error al cargar detalles del producto ${code} para edición.`;
      }
    });
  }

  async openProductDetails(code: string) {
    try {
      const product = await this.service.getProductByCode(code).toPromise();
      if (product) {
        this.selectedProductForDetails = product;
        this.imageLoading = true;
        this.imageExists = false;
        this.showDetailsModal = true;

        // Verificar si la imagen existe
        if (product.urlImage) {
          this.imageExists = await this.service.verifyImageExists(product.urlImage);
        }
        this.imageLoading = false;
      }
    } catch (error) {
      console.error('Error loading product details:', error);
      this.errorMessage = `Error al cargar detalles del producto ${code}.`;
    }
  }

  saveProduct() {
    if (!this.editedProduct.code) return;
    this.loading = true;
    this.service.updateProduct(this.editedProduct.code, this.editedProduct).subscribe({
      next: (updated) => {
        this.loading = false;
        this.showEditModal = false;
        this.loadProducts();
        this.loadDashboard();
      },
      error: (err) => {
        console.error('Error updating product', err);
        this.loading = false;
        this.errorMessage = `Error al guardar cambios para el producto ${this.editedProduct.code}.`;
      }
    });
  }

  // COMPRAS PARAGUAI DIRECT API PROXY METHODS

  testCPGet() {
    if (!this.cpToken) return;
    this.loadingCP = true;
    this.cpGetResponse = null;
    this.service.getCPProducts(this.cpToken, this.cpSearchCode).subscribe({
      next: (res) => {
        this.cpGetResponse = res;
        this.loadingCP = false;
      },
      error: (err) => {
        this.cpGetResponse = { error: err.message || err };
        this.loadingCP = false;
      }
    });
  }

  testCPPost() {
    if (!this.cpToken || !this.cpPostPayload) return;
    this.loadingCP = true;
    this.cpPostResponse = null;
    let parsedPayload;
    try {
      parsedPayload = JSON.parse(this.cpPostPayload);
    } catch (e: any) {
      this.errorMessage = 'El payload JSON ingresado no es válido.';
      this.loadingCP = false;
      return;
    }
    this.service.importCPProducts(this.cpToken, parsedPayload).subscribe({
      next: (res) => {
        this.cpPostResponse = res;
        this.loadingCP = false;
      },
      error: (err) => {
        this.cpPostResponse = { error: err.message || err };
        this.loadingCP = false;
      }
    });
  }

  loadSamplePayload() {
    const sample = [
      {
        "code": "8806097656784",
        "name": {
          "pt": "Samsung SM-A075F 4GB/64GB Black ★TESTE",
          "es": "Samsung SM-A075F 4GB/64GB Negro ★PRUEBA"
        },
        "price": 97,
        "price_iva": 97,
        "stock": 10,
        "brand": "Samsung",
        "description": {
          "pt": "Celular Samsung SM-A075F 4GB/64GB Black",
          "es": "Celular Samsung SM-A075F 4GB/64GB Negro"
        },
        "url_image": "https://www.comprasparaguai.com.br/static/images/logo.png",
        "link": "https://www.comprasparaguai.com.br/"
      }
    ];
    this.cpPostPayload = JSON.stringify(sample, null, 2);
  }

  useForCPTest(prod: Producto) {
    this.cpSearchCode = prod.code;
    const mappedPayload = [
      {
        code: prod.code,
        name: {
          pt: prod.namePt,
          es: prod.nameEs || prod.namePt
        },
        price: prod.price ?? 0,
        price_iva: prod.priceIva ?? prod.price ?? 0,
        stock: prod.stock,
        brand: prod.brand || null,
        description: {
          pt: prod.descriptionPt || `${prod.namePt} importado`,
          es: prod.descriptionEs || prod.nameEs || `${prod.namePt} importado`
        },
        url_image: prod.urlImage || "https://www.comprasparaguai.com.br/static/images/logo.png",
        link: prod.link || "https://www.comprasparaguai.com.br/"
      }
    ];
    this.cpPostPayload = JSON.stringify(mappedPayload, null, 2);
    this.setPage('comprasparaguai');
  }

  // UTILITY HELPERS
  getStatusLabel(status: number | undefined): string {
    if (status === 0) return 'Pendiente';
    if (status === 1) return 'Enviado';
    if (status === 2) return 'Error';
    return 'Desconocido';
  }

  getProcesados(run: SyncRun): number {
    return (run.newCount ?? 0) + (run.updatedCount ?? 0) + (run.sentCount ?? 0) + (run.errorCount ?? 0);
  }

  getDuration(run: SyncRun): string {
    if (!run.finishedAt) return 'En curso';
    try {
      const start = new Date(run.startedAt).getTime();
      const end = new Date(run.finishedAt).getTime();
      const diffMs = end - start;
      const seconds = Math.floor(diffMs / 1000);
      if (seconds < 60) return `${seconds}s`;
      const minutes = Math.floor(seconds / 60);
      const remainingSeconds = seconds % 60;
      return `${minutes}m ${remainingSeconds}s`;
    } catch {
      return '—';
    }
  }

  calculateLastRunTime() {
    if (this.syncRuns.length === 0) {
      this.lastRunTimeAgo = 'N/A';
      return;
    }
    const lastRun = this.syncRuns[0];
    const finishedStr = lastRun.finishedAt || lastRun.startedAt;
    try {
      const finishedTime = new Date(finishedStr).getTime();
      const now = new Date().getTime();
      const diffMs = now - finishedTime;
      const minutes = Math.floor(diffMs / (1000 * 60));
      if (minutes < 1) {
        this.lastRunTimeAgo = 'Hace instantes';
      } else if (minutes < 60) {
        this.lastRunTimeAgo = `Hace ${minutes}m`;
      } else {
        const hours = Math.floor(minutes / 60);
        this.lastRunTimeAgo = `Hace ${hours}h`;
      }
    } catch {
      this.lastRunTimeAgo = 'N/A';
    }
  }
}
