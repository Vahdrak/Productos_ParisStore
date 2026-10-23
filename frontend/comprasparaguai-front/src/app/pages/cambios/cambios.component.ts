import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Cambio, CambioList, CambiosService } from '../../shared/services/cambios.service';
import { ErrorService } from '../../shared/services/error.service';
import { MarcaPickerComponent, MarcaSeleccion } from '../../shared/ui/marca-picker.component';
import { CambiosListaComponent } from '../../shared/ui/cambios-lista.component';
import { DateRangeComponent } from '../../shared/ui/date-range.component';
import { PaginadorComponent } from '../../shared/ui/paginador.component';
import { IconComponent } from '../../shared/ui/icon.component';

@Component({
  selector: 'app-cambios',
  standalone: true,
  imports: [CommonModule, FormsModule, MarcaPickerComponent, CambiosListaComponent, DateRangeComponent, PaginadorComponent, IconComponent],
  template: `
    <div class="fade-in">
      <div class="section-card">
        <h3 class="section-title"><app-icon name="history" size="1em"/> Últimos cambios de productos</h3>
        <div class="search-bar">
          <input type="text" placeholder="Producto (código o nombre)..." aria-label="Producto (código o nombre)" [(ngModel)]="texto" (keyup.enter)="search()" class="form-input" />
          <app-marca-picker placeholder="Marca (ej. NIKE)..." ariaLabel="Marca" (marcaChange)="onMarca($event)"></app-marca-picker>
          <select [(ngModel)]="campo" (ngModelChange)="search()" class="form-input w-md" aria-label="Filtrar por campo">
            <option value="">Campo: Todos</option>
            <option value="price">Precio</option>
            <option value="stock">Stock</option>
            <option value="name_es">Nombre</option>
            <option value="marca">Marca</option>
            <option value="categoria">Categoría</option>
            <option value="regimen_turismo">Régimen turismo</option>
            <option value="url_image">Imagen</option>
          </select>
          <select [(ngModel)]="origen" (ngModelChange)="search()" class="form-input w-md" aria-label="Filtrar por origen">
            <option value="">Origen: Todos</option>
            <option value="sync">Sync</option>
            <option value="reconciliacion">Baja: ya no está en tu ERP</option>
            <option value="api">Panel</option>
            <option value="historico">Histórico</option>
          </select>
          <button type="button" (click)="search()" class="btn btn-primary">Buscar</button>
        </div>
        <div class="search-bar">
          <app-date-range [(desde)]="desde" [(hasta)]="hasta" label="Rango de fechas:" (changed)="search()" (cleared)="search()"></app-date-range>
          <select [(ngModel)]="pageSize" (ngModelChange)="search()" class="form-input w-md" aria-label="Cambios por página">
            <option [ngValue]="25">25 por página</option>
            <option [ngValue]="50">50 por página</option>
            <option [ngValue]="100">100 por página</option>
          </select>
        </div>

        <div class="label-dim" *ngIf="lista">{{ lista.totalCount }} cambios</div>
        <span class="spinner" role="status" aria-label="Cargando" *ngIf="cargando"></span>
        <app-cambios-lista [items]="items" [cargando]="cargando"></app-cambios-lista>

        <app-paginador [page]="page" [totalPages]="totalPages" (go)="goToPage($event)"></app-paginador>
      </div>
    </div>
  `
})
export class CambiosComponent implements OnInit {
  lista: CambioList | null = null;
  items: Cambio[] = [];
  cargando = false;

  texto = '';
  marcaId: number | null = null;
  campo = '';
  origen = '';
  desde = '';
  hasta = '';

  pageSize = 50;
  page = 1;
  totalPages = 1;

  constructor(private cambios: CambiosService, private errors: ErrorService) {}

  ngOnInit() {
    this.search();
  }

  onMarca(sel: MarcaSeleccion) {
    this.marcaId = sel.id;
    if (sel.id !== null || sel.nombre === '') this.search();
  }

  search() {
    this.goToPage(1);
  }

  goToPage(page: number) {
    this.page = Math.max(1, page);
    this.cargando = true;
    this.cambios.getCambios({
      q: this.texto.trim(),
      marcaId: this.marcaId,
      campo: this.campo,
      origen: this.origen,
      desde: this.desde,
      hasta: this.hasta
    }, this.page, this.pageSize).subscribe(
      lista => {
        this.lista = lista;
        this.items = lista.items;
        this.totalPages = lista.totalPages || 1;
        this.cargando = false;
      },
      () => {
        this.cargando = false;
        this.errors.show('Error cargando cambios');
      }
    );
  }
}
