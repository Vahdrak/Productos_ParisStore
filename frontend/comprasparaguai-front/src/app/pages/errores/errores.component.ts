import { FechaPyPipe } from '../../shared/utils/fecha-py.pipe';
import { Component, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Destino } from '../../shared/services/envio.service';
import { ErrorGrupo, ErrorProducto, ErroresFiltro, ErroresResultado, ErroresService, ErrorTipoResumen } from '../../shared/services/errores.service';
import { ErrorService } from '../../shared/services/error.service';
import { AuthService } from '../../shared/services/auth.service';
import { MarcaPickerComponent, MarcaSeleccion } from '../../shared/ui/marca-picker.component';
import { IconComponent } from '../../shared/ui/icon.component';
import { PaginadorComponent } from '../../shared/ui/paginador.component';

@Component({
  selector: 'app-errores',
  standalone: true,
  imports: [CommonModule, FechaPyPipe, FormsModule, MarcaPickerComponent, IconComponent, PaginadorComponent],
  template: `
    <div class="fade-in">
      <div class="section-card">
        <div class="filter-tabs">
          <button type="button" [class.active]="target === 'compras'" (click)="cambiarDestino('compras')">Compras Paraguai</button>
          <button type="button" [class.active]="target === 'compara'" (click)="cambiarDestino('compara')">Compara Paraguai</button>
        </div>
        <p class="text-dim text-small" style="margin: 0;">
          Productos que el destino rechazó. Los errores <b>no se reintentan solos</b>: un producto en Error se corrige en tu sistema (ERP)
          (si el dato cambia, el próximo sync lo reenvía solo) o se vuelve a intentar desde aquí con <b>Reintentar</b>.
        </p>
        <p *ngIf="!auth.logueado" class="text-small text-warn" style="margin: 12px 0 0 0;">
          <app-icon name="lock"></app-icon> Solo lectura: <button type="button" class="link" (click)="auth.pedirLogin = true">inicia sesión</button> para reintentar.
        </p>
        <p *ngIf="mensaje" class="text-small text-ok" role="status" style="margin: 12px 0 0 0;"><app-icon name="check"></app-icon> {{ mensaje }}</p>
      </div>

      <!-- POR TIPO DE ERROR -->
      <div class="section-card">
        <h3 class="section-title"><app-icon name="info"></app-icon> Por tipo de error ({{ totalTipos }})</h3>
        <div *ngIf="!cargando && res && res.tipos.length === 0" class="empty-state">Sin errores en {{ nombreDestino }} <app-icon name="check"></app-icon></div>
        <div class="filters-grid" style="grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));">
          <div *ngFor="let t of res?.tipos" class="stat-card row-click" role="button" tabindex="0"
               [attr.aria-pressed]="filtro.tipo === t.codigo"
               [style.border-color]="filtro.tipo === t.codigo ? '#2563eb' : ''" (click)="elegirTipo(t)" (keydown.enter)="elegirTipo(t)">
            <div style="display: flex; justify-content: space-between; align-items: baseline; gap: 8px;">
              <span class="text-bold">{{ t.titulo }}</span>
              <span class="stat-val">{{ t.total }}</span>
            </div>
            <span class="badge" [class.badge-success]="t.reintentable" [class.badge-secondary]="!t.reintentable">
              {{ t.reintentable ? 'Reintentar tiene sentido' : 'Hay que corregir el dato' }}
            </span>
            <span class="text-small text-dim" style="white-space: normal;">{{ t.queHacer }}</span>
          </div>
        </div>
      </div>

      <!-- AGRUPAR POR DEPARTAMENTO Y MARCA -->
      <div class="filters-grid" style="grid-template-columns: repeat(auto-fit, minmax(380px, 1fr)); align-items: start;">
        <div class="section-card">
          <h3 class="section-title"><app-icon name="box"></app-icon> Por departamento</h3>
          <div class="table-container" style="margin: 0;">
            <table class="data-table">
              <tbody>
                <tr *ngFor="let d of res?.porDepartamento" class="row-click" tabindex="0" (click)="elegirDepartamento(d)" (keydown.enter)="elegirDepartamento(d)"
                    [class.row-selected]="!!d.id && filtro.categoriaId === d.id">
                  <td>{{ d.nombre }}</td>
                  <td class="text-center" style="width: 80px;">{{ d.total }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
        <div class="section-card">
          <h3 class="section-title"><app-icon name="tag"></app-icon> Por marca (las 20 con más errores)</h3>
          <div class="table-container" style="margin: 0;">
            <table class="data-table">
              <tbody>
                <tr *ngFor="let m of res?.porMarca" class="row-click" tabindex="0" (click)="elegirMarca(m)" (keydown.enter)="elegirMarca(m)"
                    [class.row-selected]="!!m.id && filtro.marcaId === m.id">
                  <td>{{ m.nombre }}</td>
                  <td class="text-center" style="width: 80px;">{{ m.total }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- LISTA -->
      <div class="section-card">
        <h3 class="section-title"><app-icon name="warn"></app-icon> Productos en Error ({{ res?.totalCount ?? 0 }})</h3>
        <div class="search-bar">
          <input type="text" class="form-input" placeholder="Código o nombre..." aria-label="Buscar por código o nombre" [(ngModel)]="filtro.q" (keyup.enter)="buscar()" />
          <app-marca-picker #picker placeholder="Marca..." (marcaChange)="onMarca($event)"></app-marca-picker>
          <button type="button" class="btn btn-primary" (click)="buscar()">Buscar</button>
          <button type="button" class="btn btn-ghost" *ngIf="hayFiltros" (click)="limpiarFiltros()">Quitar filtros</button>
        </div>
        <div class="search-bar" *ngIf="hayFiltros">
          <span class="label-dim">Filtrando por:</span>
          <span class="badge badge-secondary" *ngIf="filtro.tipo">Tipo: {{ tituloTipo(filtro.tipo) }}</span>
          <span class="badge badge-secondary" *ngIf="filtro.categoriaId">Departamento: {{ nombreDepartamento }}</span>
          <span class="badge badge-secondary" *ngIf="filtro.marcaId">Marca: {{ nombreMarca }}</span>
          <span class="badge badge-secondary" *ngIf="filtro.q">Texto: {{ filtro.q }}</span>
        </div>

        <div class="search-bar" *ngIf="res && res.totalCount > 0">
          <button type="button" class="btn btn-primary" [disabled]="seleccion.size === 0" (click)="reintentarSeleccion()">
            <app-icon name="refresh"></app-icon> Reintentar seleccionados ({{ seleccion.size }})
          </button>
          <button type="button" class="btn btn-ghost" (click)="reintentarFiltrados()">
            <app-icon name="refresh"></app-icon> Reintentar {{ hayFiltros ? 'todo lo filtrado' : 'todos los errores' }} ({{ res.totalCount }})
          </button>
          <select [(ngModel)]="pageSize" (ngModelChange)="buscar()" class="form-input w-md" aria-label="Productos por página">
            <option [ngValue]="25">25 por página</option>
            <option [ngValue]="50">50 por página</option>
            <option [ngValue]="100">100 por página</option>
          </select>
        </div>

        <div *ngIf="cargando" class="empty-state"><span class="spinner" role="status" aria-label="Cargando"></span></div>
        <div class="table-container" *ngIf="res && res.items.length > 0">
          <table class="data-table">
            <thead>
              <tr>
                <th scope="col" style="width: 36px;"><input type="checkbox" aria-label="Seleccionar todos los errores de la página" [checked]="todosDeLaPaginaElegidos" (change)="alternarPagina($any($event.target).checked)" /></th>
                <th scope="col" style="width: 24%;">Producto</th>
                <th scope="col" style="width: 120px;">Marca</th>
                <th scope="col" style="width: 18%;">Categoría</th>
                <th scope="col" style="width: 150px;">Tipo</th>
                <th scope="col">Mensaje del destino</th>
                <th scope="col" style="width: 110px;">Fecha</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let e of res.items">
                <td><input type="checkbox" [attr.aria-label]="'Seleccionar ' + e.code" [checked]="seleccion.has(e.code)" (change)="alternarProducto(e.code, $any($event.target).checked)" /></td>
                <td>
                  <div class="text-bold">{{ e.code }}</div>
                  <div class="text-small text-dim">{{ e.nombre }}</div>
                </td>
                <td>{{ e.marca }}</td>
                <td class="text-small" style="white-space: normal;">{{ e.categoria }}</td>
                <td>
                  <span class="badge badge-error">{{ e.tipoTitulo }}</span>
                  <span *ngIf="e.etapa === 'retiro'" class="badge badge-warn" title="Falló al retirarlo del destino (stock 0)"><app-icon name="box"></app-icon> retiro</span>
                </td>
                <td class="text-small" style="white-space: normal; word-break: break-word;" [title]="e.mensaje || ''">{{ recortar(e.mensaje) }}</td>
                <td class="text-small">{{ e.updatedAt | fechaPy:'yyyy-MM-dd HH:mm' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div *ngIf="!cargando && res && res.items.length === 0" class="empty-state">No hay errores con estos filtros</div>

        <app-paginador *ngIf="res" [page]="page" [totalPages]="res.totalPages"
                       [info]="res.totalCount + ' errores'" (go)="irAPagina($event)"></app-paginador>
      </div>
    </div>
  `
})
export class ErroresComponent implements OnInit {
  @ViewChild('picker') picker?: MarcaPickerComponent;

  target: Destino = 'compras';
  res: ErroresResultado | null = null;
  filtro: ErroresFiltro = {};
  page = 1;
  pageSize = 25;
  seleccion = new Set<string>();
  mensaje = '';
  cargando = false;

  nombreMarca = '';
  nombreDepartamento = '';

  constructor(private servicio: ErroresService, private errors: ErrorService, public auth: AuthService) {}

  get nombreDestino(): string {
    return this.target === 'compras' ? 'Compras Paraguai' : 'Compara Paraguai';
  }

  get totalTipos(): number {
    return (this.res?.tipos ?? []).reduce((s, t) => s + t.total, 0);
  }

  get hayFiltros(): boolean {
    return !!(this.filtro.tipo || this.filtro.marcaId || this.filtro.categoriaId || this.filtro.q);
  }

  get todosDeLaPaginaElegidos(): boolean {
    return !!this.res && this.res.items.length > 0 && this.res.items.every(i => this.seleccion.has(i.code));
  }

  ngOnInit() {
    this.cargar();
  }

  cambiarDestino(t: Destino) {
    this.target = t;
    this.limpiarFiltros();
  }

  // ----------------------------------------------------------- carga

  cargar() {
    this.cargando = true;
    this.servicio.getErrores(this.target, this.filtro, this.page, this.pageSize).subscribe(
      r => {
        this.cargando = false;
        this.res = r;
        this.page = r.page;
      },
      () => {
        this.cargando = false;
        this.errors.show('Error cargando los errores de envío');
      }
    );
  }

  buscar() {
    this.page = 1;
    this.seleccion.clear();
    this.cargar();
  }

  irAPagina(p: number) {
    this.page = Math.max(1, p);
    this.cargar();
  }

  // --------------------------------------------------------- filtros

  elegirTipo(t: ErrorTipoResumen) {
    this.filtro.tipo = this.filtro.tipo === t.codigo ? undefined : t.codigo;
    this.buscar();
  }

  elegirDepartamento(d: ErrorGrupo) {
    if (!d.id) return;   // "(sin categoría)" no se puede filtrar por id
    const mismo = this.filtro.categoriaId === d.id;
    this.filtro.categoriaId = mismo ? null : d.id;
    this.nombreDepartamento = mismo ? '' : d.nombre;
    this.buscar();
  }

  elegirMarca(m: ErrorGrupo) {
    if (!m.id) return;
    const misma = this.filtro.marcaId === m.id;
    this.filtro.marcaId = misma ? null : m.id;
    this.nombreMarca = misma ? '' : m.nombre;
    if (misma) this.picker?.limpiar(); else if (this.picker) this.picker.texto = '';
    this.buscar();
  }

  onMarca(sel: MarcaSeleccion) {
    this.filtro.marcaId = sel.id;
    this.nombreMarca = sel.nombre;
    if (sel.id !== null || sel.nombre === '') this.buscar();
  }

  limpiarFiltros() {
    this.filtro = {};
    this.nombreMarca = '';
    this.nombreDepartamento = '';
    this.picker?.limpiar();
    this.mensaje = '';
    this.buscar();
  }

  tituloTipo(codigo?: string): string {
    return this.res?.tipos.find(t => t.codigo === codigo)?.titulo ?? codigo ?? '';
  }

  // ------------------------------------------------------- selección

  alternarProducto(code: string, marcado: boolean) {
    if (marcado) this.seleccion.add(code); else this.seleccion.delete(code);
  }

  alternarPagina(marcado: boolean) {
    for (const e of this.res?.items ?? []) {
      if (marcado) this.seleccion.add(e.code); else this.seleccion.delete(e.code);
    }
  }

  // ------------------------------------------------------- reintento

  private requiereSesion(): boolean {
    if (this.auth.logueado) return true;
    this.auth.pedirLogin = true;
    return false;
  }

  reintentarSeleccion() {
    if (!this.requiereSesion() || this.seleccion.size === 0) return;
    if (!confirm(`¿Volver a Pendiente ${this.seleccion.size} productos? Se reenviarán en el próximo ciclo del worker.`)) return;
    this.ejecutar(this.servicio.reintentar(this.target, {}, [...this.seleccion]));
  }

  reintentarFiltrados() {
    if (!this.requiereSesion() || !this.res) return;
    const total = this.res.totalCount;
    const tipo = this.res.tipos.find(t => t.codigo === this.filtro.tipo);
    const aviso = tipo && !tipo.reintentable
      ? `\n\nOjo: "${tipo.titulo}" vuelve a fallar si no cambias el dato en el origen.`
      : '';
    if (!confirm(`¿Volver a Pendiente ${total} productos${this.hayFiltros ? ' (los del filtro actual)' : ' (TODOS los errores)'}? Se reenviarán en el próximo ciclo del worker.${aviso}`)) return;
    this.ejecutar(this.servicio.reintentar(this.target, this.filtro));
  }

  private ejecutar(peticion: ReturnType<ErroresService['reintentar']>) {
    peticion.subscribe(
      r => {
        this.mensaje = `Se reiniciaron ${r.reiniciados} productos: pasan a Pendiente y se enviarán en el próximo ciclo del worker.`;
        this.seleccion.clear();
        this.cargar();
      },
      e => {
        if (!String(e?.message ?? '').includes('401')) this.errors.show('No se pudo reintentar');
      }
    );
  }

  recortar(texto: string | null, max = 220): string {
    if (!texto) return '—';
    return texto.length > max ? texto.slice(0, max) + '…' : texto;
  }
}
