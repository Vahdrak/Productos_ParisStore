import { FechaPyPipe } from '../../shared/utils/fecha-py.pipe';
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import { ErroresService } from '../../shared/services/errores.service';
import { NoDisponible, NoDisponiblesList, ProductoService, SendRun, VerifyResult, VerifyRun } from '../../shared/services/producto.service';
import { ErrorService } from '../../shared/services/error.service';
import { fechaPy, isDateInRange, quickRange } from '../../shared/utils/fechas';
import { QuickRangeComponent } from '../../shared/ui/quick-range.component';

@Component({
  selector: 'app-compras',
  standalone: true,
  imports: [CommonModule, FechaPyPipe, FormsModule, QuickRangeComponent],
  template: `
    <div class="fade-in">
      <div class="section-card">
        <h3 class="section-title">📤 Envíos a Compras Paraguai</h3>
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

      <!-- No disponibles en Compras -->
      <div class="section-card mt-6">
        <h3 class="section-title">⚠️ No disponibles en Compras ({{ noDisp?.totalCount ?? 0 }})</h3>
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
          <button class="btn btn-ghost btn-sm" (click)="descargarCsv()" [disabled]="!noDisp || noDisp.totalCount === 0">Descargar CSV</button>
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
        <div *ngIf="noDisp && noDisp.totalPages > 1" class="pagination">
          <button [disabled]="noDisp.page === 1" (click)="cargarNoDisponibles(1)">«</button>
          <button [disabled]="noDisp.page === 1" (click)="cargarNoDisponibles(noDisp.page - 1)">‹</button>
          <span class="page-info">Pág {{ noDisp.page }}/{{ noDisp.totalPages }} · {{ noDisp.totalCount }} productos</span>
          <button [disabled]="noDisp.page === noDisp.totalPages" (click)="cargarNoDisponibles(noDisp.page + 1)">›</button>
          <button [disabled]="noDisp.page === noDisp.totalPages" (click)="cargarNoDisponibles(noDisp.totalPages)">»</button>
        </div>
        <div *ngIf="noDisp && noDisp.checkedAt && noDisp.totalCount === 0" class="empty-state">
          Ninguno: todo lo que tiene stock figura disponible en Compras ✅
        </div>
      </div>

      <!-- Verificaciones -->
      <div class="section-card mt-6">
        <h3 class="section-title">✔️ Verificaciones</h3>
        <div class="table-container">
          <table class="data-table" *ngIf="verifyRuns.length > 0">
            <thead>
              <tr>
                <th>Inicio</th>
                <th>Muestra</th>
                <th>Encontrados</th>
                <th>Precio OK</th>
                <th>Stock OK</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let v of verifyRuns" (click)="expandVerifyRun(v.id)" class="row-click" [class.row-selected]="verifyAbierta === v.id" title="Ver el detalle de esta verificación">
                <td>{{ v.startedAt | fechaPy:'yyyy-MM-dd HH:mm:ss' }}</td>
                <td>{{ v.sampleSize }}</td>
                <td>{{ v.found }}/{{ v.sampleSize }}</td>
                <td>{{ v.priceMatch }}</td>
                <td>{{ v.stockMatch }}</td>
              </tr>
            </tbody>
          </table>
          <div *ngIf="verifyRuns.length === 0" class="text-center py-8 text-dim">
            Sin verificaciones
          </div>
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
      },
      () => this.errors.show('Error cargando send runs')
    );
    this.cargarErrores(this.erroresPage);
    this.productoService.getVerifyRuns().subscribe(
      runs => this.verifyRuns = runs
    );
    this.cargarNoDisponibles(this.noDispPage);
  }

  /** Productos con stock que Compras marca como no disponibles (foto de Worker.Verify --disponibilidad). */
  cargarNoDisponibles(page: number) {
    this.noDispPage = Math.max(1, page);
    this.productoService.getNoDisponibles(this.noDispPage, 25).subscribe(
      r => this.noDisp = r,
      () => this.errors.show('Error cargando los productos no disponibles en Compras')
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
    this.erroresService.getErrores('compras', {}, this.erroresPage, 15).subscribe(
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
