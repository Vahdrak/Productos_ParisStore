import { Component, Input, OnChanges, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, forkJoin, of } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { Destino, EnvioService, Retenido, RetenidosList } from '../../shared/services/envio.service';
import { AuthService } from '../../shared/services/auth.service';
import { ErrorService } from '../../shared/services/error.service';
import { IconComponent } from '../../shared/ui/icon.component';
import { PaginadorComponent } from '../../shared/ui/paginador.component';

/**
 * Todo lo que hoy NO se envía a un destino (lo que cubre una pausa y los SKUs con excepción "retener"),
 * para revisarlo y liberar algunos. Liberar exige sesión y queda en el historial con el usuario.
 */
@Component({
  selector: 'app-retenidos',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, PaginadorComponent],
  template: `
    <div class="section-card">
      <h3 class="section-title"><app-icon name="lock" size="1em"/> Retenidos en {{ nombreDestino }} ({{ lista?.totalCount ?? 0 }})</h3>
      <p class="text-small text-dim">
        Todo lo que hoy <b>no se envía</b> a {{ nombreDestino }}: lo que cubre una pausa y los productos con la excepción «retener».
        Marca los que quieras y libéralos: a los de excepción se les quita la excepción; a los que retiene una pausa se les pone «enviar»
        (la excepción gana sobre la pausa, que sigue vigente para el resto).
      </p>

      <div class="search-bar">
        <input type="text" class="form-input" placeholder="Buscar código, nombre o marca..." aria-label="Buscar entre los retenidos"
               [(ngModel)]="q" (ngModelChange)="q$.next($event)" />
        <select class="form-input w-md" aria-label="Filtrar por qué los retiene" [(ngModel)]="origen" (ngModelChange)="irA(1)">
          <option value="">Retenidos por: todo</option>
          <option value="excepcion">Solo por excepción</option>
          <option value="pausa">Solo por pausa</option>
        </select>
        <select class="form-input w-md" aria-label="Retenidos por página" [(ngModel)]="pageSize" (ngModelChange)="irA(1)">
          <option [ngValue]="25">25 por página</option>
          <option [ngValue]="50">50 por página</option>
          <option [ngValue]="100">100 por página</option>
          <option [ngValue]="200">200 por página</option>
        </select>
        <span class="text-small text-dim" *ngIf="lista">{{ lista.porExcepcion }} por excepción · {{ lista.porPausa }} por pausa</span>
        <button type="button" class="btn btn-ghost btn-sm" (click)="cargar()" title="Volver a pedir la lista"><app-icon name="refresh" size="1em"/> Actualizar</button>
      </div>

      <div class="search-bar" *ngIf="lista && lista.items.length > 0">
        <input type="text" class="form-input w-md" placeholder="Motivo (opcional)" aria-label="Motivo de la liberación" [(ngModel)]="motivo" maxlength="200" />
        <button type="button" class="btn btn-primary" [disabled]="seleccion.size === 0" (click)="liberarSeleccion()"
                [title]="auth.logueado ? '' : 'Inicia sesión para modificar'">
          <app-icon name="check" size="1em"/> Liberar seleccionados ({{ seleccion.size }})
        </button>
        <span *ngIf="mensaje" class="text-small text-ok" role="status"><app-icon name="check" size="1em"/> {{ mensaje }}</span>
      </div>

      <div class="table-container" *ngIf="lista && lista.items.length > 0">
        <table class="data-table">
          <thead>
            <tr>
              <th scope="col" style="width: 36px;">
                <input type="checkbox" aria-label="Elegir todos los de esta página" [checked]="todosElegidos"
                       (change)="alternarPagina($any($event.target).checked)" />
              </th>
              <th scope="col">Código</th>
              <th scope="col">Nombre</th>
              <th scope="col">Marca</th>
              <th scope="col">Stock</th>
              <th scope="col">En {{ nombreDestino }}</th>
              <th scope="col">Lo retiene</th>
              <th scope="col"></th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let r of lista.items" [class.row-selected]="seleccion.has(r.code)">
              <td><input type="checkbox" [attr.aria-label]="'Elegir ' + r.code" [checked]="seleccion.has(r.code)"
                         (change)="alternar(r.code, $any($event.target).checked)" /></td>
              <td class="text-bold">{{ r.code }}</td>
              <td class="td-wrap">{{ r.namePt }}</td>
              <td>{{ r.marca }}</td>
              <td class="text-center">{{ r.stock }}</td>
              <td>{{ estadoTexto(r.estado) }}</td>
              <td class="td-wrap">
                <span class="badge" [class.badge-warn]="r.porExcepcion" [class.badge-secondary]="!r.porExcepcion">
                  {{ r.porExcepcion ? 'Excepción' : 'Pausa' }}
                </span>
                <span class="text-small text-dim">{{ r.porExcepcion ? (r.motivo || 'sin motivo') : r.retenidoPor }}</span>
              </td>
              <td>
                <button type="button" class="btn btn-ghost btn-sm" (click)="liberarUno(r)"
                        [title]="auth.logueado ? 'Liberar este producto' : 'Inicia sesión para modificar'">Liberar</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div *ngIf="cargando && !lista" class="empty-state"><span class="spinner" role="status" aria-label="Cargando"></span></div>
      <div *ngIf="!cargando && lista && lista.items.length === 0" class="empty-state">
        {{ q || origen ? 'Ningún retenido coincide con la búsqueda' : 'No hay nada retenido en ' + nombreDestino }}
      </div>
      <app-paginador *ngIf="lista" [page]="lista.page" [totalPages]="lista.totalPages" [info]="lista.totalCount + ' retenidos'" (go)="irA($event)"></app-paginador>
    </div>
  `
})
export class RetenidosComponent implements OnInit, OnChanges {
  @Input() target: Destino = 'compras';

  lista: RetenidosList | null = null;
  cargando = false;
  q = '';
  origen: '' | 'pausa' | 'excepcion' = '';
  pageSize = 50;
  motivo = '';
  mensaje = '';
  seleccion = new Set<string>();
  readonly q$ = new Subject<string>();
  private page = 1;

  constructor(private envio: EnvioService, private errors: ErrorService, public auth: AuthService) {
    this.q$.pipe(debounceTime(350), distinctUntilChanged(), takeUntilDestroyed()).subscribe(() => this.irA(1));
  }

  get nombreDestino(): string {
    return this.target === 'compras' ? 'Compras' : 'Compara';
  }

  ngOnInit() {
    this.cargar();
  }

  /** Al cambiar de destino se empieza de cero. */
  ngOnChanges(cambios: { [k: string]: unknown }) {
    if (cambios['target'] && !(cambios['target'] as { firstChange?: boolean }).firstChange) {
      this.lista = null;
      this.page = 1;
      this.mensaje = '';
      this.seleccion.clear();
      this.cargar();
    }
  }

  estadoTexto(estado: number): string {
    return estado === 0 ? 'Pendiente' : estado === 1 ? 'Enviado' : 'Error';
  }

  irA(page: number) {
    this.page = Math.max(1, page);
    this.seleccion.clear();
    this.cargar();
  }

  /** Vuelve a pedir la lista (también la usa quien cambie reglas desde fuera). */
  cargar() {
    this.cargando = true;
    this.envio.getRetenidos(this.target, this.origen, this.q.trim(), this.page, this.pageSize).subscribe(
      l => {
        this.lista = l;
        this.cargando = false;
      },
      () => {
        this.cargando = false;
        this.errors.show('Error cargando los productos retenidos');
      }
    );
  }

  get todosElegidos(): boolean {
    return !!this.lista && this.lista.items.length > 0 && this.lista.items.every(r => this.seleccion.has(r.code));
  }

  alternar(code: string, marcado: boolean) {
    if (marcado) this.seleccion.add(code); else this.seleccion.delete(code);
  }

  alternarPagina(marcado: boolean) {
    for (const r of this.lista?.items ?? []) this.alternar(r.code, marcado);
  }

  liberarUno(r: Retenido) {
    this.liberar([r], `¿Liberar ${r.code}?`);
  }

  liberarSeleccion() {
    const elegidos = (this.lista?.items ?? []).filter(r => this.seleccion.has(r.code));
    this.liberar(elegidos, `¿Liberar ${elegidos.length} productos en ${this.nombreDestino}?`);
  }

  private liberar(items: Retenido[], pregunta: string) {
    if (items.length === 0) return;
    if (!this.auth.logueado) {
      this.auth.pedirLogin = true;
      return;
    }
    const porExcepcion = items.filter(r => r.porExcepcion).map(r => r.code);
    const porPausa = items.filter(r => !r.porExcepcion).map(r => r.code);
    const detalle =
      (porExcepcion.length ? `\n• ${porExcepcion.length} con excepción «retener»: se les quita la excepción.` : '') +
      (porPausa.length ? `\n• ${porPausa.length} retenido(s) por una pausa: se les pone la excepción «enviar» (la pausa sigue para el resto).` : '');
    if (!confirm(pregunta + '\n' + detalle + '\n\nSe enviarán en el próximo ciclo del worker.')) return;

    forkJoin([
      porExcepcion.length ? this.envio.excepcionesLote({ target: this.target, accion: 'quitar', codes: porExcepcion }) : of({ afectados: 0 }),
      porPausa.length ? this.envio.excepcionesLote({ target: this.target, accion: 'enviar', codes: porPausa, motivo: this.motivo || undefined }) : of({ afectados: 0 })
    ]).subscribe(
      ([a, b]) => {
        this.mensaje = `${a.afectados + b.afectados} liberado(s) (${a.afectados} sin excepción, ${b.afectados} forzados a enviar).`;
        this.seleccion.clear();
        this.cargar();
      },
      e => {
        if (!String(e?.message ?? '').includes('401')) this.errors.show('No se pudieron liberar los productos');
      }
    );
  }
}
