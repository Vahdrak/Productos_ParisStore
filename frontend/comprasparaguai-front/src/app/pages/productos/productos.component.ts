import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProductoService } from '../../shared/services/producto.service';
import { CatalogoService } from '../../shared/services/catalogo.service';
import { Cambio, CambiosService } from '../../shared/services/cambios.service';
import { ErrorService } from '../../shared/services/error.service';
import { AuthService } from '../../shared/services/auth.service';
import { Destino, EnvioEstadoProducto, EnvioService } from '../../shared/services/envio.service';
import { statusClass, statusLabel, statusField } from '../../shared/utils/estado';
import { MarcaPickerComponent, MarcaSeleccion } from '../../shared/ui/marca-picker.component';
import { CambiosListaComponent } from '../../shared/ui/cambios-lista.component';

@Component({
  selector: 'app-productos',
  standalone: true,
  imports: [CommonModule, FormsModule, MarcaPickerComponent, CambiosListaComponent],
  template: `
    <div class="fade-in">
      <div class="search-bar">
        <input type="text" placeholder="Buscar código..." [(ngModel)]="filterCode" (keyup.enter)="search()" class="form-input" />
        <app-marca-picker placeholder="Marca (ej. NIKE)..." (marcaChange)="onMarca($event)"></app-marca-picker>
        <select [(ngModel)]="filterCategoriaId" (ngModelChange)="search()" class="form-input" style="width: 190px;">
          <option [ngValue]="null">Departamento: Todos</option>
          <option *ngFor="let d of departamentos" [ngValue]="d.id">{{ d.nombre }} ({{ d.productos }})</option>
        </select>
        <button (click)="search()" class="btn btn-primary">Buscar</button>
      </div>

      <div class="search-bar">
        <select [(ngModel)]="filterTarget" (ngModelChange)="search()" class="form-input" style="width: 120px;">
          <option value="">Destino: Todos</option>
          <option value="compras">Compras</option>
          <option value="compara">Compara</option>
        </select>
        <select [(ngModel)]="filterRegimen" (ngModelChange)="search()" class="form-input" style="width: 140px;">
          <option value="">Régimen: Todos</option>
          <option value="true">🎫 Con régimen turismo</option>
          <option value="false">Normal (sin régimen)</option>
        </select>
        <select [(ngModel)]="filterState" (ngModelChange)="search()" class="form-input" style="width: 140px;">
          <option value="">Estado: Todos</option>
          <option value="0">⏳ Pendiente</option>
          <option value="1">✅ Enviado</option>
          <option value="2">❌ Error</option>
        </select>
      </div>

      <div class="search-bar">

      <div class="search-bar" *ngIf="productList && productList.items.length > 0">
        <select [(ngModel)]="pageSize" (ngModelChange)="search()" class="form-input" style="width: 140px;">
          <option [ngValue]="10">10 por página</option>
          <option [ngValue]="25">25 por página</option>
          <option [ngValue]="50">50 por página</option>
          <option [ngValue]="100">100 por página</option>
        </select>
        <span class="label-dim" *ngIf="productList">{{ productList.totalCount }} productos</span>
      </div>

      <!-- Últimos cambios del producto elegido -->
      <div id="panel-cambios" class="section-card" *ngIf="seleccionado">
        <div style="display: flex; justify-content: space-between; align-items: center;">
          <h3 class="section-title" style="margin: 0;">🕘 Últimos cambios de {{ seleccionado.code }} <span class="text-dim text-small">{{ seleccionado.nameEs }}</span></h3>
          <button class="btn btn-ghost" (click)="seleccionado = null">Cerrar</button>
        </div>
        <!-- Envío por destino: excepción propia de este SKU -->
        <div class="filters-grid" style="margin: 16px 0; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));">
          <div *ngFor="let e of estadoEnvio" class="stat-card">
            <span class="stat-title">{{ e.target === 'compras' ? 'Compras Paraguai' : 'Compara Paraguai' }}</span>
            <span class="text-small">
              <span *ngIf="!e.retenido" class="badge badge-success">✅ Se envía</span>
              <span *ngIf="e.retenido" class="badge badge-warn">🚫 Retenido</span>
              <span *ngIf="e.retirado" class="badge badge-secondary" title="Se le envió stock 0 al destino">📦 Retirado</span>
              <span *ngIf="e.retenidoPor" class="text-dim"> por {{ e.retenidoPor }}</span>
            </span>
            <div style="display: flex; gap: 8px; align-items: center; flex-wrap: wrap; margin-top: 6px;">
              <select class="form-input" style="min-width: 150px;" [(ngModel)]="excepciones[e.target].accion">
                <option value="">Seguir las pausas</option>
                <option value="enviar">Forzar envío</option>
                <option value="retener">Retener este SKU</option>
              </select>
              <input type="text" class="form-input" placeholder="Motivo (opcional)" [(ngModel)]="excepciones[e.target].motivo" style="min-width: 150px;" />
              <button class="btn btn-primary btn-sm" (click)="guardarExcepcion(e.target)" [title]="auth.logueado ? '' : 'Inicia sesión para modificar'">Guardar</button>
            </div>
          </div>
        </div>

        <app-cambios-lista [items]="cambiosProducto" [mostrarProducto]="false"></app-cambios-lista>
      </div>

      <div class="table-container mt-6" *ngIf="pagedProducts.length > 0">
        <table class="data-table">
          <thead>
            <tr>
              <th style="width: 12%;">Código</th>
              <th style="width: 24%;">Nombre (ES)</th>
              <th>Marca</th>
              <th>Régimen</th>
              <th>Precio (IVA)</th>
              <th>Precio Exenta</th>
              <th>Stock</th>
              <th>Compras</th>
              <th>Compara</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let p of pagedProducts" (click)="verCambios(p)" style="cursor: pointer;"
                [style.background]="seleccionado?.code === p.code ? 'rgba(37, 99, 235, 0.15)' : ''" title="Ver últimos cambios">
              <td class="text-bold">{{ p.code }}</td>
              <td>{{ p.nameEs }}</td>
              <td>{{ p.brand }}</td>
              <td><span class="badge" [class.badge-success]="p.regimenTurismo" [class.badge-secondary]="!p.regimenTurismo">{{ p.regimenTurismo ? '🎫 Sí' : 'No' }}</span></td>
              <td>{{ p.price | currency:'':'symbol-narrow' }}</td>
              <td>{{ (p.precioExenta || 0) | currency:'':'symbol-narrow' }}</td>
              <td class="text-center">{{ p.stock }}</td>
              <td>
                <span class="badge" [ngClass]="statusClass(p.syncStatus)">
                  {{ statusLabel(p.syncStatus) }}
                </span>
              </td>
              <td>
                <span class="badge" [ngClass]="statusClass(p.syncComparaStatus)">
                  {{ statusLabel(p.syncComparaStatus) }}
                </span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div *ngIf="totalPages > 1" class="pagination">
        <button [disabled]="page === 1" (click)="goToPage(1)">«</button>
        <button [disabled]="page === 1" (click)="goToPage(page - 1)">‹</button>
        <span class="page-info">Pág {{ page }}/{{ totalPages }}</span>
        <button [disabled]="page === totalPages" (click)="goToPage(page + 1)">›</button>
        <button [disabled]="page === totalPages" (click)="goToPage(totalPages)">»</button>
      </div>

      <div *ngIf="!productList || productList.items.length === 0" class="text-center py-8 text-dim">
        No hay productos
      </div>
    </div>
  `
})
export class ProductosComponent implements OnInit {
  readonly statusClass = statusClass;
  readonly statusLabel = statusLabel;

  productList: any = null;
  pagedProducts: any[] = [];
  departamentos: { id: number; nombre: string; productos: number }[] = [];

  filterCode = '';
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

  /** Pide la página al servidor; el régimen se filtra sobre la página recibida. */
  goToPage(page: number) {
    this.page = Math.max(1, page);
    this.productoService.getProductos({
      code: this.filterCode,
      target: this.filterTarget,
      syncStatus: this.filterState ? parseInt(this.filterState) : undefined,
      marcaId: this.filterMarcaId,
      categoriaId: this.filterCategoriaId
    }, this.page, this.pageSize).subscribe(
      list => {
        if (!list || !list.items) return;
        let items: any[] = list.items;
        if (this.filterRegimen === 'true') {
          items = items.filter(p => p.regimenTurismo === true);
        } else if (this.filterRegimen === 'false') {
          items = items.filter(p => !p.regimenTurismo);
        }
        if (this.filterState) {
          const field = statusField(this.filterTarget || 'compras');
          items = items.filter(p => p[field]?.toString() === this.filterState);
        }
        this.totalPages = list.totalPages || 1;
        this.pagedProducts = items;
        this.productList = list;
      },
      () => this.errors.show('Error cargando productos')
    );
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
