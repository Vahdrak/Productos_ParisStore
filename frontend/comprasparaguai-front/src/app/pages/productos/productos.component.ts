import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProductoService } from '../../shared/services/producto.service';
import { CatalogoService } from '../../shared/services/catalogo.service';
import { Cambio, CambiosService } from '../../shared/services/cambios.service';
import { ErrorService } from '../../shared/services/error.service';
import { AuthService } from '../../shared/services/auth.service';
import { Destino, EnvioEstadoProducto, EnvioService } from '../../shared/services/envio.service';
import { statusClass } from '../../shared/utils/estado';
import { MarcaPickerComponent, MarcaSeleccion } from '../../shared/ui/marca-picker.component';
import { CambiosListaComponent } from '../../shared/ui/cambios-lista.component';
import { IconComponent } from '../../shared/ui/icon.component';
import { PaginadorComponent } from '../../shared/ui/paginador.component';

@Component({
  selector: 'app-productos',
  standalone: true,
  imports: [CommonModule, FormsModule, MarcaPickerComponent, CambiosListaComponent, IconComponent, PaginadorComponent],
  template: `
    <div class="fade-in">
      <div class="search-bar">
        <input type="text" placeholder="Buscar código..." aria-label="Buscar por código" [(ngModel)]="filterCode" (keyup.enter)="search()" class="form-input" />
        <input type="text" placeholder="Buscar nombre..." aria-label="Buscar por nombre" [(ngModel)]="filterName" (keyup.enter)="search()" class="form-input" />
        <app-marca-picker placeholder="Marca (ej. NIKE)..." (marcaChange)="onMarca($event)"></app-marca-picker>
        <select [(ngModel)]="filterCategoriaId" (ngModelChange)="search()" class="form-input w-md" aria-label="Filtrar por departamento">
          <option [ngValue]="null">Departamento: Todos</option>
          <option *ngFor="let d of departamentos" [ngValue]="d.id">{{ d.nombre }} ({{ d.productos }})</option>
        </select>
        <button type="button" (click)="search()" class="btn btn-primary">Buscar</button>
      </div>

      <div class="search-bar">
        <select [(ngModel)]="filterTarget" (ngModelChange)="search()" class="form-input w-md" aria-label="Filtrar por destino">
          <option value="">Destino: Todos</option>
          <option value="compras">Compras</option>
          <option value="compara">Compara</option>
        </select>
        <select [(ngModel)]="filterRegimen" (ngModelChange)="search()" class="form-input w-md" aria-label="Filtrar por régimen">
          <option value="">Régimen: Todos</option>
          <option value="true">Con régimen turismo</option>
          <option value="false">Normal (sin régimen)</option>
        </select>
        <select [(ngModel)]="filterState" (ngModelChange)="search()" class="form-input w-md" aria-label="Filtrar por estado">
          <option value="">Estado: Todos</option>
          <option value="0">Pendiente</option>
          <option value="1">Enviado</option>
          <option value="2">Error</option>
        </select>
      </div>

      <div class="search-bar" *ngIf="productList && productList.items.length > 0">
        <select [(ngModel)]="pageSize" (ngModelChange)="search()" class="form-input w-md" aria-label="Productos por página">
          <option [ngValue]="10">10 por página</option>
          <option [ngValue]="25">25 por página</option>
          <option [ngValue]="50">50 por página</option>
          <option [ngValue]="100">100 por página</option>
        </select>
        <span class="label-dim">{{ productList.totalCount }} productos</span>
      </div>

      <!-- Reenviar: vuelve a Pendiente lo ya enviado o con error, para que el worker lo envíe de nuevo -->
      <div class="search-bar" *ngIf="productList && productList.items.length > 0">
        <span class="label-dim"><app-icon name="refresh"></app-icon> Reenviar a:</span>
        <select [(ngModel)]="destinoReenvio" class="form-input w-md" aria-label="Destino del reenvío">
          <option value="compras">Compras</option>
          <option value="compara">Compara</option>
          <option value="ambos">Ambos destinos</option>
        </select>
        <button type="button" class="btn btn-primary" [disabled]="seleccion.size === 0" (click)="reenviarSeleccion()"
                [title]="auth.logueado ? '' : 'Inicia sesión para modificar'"><app-icon name="refresh"></app-icon> Reenviar seleccionados ({{ seleccion.size }})</button>
        <button type="button" class="btn btn-ghost" (click)="reenviarFiltrados()"
                [title]="auth.logueado ? '' : 'Inicia sesión para modificar'"><app-icon name="refresh"></app-icon> Reenviar todo lo filtrado ({{ productList.totalCount }})</button>
        <span *ngIf="mensaje" class="text-small text-ok" role="status"><app-icon name="check"></app-icon> {{ mensaje }}</span>
      </div>

      <!-- Retener: excepción por SKU en bloque (los elegidos o todo lo filtrado); lo retenido se ve y se libera en "Filtros de envío" -->
      <div class="search-bar" *ngIf="productList && productList.items.length > 0">
        <span class="label-dim"><app-icon name="ban"></app-icon> Retener en:</span>
        <select [(ngModel)]="destinoRetencion" class="form-input w-md" aria-label="Destino de la retención">
          <option value="compras">Compras</option>
          <option value="compara">Compara</option>
          <option value="ambos">Ambos destinos</option>
        </select>
        <input type="text" class="form-input w-md" placeholder="Motivo (opcional)" aria-label="Motivo de la retención" [(ngModel)]="motivoRetencion" maxlength="200" />
        <button type="button" class="btn btn-primary" [disabled]="seleccion.size === 0" (click)="retenerSeleccion()"
                [title]="auth.logueado ? '' : 'Inicia sesión para modificar'"><app-icon name="ban"></app-icon> Retener seleccionados ({{ seleccion.size }})</button>
        <button type="button" class="btn btn-ghost" [disabled]="!hayFiltros" (click)="retenerFiltrados()"
                [title]="!auth.logueado ? 'Inicia sesión para modificar' : hayFiltros ? '' : 'Aplica un filtro (nombre, marca, departamento, código, régimen o estado) para retener en bloque'"><app-icon name="ban"></app-icon> Retener todo lo filtrado ({{ productList.totalCount }})</button>
        <span *ngIf="mensajeRetencion" class="text-small text-ok" role="status"><app-icon name="check"></app-icon> {{ mensajeRetencion }}</span>
      </div>

      <!-- Últimos cambios del producto elegido -->
      <div id="panel-cambios" class="section-card" *ngIf="seleccionado">
        <div style="display: flex; justify-content: space-between; align-items: center;">
          <h3 class="section-title" style="margin: 0;"><app-icon name="history"></app-icon> Últimos cambios de {{ seleccionado.code }} <span class="text-dim text-small">{{ seleccionado.nameEs }}</span></h3>
          <button type="button" class="btn btn-ghost" (click)="seleccionado = null">Cerrar</button>
        </div>
        <!-- Envío por destino: excepción propia de este SKU -->
        <div class="filters-grid" style="margin: 16px 0; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));">
          <div *ngFor="let e of estadoEnvio" class="stat-card">
            <span class="stat-title">{{ e.target === 'compras' ? 'Compras Paraguai' : 'Compara Paraguai' }}</span>
            <span class="text-small">
              <span *ngIf="!e.retenido" class="badge badge-success"><app-icon name="check"></app-icon> Se envía</span>
              <span *ngIf="e.retenido" class="badge badge-warn"><app-icon name="ban"></app-icon> Retenido</span>
              <span *ngIf="e.retirado" class="badge badge-secondary" title="Se le envió stock 0 al destino"><app-icon name="box"></app-icon> Retirado</span>
              <span *ngIf="e.retenidoPor" class="text-dim"> por {{ e.retenidoPor }}</span>
            </span>
            <div style="display: flex; gap: 8px; align-items: center; flex-wrap: wrap; margin-top: 6px;">
              <select class="form-input" style="min-width: 150px;" [(ngModel)]="excepciones[e.target].accion"
                      [attr.aria-label]="'Excepción de envío a ' + (e.target === 'compras' ? 'Compras' : 'Compara')">
                <option value="">Seguir las pausas</option>
                <option value="enviar">Forzar envío</option>
                <option value="retener">Retener este SKU</option>
              </select>
              <input type="text" class="form-input" placeholder="Motivo (opcional)" aria-label="Motivo de la excepción" [(ngModel)]="excepciones[e.target].motivo" style="min-width: 150px;" />
              <button type="button" class="btn btn-primary btn-sm" (click)="guardarExcepcion(e.target)" [title]="auth.logueado ? '' : 'Inicia sesión para modificar'">Guardar</button>
            </div>
          </div>
        </div>

        <app-cambios-lista [items]="cambiosProducto" [mostrarProducto]="false"></app-cambios-lista>
      </div>

      <div *ngIf="cargando" class="empty-state"><span class="spinner" role="status" aria-label="Cargando"></span></div>

      <div class="table-container mt-6" *ngIf="pagedProducts.length > 0">
        <table class="data-table">
          <thead>
            <tr>
              <th scope="col" style="width: 36px;"><input type="checkbox" aria-label="Seleccionar todos los productos de la página" [checked]="todosDeLaPaginaElegidos" (change)="alternarPagina($any($event.target).checked)" /></th>
              <th scope="col" style="width: 12%;">Código</th>
              <th scope="col" style="width: 24%;">Nombre (ES)</th>
              <th scope="col">Marca</th>
              <th scope="col">Régimen</th>
              <th scope="col">Precio (IVA)</th>
              <th scope="col">Precio Exenta</th>
              <th scope="col">Stock</th>
              <th scope="col">Compras</th>
              <th scope="col">Compara</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let p of pagedProducts" class="row-click" tabindex="0" (click)="verCambios(p)" (keydown.enter)="verCambios(p)"
                [class.row-selected]="seleccionado?.code === p.code" title="Ver últimos cambios">
              <td (click)="$event.stopPropagation()" (keydown.enter)="$event.stopPropagation()"><input type="checkbox" [attr.aria-label]="'Seleccionar ' + p.code" [checked]="seleccion.has(p.code)" (change)="alternarProducto(p.code, $any($event.target).checked)" /></td>
              <td class="text-bold">{{ p.code }}</td>
              <td>{{ p.nameEs }}</td>
              <td>{{ p.brand }}</td>
              <td><span class="badge" [class.badge-success]="p.regimenTurismo" [class.badge-secondary]="!p.regimenTurismo"><app-icon *ngIf="p.regimenTurismo" name="ticket"></app-icon> {{ p.regimenTurismo ? 'Sí' : 'No' }}</span></td>
              <td>{{ p.price | currency:'':'symbol-narrow' }}</td>
              <td>{{ (p.precioExenta || 0) | currency:'':'symbol-narrow' }}</td>
              <td class="text-center">{{ p.stock }}</td>
              <td>
                <span class="badge" [ngClass]="statusClass(p.syncStatus)">
                  <app-icon [name]="statusIcon(p.syncStatus)"></app-icon> {{ statusText(p.syncStatus) }}
                </span>
                <span *ngIf="noDisponibles.has(p.code)" class="badge badge-warn"
                      title="Compras recibió el producto (stock y precio al día) pero lo marca como no disponible. Lo decide Compras.">
                  No disp.
                </span>
              </td>
              <td>
                <span class="badge" [ngClass]="statusClass(p.syncComparaStatus)">
                  <app-icon [name]="statusIcon(p.syncComparaStatus)"></app-icon> {{ statusText(p.syncComparaStatus) }}
                </span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <app-paginador [page]="page" [totalPages]="totalPages"
                     [info]="productList ? productList.totalCount + ' productos' : ''" (go)="goToPage($event)"></app-paginador>

      <div *ngIf="!cargando && (!productList || productList.items.length === 0)" class="empty-state">
        No hay productos
      </div>
    </div>
  `
})
export class ProductosComponent implements OnInit {
  readonly statusClass = statusClass;

  /** Texto del estado sin emoji (el icono lo pone la plantilla). 0 = Pendiente, 1 = Enviado, otro = Error. */
  statusText(status?: number | null): string {
    return status === 0 ? 'Pendiente' : status === 1 ? 'Enviado' : 'Error';
  }

  statusIcon(status?: number | null): string {
    return status === 0 ? 'clock' : status === 1 ? 'check' : 'x';
  }

  productList: any = null;
  pagedProducts: any[] = [];
  departamentos: { id: number; nombre: string; productos: number }[] = [];
  cargando = false;

  filterCode = '';
  filterName = '';
  filterMarcaId: number | null = null;
  filterCategoriaId: number | null = null;
  filterTarget = '';
  filterRegimen = '';
  filterState = '';

  pageSize = 25;
  page = 1;
  totalPages = 1;

  seleccionado: any = null;
  cambiosProducto: Cambio[] = [];

  /** Códigos que Compras marca como no disponibles aunque tengan stock (foto de Worker.Verify --disponibilidad). */
  noDisponibles = new Set<string>();

  // Retener en bloque
  destinoRetencion: 'compras' | 'compara' | 'ambos' = 'ambos';
  motivoRetencion = '';
  mensajeRetencion = '';

  // Reenviar
  seleccion = new Set<string>();
  destinoReenvio: 'compras' | 'compara' | 'ambos' = 'compras';
  mensaje = '';
  estadoEnvio: EnvioEstadoProducto[] = [];
  excepciones: Record<Destino, { accion: string; motivo: string }> = {
    compras: { accion: '', motivo: '' },
    compara: { accion: '', motivo: '' }
  };

  constructor(
    private productoService: ProductoService,
    private catalogo: CatalogoService,
    private cambios: CambiosService,
    private errors: ErrorService,
    private envio: EnvioService,
    public auth: AuthService
  ) {}

  ngOnInit() {
    this.search();
    this.productoService.getNoDisponiblesCodigos().subscribe(
      codigos => this.noDisponibles = new Set(codigos),
      () => { /* la insignia es opcional: si falla, la lista se ve igual */ }
    );
    // Departamentos = hijos de la raíz del árbol de categorías
    this.catalogo.getArbolCategorias().subscribe(
      arbol => this.departamentos = (arbol[0]?.hijos ?? []).map(h => ({ id: h.id, nombre: h.nombre, productos: h.productos })),
      () => this.errors.show('Error cargando categorías')
    );
  }

  onMarca(sel: MarcaSeleccion) {
    this.filterMarcaId = sel.id;
    // Solo se busca cuando hay una marca válida o se vació el cuadro (no por cada letra tipeada)
    if (sel.id !== null || sel.nombre === '') this.search();
  }

  /** Nueva búsqueda (cambió un filtro): vuelve a la página 1. */
  search() {
    this.goToPage(1);
  }

  /** Pide la página al servidor con todos los filtros (el régimen y el estado también se filtran allí). */
  goToPage(page: number) {
    this.page = Math.max(1, page);
    this.limpiarSeleccion();
    this.cargando = true;
    this.productoService.getProductos({
      code: this.filterCode,
      name: this.filterName,
      target: this.filterTarget,
      syncStatus: this.filterState ? parseInt(this.filterState) : undefined,
      marcaId: this.filterMarcaId,
      categoriaId: this.filterCategoriaId,
      regimen: this.filterRegimen
    }, this.page, this.pageSize).subscribe(
      list => {
        this.cargando = false;
        if (!list || !list.items) return;
        const items: any[] = list.items;
        this.totalPages = list.totalPages || 1;
        this.pagedProducts = items;
        this.productList = list;
      },
      () => {
        this.cargando = false;
        this.errors.show('Error cargando productos');
      }
    );
  }

  // ------------------------------------------------------- reenviar

  /** Cualquier cambio de filtro o de página deja sin efecto la selección anterior. */
  private limpiarSeleccion() {
    this.seleccion.clear();
    this.mensaje = '';
    this.mensajeRetencion = '';
  }

  /** Hay al menos un filtro aplicado (sin filtro no se permite retener "todo lo filtrado": sería todo el catálogo). */
  get hayFiltros(): boolean {
    return !!(this.filterCode || this.filterName || this.filterMarcaId || this.filterCategoriaId || this.filterRegimen || this.filterState);
  }

  private get nombreDestinoRetencion(): string {
    return this.destinoRetencion === 'ambos' ? 'Compras y Compara' : this.destinoRetencion === 'compras' ? 'Compras' : 'Compara';
  }

  retenerSeleccion() {
    if (!this.requiereSesion() || this.seleccion.size === 0) return;
    if (!confirm(`¿Retener ${this.seleccion.size} productos en ${this.nombreDestinoRetencion}?\n\n` +
        'Dejan de enviarse (se siguen actualizando en la base). Los ves y los liberas en "Filtros de envío" → Retenidos.')) return;
    this.aplicarRetencion([...this.seleccion]);
  }

  retenerFiltrados() {
    if (!this.requiereSesion() || !this.productList || !this.hayFiltros) return;
    const total = this.productList.totalCount;
    if (!confirm(`¿Retener en ${this.nombreDestinoRetencion} los ${total} productos del filtro actual?\n\n` +
        'Dejan de enviarse (se siguen actualizando en la base). Los ves y los liberas en "Filtros de envío" → Retenidos.')) return;
    this.aplicarRetencion();
  }

  private aplicarRetencion(codes?: string[]) {
    this.envio.excepcionesLote({
      target: this.destinoRetencion,
      accion: 'retener',
      motivo: this.motivoRetencion || undefined,
      codes,
      code: this.filterCode || undefined,
      name: this.filterName || undefined,
      syncStatus: this.filterState ? parseInt(this.filterState) : undefined,
      filtroTarget: this.filterTarget || undefined,
      marcaId: this.filterMarcaId,
      categoriaId: this.filterCategoriaId,
      regimen: this.filterRegimen === '' ? undefined : this.filterRegimen === 'true'
    }).subscribe(
      r => {
        this.seleccion.clear();
        this.mensajeRetencion = `${r.afectados} retenciones aplicadas en ${this.nombreDestinoRetencion}.`;
      },
      e => {
        if (!String(e?.message ?? '').includes('401')) this.errors.show('No se pudo retener');
      }
    );
  }

  get todosDeLaPaginaElegidos(): boolean {
    return this.pagedProducts.length > 0 && this.pagedProducts.every(p => this.seleccion.has(p.code));
  }

  alternarProducto(code: string, marcado: boolean) {
    if (marcado) this.seleccion.add(code); else this.seleccion.delete(code);
  }

  alternarPagina(marcado: boolean) {
    for (const p of this.pagedProducts) {
      if (marcado) this.seleccion.add(p.code); else this.seleccion.delete(p.code);
    }
  }

  private get nombreDestinoReenvio(): string {
    return this.destinoReenvio === 'ambos' ? 'Compras y Compara' : this.destinoReenvio === 'compras' ? 'Compras' : 'Compara';
  }

  reenviarSeleccion() {
    if (!this.requiereSesion() || this.seleccion.size === 0) return;
    if (!confirm(`¿Reenviar ${this.seleccion.size} productos a ${this.nombreDestinoReenvio}?\n\n` +
        'Los que ya estaban enviados o con error vuelven a Pendiente y el worker los envía en su próximo ciclo. ' +
        'Los retenidos por una pausa seguirán sin enviarse.')) return;
    this.enviarReenvio([...this.seleccion]);
  }

  reenviarFiltrados() {
    if (!this.requiereSesion() || !this.productList) return;
    const total = this.productList.totalCount;
    const hayFiltros = this.hayFiltros;
    const estado = this.filterState
      ? `\nSolo los que están "${this.filterState === '1' ? 'Enviado' : this.filterState === '2' ? 'Error' : 'Pendiente'}" según la columna de ${this.filterTarget === 'compara' ? 'Compara' : 'Compras'}.`
      : '';
    if (!confirm(`¿Reenviar a ${this.nombreDestinoReenvio} ${hayFiltros ? `los ${total} productos del filtro actual` : `TODOS los productos (${total})`}?${estado}\n\n` +
        'Los que ya estaban enviados o con error vuelven a Pendiente. Los que ya están Pendientes no cambian.')) return;
    this.enviarReenvio(undefined, !hayFiltros);
  }

  private enviarReenvio(codes?: string[], todos = false) {
    this.productoService.reenviar({
      target: this.destinoReenvio,
      codes,
      code: this.filterCode || undefined,
      name: this.filterName || undefined,
      syncStatus: this.filterState ? parseInt(this.filterState) : undefined,
      filtroTarget: this.filterTarget || undefined,
      marcaId: this.filterMarcaId,
      categoriaId: this.filterCategoriaId,
      regimen: this.filterRegimen === '' ? undefined : this.filterRegimen === 'true',
      todos
    }).subscribe(
      r => {
        this.seleccion.clear();
        this.goToPage(this.page);
        this.mensaje = `${r.reiniciados} productos vuelven a Pendiente para ${this.nombreDestinoReenvio}.`;
      },
      e => {
        if (!String(e?.message ?? '').includes('401')) this.errors.show('No se pudo marcar para reenviar');
      }
    );
  }

  private requiereSesion(): boolean {
    if (this.auth.logueado) return true;
    this.auth.pedirLogin = true;
    return false;
  }

  /** Carga cómo se envía el SKU a cada destino (excepción propia, si está retenido y por qué). */
  private cargarEstadoEnvio(code: string) {
    this.envio.getEstadoProducto(code).subscribe(
      estado => {
        this.estadoEnvio = estado;
        for (const e of estado) {
          this.excepciones[e.target] = { accion: e.excepcion?.accion ?? '', motivo: e.excepcion?.motivo ?? '' };
        }
      },
      () => this.errors.show('Error cargando el estado de envío del producto')
    );
  }

  guardarExcepcion(target: Destino) {
    if (!this.auth.logueado) {
      this.auth.pedirLogin = true;
      return;
    }
    const code = this.seleccionado?.code;
    if (!code) return;
    const { accion, motivo } = this.excepciones[target];
    const peticion = accion === 'enviar' || accion === 'retener'
      ? this.envio.guardarExcepcion(code, target, accion, motivo.trim() || undefined)
      : this.envio.borrarExcepcion(code, target);

    peticion.subscribe({
      next: () => this.cargarEstadoEnvio(code),
      // Quitar una excepción que no existía responde 404: no es un error para el usuario.
      error: e => {
        if (accion === '' && String(e?.message ?? '').includes('404')) this.cargarEstadoEnvio(code);
        else if (!String(e?.message ?? '').includes('401')) this.errors.show('No se pudo guardar la excepción');
      }
    });
  }

  verCambios(producto: any) {
    this.seleccionado = producto;
    this.cargarEstadoEnvio(producto.code);
    this.cambiosProducto = [];
    this.cambios.getCambios({ code: producto.code }, 1, 30).subscribe(
      lista => {
        this.cambiosProducto = lista.items;
        setTimeout(() => document.getElementById('panel-cambios')?.scrollIntoView({ behavior: 'smooth', block: 'start' }));
      },
      () => this.errors.show('Error cargando los cambios del producto')
    );
  }
}
