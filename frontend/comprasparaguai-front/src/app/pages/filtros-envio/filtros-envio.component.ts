import { FechaPyPipe } from '../../shared/utils/fecha-py.pipe';
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { CatalogoService, CategoriaNodo, Marca } from '../../shared/services/catalogo.service';
import { Destino, EnvioImpacto, EnvioPausa, EnvioService, MarcaCategoria, ReglaHistorial } from '../../shared/services/envio.service';
import { ErrorService } from '../../shared/services/error.service';
import { AuthService } from '../../shared/services/auth.service';
import { IconComponent } from '../../shared/ui/icon.component';
import { PaginadorComponent } from '../../shared/ui/paginador.component';
import { RetenidosComponent } from './retenidos.component';

interface FilaArbol {
  nodo: CategoriaNodo;
  nivel: number;
}

/** Qué se va a pausar en el diálogo de confirmación. */
interface Dialogo {
  etiqueta: string;
  marcaId: number | null;
  categoriaId: number | null;
  impacto: EnvioImpacto | null;
  motivo: string;
  retirar: boolean;
  enviando: boolean;
}

@Component({
  selector: 'app-filtros-envio',
  standalone: true,
  imports: [CommonModule, FechaPyPipe, FormsModule, IconComponent, PaginadorComponent, RetenidosComponent],
  template: `
    <div class="fade-in">
      <div class="section-card">
        <div class="filter-tabs" role="tablist" aria-label="Destino">
          <button type="button" role="tab" [attr.aria-selected]="target === 'compras'" [class.active]="target === 'compras'" (click)="cambiarDestino('compras')">Compras Paraguai</button>
          <button type="button" role="tab" [attr.aria-selected]="target === 'compara'" [class.active]="target === 'compara'" (click)="cambiarDestino('compara')">Compara Paraguai</button>
        </div>
        <p class="text-dim text-small" style="margin: 0;">
          Pausa lo que <b>no</b> quieres enviar a este destino. Mientras esté pausado se sigue actualizando en la base de datos
          pero no se envía; al reactivarlo se envía con los datos más recientes. Una marca y una categoría se pueden combinar
          (por ejemplo, solo la ropa de una marca) y un producto puntual puede tener su propia excepción desde la pestaña Productos.
        </p>
        <p *ngIf="!auth.logueado" class="text-small text-warn" style="margin: 12px 0 0 0;">
          <app-icon name="lock" size="1em"/> Solo lectura: <button type="button" class="link" (click)="auth.pedirLogin = true" style="color: #93c5fd;">inicia sesión</button> para pausar o reactivar.
        </p>
      </div>

      <!-- PAUSAS ACTIVAS -->
      <div class="section-card">
        <h3 class="section-title"><app-icon name="ban" size="1em"/> Pausas de {{ nombreDestino }} ({{ pausas.length }})</h3>
        <div class="table-container" *ngIf="pausas.length > 0">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col" style="width: 30%;">Qué se pausa</th>
                <th scope="col">Motivo</th>
                <th scope="col" style="width: 90px;">Productos</th>
                <th scope="col" style="width: 120px;">Retira del destino</th>
                <th scope="col" style="width: 150px;">Creada por</th>
                <th scope="col" style="width: 130px;">Estado</th>
                <th scope="col" style="width: 70px;"><span class="sr-only">Acciones</span></th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let p of pausas" [style.opacity]="p.activo ? 1 : 0.55">
                <td>
                  <span class="badge badge-secondary">{{ tipoPausa(p) }}</span>
                  <span class="text-bold"> {{ descripcionPausa(p) }}</span>
                </td>
                <td class="text-small td-wrap">{{ p.motivo || '—' }}</td>
                <td class="text-center">{{ p.productos }}</td>
                <td class="text-center"><ng-container *ngIf="p.retirarDelDestino"><app-icon name="box" size="1em"/> Sí</ng-container><ng-container *ngIf="!p.retirarDelDestino">No</ng-container></td>
                <td class="text-small">{{ p.createdBy || '—' }}<div class="text-dim">{{ p.createdAt | fechaPy:'yyyy-MM-dd' }}</div></td>
                <td>
                  <button type="button" class="pill" [class.off]="p.activo" [class.on]="!p.activo" (click)="alternarPausa(p)"
                          [title]="p.activo ? 'Reactivar el envío' : 'Volver a pausar'">
                    <ng-container *ngIf="p.activo"><app-icon name="ban" size="1em"/> Pausada</ng-container>
                    <ng-container *ngIf="!p.activo"><app-icon name="check" size="1em"/> Enviando</ng-container>
                  </button>
                </td>
                <td><button type="button" class="btn btn-ghost btn-sm" (click)="borrarPausa(p)" title="Borrar la regla" aria-label="Borrar la regla"><app-icon name="trash" size="1em"/></button></td>
              </tr>
            </tbody>
          </table>
        </div>
        <div *ngIf="cargandoPausas && pausas.length === 0" class="empty-state"><span class="spinner" role="status" aria-label="Cargando"></span></div>
        <div *ngIf="!cargandoPausas && pausas.length === 0" class="empty-state">No hay pausas: se envía todo a {{ nombreDestino }}.</div>
      </div>

      <!-- RETENIDOS: todo lo que hoy no se envía, para revisarlo y liberar algunos -->
      <app-retenidos [target]="target"></app-retenidos>

      <!-- CATEGORÍAS -->
      <div class="section-card">
        <h3 class="section-title"><app-icon name="box" size="1em"/> Categorías</h3>
        <div class="search-bar">
          <input type="text" class="form-input" placeholder="Buscar categoría (ej. perfumes)..." aria-label="Buscar categoría" [(ngModel)]="catBusqueda" (ngModelChange)="construirFilas()" />
          <button type="button" class="btn btn-ghost" (click)="expandirTodo(false)" *ngIf="!catBusqueda">Contraer todo</button>
        </div>
        <div class="table-container" *ngIf="filas.length > 0">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Categoría</th>
                <th scope="col" style="width: 110px;">Productos</th>
                <th scope="col" style="width: 130px;">Pendientes</th>
                <th scope="col" style="width: 150px;">Envío a {{ nombreDestino }}</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let f of filas" [style.opacity]="heredada(f.nodo) ? 0.55 : 1">
                <td [style.padding-left.px]="8 + f.nivel * 22">
                  <button type="button" *ngIf="f.nodo.hijos.length > 0 && !catBusqueda" class="flecha" (click)="alternarExpandido(f.nodo)"
                          [attr.aria-expanded]="expandidos.has(f.nodo.id)" [attr.aria-label]="(expandidos.has(f.nodo.id) ? 'Contraer ' : 'Expandir ') + f.nodo.nombre">
                    {{ expandidos.has(f.nodo.id) ? '▾' : '▸' }}
                  </button>
                  <span *ngIf="f.nodo.hijos.length === 0 || catBusqueda" class="flecha"></span>
                  <span [class.text-bold]="f.nivel === 0">{{ f.nodo.nombre }}</span>
                  <span *ngIf="catBusqueda" class="text-small text-dim"> · {{ f.nodo.ruta }}</span>
                </td>
                <td>{{ f.nodo.productos }}</td>
                <td>{{ pendientesNodo(f.nodo) }}</td>
                <td>
                  <button type="button" *ngIf="heredada(f.nodo)" class="pill off" disabled title="Un nivel superior está pausado"><app-icon name="ban" size="1em"/> Heredado</button>
                  <button type="button" *ngIf="!heredada(f.nodo)" class="pill" [class.on]="!pausaIdNodo(f.nodo)" [class.off]="!!pausaIdNodo(f.nodo)"
                          (click)="alternarCategoria(f.nodo)">
                    <ng-container *ngIf="pausaIdNodo(f.nodo)"><app-icon name="ban" size="1em"/> Pausada</ng-container>
                    <ng-container *ngIf="!pausaIdNodo(f.nodo)"><app-icon name="check" size="1em"/> Enviando</ng-container>
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <div *ngIf="cargandoArbol && filas.length === 0" class="empty-state"><span class="spinner" role="status" aria-label="Cargando"></span></div>
        <div *ngIf="!cargandoArbol && filas.length === 0" class="empty-state">Sin categorías para mostrar</div>
      </div>

      <!-- MARCAS -->
      <div class="section-card">
        <h3 class="section-title"><app-icon name="tag" size="1em"/> Marcas</h3>
        <div class="search-bar">
          <input type="text" class="form-input" placeholder="Buscar marca (ej. lacoste)..." aria-label="Buscar marca" [(ngModel)]="marcaBusqueda" (ngModelChange)="busquedaMarca$.next($event)" />
          <span class="label-dim" *ngIf="marcasTotal">{{ marcasTotal }} marcas</span>
        </div>
        <div class="table-container" *ngIf="marcas.length > 0">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col">Marca</th>
                <th scope="col" style="width: 110px;">Productos</th>
                <th scope="col" style="width: 130px;">Pendientes</th>
                <th scope="col" style="width: 150px;">Envío a {{ nombreDestino }}</th>
                <th scope="col" style="width: 150px;"><span class="sr-only">Por categoría</span></th>
              </tr>
            </thead>
            <tbody>
              <ng-container *ngFor="let m of marcas">
                <tr>
                  <td class="text-bold">{{ m.nombre }}</td>
                  <td>{{ m.productos }}</td>
                  <td>{{ target === 'compras' ? m.pendientesCompras : m.pendientesCompara }}</td>
                  <td>
                    <button type="button" class="pill" [class.on]="!pausaIdMarca(m)" [class.off]="!!pausaIdMarca(m)" (click)="alternarMarca(m)">
                      <ng-container *ngIf="pausaIdMarca(m)"><app-icon name="ban" size="1em"/> Pausada</ng-container>
                      <ng-container *ngIf="!pausaIdMarca(m)"><app-icon name="check" size="1em"/> Enviando</ng-container>
                    </button>
                  </td>
                  <td>
                    <button type="button" class="btn btn-ghost btn-sm" (click)="alternarCategoriasDeMarca(m)"
                            [attr.aria-expanded]="marcaAbierta === m.id" title="Pausar esta marca solo en ciertas categorías">
                      {{ marcaAbierta === m.id ? '▾' : '▸' }} por categoría
                    </button>
                  </td>
                </tr>
                <tr *ngIf="marcaAbierta === m.id">
                  <td colspan="5" style="background: rgba(255,255,255,0.02); padding-left: 32px;">
                    <div *ngIf="categoriasMarca.length === 0" class="text-small text-dim"><span class="spinner" role="status" aria-label="Cargando"></span> Cargando...</div>
                    <table class="data-table" *ngIf="categoriasMarca.length > 0" style="margin: 4px 0;">
                      <tbody>
                        <tr *ngFor="let c of categoriasMarca">
                          <td [style.padding-left.px]="8 + (c.nivel - 2) * 22">{{ c.nombre }}</td>
                          <td style="width: 110px;">{{ c.productos }}</td>
                          <td style="width: 150px;">
                            <button type="button" *ngIf="pausaIdMarca(m)" class="pill off" disabled title="La marca completa está pausada"><app-icon name="ban" size="1em"/> Pausada (marca)</button>
                            <button type="button" *ngIf="!pausaIdMarca(m)" class="pill" [class.on]="!c.pausaId" [class.off]="!!c.pausaId" (click)="alternarMarcaEnCategoria(m, c)">
                              <ng-container *ngIf="c.pausaId"><app-icon name="ban" size="1em"/> Pausada</ng-container>
                              <ng-container *ngIf="!c.pausaId"><app-icon name="check" size="1em"/> Enviando</ng-container>
                            </button>
                          </td>
                        </tr>
                      </tbody>
                    </table>
                  </td>
                </tr>
              </ng-container>
            </tbody>
          </table>
        </div>
        <div *ngIf="cargandoMarcas && marcas.length === 0" class="empty-state"><span class="spinner" role="status" aria-label="Cargando"></span></div>
        <div *ngIf="!cargandoMarcas && marcas.length === 0" class="empty-state">Sin marcas</div>
        <app-paginador [page]="marcasPage" [totalPages]="marcasTotalPages" (go)="cargarMarcas($event)"></app-paginador>
      </div>

      <!-- HISTORIAL -->
      <div class="section-card">
        <h3 class="section-title"><app-icon name="history" size="1em"/> Historial de reglas de {{ nombreDestino }}</h3>
        <div class="table-container" *ngIf="historial.length > 0">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col" style="width: 160px;">Fecha</th>
                <th scope="col" style="width: 130px;">Usuario</th>
                <th scope="col" style="width: 120px;">Acción</th>
                <th scope="col">Regla</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let h of historial">
                <td>{{ h.changedAt | fechaPy:'yyyy-MM-dd HH:mm:ss' }}</td>
                <td>{{ h.usuario || '—' }}</td>
                <td><span class="badge" [ngClass]="claseAccion(h.accion)">{{ h.accion }}</span></td>
                <td class="td-wrap">
                  <span class="badge badge-secondary">{{ h.tabla === 'pausa' ? 'Pausa' : 'Excepción SKU' }}</span>
                  {{ h.detalle }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <div *ngIf="cargandoHistorial && historial.length === 0" class="empty-state"><span class="spinner" role="status" aria-label="Cargando"></span></div>
        <div *ngIf="!cargandoHistorial && historial.length === 0" class="empty-state">Aún no hay cambios de reglas</div>
        <app-paginador [page]="historialPage" [totalPages]="historialTotalPages" (go)="cargarHistorial($event)"></app-paginador>
      </div>

      <!-- CONFIRMAR PAUSA -->
      <div *ngIf="dlg" class="modal-fondo" (click)="dlg = null">
        <div class="modal-caja" role="dialog" aria-modal="true" aria-labelledby="dlg-titulo" (click)="$event.stopPropagation()">
          <h3 class="section-title" id="dlg-titulo">Pausar el envío a {{ nombreDestino }}</h3>
          <div><b>{{ dlg.etiqueta }}</b></div>
          <div *ngIf="!dlg.impacto" class="text-small text-dim"><span class="spinner" role="status" aria-label="Cargando"></span> Calculando el impacto...</div>
          <div *ngIf="dlg.impacto" class="text-small" style="line-height: 1.6;">
            Se retendrán <b>{{ dlg.impacto.productos }}</b> productos
            (<b>{{ dlg.impacto.pendientes }}</b> pendientes de envío; <b>{{ dlg.impacto.publicados }}</b> ya publicados en el destino).
            Se siguen actualizando en la base de datos, pero no se envían mientras dure la pausa.
          </div>
          <label class="text-small" style="display: flex; gap: 8px; align-items: flex-start;">
            <input type="checkbox" [(ngModel)]="dlg.retirar" style="margin-top: 3px;" />
            <span>Retirar del destino: enviar <b>stock 0</b> a lo ya publicado (no se mostrará como disponible). Al reactivar se reenvía con su stock real.</span>
          </label>
          <input type="text" class="form-input" placeholder="Motivo (opcional, ej. Temporada verano)" aria-label="Motivo de la pausa" [(ngModel)]="dlg.motivo" />
          <div class="modal-botones">
            <button type="button" class="btn btn-ghost" (click)="dlg = null">Cancelar</button>
            <button type="button" class="btn btn-primary" [disabled]="dlg.enviando || !dlg.impacto" (click)="confirmarPausa()">Pausar envío</button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .flecha {
      display: inline-block;
      width: 20px;
      background: none;
      border: none;
      color: var(--text-dim);
      cursor: pointer;
      padding: 0;
    }
  `]
})
export class FiltrosEnvioComponent implements OnInit {
  target: Destino = 'compras';

  pausas: EnvioPausa[] = [];

  cargandoPausas = true;
  cargandoArbol = true;
  cargandoMarcas = true;
  cargandoHistorial = true;

  // Categorías: árbol completo (sin la raíz) y filas visibles
  arbol: CategoriaNodo[] = [];
  expandidos = new Set<number>();
  filas: FilaArbol[] = [];
  catBusqueda = '';

  // Marcas
  marcas: Marca[] = [];
  marcaBusqueda = '';
  marcasPage = 1;
  marcasTotalPages = 1;
  marcasTotal = 0;
  marcaAbierta: number | null = null;
  categoriasMarca: MarcaCategoria[] = [];
  readonly busquedaMarca$ = new Subject<string>();

  // Historial de reglas
  historial: ReglaHistorial[] = [];
  historialPage = 1;
  historialTotalPages = 1;

  dlg: Dialogo | null = null;

  constructor(
    private catalogo: CatalogoService,
    private envio: EnvioService,
    private errors: ErrorService,
    public auth: AuthService
  ) {
    this.busquedaMarca$.pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => this.cargarMarcas(1));
  }

  get nombreDestino(): string {
    return this.target === 'compras' ? 'Compras Paraguai' : 'Compara Paraguai';
  }

  ngOnInit() {
    this.recargarTodo();
  }

  cambiarDestino(t: Destino) {
    this.target = t;
    this.marcaAbierta = null;
    this.recargarTodo();
  }

  recargarTodo() {
    this.cargarPausas();
    this.cargarArbol();
    this.cargarMarcas(this.marcasPage);
    this.cargarHistorial(1);
    if (this.marcaAbierta !== null) this.cargarCategoriasDeMarca(this.marcaAbierta);
  }

  // ---------------------------------------------------------------- carga

  cargarPausas() {
    this.cargandoPausas = true;
    this.envio.getPausas(this.target).subscribe(
      p => {
        this.pausas = p;
        this.cargandoPausas = false;
      },
      () => {
        this.cargandoPausas = false;
        this.errors.show('Error cargando las pausas');
      }
    );
  }

  cargarArbol() {
    this.cargandoArbol = true;
    this.catalogo.getArbolCategorias().subscribe(
      raices => {
        this.arbol = raices[0]?.hijos ?? [];   // sin la raíz "TOKU IMPORTADOS"
        this.construirFilas();
        this.cargandoArbol = false;
      },
      () => {
        this.cargandoArbol = false;
        this.errors.show('Error cargando las categorías');
      }
    );
  }

  cargarMarcas(page: number) {
    this.marcasPage = Math.max(1, page);
    this.cargandoMarcas = true;
    this.catalogo.getMarcas(this.marcaBusqueda.trim(), this.marcasPage, 15).subscribe(
      lista => {
        this.marcas = lista.items;
        this.marcasTotalPages = lista.totalPages || 1;
        this.marcasTotal = lista.totalCount;
        this.cargandoMarcas = false;
      },
      () => {
        this.cargandoMarcas = false;
        this.errors.show('Error cargando las marcas');
      }
    );
  }

  cargarHistorial(page: number) {
    this.historialPage = Math.max(1, page);
    this.cargandoHistorial = true;
    this.envio.getHistorial(this.target, this.historialPage, 15).subscribe(
      h => {
        this.historial = h.items;
        this.historialTotalPages = h.totalPages || 1;
        this.cargandoHistorial = false;
      },
      () => {
        this.cargandoHistorial = false;
        this.errors.show('Error cargando el historial de reglas');
      }
    );
  }

  private cargarCategoriasDeMarca(marcaId: number) {
    this.envio.getCategoriasDeMarca(marcaId, this.target).subscribe(
      c => this.categoriasMarca = c,
      () => this.errors.show('Error cargando las categorías de la marca')
    );
  }

  // --------------------------------------------------------------- árbol

  construirFilas() {
    const filas: FilaArbol[] = [];
    const texto = this.catBusqueda.trim().toLowerCase();

    const visitar = (nodos: CategoriaNodo[], nivel: number) => {
      for (const n of nodos) {
        if (texto) {
          if (n.nombre.toLowerCase().includes(texto) && filas.length < 300) filas.push({ nodo: n, nivel: 0 });
          visitar(n.hijos, 0);
        } else {
          filas.push({ nodo: n, nivel });
          if (n.hijos.length > 0 && this.expandidos.has(n.id)) visitar(n.hijos, nivel + 1);
        }
      }
    };
    visitar(this.arbol, 0);
    this.filas = filas;
  }

  alternarExpandido(n: CategoriaNodo) {
    if (this.expandidos.has(n.id)) this.expandidos.delete(n.id); else this.expandidos.add(n.id);
    this.construirFilas();
  }

  expandirTodo(abrir: boolean) {
    this.expandidos.clear();
    if (abrir) {
      const abrirNodos = (nodos: CategoriaNodo[]) => nodos.forEach(n => { this.expandidos.add(n.id); abrirNodos(n.hijos); });
      abrirNodos(this.arbol);
    }
    this.construirFilas();
  }

  pausaIdNodo(n: CategoriaNodo): number | null {
    return (this.target === 'compras' ? n.pausaIdCompras : n.pausaIdCompara) ?? null;
  }

  heredada(n: CategoriaNodo): boolean {
    return this.target === 'compras' ? n.heredadaCompras : n.heredadaCompara;
  }

  pendientesNodo(n: CategoriaNodo): number {
    return this.target === 'compras' ? n.pendientesCompras : n.pendientesCompara;
  }

  pausaIdMarca(m: Marca): number | null {
    return (this.target === 'compras' ? m.pausaIdCompras : m.pausaIdCompara) ?? null;
  }

  // ------------------------------------------------------------ acciones

  /** Escribir exige sesión: sin ella se abre el login. */
  private requiereSesion(): boolean {
    if (this.auth.logueado) return true;
    this.auth.pedirLogin = true;
    return false;
  }

  alternarCategoria(n: CategoriaNodo) {
    if (!this.requiereSesion()) return;
    const id = this.pausaIdNodo(n);
    if (id) this.reactivar(id);
    else this.abrirDialogo(`Categoría: ${n.ruta}`, null, n.id);
  }

  alternarMarca(m: Marca) {
    if (!this.requiereSesion()) return;
    const id = this.pausaIdMarca(m);
    if (id) this.reactivar(id);
    else this.abrirDialogo(`Marca: ${m.nombre} (en todas sus categorías)`, m.id, null);
  }

  alternarMarcaEnCategoria(m: Marca, c: MarcaCategoria) {
    if (!this.requiereSesion()) return;
    if (c.pausaId) this.reactivar(c.pausaId);
    else this.abrirDialogo(`${m.nombre} solo en ${c.ruta}`, m.id, c.id);
  }

  alternarCategoriasDeMarca(m: Marca) {
    if (this.marcaAbierta === m.id) {
      this.marcaAbierta = null;
      return;
    }
    this.marcaAbierta = m.id;
    this.categoriasMarca = [];
    this.cargarCategoriasDeMarca(m.id);
  }

  alternarPausa(p: EnvioPausa) {
    if (!this.requiereSesion()) return;
    if (p.activo) {
      this.reactivar(p.id);
    } else {
      // volver a pausar una regla apagada
      this.envio.actualizarPausa(p.id, { activo: true }).subscribe(
        () => this.recargarTodo(),
        e => this.errorEscritura(e)
      );
    }
  }

  borrarPausa(p: EnvioPausa) {
    if (!this.requiereSesion()) return;
    if (!confirm(`¿Borrar la regla "${this.descripcionPausa(p)}"? Si estaba activa, esos productos vuelven a enviarse.`)) return;
    this.envio.borrarPausa(p.id).subscribe(
      () => this.recargarTodo(),
      e => this.errorEscritura(e)
    );
  }

  /** Reactivar el envío = apagar la pausa (la regla queda guardada para la próxima temporada). */
  private reactivar(pausaId: number) {
    this.envio.actualizarPausa(pausaId, { activo: false }).subscribe(
      () => this.recargarTodo(),
      e => this.errorEscritura(e)
    );
  }

  private abrirDialogo(etiqueta: string, marcaId: number | null, categoriaId: number | null) {
    this.dlg = { etiqueta, marcaId, categoriaId, impacto: null, motivo: '', retirar: false, enviando: false };
    const dlg = this.dlg;
    this.envio.getImpacto(this.target, marcaId, categoriaId).subscribe(
      i => dlg.impacto = i,
      () => { this.dlg = null; this.errors.show('Error calculando el impacto de la pausa'); }
    );
  }

  confirmarPausa() {
    const d = this.dlg;
    if (!d) return;
    d.enviando = true;
    this.envio.crearPausa({
      target: this.target,
      marcaId: d.marcaId,
      categoriaId: d.categoriaId,
      motivo: d.motivo.trim() || undefined,
      retirarDelDestino: d.retirar
    }).subscribe(
      () => {
        this.dlg = null;
        this.recargarTodo();
      },
      e => {
        d.enviando = false;
        this.errorEscritura(e);
      }
    );
  }

  private errorEscritura(e: any) {
    // El 401 ya abre el login desde el interceptor; los demás errores se muestran.
    if (!String(e?.message ?? '').includes('401')) this.errors.show('No se pudo guardar el cambio');
  }

  // ------------------------------------------------------------- textos

  tipoPausa(p: EnvioPausa): string {
    return p.marcaId && p.categoriaId ? 'Marca en categoría' : p.marcaId ? 'Marca' : 'Categoría';
  }

  descripcionPausa(p: EnvioPausa): string {
    const categoria = p.categoriaRuta ? p.categoriaRuta.split('>').slice(1).join(' > ') : '';
    return [p.marca, categoria].filter(x => !!x).join(' en ');
  }

  claseAccion(accion: string): any {
    return {
      'badge-success': accion === 'activar' || accion === 'crear',
      'badge-warn': accion === 'desactivar',
      'badge-error': accion === 'borrar'
    };
  }
}
