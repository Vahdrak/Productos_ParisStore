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
  imports: [CommonModule, FechaPyPipe, FormsModule],
  template: `
    <div class="fade-in">
      <div class="section-card">
        <div class="filter-tabs">
          <button [class.active]="target === 'compras'" (click)="cambiarDestino('compras')">Compras Paraguai</button>
          <button [class.active]="target === 'compara'" (click)="cambiarDestino('compara')">Compara Paraguai</button>
        </div>
        <p class="text-dim text-small" style="margin: 0;">
          Pausa lo que <b>no</b> quieres enviar a este destino. Mientras esté pausado se sigue actualizando en la base de datos
          pero no se envía; al reactivarlo se envía con los datos más recientes. Una marca y una categoría se pueden combinar
          (por ejemplo, solo la ropa de una marca) y un producto puntual puede tener su propia excepción desde la pestaña Productos.
        </p>
        <p *ngIf="!auth.logueado" class="text-small" style="margin: 12px 0 0 0; color: #fbbf24;">
          🔒 Solo lectura: <a href="javascript:void(0)" (click)="auth.pedirLogin = true" style="color: #93c5fd;">inicia sesión</a> para pausar o reactivar.
        </p>
      </div>

      <!-- PAUSAS ACTIVAS -->
      <div class="section-card">
        <h3 class="section-title">⏸ Pausas de {{ nombreDestino }} ({{ pausas.length }})</h3>
        <div class="table-container" *ngIf="pausas.length > 0">
          <table class="data-table">
            <thead>
              <tr>
                <th style="width: 30%;">Qué se pausa</th>
                <th>Motivo</th>
                <th style="width: 90px;">Productos</th>
                <th style="width: 120px;">Retira del destino</th>
                <th style="width: 150px;">Creada por</th>
                <th style="width: 130px;">Estado</th>
                <th style="width: 70px;"></th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let p of pausas" [style.opacity]="p.activo ? 1 : 0.55">
                <td>
                  <span class="badge badge-secondary">{{ tipoPausa(p) }}</span>
                  <span class="text-bold"> {{ descripcionPausa(p) }}</span>
                </td>
                <td class="text-small" style="white-space: normal;">{{ p.motivo || '—' }}</td>
                <td class="text-center">{{ p.productos }}</td>
                <td class="text-center">{{ p.retirarDelDestino ? '📦 Sí' : 'No' }}</td>
                <td class="text-small">{{ p.createdBy || '—' }}<div class="text-dim">{{ p.createdAt | fechaPy:'yyyy-MM-dd' }}</div></td>
                <td>
                  <button class="pill" [class.off]="p.activo" [class.on]="!p.activo" (click)="alternarPausa(p)"
                          [title]="p.activo ? 'Reactivar el envío' : 'Volver a pausar'">
                    {{ p.activo ? '⏸ Pausada' : '✅ Enviando' }}
                  </button>
                </td>
                <td><button class="btn btn-ghost btn-sm" (click)="borrarPausa(p)" title="Borrar la regla">🗑</button></td>
              </tr>
            </tbody>
          </table>
        </div>
        <div *ngIf="pausas.length === 0" class="text-center py-8 text-dim">No hay pausas: se envía todo a {{ nombreDestino }}.</div>
      </div>

      <!-- CATEGORÍAS -->
      <div class="section-card">
        <h3 class="section-title">🗂 Categorías</h3>
        <div class="search-bar">
          <input type="text" class="form-input" placeholder="Buscar categoría (ej. perfumes)..." [(ngModel)]="catBusqueda" (ngModelChange)="construirFilas()" />
          <button class="btn btn-ghost" (click)="expandirTodo(false)" *ngIf="!catBusqueda">Contraer todo</button>
        </div>
        <div class="table-container" *ngIf="filas.length > 0">
          <table class="data-table">
            <thead>
              <tr>
                <th>Categoría</th>
                <th style="width: 110px;">Productos</th>
                <th style="width: 130px;">Pendientes</th>
                <th style="width: 150px;">Envío a {{ nombreDestino }}</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let f of filas" [style.opacity]="heredada(f.nodo) ? 0.55 : 1">
                <td [style.padding-left.px]="8 + f.nivel * 22">
                  <button *ngIf="f.nodo.hijos.length > 0 && !catBusqueda" class="flecha" (click)="alternarExpandido(f.nodo)">
                    {{ expandidos.has(f.nodo.id) ? '▾' : '▸' }}
                  </button>
                  <span *ngIf="f.nodo.hijos.length === 0 || catBusqueda" class="flecha"></span>
                  <span [class.text-bold]="f.nivel === 0">{{ f.nodo.nombre }}</span>
                  <span *ngIf="catBusqueda" class="text-small text-dim"> · {{ f.nodo.ruta }}</span>
                </td>
                <td>{{ f.nodo.productos }}</td>
                <td>{{ pendientesNodo(f.nodo) }}</td>
                <td>
                  <button *ngIf="heredada(f.nodo)" class="pill off" disabled title="Un nivel superior está pausado">⏸ Heredado</button>
                  <button *ngIf="!heredada(f.nodo)" class="pill" [class.on]="!pausaIdNodo(f.nodo)" [class.off]="!!pausaIdNodo(f.nodo)"
                          (click)="alternarCategoria(f.nodo)">
                    {{ pausaIdNodo(f.nodo) ? '⏸ Pausada' : '✅ Enviando' }}
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <div *ngIf="filas.length === 0" class="text-center py-8 text-dim">Sin categorías para mostrar</div>
      </div>

      <!-- MARCAS -->
      <div class="section-card">
        <h3 class="section-title">🏷 Marcas</h3>
        <div class="search-bar">
          <input type="text" class="form-input" placeholder="Buscar marca (ej. lacoste)..." [(ngModel)]="marcaBusqueda" (ngModelChange)="busquedaMarca$.next($event)" />
          <span class="label-dim" *ngIf="marcasTotal">{{ marcasTotal }} marcas</span>
        </div>
        <div class="table-container" *ngIf="marcas.length > 0">
          <table class="data-table">
            <thead>
              <tr>
                <th>Marca</th>
                <th style="width: 110px;">Productos</th>
                <th style="width: 130px;">Pendientes</th>
                <th style="width: 150px;">Envío a {{ nombreDestino }}</th>
                <th style="width: 150px;"></th>
              </tr>
            </thead>
            <tbody>
              <ng-container *ngFor="let m of marcas">
                <tr>
                  <td class="text-bold">{{ m.nombre }}</td>
                  <td>{{ m.productos }}</td>
                  <td>{{ target === 'compras' ? m.pendientesCompras : m.pendientesCompara }}</td>
                  <td>
                    <button class="pill" [class.on]="!pausaIdMarca(m)" [class.off]="!!pausaIdMarca(m)" (click)="alternarMarca(m)">
                      {{ pausaIdMarca(m) ? '⏸ Pausada' : '✅ Enviando' }}
                    </button>
                  </td>
                  <td>
                    <button class="btn btn-ghost btn-sm" (click)="alternarCategoriasDeMarca(m)"
                            title="Pausar esta marca solo en ciertas categorías">
                      {{ marcaAbierta === m.id ? '▾' : '▸' }} por categoría
                    </button>
                  </td>
                </tr>
                <tr *ngIf="marcaAbierta === m.id">
                  <td colspan="5" style="background: rgba(255,255,255,0.02); padding-left: 32px;">
                    <div *ngIf="categoriasMarca.length === 0" class="text-small text-dim">Cargando...</div>
                    <table class="data-table" *ngIf="categoriasMarca.length > 0" style="margin: 4px 0;">
                      <tbody>
                        <tr *ngFor="let c of categoriasMarca">
                          <td [style.padding-left.px]="8 + (c.nivel - 2) * 22">{{ c.nombre }}</td>
                          <td style="width: 110px;">{{ c.productos }}</td>
                          <td style="width: 150px;">
                            <button *ngIf="pausaIdMarca(m)" class="pill off" disabled title="La marca completa está pausada">⏸ Pausada (marca)</button>
                            <button *ngIf="!pausaIdMarca(m)" class="pill" [class.on]="!c.pausaId" [class.off]="!!c.pausaId" (click)="alternarMarcaEnCategoria(m, c)">
                              {{ c.pausaId ? '⏸ Pausada' : '✅ Enviando' }}
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
        <div *ngIf="marcas.length === 0" class="text-center py-8 text-dim">Sin marcas</div>
        <div *ngIf="marcasTotalPages > 1" class="pagination">
          <button [disabled]="marcasPage === 1" (click)="cargarMarcas(1)">«</button>
          <button [disabled]="marcasPage === 1" (click)="cargarMarcas(marcasPage - 1)">‹</button>
          <span class="page-info">Pág {{ marcasPage }}/{{ marcasTotalPages }}</span>
          <button [disabled]="marcasPage === marcasTotalPages" (click)="cargarMarcas(marcasPage + 1)">›</button>
          <button [disabled]="marcasPage === marcasTotalPages" (click)="cargarMarcas(marcasTotalPages)">»</button>
        </div>
      </div>

      <!-- HISTORIAL -->
      <div class="section-card">
        <h3 class="section-title">🕘 Historial de reglas de {{ nombreDestino }}</h3>
        <div class="table-container" *ngIf="historial.length > 0">
          <table class="data-table">
            <thead>
              <tr>
                <th style="width: 160px;">Fecha</th>
                <th style="width: 130px;">Usuario</th>
                <th style="width: 120px;">Acción</th>
                <th>Regla</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let h of historial">
                <td>{{ h.changedAt | fechaPy:'yyyy-MM-dd HH:mm:ss' }}</td>
                <td>{{ h.usuario || '—' }}</td>
                <td><span class="badge" [ngClass]="claseAccion(h.accion)">{{ h.accion }}</span></td>
                <td style="white-space: normal;">
                  <span class="badge badge-secondary">{{ h.tabla === 'pausa' ? 'Pausa' : 'Excepción SKU' }}</span>
                  {{ h.detalle }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <div *ngIf="historial.length === 0" class="text-center py-8 text-dim">Aún no hay cambios de reglas</div>
        <div *ngIf="historialTotalPages > 1" class="pagination">
          <button [disabled]="historialPage === 1" (click)="cargarHistorial(historialPage - 1)">‹</button>
          <span class="page-info">Pág {{ historialPage }}/{{ historialTotalPages }}</span>
          <button [disabled]="historialPage === historialTotalPages" (click)="cargarHistorial(historialPage + 1)">›</button>
        </div>
      </div>

      <!-- CONFIRMAR PAUSA -->
      <div *ngIf="dlg" class="modal-fondo" (click)="dlg = null">
        <div class="modal-caja" (click)="$event.stopPropagation()">
          <h3 class="section-title">Pausar el envío a {{ nombreDestino }}</h3>
          <div><b>{{ dlg.etiqueta }}</b></div>
          <div *ngIf="!dlg.impacto" class="text-small text-dim">Calculando el impacto...</div>
          <div *ngIf="dlg.impacto" class="text-small" style="line-height: 1.6;">
            Se retendrán <b>{{ dlg.impacto.productos }}</b> productos
            (<b>{{ dlg.impacto.pendientes }}</b> pendientes de envío; <b>{{ dlg.impacto.publicados }}</b> ya publicados en el destino).
            Se siguen actualizando en la base de datos, pero no se envían mientras dure la pausa.
          </div>
          <label class="text-small" style="display: flex; gap: 8px; align-items: flex-start;">
            <input type="checkbox" [(ngModel)]="dlg.retirar" style="margin-top: 3px;" />
            <span>Retirar del destino: enviar <b>stock 0</b> a lo ya publicado (no se mostrará como disponible). Al reactivar se reenvía con su stock real.</span>
          </label>
          <input type="text" class="form-input" placeholder="Motivo (opcional, ej. Temporada verano)" [(ngModel)]="dlg.motivo" />
          <div class="modal-botones">
            <button class="btn btn-ghost" (click)="dlg = null">Cancelar</button>
            <button class="btn btn-primary" [disabled]="dlg.enviando || !dlg.impacto" (click)="confirmarPausa()">Pausar envío</button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .pill {
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 999px;
      padding: 4px 12px;
      font-size: 12px;
      font-weight: 600;
      cursor: pointer;
      background: transparent;
      white-space: nowrap;
    }
    .pill.on { background: rgba(74, 222, 128, 0.15); color: #4ade80; border-color: rgba(74, 222, 128, 0.4); }
    .pill.off { background: rgba(245, 158, 11, 0.15); color: #fbbf24; border-color: rgba(245, 158, 11, 0.4); }
    .pill:disabled { cursor: not-allowed; opacity: 0.8; }
    .flecha {
      display: inline-block;
      width: 20px;
      background: none;
      border: none;
      color: #8f8f8f;
      cursor: pointer;
      padding: 0;
    }
    .modal-fondo {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.6);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 100;
    }
    .modal-caja {
      background: #131313;
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 8px;
      padding: 24px;
      width: 460px;
      max-width: 92vw;
      display: flex;
      flex-direction: column;
      gap: 14px;
    }
    .modal-botones { display: flex; justify-content: flex-end; gap: 8px; }
  `]
})
export class FiltrosEnvioComponent implements OnInit {
  target: Destino = 'compras';

  pausas: EnvioPausa[] = [];

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
    this.envio.getPausas(this.target).subscribe(
      p => this.pausas = p,
      () => this.errors.show('Error cargando las pausas')
    );
  }

  cargarArbol() {
    this.catalogo.getArbolCategorias().subscribe(
      raices => {
        this.arbol = raices[0]?.hijos ?? [];   // sin la raíz "TOKU IMPORTADOS"
        this.construirFilas();
      },
      () => this.errors.show('Error cargando las categorías')
    );
  }

  cargarMarcas(page: number) {
    this.marcasPage = Math.max(1, page);
    this.catalogo.getMarcas(this.marcaBusqueda.trim(), this.marcasPage, 15).subscribe(
      lista => {
        this.marcas = lista.items;
        this.marcasTotalPages = lista.totalPages || 1;
        this.marcasTotal = lista.totalCount;
      },
      () => this.errors.show('Error cargando las marcas')
    );
  }

  cargarHistorial(page: number) {
    this.historialPage = Math.max(1, page);
    this.envio.getHistorial(this.target, this.historialPage, 15).subscribe(
      h => {
        this.historial = h.items;
        this.historialTotalPages = h.totalPages || 1;
      },
      () => this.errors.show('Error cargando el historial de reglas')
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
