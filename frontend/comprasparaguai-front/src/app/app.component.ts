import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClientModule } from '@angular/common/http';
import { ErrorService } from './shared/services/error.service';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { ProductosComponent } from './pages/productos/productos.component';
import { PendientesComponent } from './pages/pendientes/pendientes.component';
import { SyncComponent } from './pages/sync/sync.component';
import { ComprasComponent } from './pages/compras/compras.component';
import { ComparaComponent } from './pages/compara/compara.component';
import { ApiComponent } from './pages/api/api.component';
import { CambiosComponent } from './pages/cambios/cambios.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule, HttpClientModule,
    DashboardComponent, ProductosComponent, PendientesComponent,
    SyncComponent, ComprasComponent, ComparaComponent, ApiComponent, CambiosComponent
  ],
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
        <button *ngFor="let t of tabs" [class.active]="page === t.id" (click)="setPage(t.id)" class="tab-btn">{{ t.label }}</button>
      </nav>

      <!-- ERROR MESSAGE -->
      <div *ngIf="errors.message" class="alert alert-error">
        <span>⚠️ {{ errors.message }}</span>
        <button (click)="errors.clear()" class="close-btn">&times;</button>
      </div>

      <!-- CONTENT AREA: cada pestaña es un componente propio (se crea al entrar y carga sus datos) -->
      <main class="app-content">
        <app-dashboard *ngIf="page === 'dashboard'"></app-dashboard>
        <app-productos *ngIf="page === 'productos'"></app-productos>
        <app-pendientes *ngIf="page === 'pendientes'"></app-pendientes>
        <app-cambios *ngIf="page === 'cambios'"></app-cambios>
        <app-sync *ngIf="page === 'sync'"></app-sync>
        <app-compras *ngIf="page === 'compras'"></app-compras>
        <app-compara *ngIf="page === 'compara'"></app-compara>
        <app-api *ngIf="page === 'api'"></app-api>
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
      display: block;
      animation: fadeIn 0.3s ease-in-out;
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
  `]
})
export class AppComponent {
  readonly tabs = [
    { id: 'dashboard', label: 'Dashboard' },
    { id: 'productos', label: 'Productos' },
    { id: 'pendientes', label: 'Pendientes' },
    { id: 'cambios', label: 'Cambios' },
    { id: 'sync', label: 'Sync' },
    { id: 'compras', label: 'Compras' },
    { id: 'compara', label: 'Compara' },
    { id: 'api', label: 'Probar API' }
  ];

  page = 'dashboard';

  constructor(public errors: ErrorService) {}

  setPage(p: string) {
    this.page = p;
  }
}
