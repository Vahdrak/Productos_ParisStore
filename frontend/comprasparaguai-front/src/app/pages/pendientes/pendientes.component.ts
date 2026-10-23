import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PendientesFiltro, PendientesLista, ProductoService } from '../../shared/services/producto.service';
import { CatalogoService } from '../../shared/services/catalogo.service';
import { MarcaPickerComponent, MarcaSeleccion } from '../../shared/ui/marca-picker.component';
import { ErrorService } from '../../shared/services/error.service';
import { IconComponent } from '../../shared/ui/icon.component';
import { PaginadorComponent } from '../../shared/ui/paginador.component';

@Component({
  selector: 'app-pendientes',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, PaginadorComponent, MarcaPickerComponent],
  styles: [`
    /* Nombre largo: una sola línea con elipsis (el nombre completo va en el title) */
    .td-nombre {
      max-width: 320px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
  `],
  template: `
    <div class="fade-in">
      <div class="section-card">
        <div class="filter-tabs">
          <button type="button" [class.active]="target === 'compras'" (click)="switchTarget('compras')">Compras Paraguai</button>
          <button type="button" [class.active]="target === 'compara'" (click)="switchTarget('compara')">Compara Paraguai</button>
        </div>
      </div>

      <div class="section-card">
        <h3 class="section-title">Filtros</h3>
        <div class="filters-grid">
          <input type="text" placeholder="Código..." aria-label="Filtrar por código" [(ngModel)]="filtro.code" (keyup.enter)="buscar()" class="form-input" />
          <input type="text" placeholder="Nombre..." aria-label="Filtrar por nombre" [(ngModel)]="filtro.name" (keyup.enter)="buscar()" class="form-input" />
          <app-marca-picker placeholder="Marca (ej. NIKE)..." (marcaChange)="onMarca($event)"></app-marca-picker>
          <select [(ngModel)]="filtro.categoriaId" (ngModelChange)="buscar()" class="form-input" aria-label="Filtrar por departamento">
            <option [ngValue]="null">Departamento: Todos</option>
            <option *ngFor="let d of departamentos" [ngValue]="d.id">{{ d.nombre }} ({{ d.productos }})</option>
          </select>
          <select [(ngModel)]="filtro.regimen" (ngModelChange)="buscar()" class="form-input" aria-label="Filtrar por régimen">
            <option value="">Régimen: Todos</option>
            <option value="true">Con régimen turismo</option>
            <option value="false">Normal (sin régimen)</option>
          </select>
          <select [(ngModel)]="filtro.retencion" (ngModelChange)="buscar()" class="form-input" aria-label="Filtrar por envío">
            <option value="">Envío: Todos</option>
            <option value="enviables">Solo enviables</option>
            <option value="retenidos">Solo retenidos</option>
          </select>
          <select [(ngModel)]="pageSize" (ngModelChange)="buscar()" class="form-input" aria-label="Productos por página">
            <option [ngValue]="25">25 por página</option>
            <option [ngValue]="50">50 por página</option>
            <option [ngValue]="100">100 por página</option>
          </select>
          <button type="button" class="btn btn-primary" (click)="buscar()">Buscar</button>
        </div>
      </div>

      <div class="section-card">
        <h3 class="section-title">Productos ({{ lista?.totalCount ?? 0 }} resultados)</h3>
        <div *ngIf="cargando" class="empty-state"><span class="spinner" role="status" aria-label="Cargando"></span></div>
        <div class="table-container" *ngIf="lista && lista.items.length > 0">
          <table class="data-table">
            <thead><tr><th scope="col">Código</th><th scope="col">Nombre</th><th scope="col">Marca</th><th scope="col">Régimen</th><th scope="col">Precio (IVA)</th><th scope="col">Precio Exenta</th><th scope="col">Stock</th><th scope="col">Estado</th></tr></thead>
            <tbody>
              <tr *ngFor="let p of lista.items">
                <td class="text-bold">{{ p.code }}</td>
                <td class="td-nombre" [title]="p.namePt || ''">{{ p.namePt }}</td>
                <td>{{ p.brand }}</td>
                <td><span class="badge" [class.badge-success]="p.regimenTurismo" [class.badge-secondary]="!p.regimenTurismo"><app-icon *ngIf="p.regimenTurismo" name="ticket"></app-icon> {{ p.regimenTurismo ? 'Sí' : 'No' }}</span></td>
                <td>{{ p.price | currency:'':'symbol-narrow' }}</td>
                <td>{{ (p.precioExenta || 0) | currency:'':'symbol-narrow' }}</td>
                <td class="text-center">{{ p.stock }}</td>
                <td>
                  <span class="badge"><app-icon name="clock"></app-icon> Pendiente</span>
                  <div *ngIf="p.retenido" class="text-small" style="margin-top: 4px;">
                    <span class="badge badge-warn" title="No se envía mientras dure la pausa"><app-icon name="ban"></app-icon> Retenido</span>
                    <span class="text-dim">{{ p.retenidoPor }}</span>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <div *ngIf="!cargando && lista && lista.items.length === 0" class="empty-state">No hay productos que coincidan</div>

        <app-paginador *ngIf="lista" [page]="page" [totalPages]="lista.totalPages"
                       [info]="lista.totalCount + ' productos'" (go)="irAPagina($event)"></app-paginador>

        <div *ngIf="lista && lista.totalCount > 0" class="summary-bar">
          <div class="summary-item"><span class="summary-label"><app-icon name="ticket"></app-icon> Con régimen:</span><span class="summary-value">{{ lista.conRegimen }}</span></div>
          <div class="summary-item"><span class="summary-label"><app-icon name="ban"></app-icon> Retenidos:</span><span class="summary-value">{{ lista.retenidos }}</span></div>
          <div class="summary-item"><span class="summary-label">Normal:</span><span class="summary-value">{{ lista.totalCount - lista.conRegimen }}</span></div>
          <div class="summary-item"><span class="summary-label">Total:</span><span class="summary-value">{{ lista.totalCount }}</span></div>
        </div>
      </div>
    </div>
  `
})
export class PendientesComponent implements OnInit {
  target = 'compras';
  lista: PendientesLista | null = null;
  filtro: PendientesFiltro = { code: '', name: '', marcaId: null, categoriaId: null, regimen: '', retencion: '' };
  departamentos: { id: number; nombre: string; productos: number }[] = [];
  cargando = false;

  pageSize = 50;
  page = 1;

  constructor(private productoService: ProductoService, private catalogo: CatalogoService, private errors: ErrorService) {}

  ngOnInit() {
    this.cargar();
    // Departamentos = hijos de la raíz del árbol de categorías (igual que en Productos)
    this.catalogo.getArbolCategorias().subscribe(
      arbol => this.departamentos = (arbol[0]?.hijos ?? []).map(h => ({ id: h.id, nombre: h.nombre, productos: h.productos })),
      () => this.errors.show('Error cargando categorías')
    );
  }

  onMarca(sel: MarcaSeleccion) {
    this.filtro.marcaId = sel.id;
    // Solo se busca cuando hay una marca válida o se vació el cuadro (no por cada letra tipeada)
    if (sel.id !== null || sel.nombre === '') this.buscar();
  }

  /** Cada filtro nuevo vuelve a la página 1. */
  buscar() {
    this.page = 1;
    this.cargar();
  }

  irAPagina(p: number) {
    this.page = Math.max(1, p);
    this.cargar();
  }

  /** Pide solo la página actual: hay más de 100.000 pendientes y no se cargan juntos. */
  private cargar() {
    this.cargando = true;
    this.productoService.getPendientes(this.target, this.filtro, this.page, this.pageSize).subscribe(
      lista => {
        this.cargando = false;
        this.lista = lista;
        this.page = lista.page;
      },
      () => {
        this.cargando = false;
        this.errors.show('Error cargando productos pendientes');
      }
    );
  }

  switchTarget(target: string) {
    this.target = target;
    this.buscar();
  }
}
