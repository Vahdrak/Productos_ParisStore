import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Cambio, CambioList, CambiosService } from '../../shared/services/cambios.service';
import { ErrorService } from '../../shared/services/error.service';
import { quickRange } from '../../shared/utils/fechas';
import { QuickRangeComponent } from '../../shared/ui/quick-range.component';
import { MarcaPickerComponent, MarcaSeleccion } from '../../shared/ui/marca-picker.component';
import { CambiosListaComponent } from '../../shared/ui/cambios-lista.component';

@Component({
  selector: 'app-cambios',
  standalone: true,
  imports: [CommonModule, FormsModule, QuickRangeComponent, MarcaPickerComponent, CambiosListaComponent],
  template: `
    <div class="fade-in">
      <div class="section-card">
        <h3 class="section-title">🕘 Últimos cambios de productos</h3>
        <div class="search-bar">
          <input type="text" placeholder="Producto (código o nombre)..." [(ngModel)]="texto" (keyup.enter)="search()" class="form-input" />
          <app-marca-picker placeholder="Marca (ej. NIKE)..." (marcaChange)="onMarca($event)"></app-marca-picker>
          <select [(ngModel)]="campo" (ngModelChange)="search()" class="form-input" style="width: 170px;">
            <option value="">Campo: Todos</option>
            <option value="price">Precio</option>
            <option value="stock">Stock</option>
            <option value="name_es">Nombre</option>
            <option value="marca">Marca</option>
            <option value="categoria">Categoría</option>
            <option value="regimen_turismo">Régimen turismo</option>
            <option value="url_image">Imagen</option>
          </select>
          <select [(ngModel)]="origen" (ngModelChange)="search()" class="form-input" style="width: 170px;">
            <option value="">Origen: Todos</option>
            <option value="sync">Sync</option>
            <option value="reconciliacion">Reconciliación (bajas)</option>
            <option value="api">API</option>
            <option value="historico">Histórico</option>
          </select>
          <button (click)="search()" class="btn btn-primary">Buscar</button>
        </div>
        <div class="search-bar">
          <label class="label-dim">📅 Rango de fechas:</label>
          <input type="date" [(ngModel)]="desde" (ngModelChange)="search()" class="form-input" style="width: 120px;" />
          <span style="color: #8f8f8f;">hasta</span>
          <input type="date" [(ngModel)]="hasta" (ngModelChange)="search()" class="form-input" style="width: 120px;" />
          <app-quick-range (pick)="setQuickRange($event)" (clear)="desde = ''; hasta = ''; search();"></app-quick-range>
          <select [(ngModel)]="pageSize" (ngModelChange)="search()" class="form-input" style="width: 140px;">
            <option [ngValue]="25">25 por página</option>
            <option [ngValue]="50">50 por página</option>
            <option [ngValue]="100">100 por página</option>
          </select>
        </div>

        <div class="label-dim" *ngIf="lista">{{ lista.totalCount }} cambios</div>
        <app-cambios-lista [items]="items"></app-cambios-lista>

        <div *ngIf="totalPages > 1" class="pagination">
          <button [disabled]="page === 1" (click)="goToPage(1)">«</button>
          <button [disabled]="page === 1" (click)="goToPage(page - 1)">‹</button>
          <span class="page-info">Pág {{ page }}/{{ totalPages }}</span>
          <button [disabled]="page === totalPages" (click)="goToPage(page + 1)">›</button>
          <button [disabled]="page === totalPages" (click)="goToPage(totalPages)">»</button>
        </div>
      </div>
    </div>
  `
})
export class CambiosComponent implements OnInit {
  lista: CambioList | null = null;
  items: Cambio[] = [];

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
      },
      () => this.errors.show('Error cargando cambios')
    );
  }

  setQuickRange(range: string) {
    const r = quickRange(range);
    this.desde = r.desde;
    this.hasta = r.hasta;
    this.search();
  }
}
