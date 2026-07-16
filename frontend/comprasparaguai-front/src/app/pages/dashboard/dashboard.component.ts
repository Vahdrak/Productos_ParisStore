import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CardComponent } from '../../shared/components';
import { ProductoService } from '../../shared/services/producto.service';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [CommonModule, CardComponent],
  template: `
    <div class="dashboard">
      <h2>Dashboard Operativo</h2>
      <p class="subtitle">Resumen en tiempo real</p>

      <div *ngIf="dashboard" class="stats-grid">
        <app-card title="Pendientes" [elevated]="true">
          <div class="big-number">{{ dashboard.pendingCount }}</div>
          <p class="desc">Productos sin enviar</p>
        </app-card>

        <app-card title="Nuevos" [elevated]="true">
          <div class="big-number">{{ dashboard.newCount }}</div>
          <p class="desc">Última sincronización</p>
        </app-card>

        <app-card title="Actualizados" [elevated]="true">
          <div class="big-number">{{ dashboard.updatedCount }}</div>
          <p class="desc">Cambios detectados</p>
        </app-card>

        <app-card title="Enviados" [elevated]="true">
          <div class="big-number">{{ dashboard.sentCount }}</div>
          <p class="desc">A API Compras Paraguai</p>
        </app-card>

        <app-card title="Errores" [elevated]="true">
          <div class="big-number error">{{ dashboard.errorCount }}</div>
          <p class="desc">Productos con error</p>
        </app-card>

        <app-card title="Estado" [elevated]="true">
          <div class="big-number" [class.ok]="dashboard.status === 'OK'">
            {{ dashboard.status }}
          </div>
          <p class="desc">{{ dashboard.lastSyncAt | date:'short' }}</p>
        </app-card>
      </div>

      <div *ngIf="!dashboard" class="loading">Cargando datos...</div>
    </div>
  `,
  styles: [`
    .dashboard {
      max-width: 1200px;
    }

    h2 {
      margin: 0 0 4px 0;
      font-size: 24px;
      font-weight: 500;
    }

    .subtitle {
      margin: 0 0 24px 0;
      color: var(--text-secondary);
      font-size: 14px;
    }

    .stats-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
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

    .loading {
      padding: 40px;
      text-align: center;
      color: var(--text-secondary);
    }
  `],
  providers: [ProductoService]
})
export class DashboardPageComponent implements OnInit {
  dashboard: any = null;

  constructor(private productoService: ProductoService) {}

  ngOnInit() {
    this.loadDashboard();
    setInterval(() => this.loadDashboard(), 30000); // Refresh cada 30s
  }

  loadDashboard() {
    this.productoService.getDashboard().subscribe({
      next: (data) => {
        this.dashboard = data;
      },
      error: (err) => console.error(err)
    });
  }
}
