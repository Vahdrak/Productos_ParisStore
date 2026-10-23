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
    <div class="fade-in">
      <div class="section-card">
        <h3 class="section-title">
          <app-icon name="refresh" size="1em"/> Última Sincronización Oracle
          <span class="text-small text-dim" *ngIf="actualizadoAt" style="font-weight: 400; margin-left: 8px;">Actualizado a las {{ actualizadoAt | fechaPy:'HH:mm' }}</span>
          <span class="spinner" role="status" aria-label="Cargando" *ngIf="cargando" style="margin-left: 8px;"></span>
        </h3>
        <div class="dashboard-grid">
          <div class="stat-card tono-indigo">
            <span class="stat-title">Fecha</span>
            <span class="stat-val">{{ dashboard ? (dashboard.lastSyncRun?.startedAt | fechaPy:'yyyy-MM-dd HH:mm:ss') : '—' }}</span>
          </div>
          <div class="stat-card tono-teal">
            <span class="stat-title"><app-icon name="plus" size="1em"/> Nuevos</span>
            <span class="stat-val">{{ dashboard ? (dashboard.lastSyncRun?.newCount ?? 0) : '—' }}</span>
          </div>
          <div class="stat-card tono-sky">
            <span class="stat-title"><app-icon name="refresh" size="1em"/> Actualizados</span>
            <span class="stat-val">{{ dashboard ? (dashboard.lastSyncRun?.updatedCount ?? 0) : '—' }}</span>
          </div>
          <div class="stat-card tono-slate">
            <span class="stat-title"><app-icon name="info" size="1em"/> Notas</span>
            <span class="stat-val val-small">{{ dashboard?.lastSyncRun?.notes || '—' }}</span>
          </div>
        </div>
      </div>

      <!-- Targets Status -->
      <div *ngFor="let target of dashboard?.targets" class="section-card mt-6">
        <h3 class="section-title">{{ target.target }}</h3>
        <div class="dashboard-grid">
          <div class="stat-card tono-pend">
            <span class="stat-title"><app-icon name="clock" size="1em"/> Pendientes (se enviarán)</span>
            <span class="stat-val">{{ target.pending }}</span>
          </div>
          <div class="stat-card tono-ret" title="Pendientes retenidos por una pausa o excepción: se actualizan pero no se envían">
            <span class="stat-title"><app-icon name="ban" size="1em"/> Retenidos</span>
            <span class="stat-val text-warn">{{ target.held }}</span>
          </div>
          <div class="stat-card tono-violeta" title="Productos retirados del destino (se les envió stock 0)">
            <span class="stat-title"><app-icon name="box" size="1em"/> Retirados</span>
            <span class="stat-val">{{ target.retired }}</span>
          </div>
          <div class="stat-card tono-ok">
            <span class="stat-title"><app-icon name="check" size="1em"/> Enviados</span>
            <span class="stat-val text-ok">{{ target.sent }}</span>
          </div>
          <div class="stat-card tono-err">
            <span class="stat-title"><app-icon name="x" size="1em"/> Errores</span>
            <span class="stat-val text-err">{{ target.error }}</span>
          </div>
          <div class="stat-card tono-rosa" *ngIf="target.target === 'ComprasParaguai'"
               title="Productos con stock que Compras Paraguai marca como no disponibles. Lo decide Compras, no el envío. Foto tomada con Worker.Verify --disponibilidad">
            <span class="stat-title"><app-icon name="warn" size="1em"/> No disponibles en Compras</span>
            <span class="stat-val" [class.text-warn]="!!dashboard?.noDisponiblesCompras">
              {{ dashboard?.noDisponiblesComprasAt ? dashboard?.noDisponiblesCompras : '—' }}
            </span>
            <span class="text-small text-dim" *ngIf="dashboard?.noDisponiblesComprasAt">al {{ dashboard?.noDisponiblesComprasAt | fechaPy:'yyyy-MM-dd HH:mm' }}</span>
            <span class="text-small text-dim" *ngIf="!dashboard?.noDisponiblesComprasAt">sin revisar</span>
          </div>
          <div class="stat-card tono-azul">
            <span class="stat-title"><app-icon name="chart" size="1em"/> Total</span>
            <span class="stat-val">{{ target.total }}</span>
          </div>
          <div class="stat-card tono-cian">
            <span class="stat-title"><app-icon name="clock" size="1em"/> Último envío</span>
            <span class="stat-val val-small">{{ target.lastSentAt | fechaPy:'yyyy-MM-dd HH:mm:ss' }}</span>
          </div>
        </div>
      </div>
    </div>

  `
})
export class DashboardComponent implements OnInit {
  dashboard: Dashboard | null = null;
  cargando = true;
  actualizadoAt: Date | null = null;

  constructor(private productoService: ProductoService, private errors: ErrorService) {
    interval(30000).pipe(takeUntilDestroyed()).subscribe(() => this.loadDashboard());
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
