import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClientModule } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { BtnComponent, InputComponent, CardComponent } from './shared/components';
import { ProductoService } from './shared/services/producto.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, HttpClientModule, FormsModule, BtnComponent, InputComponent, CardComponent],
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
          <button [class.active]="page === 'buscar'" (click)="page = 'buscar'" class="nav-btn">
            🔍 Buscar
          </button>
        </nav>
      </aside>

      <!-- Content -->
      <main class="content">
        <!-- DASHBOARD -->
        <div *ngIf="page === 'dashboard'">
          <h2>Dashboard Operativo</h2>
          <p class="subtitle">Resumen en tiempo real</p>

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
              <p class="desc">{{ dashboard.lastSyncAt | date:'short' }}</p>
            </app-card>
          </div>

          <div *ngIf="!dashboard" class="loading">Cargando...</div>
        </div>

        <!-- BUSCAR -->
        <div *ngIf="page === 'buscar'">
          <h2>Buscar Producto</h2>
          <p class="subtitle">Edita y visualiza cambios</p>

          <app-card title="Búsqueda" [elevated]="true">
            <div class="search-box">
              <app-input label="Código" placeholder="Ej: PROD-001847" [(ngModel)]="searchCode" />
              <app-btn label="Buscar" variant="primary" (onClick)="search()" />
            </div>
            <div *ngIf="loading" class="msg loading">Buscando...</div>
            <div *ngIf="error" class="msg error">⚠️ {{ error }}</div>
          </app-card>

          <!-- Resultado -->
          <div *ngIf="product">
            <app-card [title]="product.code" [elevated]="true">
              <div class="product-info">
                <div><strong>Nombre:</strong> {{ product.namePt }}</div>
                <div><strong>Precio:</strong> {{ product.price | currency }}</div>
                <div><strong>Stock:</strong> {{ product.stock }}</div>
                <div *ngIf="product.brand"><strong>Marca:</strong> {{ product.brand }}</div>
              </div>

              <div class="actions">
                <app-btn label="Editar" (onClick)="toggleEdit()" />
                <app-btn label="Ver Diff" variant="primary" (onClick)="loadDiff()" />
                <app-btn label="Historial" (onClick)="loadHistory()" />
              </div>
            </app-card>
          </div>

          <!-- Edición -->
          <div *ngIf="editing && product">
            <app-card title="Editar" [elevated]="true">
              <div class="form">
                <app-input label="Nombre" [(ngModel)]="edited.namePt" />
                <app-input label="Precio" type="number" [(ngModel)]="edited.price" />
                <app-input label="Stock" type="number" [(ngModel)]="edited.stock" />
              </div>

              <div class="actions">
                <app-btn label="Guardar" variant="primary" (onClick)="save()" />
                <app-btn label="Cancelar" (onClick)="toggleEdit()" />
              </div>
            </app-card>
          </div>

          <!-- Diff -->
          <div *ngIf="diff && !showHistory">
            <app-card [title]="'Cambios: ' + diff.code" [elevated]="true">
              <div *ngIf="diff.hasChanges; else noDiff" class="diff">
                <div class="diff-col">
                  <h4>Antes</h4>
                  <div *ngIf="diff.previous">
                    <div>Precio: {{ diff.previous.price | currency }}</div>
                    <div>Stock: {{ diff.previous.stock }}</div>
                  </div>
                </div>
                <div class="diff-col">
                  <h4>Ahora</h4>
                  <div>Precio: {{ diff.current.price | currency }}</div>
                  <div>Stock: {{ diff.current.stock }}</div>
                </div>
              </div>
              <ng-template #noDiff>
                <p class="info">Sin cambios</p>
              </ng-template>
            </app-card>
          </div>

          <!-- Historial -->
          <div *ngIf="showHistory">
            <app-card title="Historial" [elevated]="true">
              <div *ngIf="history.length > 0; else noHistory" class="history">
                <div *ngFor="let h of history" class="hist-row">
                  <div class="hist-date">{{ h.changedAt | date:'short' }}</div>
                  <div>Precio: {{ h.price | currency }} | Stock: {{ h.stock }}</div>
                </div>
              </div>
              <ng-template #noHistory>
                <p class="info">Sin historial</p>
              </ng-template>

              <div class="actions">
                <app-btn label="Volver" (onClick)="showHistory = false" />
              </div>
            </app-card>
          </div>
        </div>
      </main>
    </div>
  `,
  styles: [`
    .app-layout {
      display: flex;
      height: 100vh;
      background: var(--surface-0);
    }

    .sidebar {
      width: 240px;
      background: var(--surface-1);
      border-right: 0.5px solid var(--border);
      display: flex;
      flex-direction: column;
      overflow-y: auto;
    }

    .sidebar-header {
      padding: 20px;
      border-bottom: 0.5px solid var(--border);
      background: var(--surface-2);
    }

    .sidebar-header h1 {
      margin: 0;
      font-size: 18px;
      font-weight: 500;
    }

    .sidebar-nav {
      display: flex;
      flex-direction: column;
      padding: 12px;
      gap: 4px;
    }

    .nav-btn {
      padding: 12px 16px;
      background: transparent;
      border: 0.5px solid transparent;
      border-radius: var(--radius);
      cursor: pointer;
      font-size: 14px;
      color: var(--text-secondary);
      text-align: left;
      transition: all 0.2s;
      font-family: inherit;
    }

    .nav-btn:hover {
      background: var(--surface-2);
      color: var(--text-primary);
    }

    .nav-btn.active {
      background: var(--fill-accent);
      color: white;
      border-color: var(--fill-accent);
    }

    .content {
      flex: 1;
      overflow-y: auto;
      padding: 24px;
      max-width: 1200px;
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

    .stats-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
      gap: 12px;
    }

    .big-number {
      font-size: 32px;
      font-weight: 500;
      color: var(--fill-accent);
      margin: 12px 0;
    }

    .big-number.error {
      color: var(--fill-danger);
    }

    .big-number.ok {
      color: var(--fill-success);
    }

    .desc {
      font-size: 12px;
      color: var(--text-secondary);
      margin: 8px 0 0 0;
    }

    .search-box {
      display: flex;
      gap: 12px;
      align-items: flex-end;
      margin-bottom: 12px;
    }

    .search-box app-input {
      flex: 1;
    }

    .product-info {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding: 12px 0;
      font-size: 14px;
    }

    .form {
      display: flex;
      flex-direction: column;
      gap: 12px;
      margin-bottom: 12px;
    }

    .actions {
      display: flex;
      gap: 8px;
      margin-top: 16px;
      padding-top: 12px;
      border-top: 0.5px solid var(--border);
    }

    .diff {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 24px;
      padding: 12px 0;
    }

    .diff-col {
      font-size: 13px;
    }

    .diff-col h4 {
      margin: 0 0 8px 0;
      color: var(--text-secondary);
      font-size: 13px;
    }

    .diff-col div {
      margin: 4px 0;
    }

    .history {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding: 12px 0;
    }

    .hist-row {
      padding: 12px;
      border-left: 2px solid var(--fill-accent);
      background: var(--surface-1);
      border-radius: var(--radius);
      font-size: 13px;
    }

    .hist-date {
      font-size: 12px;
      color: var(--text-secondary);
      margin-bottom: 4px;
    }

    .msg {
      padding: 12px;
      border-radius: var(--radius);
      font-size: 13px;
      margin-top: 12px;
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
      padding: 12px;
      text-align: center;
      color: var(--text-secondary);
      font-size: 13px;
    }
  `],
  providers: [ProductoService]
})
export class AppComponent implements OnInit {
  page: 'dashboard' | 'buscar' = 'dashboard';
  dashboard: any = null;
  searchCode = '';
  product: any = null;
  edited: any = {};
  editing = false;
  loading = false;
  error = '';
  diff: any = null;
  history: any[] = [];
  showHistory = false;

  constructor(private service: ProductoService) {}

  ngOnInit() {
    this.loadDashboard();
    setInterval(() => this.loadDashboard(), 30000);
  }

  loadDashboard() {
    this.service.getDashboard().subscribe({
      next: (d) => { this.dashboard = d; },
      error: (e) => console.error(e)
    });
  }

  search() {
    if (!this.searchCode.trim()) {
      this.error = 'Ingresa código';
      return;
    }
    this.loading = true;
    this.error = '';
    this.service.getProductByCode(this.searchCode).subscribe({
      next: (p) => {
        this.product = p;
        this.edited = { ...p };
        this.loading = false;
      },
      error: (e) => {
        this.error = e.message;
        this.loading = false;
      }
    });
  }

  toggleEdit() {
    this.editing = !this.editing;
    if (!this.editing) {
      this.edited = { ...this.product };
    }
  }

  save() {
    if (!this.product) return;
    this.loading = true;
    this.service.updateProduct(this.product.code, this.edited).subscribe({
      next: (p) => {
        this.product = p;
        this.editing = false;
        this.loading = false;
      },
      error: (e) => {
        this.error = e.message;
        this.loading = false;
      }
    });
  }

  loadDiff() {
    if (!this.product) return;
    this.service.getProductDiff(this.product.code).subscribe({
      next: (d) => {
        this.diff = d;
        this.showHistory = false;
      },
      error: (e) => { this.error = e.message; }
    });
  }

  loadHistory() {
    if (!this.product) return;
    this.showHistory = true;
    this.service.getProductHistorial(this.product.code).subscribe({
      next: (h) => { this.history = h; },
      error: (e) => { this.error = e.message; }
    });
  }
}
