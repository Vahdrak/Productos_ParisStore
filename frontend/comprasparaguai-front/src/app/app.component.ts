import { Component, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ErrorService } from './shared/services/error.service';
import { AuthService } from './shared/services/auth.service';
import { DashboardComponent } from './pages/dashboard/dashboard.component';
import { ProductosComponent } from './pages/productos/productos.component';
import { PendientesComponent } from './pages/pendientes/pendientes.component';
import { SyncComponent } from './pages/sync/sync.component';
import { ComprasComponent } from './pages/compras/compras.component';
import { ComparaComponent } from './pages/compara/compara.component';
import { ApiComponent } from './pages/api/api.component';
import { CambiosComponent } from './pages/cambios/cambios.component';
import { FiltrosEnvioComponent } from './pages/filtros-envio/filtros-envio.component';
import { ErroresComponent } from './pages/errores/errores.component';
import { IconComponent } from './shared/ui/icon.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule, FormsModule, IconComponent,
    DashboardComponent, ProductosComponent, PendientesComponent,
    SyncComponent, ComprasComponent, ComparaComponent, ApiComponent, CambiosComponent, FiltrosEnvioComponent, ErroresComponent
  ],
  template: `
    <div class="app-container">
      <!-- HEADER -->
      <header class="app-header">
        <div class="header-left">
          <svg class="logo-icon" aria-hidden="true" focusable="false" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
            <path d="M4 5h16v4H4z"></path>
            <path d="M5 9v10h14V9"></path>
            <path d="M10 13h4"></path>
          </svg>
          <h1>Catálogos Paris Store</h1>
        </div>
        <div class="header-right">
          <ng-container *ngIf="auth.logueado; else sinSesion">
            <span class="usuario"><app-icon name="user"></app-icon> {{ auth.nombre || auth.usuario }}</span>
            <button class="btn btn-ghost btn-sm" (click)="auth.logout()">Salir</button>
          </ng-container>
          <ng-template #sinSesion>
            <button class="btn btn-ghost btn-sm" (click)="abrirLogin()">Iniciar sesión</button>
          </ng-template>
        </div>
      </header>

      <!-- LOGIN (ver datos es libre; modificar exige sesión) -->
      <div *ngIf="mostrarLogin || auth.pedirLogin" class="modal-fondo" (click)="cerrarLogin()">
        <form class="modal-caja" role="dialog" aria-modal="true" aria-labelledby="login-titulo" (click)="$event.stopPropagation()" (ngSubmit)="entrar()">
          <h3 id="login-titulo" class="section-title">Iniciar sesión</h3>
          <p class="text-dim text-small" *ngIf="auth.pedirLogin">Tu sesión venció o no existe: ingresa para modificar.</p>
          <input name="usuario" class="form-input" placeholder="Usuario" aria-label="Usuario" [(ngModel)]="loginUsuario" autocomplete="username" autofocus />
          <input name="password" type="password" class="form-input" placeholder="Contraseña" aria-label="Contraseña" [(ngModel)]="loginPassword" autocomplete="current-password" />
          <div *ngIf="loginError" class="text-small text-err" role="alert">{{ loginError }}</div>
          <div class="modal-botones">
            <button type="button" class="btn btn-ghost" (click)="cerrarLogin()">Cancelar</button>
            <button type="submit" class="btn btn-primary" [disabled]="entrando">Entrar</button>
          </div>
        </form>
      </div>

      <!-- NAVIGATION TABS -->
      <nav class="app-nav" role="tablist" aria-label="Secciones">
        <button *ngFor="let t of tabs" role="tab" [attr.aria-selected]="page === t.id" [class.active]="page === t.id" (click)="setPage(t.id)" class="tab-btn">{{ t.label }}</button>
      </nav>

      <!-- ERROR MESSAGE -->
      <div *ngIf="errors.message" class="alert alert-error" role="alert">
        <span><app-icon name="warn"></app-icon> {{ errors.message }}</span>
        <button (click)="errors.clear()" class="close-btn" aria-label="Cerrar aviso">&times;</button>
      </div>

      <!-- CONTENT AREA: cada pestaña es un componente propio (se crea al entrar y carga sus datos) -->
      <main class="app-content">
        <app-dashboard *ngIf="page === 'dashboard'"></app-dashboard>
        <app-productos *ngIf="page === 'productos'"></app-productos>
        <app-pendientes *ngIf="page === 'pendientes'"></app-pendientes>
        <app-cambios *ngIf="page === 'cambios'"></app-cambios>
        <app-filtros-envio *ngIf="page === 'filtros'"></app-filtros-envio>
        <app-errores *ngIf="page === 'errores'"></app-errores>
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
      background-color: var(--bg);
      color: var(--text);
      font-family: var(--font-sans);
    }

    .app-container {
      max-width: 1300px;
      margin: 0 auto;
      padding: var(--space-4) var(--space-5);
    }

    .app-header {
      position: sticky;
      top: 0;
      z-index: 50;
      background: var(--bg);
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: var(--space-3) 0;
    }

    .header-left {
      display: flex;
      align-items: center;
      gap: var(--space-3);
    }

    .logo-icon {
      color: var(--accent);
      flex-shrink: 0;
    }

    .app-header h1 {
      font-size: var(--fs-xl);
      font-weight: 600;
      color: var(--text-strong);
      margin: 0;
    }

    .header-right {
      display: flex;
      align-items: center;
      gap: var(--space-3);
    }

    .usuario {
      color: #b8b8b8;
      font-size: var(--fs-sm);
    }

    /* Login: mas angosto que el modal por defecto (.modal-fondo/.modal-caja son globales) */
    .modal-caja {
      width: min(340px, 92vw);
      gap: var(--space-3);
    }

    .app-nav {
      display: flex;
      gap: var(--space-2);
      margin-bottom: var(--space-5);
      border-bottom: 1px solid var(--line);
      padding-bottom: var(--space-2);
      overflow-x: auto;
    }

    .tab-btn {
      flex-shrink: 0;
      white-space: nowrap;
      background: transparent;
      border: 1px solid transparent;
      color: var(--text-dim);
      padding: var(--space-2) var(--space-4);
      font-size: var(--fs-md);
      font-weight: 500;
      border-radius: var(--radius-sm);
      cursor: pointer;
      transition: all 0.2s ease;
    }

    .tab-btn:hover {
      color: var(--text-strong);
      background: var(--hover);
    }

    .tab-btn.active {
      color: var(--text-strong);
      background: var(--card-2);
      border-color: var(--line-strong);
      border-bottom: 2px solid var(--accent);
    }

    .app-content {
      display: block;
      animation: fadeIn 0.3s ease-in-out;
    }

    .alert {
      padding: var(--space-3) var(--space-4);
      border-radius: var(--radius-sm);
      margin-bottom: var(--space-4);
      display: flex;
      justify-content: space-between;
      align-items: center;
    }

    .alert-error {
      background: rgba(239, 68, 68, 0.1);
      border: 1px solid rgba(239, 68, 68, 0.3);
      color: var(--err-soft);
    }

    .close-btn {
      background: none;
      border: none;
      color: var(--err-soft);
      cursor: pointer;
      font-size: 18px;
    }

    @media (max-width: 768px) {
      .app-container {
        padding: var(--space-3);
      }
    }
  `]
})
export class AppComponent {
  readonly tabs = [
    { id: 'dashboard', label: 'Dashboard' },
    { id: 'productos', label: 'Productos' },
    { id: 'pendientes', label: 'Pendientes' },
    { id: 'filtros', label: 'Filtros de envío' },
    { id: 'errores', label: 'Errores' },
    { id: 'cambios', label: 'Cambios' },
    { id: 'sync', label: 'Sync' },
    { id: 'compras', label: 'Compras' },
    { id: 'compara', label: 'Compara' },
    { id: 'api', label: 'Probar API' }
  ];

  page = 'dashboard';

  mostrarLogin = false;
  loginUsuario = '';
  loginPassword = '';
  loginError = '';
  entrando = false;

  constructor(public errors: ErrorService, public auth: AuthService) {}

  @HostListener('document:keydown.escape')
  onEscape() {
    if (this.mostrarLogin || this.auth.pedirLogin) {
      this.cerrarLogin();
    }
  }

  setPage(p: string) {
    this.page = p;
  }

  abrirLogin() {
    this.loginError = '';
    this.mostrarLogin = true;
  }

  cerrarLogin() {
    this.mostrarLogin = false;
    this.auth.pedirLogin = false;
    this.loginPassword = '';
  }

  entrar() {
    if (!this.loginUsuario.trim() || !this.loginPassword) {
      this.loginError = 'Ingresa usuario y contraseña';
      return;
    }
    this.entrando = true;
    this.loginError = '';
    this.auth.login(this.loginUsuario.trim(), this.loginPassword).subscribe({
      next: () => {
        this.entrando = false;
        this.mostrarLogin = false;
        this.loginPassword = '';
      },
      error: (e) => {
        this.entrando = false;
        this.loginError = e?.status === 401 ? 'Usuario o contraseña incorrectos' : 'No se pudo iniciar sesión';
      }
    });
  }
}
