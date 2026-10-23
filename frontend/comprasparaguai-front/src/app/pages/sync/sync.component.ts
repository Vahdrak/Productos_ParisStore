import { FechaPyPipe } from '../../shared/utils/fecha-py.pipe';
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import { ProductoService } from '../../shared/services/producto.service';
import { ErrorService } from '../../shared/services/error.service';
import { isDateInRange, parseFecha } from '../../shared/utils/fechas';
import { DateRangeComponent } from '../../shared/ui/date-range.component';
import { PaginadorComponent } from '../../shared/ui/paginador.component';
import { IconComponent } from '../../shared/ui/icon.component';

@Component({
  selector: 'app-sync',
  standalone: true,
  imports: [CommonModule, FechaPyPipe, FormsModule, DateRangeComponent, PaginadorComponent, IconComponent],
  template: `
    <div class="fade-in">
      <div class="section-card">
        <h3 class="section-title"><app-icon name="refresh" size="1em"/> Sincronización Oracle → PostgreSQL</h3>
        <app-date-range [(desde)]="fechaDesde" [(hasta)]="fechaHasta" label="Rango de sincronizaciones:" (changed)="applyFilters()" (cleared)="load()"></app-date-range>
        <div class="table-container">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Inicio</th>
                <th scope="col">Fin</th>
                <th scope="col">Duración</th>
                <th scope="col">Filas</th>
                <th scope="col" class="tono-teal">Nuevos</th>
                <th scope="col" class="tono-sky">Actualizados</th>
                <th scope="col">Estado</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let run of syncRuns" [class.fila-tono]="run.notes?.startsWith('Error')" [class.tono-err]="run.notes?.startsWith('Error')">
                <td>{{ run.startedAt | fechaPy:'yyyy-MM-dd HH:mm:ss' }}</td>
                <td>{{ run.finishedAt | fechaPy:'yyyy-MM-dd HH:mm:ss' }}</td>
                <td>{{ getDuration(run) }}</td>
                <td>{{ run.rowsInView ?? 0 }}</td>
                <td class="tono-teal">{{ run.newCount }}</td>
                <td class="tono-sky">{{ run.updatedCount }}</td>
                <td>
                  <span class="badge" [class.badge-success]="!run.notes?.startsWith('Error')" [class.badge-error]="run.notes?.startsWith('Error')" [class.tono-ok]="!run.notes?.startsWith('Error')" [class.tono-err]="run.notes?.startsWith('Error')">
                    {{ run.notes?.startsWith('Error') ? 'Con Error' : 'OK' }}
                  </span>
                </td>
              </tr>
            </tbody>
          </table>
          <div *ngIf="cargando" class="empty-state"><span class="spinner" role="status" aria-label="Cargando"></span></div>
          <div *ngIf="!cargando && syncRuns.length === 0" class="empty-state">No hay corridas de sync</div>
        </div>
        <app-paginador [page]="page" [totalPages]="totalPages" [info]="filtered.length + ' corridas'" (go)="irAPagina($event)"></app-paginador>
      </div>
    </div>
  `
})
export class SyncComponent implements OnInit {
  syncRuns: any[] = [];
  cargando = true;
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
        this.cargando = false;
      },
      () => {
        this.cargando = false;
        this.errors.show('Error cargando sync runs');
      }
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

  irAPagina(p: number) {
    this.page = Math.min(Math.max(1, p), this.totalPages);
    this.applyFilters();
  }

  getDuration(run: any): string {
    if (!run.startedAt || !run.finishedAt) return '—';
    const start = (parseFecha(run.startedAt) as Date).getTime();
    const end = (parseFecha(run.finishedAt) as Date).getTime();
    const secs = Math.round((end - start) / 1000);
    return secs < 60 ? `${secs}s` : `${Math.round(secs / 60)}m`;
  }
}
