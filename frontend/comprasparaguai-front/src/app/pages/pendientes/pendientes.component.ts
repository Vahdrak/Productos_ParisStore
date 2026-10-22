import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PendientesFiltro, PendientesLista, ProductoPendiente, ProductoService } from '../../shared/services/producto.service';
import { ErrorService } from '../../shared/services/error.service';
import { statusLabel } from '../../shared/utils/estado';

@Component({
  selector: 'app-pendientes',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="fade-in">
      <div class="section-card">
        <div class="filter-tabs">
          <button [class.active]="target === 'compras'" (click)="switchTarget('compras')">Compras Paraguai</button>
          <button [class.active]="target === 'compara'" (click)="switchTarget('compara')">Compara Paraguai</button>
        </div>
      </div>

      <div class="section-card">
        <h3 class="section-title">Filtros</h3>
        <div class="filters-grid">
          <input type="text" placeholder="Código..." [(ngModel)]="filtro.code" (keyup.enter)="buscar()" class="form-input" />
          <select [(ngModel)]="filtro.regimen" (ngModelChange)="buscar()" class="form-input">
            <option value="">Régimen: Todos</option>
            <option value="true">🎫 Con régimen turismo</option>
            <option value="false">Normal (sin régimen)</option>
          </select>
          <select [(ngModel)]="filtro.retencion" (ngModelChange)="buscar()" class="form-input">
            <option value="">Envío: Todos</option>
            <option value="enviables">✅ Solo enviables</option>
            <option value="retenidos">🚫 Solo retenidos</option>
          </select>
          <select [(ngModel)]="pageSize" (ngModelChange)="buscar()" class="form-input">
            <option [ngValue]="25">25 por página</option>
            <option [ngValue]="50">50 por página</option>
            <option [ngValue]="100">100 por página</option>
          </select>
          <button class="btn btn-primary" (click)="buscar()">Buscar</button>
        </div>
      </div>

      <div class="section-card">
        <h3 class="section-title">Productos ({{ lista?.totalCount ?? 0 }} resultados)</h3>
        <div class="table-container" *ngIf="lista && lista.items.length > 0">
          <table class="data-table">
            <thead><tr><th>Código</th><th>Nombre</th><th>Régimen</th><th>Precio (IVA)</th><th>Precio Exenta</th><th>Stock</th><th>Estado</th></tr></thead>
            <tbody>
              <tr *ngFor="let p of lista.items">
                <td class="text-bold">{{ p.code }}</td>
                <td>{{ p.namePt | slice:0:35 }}</td>
                <td><span class="badge" [class.badge-success]="p.regimenTurismo" [class.badge-secondary]="!p.regimenTurismo">{{ p.regimenTurismo ? '🎫 Sí' : 'No' }}</span></td>
                <td>{{ p.price | currency:'':'symbol-narrow' }}</td>
                <td>{{ (p.precioExenta || 0) | currency:'':'symbol-narrow' }}</td>
                <td class="text-center">{{ p.stock }}</td>
                <td>
                  <span class="badge">{{ estado(p) }}</span>
                  <div *ngIf="p.retenido" class="text-small" style="margin-top: 4px;">
                    <span class="badge badge-warn" title="No se envía mientras dure la pausa">🚫 Retenido</span>
                    <span class="text-dim">{{ p.retenidoPor }}</span>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <div *ngIf="lista && lista.items.length === 0" class="text-center py-8 text-dim">No hay productos que coincidan</div>

        <div *ngIf="lista && lista.totalPages > 1" class="pagination">
          <button [disabled]="page === 1" (click)="irAPagina(1)">«</button>
          <button [disabled]="page === 1" (click)="irAPagina(page - 1)">‹</button>
          <span class="page-info">Pág {{ page }}/{{ lista.totalPages }}</span>
          <button [disabled]="page === lista.totalPages" (click)="irAPagina(page + 1)">›</button>
          <button [disabled]="page === lista.totalPages" (click)="irAPagina(lista.totalPages)">»</button>
        </div>

        <div *ngIf="lista && lista.totalCount > 0" class="summary-bar">
          <div class="summary-item"><span class="summary-label">🎫 Con régimen:</span><span class="summary-value">{{ lista.conRegimen }}</span></div>
          <div class="summary-item"><span class="summary-label">🚫 Retenidos:</span><span class="summary-value">{{ lista.retenidos }}</span></div>
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
  filtro: PendientesFiltro = { code: '', regimen: '', retencion: '' };

  pageSize = 50;
  page = 1;

  constructor(private productoService: ProductoService, private errors: ErrorService) {}

  ngOnInit() {
    this.cargar();
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
    this.productoService.getPendientes(this.target, this.filtro, this.page, this.pageSize).subscribe(
      lista => {
        this.lista = lista;
        this.page = lista.page;
      },
      () => this.errors.show('Error cargando productos pendientes')
    );
  }

  switchTarget(target: string) {
    this.target = target;
    this.buscar();
  }

  estado(p: ProductoPendiente): string {
    return statusLabel(0);
  }
}
