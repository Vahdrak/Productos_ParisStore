import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import { ProductoService, Dashboard } from '../../shared/services/producto.service';
import { ErrorService } from '../../shared/services/error.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="fade-in">
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
            <span class="stat-title">⏳ Pendientes (se enviarán)</span>
            <span class="stat-val">{{ target.pending }}</span>
          </div>
          <div class="stat-card" title="Pendientes retenidos por una pausa o excepción: se actualizan pero no se envían">
            <span class="stat-title">🚫 Retenidos</span>
            <span class="stat-val" style="color: #fbbf24;">{{ target.held }}</span>
          </div>
          <div class="stat-card" title="Productos retirados del destino (se les envió stock 0)">
            <span class="stat-title">📦 Retirados</span>
            <span class="stat-val">{{ target.retired }}</span>
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
  `
})
export class DashboardComponent implements OnInit {
  dashboard: Dashboard | null = null;

  constructor(private productoService: ProductoService, private errors: ErrorService) {
    interval(30000).pipe(takeUntilDestroyed()).subscribe(() => this.loadDashboard());
  }

  ngOnInit() {
    this.loadDashboard();
  }

  loadDashboard() {
    this.productoService.getDashboard().subscribe(
      d => this.dashboard = d,
      () => this.errors.show('Error cargando dashboard')
    );
  }
}
