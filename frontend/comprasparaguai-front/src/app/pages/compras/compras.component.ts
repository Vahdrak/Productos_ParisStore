import { FechaPyPipe } from '../../shared/utils/fecha-py.pipe';
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import { ErroresService } from '../../shared/services/errores.service';
import { NoDisponible, NoDisponiblesList, ProductoService, SendRun, VerifyResult, VerifyRun, VerifySolicitud } from '../../shared/services/producto.service';
import { AuthService } from '../../shared/services/auth.service';
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

      <!-- No disponibles en Compras -->
      <div class="section-card mt-6">
        <h3 class="section-title"><app-icon name="warn" size="1em"/> No disponibles en Compras ({{ noDisp?.totalCount ?? 0 }})</h3>
        <p class="text-small text-dim">
          Productos que tenemos con stock y que Compras Paraguai marca como <b>no disponibles</b>. El stock y el precio llegaron bien:
          la marca <code>available</code> la decide Compras, no el envío. Se actualiza cuando el worker de verificación consulta a Compras:
          aprieta <b>Revisar ahora</b> (requiere sesión iniciada).
        </p>
        <div class="search-bar">
          <button type="button" class="btn btn-sm" (click)="pedir('disponibilidad')"
                  [disabled]="!auth.logueado || !!abierta('disponibilidad') || pidiendo"
                  [title]="auth.logueado ? 'Pide al worker que consulte a Compras ahora' : 'Inicia sesión para pedir una revisión'">
            <app-icon name="refresh" size="1em"/> Revisar ahora
          </button>
          <ng-container *ngIf="estado('disponibilidad') as e">
            <span class="text-small" [ngClass]="e.clase" role="status">
              <span *ngIf="e.cargando" class="spinner" aria-hidden="true"></span> {{ e.texto }}
            </span>
          </ng-container>
          <span class="text-small text-dim" *ngIf="noDisp?.checkedAt">
            Revisado el {{ noDisp?.checkedAt | fechaPy:'yyyy-MM-dd HH:mm' }} · Compras tenía {{ noDisp?.totalConStock }} productos con stock
          </span>
          <span class="text-small text-warn" *ngIf="noDisp && !noDisp.checkedAt && !abierta('disponibilidad')">
            Todavía no se revisó.
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
        <p class="text-small text-dim">
          Toma 500 productos al azar marcados como Enviados y los busca uno por uno en Compras, para comprobar que llegaron con su precio y stock.
        </p>
        <div class="search-bar">
          <button type="button" class="btn btn-sm" (click)="pedir('muestra')"
                  [disabled]="!auth.logueado || !!abierta('muestra') || pidiendo"
                  [title]="auth.logueado ? 'Pide al worker que verifique una muestra ahora' : 'Inicia sesión para pedir una verificación'">
            <app-icon name="refresh" size="1em"/> Verificar muestra
          </button>
          <ng-container *ngIf="estado('muestra') as e">
            <span class="text-small" [ngClass]="e.clase" role="status">
              <span *ngIf="e.cargando" class="spinner" aria-hidden="true"></span> {{ e.texto }}
            </span>
          </ng-container>
        </div>
        <div class="table-container">
          <table class="data-table" *ngIf="verifyRuns.length > 0">
            <thead>
              <tr>
                <th scope="col">Inicio</th>
                <th scope="col">Muestra</th>
                <th scope="col" class="tono-ok">Encontrados</th>
                <th scope="col" class="tono-ok">Precio OK</th>
                <th scope="col" class="tono-ok">Stock OK</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let v of verifyRuns" (click)="expandVerifyRun(v.id)" (keydown.enter)="expandVerifyRun(v.id)" tabindex="0" class="row-click" [class.row-selected]="verifyAbierta === v.id" [attr.aria-expanded]="verifyAbierta === v.id" title="Ver el detalle de esta verificación">
                <td>{{ v.startedAt | fechaPy:'yyyy-MM-dd HH:mm:ss' }}</td>
                <td>{{ v.sampleSize }}</td>
                <td class="tono-ok">{{ v.found }}/{{ v.sampleSize }}</td>
                <td class="tono-ok">{{ v.priceMatch }}</td>
                <td class="tono-ok">{{ v.stockMatch }}</td>
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
                  <td [class.text-err]="!r.found" [class.tono-err]="!r.found" [class.tono-ok]="r.found">{{ r.found ? 'Sí' : 'No' }}</td>
                  <td>{{ r.priceLocal }} / {{ r.priceApi ?? '—' }}</td>
                  <td>{{ r.stockLocal }} / {{ r.stockApi ?? '—' }}</td>
                  <td [class.text-warn]="r.availableApi === false" [class.tono-rosa]="r.availableApi === false" [class.tono-ok]="r.availableApi === true">{{ r.availableApi === null || r.availableApi === undefined ? '—' : (r.availableApi ? 'Sí' : 'No') }}</td>
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

  solicitudes: VerifySolicitud[] = [];
  pidiendo = false;

  constructor(
    private productoService: ProductoService,
    private erroresService: ErroresService,
    private errors: ErrorService,
    public auth: AuthService
  ) {
    interval(30000).pipe(takeUntilDestroyed()).subscribe(() => this.load());
    // Mientras haya un pedido abierto se mira cada 4 s si el worker ya lo terminó.
    interval(4000).pipe(takeUntilDestroyed()).subscribe(() => { if (this.solicitudes.some(s => this.estaAbierta(s))) this.cargarSolicitudes(); });
  }

  private estaAbierta(s: VerifySolicitud) { return s.estado === 'pendiente' || s.estado === 'corriendo'; }

  /** El pedido abierto (pendiente o corriendo) de este tipo, si hay. */
  abierta(tipo: string): VerifySolicitud | undefined {
    return this.solicitudes.find(s => s.tipo === tipo && this.estaAbierta(s));
  }

  /** Texto y color para el último pedido de este tipo. Null si nunca se pidió. */
  estado(tipo: string): { texto: string; clase: string; cargando: boolean } | null {
    const s = this.solicitudes.find(x => x.tipo === tipo);
    if (!s) return null;
    const quien = s.origen === 'auto' ? 'automática' : (s.solicitadoPor ? `pedida por ${s.solicitadoPor}` : 'pedida desde el panel');
    switch (s.estado) {
      case 'pendiente':
        return { texto: 'Pedida; esperando al worker (si tarda, revisa que worker_verify_prod esté encendido)', clase: 'text-warn', cargando: true };
      case 'corriendo':
        return { texto: 'Consultando a Compras…', clase: 'text-dim', cargando: true };
      case 'ok':
        return { texto: `Última vez ${fechaPy(s.terminadoAt, 'yyyy-MM-dd HH:mm')} (${quien}): ${s.resultado ?? 'listo'}`, clase: 'text-dim', cargando: false };
      default:
        return { texto: `Falló (${fechaPy(s.terminadoAt, 'yyyy-MM-dd HH:mm')}): ${s.error ?? 'sin detalle'}`, clase: 'text-err', cargando: false };
    }
  }

  cargarSolicitudes() {
    const habiaAbiertas = this.solicitudes.some(s => this.estaAbierta(s));
    this.productoService.getVerifySolicitudes(10).subscribe(
      lista => {
        this.solicitudes = lista;
        // Si lo que estaba en curso terminó, refresca las tablas que ese pedido actualiza.
        if (habiaAbiertas && !lista.some(s => this.estaAbierta(s))) {
          this.cargarNoDisponibles(this.noDispPage);
          this.productoService.getVerifyRuns().subscribe(runs => this.verifyRuns = runs);
        }
      },
      () => { /* sin la cola el resto de la pantalla sigue funcionando */ }
    );
  }

  /** Pide una verificación al worker. Solo con sesión iniciada (el servidor también lo exige). */
  pedir(tipo: VerifySolicitud['tipo']) {
    if (!this.auth.logueado || this.pidiendo) return;
    this.pidiendo = true;
    this.productoService.solicitarVerify(tipo).subscribe(
      () => {
        this.pidiendo = false;
        this.cargarSolicitudes();
      },
      () => {
        this.pidiendo = false;
        this.errors.show('No se pudo pedir la verificación');
      }
    );
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
    this.cargarSolicitudes();
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
