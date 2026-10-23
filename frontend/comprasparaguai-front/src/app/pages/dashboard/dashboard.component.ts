import { FechaPyPipe } from '../../shared/utils/fecha-py.pipe';
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import { ProductoService, Dashboard } from '../../shared/services/producto.service';
import { ErrorService } from '../../shared/services/error.service';
import { IconComponent } from '../../shared/ui/icon.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FechaPyPipe, IconComponent],
  template: `
    <div class="fade-in" [class.pintado]="pintado">
      <div class="section-card">
        <h3 class="section-title">
          <app-icon name="refresh" size="1em"/> Última Sincronización Oracle
          <span class="text-small text-dim" *ngIf="actualizadoAt" style="font-weight: 400; margin-left: 8px;">Actualizado a las {{ actualizadoAt | fechaPy:'HH:mm' }}</span>
          <span class="spinner" role="status" aria-label="Cargando" *ngIf="cargando" style="margin-left: 8px;"></span>
        </h3>
        <div class="dashboard-grid">
          <div class="stat-card t-indigo">
            <span class="stat-title">Fecha</span>
            <span class="stat-val">{{ dashboard ? (dashboard.lastSyncRun?.startedAt | fechaPy:'yyyy-MM-dd HH:mm:ss') : '—' }}</span>
          </div>
          <div class="stat-card t-teal">
            <span class="stat-title"><app-icon name="plus" size="1em"/> Nuevos</span>
            <span class="stat-val">{{ dashboard ? (dashboard.lastSyncRun?.newCount ?? 0) : '—' }}</span>
          </div>
          <div class="stat-card t-sky">
            <span class="stat-title"><app-icon name="refresh" size="1em"/> Actualizados</span>
            <span class="stat-val">{{ dashboard ? (dashboard.lastSyncRun?.updatedCount ?? 0) : '—' }}</span>
          </div>
          <div class="stat-card t-slate">
            <span class="stat-title"><app-icon name="info" size="1em"/> Notas</span>
            <span class="stat-val val-small">{{ dashboard?.lastSyncRun?.notes || '—' }}</span>
          </div>
        </div>
      </div>

      <!-- Targets Status -->
      <div *ngFor="let target of dashboard?.targets" class="section-card mt-6">
        <h3 class="section-title">{{ target.target }}</h3>
        <div class="dashboard-grid">
          <div class="stat-card t-pend">
            <span class="stat-title"><app-icon name="clock" size="1em"/> Pendientes (se enviarán)</span>
            <span class="stat-val">{{ target.pending }}</span>
          </div>
          <div class="stat-card t-ret" title="Pendientes retenidos por una pausa o excepción: se actualizan pero no se envían">
            <span class="stat-title"><app-icon name="ban" size="1em"/> Retenidos</span>
            <span class="stat-val text-warn">{{ target.held }}</span>
          </div>
          <div class="stat-card t-violeta" title="Productos retirados del destino (se les envió stock 0)">
            <span class="stat-title"><app-icon name="box" size="1em"/> Retirados</span>
            <span class="stat-val">{{ target.retired }}</span>
          </div>
          <div class="stat-card t-ok">
            <span class="stat-title"><app-icon name="check" size="1em"/> Enviados</span>
            <span class="stat-val text-ok">{{ target.sent }}</span>
          </div>
          <div class="stat-card t-err">
            <span class="stat-title"><app-icon name="x" size="1em"/> Errores</span>
            <span class="stat-val text-err">{{ target.error }}</span>
          </div>
          <div class="stat-card t-rosa" *ngIf="target.target === 'ComprasParaguai'"
               title="Productos con stock que Compras Paraguai marca como no disponibles. Lo decide Compras, no el envío. Foto tomada con Worker.Verify --disponibilidad">
            <span class="stat-title"><app-icon name="warn" size="1em"/> No disponibles en Compras</span>
            <span class="stat-val" [class.text-warn]="!!dashboard?.noDisponiblesCompras">
              {{ dashboard?.noDisponiblesComprasAt ? dashboard?.noDisponiblesCompras : '—' }}
            </span>
            <span class="text-small text-dim" *ngIf="dashboard?.noDisponiblesComprasAt">al {{ dashboard?.noDisponiblesComprasAt | fechaPy:'yyyy-MM-dd HH:mm' }}</span>
            <span class="text-small text-dim" *ngIf="!dashboard?.noDisponiblesComprasAt">sin revisar</span>
          </div>
          <div class="stat-card t-azul">
            <span class="stat-title"><app-icon name="chart" size="1em"/> Total</span>
            <span class="stat-val">{{ target.total }}</span>
          </div>
          <div class="stat-card t-cian">
            <span class="stat-title"><app-icon name="clock" size="1em"/> Último envío</span>
            <span class="stat-val val-small">{{ target.lastSentAt | fechaPy:'yyyy-MM-dd HH:mm:ss' }}</span>
          </div>
        </div>
      </div>
    </div>

    <!-- Pintar: da a cada tarjeta su color. Solo existe en el Dashboard y se recuerda en este navegador. -->
    <button type="button" class="btn-pintar" (click)="alternarPintado()" [attr.aria-pressed]="pintado"
            [title]="pintado ? 'Volver al aspecto sin colores' : 'Dar un color a cada tarjeta'">
      <app-icon name="palette" size="1.1em"/> {{ pintado ? 'Quitar color' : 'Pintar' }}
    </button>
  `,
  styles: [`
    /* Espacio al final para que el botón flotante no tape la última tarjeta. */
    :host { display: block; padding-bottom: 72px; }

    /* Un color por tarjeta (r g b). Sin el modo "pintado" no cambia nada. */
    .stat-card { --t: 148 163 184; transition: background-color .2s, border-color .2s; }
    .t-indigo { --t: 129 140 248; }
    .t-teal   { --t: 45 212 191; }
    .t-sky    { --t: 56 189 248; }
    .t-slate  { --t: 148 163 184; }
    .t-pend   { --t: 251 191 36; }
    .t-ret    { --t: 249 115 22; }
    .t-violeta{ --t: 167 139 250; }
    .t-ok     { --t: 74 222 128; }
    .t-err    { --t: 248 113 113; }
    .t-rosa   { --t: 244 114 182; }
    .t-azul   { --t: 96 165 250; }
    .t-cian   { --t: 34 211 238; }

    .pintado .stat-card {
      background: rgb(var(--t) / 0.10);
      border-color: rgb(var(--t) / 0.40);
    }
    .pintado .stat-card .stat-title { color: rgb(var(--t) / 0.95); }
    .pintado .stat-card .stat-val { color: rgb(var(--t)); }

    .btn-pintar {
      position: fixed;
      right: 20px;
      bottom: 20px;
      z-index: 30;
      display: inline-flex;
      align-items: center;
      gap: 8px;
      padding: 10px 16px;
      border-radius: 999px;
      border: 1px solid var(--line-strong);
      background: var(--card-2);
      color: var(--text);
      font: inherit;
      font-size: 14px;
      cursor: pointer;
      box-shadow: 0 4px 14px rgba(0, 0, 0, 0.45);
    }
    .btn-pintar:hover { background: var(--hover); }
    .btn-pintar[aria-pressed="true"] { border-color: var(--accent); }
  `]
})
export class DashboardComponent implements OnInit {
  dashboard: Dashboard | null = null;
  cargando = true;
  actualizadoAt: Date | null = null;

  /** Modo de color de las tarjetas; se recuerda en este navegador (si el almacenamiento falla, queda apagado). */
  pintado = DashboardComponent.leerPintado();

  constructor(private productoService: ProductoService, private errors: ErrorService) {
    interval(30000).pipe(takeUntilDestroyed()).subscribe(() => this.loadDashboard());
  }

  private static readonly CLAVE_PINTADO = 'dashboard-pintado';

  private static leerPintado(): boolean {
    try { return localStorage.getItem(DashboardComponent.CLAVE_PINTADO) === '1'; } catch { return false; }
  }

  alternarPintado() {
    this.pintado = !this.pintado;
    try { localStorage.setItem(DashboardComponent.CLAVE_PINTADO, this.pintado ? '1' : '0'); } catch { /* sin almacenamiento: solo dura la sesión */ }
  }

  ngOnInit() {
    this.loadDashboard();
  }

  loadDashboard() {
    this.productoService.getDashboard().subscribe(
      d => {
        this.dashboard = d;
        this.actualizadoAt = new Date();
        this.cargando = false;
      },
      () => {
        this.cargando = false;
        this.errors.show('Error cargando dashboard');
      }
    );
  }
}
