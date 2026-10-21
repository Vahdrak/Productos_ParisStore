import { Component } from '@angular/core';
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

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
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
        <div class="header-right">
          <ng-container *ngIf="auth.logueado; else sinSesion">
            <span class="usuario">👤 {{ auth.nombre || auth.usuario }}</span>
            <button class="btn btn-ghost btn-sm" (click)="auth.logout()">Salir</button>
          </ng-container>
          <ng-template #sinSesion>
            <button class="btn btn-ghost btn-sm" (click)="abrirLogin()">Iniciar sesión</button>
          </ng-template>
        </div>
      </header>

      <!-- LOGIN (ver datos es libre; modificar exige sesión) -->
      <div *ngIf="mostrarLogin || auth.pedirLogin" class="modal-fondo" (click)="cerrarLogin()">
        <form class="modal-caja" (click)="$event.stopPropagation()" (ngSubmit)="entrar()">
          <h3 class="section-title">Iniciar sesión</h3>
          <p class="text-dim text-small" *ngIf="auth.pedirLogin">Tu sesión venció o no existe: ingresa para modificar.</p>
          <input name="usuario" class="form-input" placeholder="Usuario" [(ngModel)]="loginUsuario" autocomplete="username" autofocus />
          <input name="password" type="password" class="form-input" placeholder="Contraseña" [(ngModel)]="loginPassword" autocomplete="current-password" />
          <div *ngIf="loginError" class="text-small" style="color: #fca5a5;">{{ loginError }}</div>
          <div class="modal-botones">
            <button type="button" class="btn btn-ghost" (click)="cerrarLogin()">Cancelar</button>
            <button type="submit" class="btn btn-primary" [disabled]="entrando">Entrar</button>
          </div>
        </form>
      </div>

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

    .header-right {
      display: flex;
      align-items: center;
      gap: 12px;
    }

    .usuario {
      color: #b0b0b0;
      font-size: 13px;
    }

    .modal-fondo {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.6);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 100;
    }

    .modal-caja {
      background: #131313;
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 8px;
      padding: 24px;
      width: 340px;
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .modal-botones {
      display: flex;
      justify-content: flex-end;
      gap: 8px;
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

  mostrarLogin = false;
  loginUsuario = '';
  loginPassword = '';
  loginError = '';
  entrando = false;

  constructor(public errors: ErrorService, public auth: AuthService) {}

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
