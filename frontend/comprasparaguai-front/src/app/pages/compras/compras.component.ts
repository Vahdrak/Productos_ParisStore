import { FechaPyPipe } from '../../shared/utils/fecha-py.pipe';
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import { ErroresService } from '../../shared/services/errores.service';
import { NoDisponible, NoDisponiblesList, ProductoService, SendRun, VerifyResult, VerifyRun } from '../../shared/services/producto.service';
import { ErrorService } from '../../shared/services/error.service';
import { fechaPy, isDateInRange } from '../../shared/utils/fechas';
import { DateRangeComponent } from '../../shared/ui/date-range.component';
import { PaginadorComponent } from '../../shared/ui/paginador.component';
import { IconComponent } from '../../shared/ui/icon.component';

@Component({
  selector: 'app-compras',
  standalone: true,
  imports: [CommonModule, FechaPyPipe, FormsModule, DateRangeComponent, PaginadorComponent, IconComponent],
  template: `
    <div class="fade-in">
      <div class="section-card">
        <h3 class="section-title"><app-icon name="send" size="1em"/> Envíos a Compras Paraguai</h3>
        <app-date-range [(desde)]="fechaDesde" [(hasta)]="fechaHasta" label="Rango de envíos:" (changed)="applyFilters()" (cleared)="load()"></app-date-range>
        <div class="table-container">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Inicio</th>
                <th scope="col">Fin</th>
                <th scope="col">Modo</th>
                <th scope="col">Total</th>
                <th scope="col"><app-icon name="check" size="1em"/> OK</th>
                <th scope="col"><app-icon name="x" size="1em"/> Fallidos</th>
                <th scope="col">Error</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let run of sendRuns">
                <td>{{ run.startedAt | fechaPy:'yyyy-MM-dd HH:mm:ss' }}</td>
                <td>{{ run.finishedAt | fechaPy:'yyyy-MM-dd HH:mm:ss' }}</td>
                <td>{{ run.mode }}</td>
                <td>{{ run.total }}</td>
                <td class="text-ok">{{ run.sent }}</td>
                <td class="text-err">{{ run.failed }}</td>
                <td class="text-small">{{ run.error || '—' }}</td>
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
                <td class="text-small td-wrap">{{ e.mensaje }}</td>
              </tr>
            </tbody>
          </table>
          <div *ngIf="cargandoErrores && errorProducts.length === 0" class="empty-state"><span class="spinner" role="status" aria-label="Cargando"></span></div>
          <div *ngIf="!cargandoErrores && errorProducts.length === 0" class="empty-state text-ok"><app-icon name="check" size="1em"/> Sin errores</div>
        </div>
        <app-paginador [page]="erroresPage" [totalPages]="erroresTotalPages" [info]="erroresTotal + ' productos'" (go)="cargarErrores($event)"></app-paginador>
      </div>

      <!-- No disponibles en Compras -->
      <div class="section-card mt-6">
        <h3 class="section-title"><app-icon name="warn" size="1em"/> No disponibles en Compras ({{ noDisp?.totalCount ?? 0 }})</h3>
        <p class="text-small text-dim">
          Productos que tenemos con stock y que Compras Paraguai marca como <b>no disponibles</b>. El stock y el precio llegaron bien:
          la marca <code>available</code> la decide Compras, no el envío. Se actualiza con <code>Worker.Verify --disponibilidad</code>.
        </p>
        <div class="search-bar">
          <span class="text-small text-dim" *ngIf="noDisp?.checkedAt">
            Revisado el {{ noDisp?.checkedAt | fechaPy:'yyyy-MM-dd HH:mm' }} · Compras tenía {{ noDisp?.totalConStock }} productos con stock
          </span>
          <span class="text-small text-warn" *ngIf="noDisp && !noDisp.checkedAt">
            Todavía no se revisó. Corre <code>Worker.Verify --disponibilidad</code>.
          </span>
          <button type="button" class="btn btn-ghost btn-sm" (click)="descargarCsv()" [disabled]="!noDisp || noDisp.totalCount === 0">Descargar CSV</button>
        </div>
        <div class="table-container" *ngIf="noDisp && noDisp.items.length > 0">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Código</th>
                <th scope="col">Nombre</th>
                <th scope="col">Marca</th>
                <th scope="col">Precio</th>
                <th scope="col">Stock</th>
                <th scope="col">Actualizado en Compras</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let p of noDisp.items">
                <td class="text-bold">{{ p.code }}</td>
                <td class="td-wrap">{{ p.namePt }}</td>
                <td>{{ p.marca }}</td>
                <td>{{ p.price | currency:'':'symbol-narrow' }}</td>
                <td>{{ p.stockLocal }}</td>
                <td>{{ p.updatedAtApi | fechaPy:'yyyy-MM-dd HH:mm' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div *ngIf="cargandoNoDisp && !noDisp" class="empty-state"><span class="spinner" role="status" aria-label="Cargando"></span></div>
        <app-paginador *ngIf="noDisp" [page]="noDisp.page" [totalPages]="noDisp.totalPages" [info]="noDisp.totalCount + ' productos'" (go)="cargarNoDisponibles($event)"></app-paginador>
        <div *ngIf="noDisp && noDisp.checkedAt && noDisp.totalCount === 0" class="empty-state text-ok">
          <app-icon name="check" size="1em"/> Ninguno: todo lo que tiene stock figura disponible en Compras
        </div>
      </div>

      <!-- Verificaciones -->
      <div class="section-card mt-6">
        <h3 class="section-title"><app-icon name="check" size="1em"/> Verificaciones</h3>
        <div class="table-container">
          <table class="data-table" *ngIf="verifyRuns.length > 0">
            <thead>
              <tr>
                <th scope="col">Inicio</th>
                <th scope="col">Muestra</th>
                <th scope="col">Encontrados</th>
                <th scope="col">Precio OK</th>
                <th scope="col">Stock OK</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let v of verifyRuns" (click)="expandVerifyRun(v.id)" (keydown.enter)="expandVerifyRun(v.id)" tabindex="0" class="row-click" [class.row-selected]="verifyAbierta === v.id" [attr.aria-expanded]="verifyAbierta === v.id" title="Ver el detalle de esta verificación">
                <td>{{ v.startedAt | fechaPy:'yyyy-MM-dd HH:mm:ss' }}</td>
                <td>{{ v.sampleSize }}</td>
                <td>{{ v.found }}/{{ v.sampleSize }}</td>
                <td>{{ v.priceMatch }}</td>
                <td>{{ v.stockMatch }}</td>
              </tr>
            </tbody>
          </table>
          <div *ngIf="cargandoVerify && verifyRuns.length === 0" class="empty-state"><span class="spinner" role="status" aria-label="Cargando"></span></div>
          <div *ngIf="!cargandoVerify && verifyRuns.length === 0" class="empty-state">Sin verificaciones</div>
        </div>
        <div *ngIf="verifyDetalle.length > 0" class="mt-6">
          <h4 class="section-title">Detalle de la verificación #{{ verifyAbierta }} ({{ verifyDetalle.length }} productos)</h4>
          <div class="table-container">
            <table class="data-table">
              <thead>
                <tr>
                  <th scope="col">Código</th>
                  <th scope="col">Nombre</th>
                  <th scope="col">En Compras</th>
                  <th scope="col">Precio local / Compras</th>
                  <th scope="col">Stock local / Compras</th>
                  <th scope="col">Disponible</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let r of verifyDetalle">
                  <td class="text-bold">{{ r.code }}</td>
                  <td class="td-wrap">{{ r.namePt }}</td>
                  <td [class.text-err]="!r.found">{{ r.found ? 'Sí' : 'No' }}</td>
                  <td>{{ r.priceLocal }} / {{ r.priceApi ?? '—' }}</td>
                  <td>{{ r.stockLocal }} / {{ r.stockApi ?? '—' }}</td>
                  <td [class.text-warn]="r.availableApi === false">{{ r.availableApi === null || r.availableApi === undefined ? '—' : (r.availableApi ? 'Sí' : 'No') }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  `
})
export class ComprasComponent implements OnInit {
  sendRuns: SendRun[] = [];
  original: SendRun[] = [];
  filtered: SendRun[] = [];
  errorProducts: any[] = [];
  erroresPage = 1;
  erroresTotalPages = 1;
  erroresTotal = 0;
  cargando = true;
  cargandoErrores = true;
  cargandoNoDisp = true;
  cargandoVerify = true;
  verifyRuns: VerifyRun[] = [];
  verifyDetalle: VerifyResult[] = [];
  verifyAbierta: number | null = null;
  noDisp: NoDisponiblesList | null = null;
  private noDispPage = 1;

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
    this.productoService.getSendRuns('compras').subscribe(
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
    this.productoService.getVerifyRuns().subscribe(
      runs => {
        this.verifyRuns = runs;
        this.cargandoVerify = false;
      },
      () => this.cargandoVerify = false
    );
    this.cargarNoDisponibles(this.noDispPage);
  }

  /** Productos con stock que Compras marca como no disponibles (foto de Worker.Verify --disponibilidad). */
  cargarNoDisponibles(page: number) {
    this.noDispPage = Math.max(1, page);
    this.cargandoNoDisp = true;
    this.productoService.getNoDisponibles(this.noDispPage, 25).subscribe(
      r => {
        this.noDisp = r;
        this.cargandoNoDisp = false;
      },
      () => {
        this.cargandoNoDisp = false;
        this.errors.show('Error cargando los productos no disponibles en Compras');
      }
    );
  }

  /** CSV con todos los no disponibles (separador ';' y BOM para que Excel en español lo abra bien). */
  descargarCsv() {
    this.productoService.getNoDisponibles(1, 200).subscribe(
      r => {
        const esc = (v: unknown) => {
          const t = v === null || v === undefined ? '' : String(v);
          return /[;"\n]/.test(t) ? '"' + t.replace(/"/g, '""') + '"' : t;
        };
        // Coma decimal: con ';' como separador, Excel en español no entiende el punto.
        const num = (n: number | null | undefined) => n === null || n === undefined ? '' : String(n).replace('.', ',');
        const filas = r.items.map((p: NoDisponible) =>
          [p.code, p.namePt, p.marca, num(p.price), num(p.stockApi), p.stockLocal, fechaPy(p.updatedAtApi), p.link].map(esc).join(';'));
        const cabecera = 'codigo;nombre;marca;precio;stock_en_compras;stock_local;actualizado_en_compras;link';
        const csv = '﻿' + [cabecera, ...filas].join('\r\n') + '\r\n';
        const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
        const a = document.createElement('a');
        a.href = url;
        a.download = `compras_no_disponibles_${fechaPy(new Date(), 'yyyy-MM-dd')}.csv`;
        a.click();
        URL.revokeObjectURL(url);
      },
      () => this.errors.show('Error generando el CSV')
    );
  }

  /** Productos en Error de este destino, una página por vez (pueden ser miles). */
  cargarErrores(page: number) {
    this.erroresPage = Math.max(1, page);
    this.cargandoErrores = true;
    this.erroresService.getErrores('compras', {}, this.erroresPage, 15).subscribe(
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

  /** Muestra el detalle de una verificación; un segundo clic sobre la misma lo cierra. */
  expandVerifyRun(id: number) {
    if (this.verifyAbierta === id) {
      this.verifyAbierta = null;
      this.verifyDetalle = [];
      return;
    }
    this.productoService.getVerifyResults(id).subscribe(
      results => {
        this.verifyAbierta = id;
        this.verifyDetalle = results;
      },
      () => this.errors.show('Error cargando resultados de verificación')
    );
  }
}
