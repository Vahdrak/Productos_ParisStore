import { FechaPyPipe } from '../../shared/utils/fecha-py.pipe';
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import { ErroresService } from '../../shared/services/errores.service';
import { ProductoService, SendRun } from '../../shared/services/producto.service';
import { ErrorService } from '../../shared/services/error.service';
import { isDateInRange } from '../../shared/utils/fechas';
import { DateRangeComponent } from '../../shared/ui/date-range.component';
import { PaginadorComponent } from '../../shared/ui/paginador.component';
import { IconComponent } from '../../shared/ui/icon.component';

@Component({
  selector: 'app-compara',
  standalone: true,
  imports: [CommonModule, FechaPyPipe, FormsModule, DateRangeComponent, PaginadorComponent, IconComponent],
  template: `
    <div class="fade-in">
      <div class="section-card">
        <h3 class="section-title"><app-icon name="send" size="1em"/> Envíos a Compara Paraguai</h3>
        <app-date-range [(desde)]="fechaDesde" [(hasta)]="fechaHasta" label="Rango de envíos:" (changed)="applyFilters()" (cleared)="load()"></app-date-range>
        <div class="table-container">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Inicio</th>
                <th scope="col">Fin</th>
                <th scope="col">Modo</th>
                <th scope="col" class="tono-azul">Total</th>
                <th scope="col" class="tono-ok"><app-icon name="check" size="1em"/> OK</th>
                <th scope="col" class="tono-err"><app-icon name="x" size="1em"/> Fallidos</th>
                <th scope="col">Error</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let run of sendRuns" [class.fila-tono]="run.failed > 0" [class.tono-err]="run.failed > 0">
                <td>{{ run.startedAt | fechaPy:'yyyy-MM-dd HH:mm:ss' }}</td>
                <td>{{ run.finishedAt | fechaPy:'yyyy-MM-dd HH:mm:ss' }}</td>
                <td>{{ run.mode }}</td>
                <td class="tono-azul">{{ run.total }}</td>
                <td class="text-ok tono-ok">{{ run.sent }}</td>
                <td class="text-err" [class.tono-err]="run.failed > 0">{{ run.failed }}</td>
                <td class="text-small" [class.tono-err]="run.error">{{ run.error || '—' }}</td>
              </tr>
            </tbody>
          </table>
          <div *ngIf="cargando && sendRuns.length === 0" class="empty-state"><span class="spinner" role="status" aria-label="Cargando"></span></div>
          <div *ngIf="!cargando && sendRuns.length === 0" class="empty-state">No hay envíos registrados</div>
        </div>
        <app-paginador [page]="page" [totalPages]="totalPages" [info]="filtered.length + ' corridas'" (go)="irAPagina($event)"></app-paginador>
      </div>

      <!-- Errores -->
      <div class="section-card mt-6">
        <h3 class="section-title"><app-icon name="x" size="1em"/> Productos con Error ({{ erroresTotal }})</h3>
        <div class="table-container">
          <table class="data-table" *ngIf="errorProducts.length > 0">
            <thead>
              <tr>
                <th scope="col">Código</th>
                <th scope="col">Nombre</th>
                <th scope="col">Error</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let e of errorProducts">
                <td>{{ e.code }}</td>
                <td>{{ e.nombre }}</td>
                <td class="text-small td-wrap tono-err">{{ e.mensaje }}</td>
              </tr>
            </tbody>
          </table>
          <div *ngIf="cargandoErrores && errorProducts.length === 0" class="empty-state"><span class="spinner" role="status" aria-label="Cargando"></span></div>
          <div *ngIf="!cargandoErrores && errorProducts.length === 0" class="empty-state text-ok"><app-icon name="check" size="1em"/> Sin errores</div>
        </div>
        <app-paginador [page]="erroresPage" [totalPages]="erroresTotalPages" [info]="erroresTotal + ' productos'" (go)="cargarErrores($event)"></app-paginador>
      </div>
    </div>
  `
})
export class ComparaComponent implements OnInit {
  sendRuns: SendRun[] = [];
  original: SendRun[] = [];
  filtered: SendRun[] = [];
  cargando = true;
  cargandoErrores = true;
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
        this.cargando = false;
      },
      () => {
        this.cargando = false;
        this.errors.show('Error cargando send runs');
      }
    );
    this.cargarErrores(this.erroresPage);
  }

  /** Productos en Error de este destino, una página por vez (pueden ser miles). */
  cargarErrores(page: number) {
    this.erroresPage = Math.max(1, page);
    this.cargandoErrores = true;
    this.erroresService.getErrores('compara', {}, this.erroresPage, 15).subscribe(
      r => {
        this.errorProducts = r.items;
        this.erroresTotal = r.totalCount;
        this.erroresTotalPages = r.totalPages || 1;
        this.cargandoErrores = false;
      },
      () => {
        this.cargandoErrores = false;
        this.errors.show('Error cargando los productos con error');
      }
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
}
