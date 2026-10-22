import { FechaPyPipe } from '../../shared/utils/fecha-py.pipe';
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import { ErroresService } from '../../shared/services/errores.service';
import { ProductoService, SendRun } from '../../shared/services/producto.service';
import { ErrorService } from '../../shared/services/error.service';
import { isDateInRange, quickRange } from '../../shared/utils/fechas';
import { QuickRangeComponent } from '../../shared/ui/quick-range.component';

@Component({
  selector: 'app-compara',
  standalone: true,
  imports: [CommonModule, FechaPyPipe, FormsModule, QuickRangeComponent],
  template: `
    <div class="fade-in">
      <div class="section-card">
        <h3 class="section-title">📤 Envíos a Compara Paraguai</h3>
        <div class="search-bar">
          <label class="label-dim">📅 Rango de envíos:</label>
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
                <th>Modo</th>
                <th>Total</th>
                <th>✅ OK</th>
                <th>❌ Fallidos</th>
                <th>Error</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let run of sendRuns">
                <td>{{ run.startedAt | fechaPy:'yyyy-MM-dd HH:mm:ss' }}</td>
                <td>{{ run.finishedAt | fechaPy:'yyyy-MM-dd HH:mm:ss' }}</td>
                <td>{{ run.mode }}</td>
                <td>{{ run.total }}</td>
                <td style="color: #4ade80;">{{ run.sent }}</td>
                <td style="color: #ef4444;">{{ run.failed }}</td>
                <td class="text-small">{{ run.error || '—' }}</td>
              </tr>
              <tr *ngIf="sendRuns.length === 0">
                <td colspan="7" class="text-center py-8 text-dim">No hay envíos registrados</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div *ngIf="totalPages > 1" class="pagination">
          <button [disabled]="page === 1" (click)="irAPagina(1)">«</button>
          <button [disabled]="page === 1" (click)="irAPagina(page - 1)">‹</button>
          <span class="page-info">Pág {{ page }}/{{ totalPages }} · {{ filtered.length }} corridas</span>
          <button [disabled]="page === totalPages" (click)="irAPagina(page + 1)">›</button>
          <button [disabled]="page === totalPages" (click)="irAPagina(totalPages)">»</button>
        </div>
      </div>

      <!-- Errores -->
      <div class="section-card mt-6">
        <h3 class="section-title">❌ Productos con Error ({{ erroresTotal }})</h3>
        <div class="table-container">
          <table class="data-table" *ngIf="errorProducts.length > 0">
            <thead>
              <tr>
                <th>Código</th>
                <th>Nombre</th>
                <th>Error</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let e of errorProducts">
                <td>{{ e.code }}</td>
                <td>{{ e.nombre }}</td>
                <td class="text-small" style="white-space: normal; word-break: break-word;">{{ e.mensaje }}</td>
              </tr>
            </tbody>
          </table>
          <div *ngIf="erroresTotalPages > 1" class="pagination">
            <button [disabled]="erroresPage === 1" (click)="cargarErrores(1)">«</button>
            <button [disabled]="erroresPage === 1" (click)="cargarErrores(erroresPage - 1)">‹</button>
            <span class="page-info">Pág {{ erroresPage }}/{{ erroresTotalPages }} · {{ erroresTotal }} productos</span>
            <button [disabled]="erroresPage === erroresTotalPages" (click)="cargarErrores(erroresPage + 1)">›</button>
            <button [disabled]="erroresPage === erroresTotalPages" (click)="cargarErrores(erroresTotalPages)">»</button>
          </div>
          <div *ngIf="errorProducts.length === 0" class="text-center py-8 text-dim">
            Sin errores ✅
          </div>
        </div>
      </div>
    </div>
  `
})
export class ComparaComponent implements OnInit {
  sendRuns: SendRun[] = [];
  original: SendRun[] = [];
  filtered: SendRun[] = [];
  errorProducts: any[] = [];
  erroresPage = 1;
  erroresTotalPages = 1;
  erroresTotal = 0;

  fechaDesde = '';
  fechaHasta = '';

  pageSize = 25;
  page = 1;
  totalPages = 1;

  constructor(private productoService: ProductoService, private erroresService: ErroresService, private errors: ErrorService) {
    interval(30000).pipe(takeUntilDestroyed()).subscribe(() => this.load());
  }

  ngOnInit() {
    this.load();
  }

  load() {
    this.productoService.getSendRuns('compara').subscribe(
      runs => {
        this.original = runs;
        this.applyFilters();
      },
      () => this.errors.show('Error cargando send runs')
    );
    this.cargarErrores(this.erroresPage);
  }

  /** Productos en Error de este destino, una página por vez (pueden ser miles). */
  cargarErrores(page: number) {
    this.erroresPage = Math.max(1, page);
    this.erroresService.getErrores('compara', {}, this.erroresPage, 15).subscribe(
      r => {
        this.errorProducts = r.items;
        this.erroresTotal = r.totalCount;
        this.erroresTotalPages = r.totalPages || 1;
      },
      () => this.errors.show('Error cargando los productos con error')
    );
  }

  applyFilters() {
    this.filtered = this.original.filter(r => isDateInRange(r.startedAt, this.fechaDesde, this.fechaHasta));
    if (this.page < 1) this.page = 1;
    this.totalPages = Math.ceil(this.filtered.length / this.pageSize) || 1;
    if (this.page > this.totalPages) this.page = this.totalPages;
    const start = (this.page - 1) * this.pageSize;
    this.sendRuns = this.filtered.slice(start, start + this.pageSize);
  }

  irAPagina(p: number) {
    this.page = Math.min(Math.max(1, p), this.totalPages);
    this.applyFilters();
  }

  setQuickRange(range: string) {
    const { desde, hasta } = quickRange(range);
    this.fechaDesde = desde;
    this.fechaHasta = hasta;
    this.applyFilters();
  }
}
