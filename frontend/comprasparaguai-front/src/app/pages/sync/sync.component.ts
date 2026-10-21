import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import { ProductoService } from '../../shared/services/producto.service';
import { ErrorService } from '../../shared/services/error.service';
import { isDateInRange, quickRange } from '../../shared/utils/fechas';
import { QuickRangeComponent } from '../../shared/ui/quick-range.component';

@Component({
  selector: 'app-sync',
  standalone: true,
  imports: [CommonModule, FormsModule, QuickRangeComponent],
  template: `
    <div class="fade-in">
      <div class="section-card">
        <h3 class="section-title">🔄 Sincronización Oracle → PostgreSQL</h3>
        <div class="search-bar">
          <label class="label-dim">📅 Rango de sincronizaciones:</label>
          <input type="date" [(ngModel)]="fechaDesde" (ngModelChange)="applyFilters()" class="form-input" style="width: 120px;" />
          <span style="color: #8f8f8f;">hasta</span>
          <input type="date" [(ngModel)]="fechaHasta" (ngModelChange)="applyFilters()" class="form-input" style="width: 120px;" />
          <app-quick-range (pick)="setQuickRange($event)" (clear)="fechaDesde = ''; fechaHasta = ''; load();"></app-quick-range>
        </div>
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
  `
})
export class SyncComponent implements OnInit {
  syncRuns: any[] = [];
  original: any[] = [];
  filtered: any[] = [];

  fechaDesde = '';
  fechaHasta = '';

  pageSize = 25;
  page = 1;
  totalPages = 1;

  constructor(private productoService: ProductoService, private errors: ErrorService) {
    interval(30000).pipe(takeUntilDestroyed()).subscribe(() => this.load());
  }

  ngOnInit() {
    this.load();
  }

  load() {
    this.productoService.getSyncRuns().subscribe(
      runs => {
        this.original = runs;
        this.applyFilters();
      },
      () => this.errors.show('Error cargando sync runs')
    );
  }

  applyFilters() {
    this.filtered = this.original.filter(r => isDateInRange(r.startedAt, this.fechaDesde, this.fechaHasta));
    if (this.page < 1) this.page = 1;
    this.totalPages = Math.ceil(this.filtered.length / this.pageSize) || 1;
    if (this.page > this.totalPages) this.page = this.totalPages;
    const start = (this.page - 1) * this.pageSize;
    this.syncRuns = this.filtered.slice(start, start + this.pageSize);
  }

  setQuickRange(range: string) {
    const { desde, hasta } = quickRange(range);
    this.fechaDesde = desde;
    this.fechaHasta = hasta;
    this.applyFilters();
  }

  getDuration(run: any): string {
    if (!run.startedAt || !run.finishedAt) return '—';
    const start = new Date(run.startedAt).getTime();
    const end = new Date(run.finishedAt).getTime();
    const secs = Math.round((end - start) / 1000);
    return secs < 60 ? `${secs}s` : `${Math.round(secs / 60)}m`;
  }
}
