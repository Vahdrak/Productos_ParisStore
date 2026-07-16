import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BtnComponent, InputComponent, CardComponent } from '../../shared/components';
import { ProductoService, Producto, ProductDiff, ProductHistorial } from '../../shared/services/producto.service';

@Component({
  selector: 'app-productos-page',
  standalone: true,
  imports: [CommonModule, FormsModule, BtnComponent, InputComponent, CardComponent],
  template: `
    <div class="productos">
      <h2>Buscar Producto</h2>
      <p class="subtitle">Edita y visualiza cambios</p>

      <app-card title="Búsqueda" [elevated]="true">
        <div class="search-box">
          <app-input
            label="Código del producto"
            placeholder="Ej: PROD-001847"
            [(ngModel)]="searchCode"
            hint="Ingresa el código para buscar" />
          <app-btn label="Buscar" variant="primary" (onClick)="search()" />
        </div>

        <div *ngIf="loading" class="msg loading">Buscando...</div>
        <div *ngIf="error" class="msg error">⚠️ {{ error }}</div>
      </app-card>

      <!-- Resultado -->
      <div *ngIf="product">
        <app-card [title]="product.code" [elevated]="true">
          <div class="product-info">
            <div><strong>Nombre:</strong> {{ product.namePt }}</div>
            <div><strong>Precio:</strong> {{ product.price | currency }}</div>
            <div><strong>Stock:</strong> {{ product.stock }}</div>
            <div *ngIf="product.brand"><strong>Marca:</strong> {{ product.brand }}</div>
          </div>

          <div class="actions">
            <app-btn label="Editar" (onClick)="toggleEdit()" />
            <app-btn label="Ver Diff" variant="primary" (onClick)="loadDiff()" />
            <app-btn label="Historial" (onClick)="loadHistory()" />
          </div>
        </app-card>
      </div>

      <!-- Edición -->
      <div *ngIf="editing && product">
        <app-card title="Editar" [elevated]="true">
          <div class="form">
            <app-input label="Nombre" [(ngModel)]="edited.namePt" />
            <app-input label="Precio" type="number" [(ngModel)]="edited.price" />
            <app-input label="Stock" type="number" [(ngModel)]="edited.stock" />
          </div>

          <div class="actions">
            <app-btn label="Guardar" variant="primary" (onClick)="save()" />
            <app-btn label="Cancelar" (onClick)="toggleEdit()" />
          </div>
        </app-card>
      </div>

      <!-- Diff -->
      <div *ngIf="diff && !showHistory">
        <app-card [title]="'Cambios: ' + diff.code" [elevated]="true">
          <div *ngIf="diff.hasChanges; else noDiff" class="diff">
            <div class="diff-col">
              <h4>Antes</h4>
              <div *ngIf="diff.previous">
                <div>Precio: {{ diff.previous.price | currency }}</div>
                <div>Stock: {{ diff.previous.stock }}</div>
              </div>
            </div>
            <div class="diff-col">
              <h4>Ahora</h4>
              <div>Precio: {{ diff.current.price | currency }}</div>
              <div>Stock: {{ diff.current.stock }}</div>
            </div>
          </div>
          <ng-template #noDiff>
            <p class="info">Sin cambios registrados</p>
          </ng-template>
        </app-card>
      </div>

      <!-- Historial -->
      <div *ngIf="showHistory">
        <app-card title="Historial" [elevated]="true">
          <div *ngIf="history.length > 0; else noHistory" class="history">
            <div *ngFor="let h of history" class="hist-row">
              <div class="hist-date">{{ h.changedAt | date:'short' }}</div>
              <div>Precio: {{ h.price | currency }} | Stock: {{ h.stock }}</div>
            </div>
          </div>
          <ng-template #noHistory>
            <p class="info">Sin historial</p>
          </ng-template>

          <div class="actions">
            <app-btn label="Volver" (onClick)="showHistory = false" />
          </div>
        </app-card>
      </div>
    </div>
  `,
  styles: [`
    .productos {
      max-width: 900px;
    }

    h2 {
      margin: 0 0 4px 0;
      font-size: 24px;
      font-weight: 500;
    }

    .subtitle {
      margin: 0 0 20px 0;
      color: var(--text-secondary);
      font-size: 14px;
    }

    .search-box {
      display: flex;
      gap: 12px;
      align-items: flex-end;
      margin-bottom: 12px;
    }

    .search-box app-input {
      flex: 1;
    }

    .product-info {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding: 12px 0;
      font-size: 14px;
    }

    .form {
      display: flex;
      flex-direction: column;
      gap: 12px;
      margin-bottom: 12px;
    }

    .actions {
      display: flex;
      gap: 8px;
      margin-top: 16px;
      padding-top: 12px;
      border-top: 0.5px solid var(--border);
    }

    .diff {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 24px;
      padding: 12px 0;
    }

    .diff-col {
      font-size: 13px;
    }

    .diff-col h4 {
      margin: 0 0 8px 0;
      color: var(--text-secondary);
      font-size: 13px;
    }

    .diff-col div {
      margin: 4px 0;
    }

    .history {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding: 12px 0;
    }

    .hist-row {
      padding: 12px;
      border-left: 2px solid var(--fill-accent);
      background: var(--surface-1);
      border-radius: var(--radius);
      font-size: 13px;
    }

    .hist-date {
      font-size: 12px;
      color: var(--text-secondary);
      margin-bottom: 4px;
    }

    .msg {
      padding: 12px;
      border-radius: var(--radius);
      font-size: 13px;
      margin-top: 12px;
    }

    .loading {
      background: var(--bg-accent);
      color: var(--text-accent);
    }

    .error {
      background: var(--bg-danger);
      color: var(--text-danger);
    }

    .info {
      padding: 12px;
      text-align: center;
      color: var(--text-secondary);
      font-size: 13px;
    }
  `],
  providers: [ProductoService]
})
export class ProductosPageComponent {
  searchCode = '';
  product: Producto | null = null;
  edited: Partial<Producto> = {};
  editing = false;
  loading = false;
  error = '';
  diff: ProductDiff | null = null;
  history: ProductHistorial[] = [];
  showHistory = false;

  constructor(private service: ProductoService) {}

  search() {
    if (!this.searchCode.trim()) {
      this.error = 'Ingresa código';
      return;
    }

    this.loading = true;
    this.error = '';
    this.product = null;

    this.service.getProductByCode(this.searchCode).subscribe({
      next: (p) => {
        this.product = p;
        this.edited = { ...p };
        this.loading = false;
      },
      error: (e) => {
        this.error = e.message;
        this.loading = false;
      }
    });
  }

  toggleEdit() {
    this.editing = !this.editing;
    if (!this.editing) {
      this.edited = { ...this.product };
    }
  }

  save() {
    if (!this.product) return;
    this.loading = true;
    this.service.updateProduct(this.product.code, this.edited).subscribe({
      next: (p) => {
        this.product = p;
        this.editing = false;
        this.loading = false;
      },
      error: (e) => {
        this.error = e.message;
        this.loading = false;
      }
    });
  }

  loadDiff() {
    if (!this.product) return;
    this.service.getProductDiff(this.product.code).subscribe({
      next: (d) => {
        this.diff = d;
        this.showHistory = false;
      },
      error: (e) => (this.error = e.message)
    });
  }

  loadHistory() {
    if (!this.product) return;
    this.showHistory = true;
    this.service.getProductHistorial(this.product.code).subscribe({
      next: (h) => (this.history = h),
      error: (e) => (this.error = e.message)
    });
  }
}
