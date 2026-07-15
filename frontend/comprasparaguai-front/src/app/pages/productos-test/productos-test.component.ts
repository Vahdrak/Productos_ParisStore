import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClientModule } from '@angular/common/http';
import { BtnComponent, InputComponent, CardComponent, BadgeComponent } from '../../shared/components';
import { ProductoService, Producto, ProductDiff, ProductHistorial } from '../../shared/services/producto.service';

@Component({
  selector: 'app-productos-test',
  standalone: true,
  imports: [CommonModule, HttpClientModule, BtnComponent, InputComponent, CardComponent, BadgeComponent],
  template: `
    <div class="container">
      <header class="header">
        <h1>🧪 Prueba de Productos</h1>
        <p>Busca, edita y visualiza cambios de productos específicos</p>
      </header>

      <!-- Búsqueda -->
      <app-card title="Buscar Producto" [elevated]="true">
        <div class="search-row">
          <app-input
            label="Código del producto"
            placeholder="Ej: PROD-001847"
            [inputValue]="searchCode"
            (inputValue)="searchCode = $event"
            hint="Ingresa el código del producto para buscarlo" />
          <app-btn
            label="Buscar"
            variant="primary"
            (onClick)="searchProduct()" />
        </div>
        <div *ngIf="loading" class="loading">Cargando...</div>
        <div *ngIf="error" class="error">⚠️ {{ error }}</div>
      </app-card>

      <!-- Resultado de búsqueda -->
      <div *ngIf="currentProduct" class="results">
        <app-card [title]="'Producto: ' + currentProduct.code" [elevated]="true">
          <div class="product-detail">
            <div class="detail-row">
              <span class="label">Código:</span>
              <span class="value">{{ currentProduct.code }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Nombre (ES):</span>
              <span class="value">{{ currentProduct.nameEs || 'N/A' }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Nombre (PT):</span>
              <span class="value">{{ currentProduct.namePt }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Precio:</span>
              <span class="value">{{ (currentProduct.price || 0) | currency }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Precio + IVA:</span>
              <span class="value">{{ (currentProduct.priceIva || 0) | currency }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Stock:</span>
              <span class="value">{{ currentProduct.stock }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Marca:</span>
              <span class="value">{{ currentProduct.brand || 'N/A' }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Descripción (ES):</span>
              <span class="value">{{ currentProduct.descriptionEs || 'N/A' }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Descripción (PT):</span>
              <span class="value">{{ currentProduct.descriptionPt || 'N/A' }}</span>
            </div>
            <div class="detail-row">
              <span class="label">URL Imagen:</span>
              <span class="value">{{ currentProduct.urlImage || 'N/A' }}</span>
            </div>
          </div>

          <div class="card-footer">
            <app-btn
              label="Cargar Diff"
              variant="primary"
              (onClick)="loadDiff()" />
            <app-btn
              label="Cargar Historial"
              (onClick)="loadHistorial()" />
            <app-btn
              label="Editar"
              (onClick)="toggleEditMode()" />
          </div>
        </app-card>
      </div>

      <!-- Modo edición -->
      <div *ngIf="editMode && currentProduct" class="edit-section">
        <app-card title="Editar Producto" [elevated]="true">
          <div class="form-grid">
            <app-input
              label="Nombre (ES)"
              [(ngModel)]="editedProduct.nameEs"
              placeholder="Nombre en español" />
            <app-input
              label="Nombre (PT)"
              [(ngModel)]="editedProduct.namePt"
              placeholder="Nombre en portugués" />
            <app-input
              label="Precio"
              type="number"
              [(ngModel)]="editedProduct.price"
              placeholder="0.00" />
            <app-input
              label="Precio + IVA"
              type="number"
              [(ngModel)]="editedProduct.priceIva"
              placeholder="0.00" />
            <app-input
              label="Stock"
              type="number"
              [(ngModel)]="editedProduct.stock"
              placeholder="0" />
            <app-input
              label="Marca"
              [(ngModel)]="editedProduct.brand"
              placeholder="Marca del producto" />
          </div>
          <div class="form-grid">
            <app-input
              label="Descripción (ES)"
              [(ngModel)]="editedProduct.descriptionEs"
              placeholder="Descripción en español" />
            <app-input
              label="Descripción (PT)"
              [(ngModel)]="editedProduct.descriptionPt"
              placeholder="Descripción en portugués" />
            <app-input
              label="URL Imagen"
              [(ngModel)]="editedProduct.urlImage"
              placeholder="https://..." />
          </div>

          <div class="card-footer">
            <app-btn
              label="Guardar cambios"
              variant="primary"
              (onClick)="saveProduct()" />
            <app-btn
              label="Cancelar"
              variant="danger"
              (onClick)="toggleEditMode()" />
          </div>
        </app-card>
      </div>

      <!-- Diff -->
      <div *ngIf="productDiff && !historialMode">
        <app-card [title]="'Cambios: ' + productDiff.code" [elevated]="true">
          <div *ngIf="productDiff.hasChanges; else noDiff">
            <div class="diff-comparison">
              <div class="diff-column">
                <h4>Antes</h4>
                <div *ngIf="productDiff.previous" class="diff-values">
                  <div class="diff-item">
                    <span class="label">Nombre (PT):</span>
                    <span class="value">{{ productDiff.previous.namePt }}</span>
                  </div>
                  <div class="diff-item">
                    <span class="label">Precio:</span>
                    <span class="value">{{ (productDiff.previous.price || 0) | currency }}</span>
                  </div>
                  <div class="diff-item">
                    <span class="label">Stock:</span>
                    <span class="value">{{ productDiff.previous.stock }}</span>
                  </div>
                </div>
              </div>
              <div class="diff-column">
                <h4>Ahora</h4>
                <div class="diff-values">
                  <div class="diff-item">
                    <span class="label">Nombre (PT):</span>
                    <span class="value">{{ productDiff.current.namePt }}</span>
                  </div>
                  <div class="diff-item">
                    <span class="label">Precio:</span>
                    <span class="value">{{ (productDiff.current.price || 0) | currency }}</span>
                  </div>
                  <div class="diff-item">
                    <span class="label">Stock:</span>
                    <span class="value">{{ productDiff.current.stock }}</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
          <ng-template #noDiff>
            <p class="empty-state">No hay cambios registrados para este producto</p>
          </ng-template>
        </app-card>
      </div>

      <!-- Historial -->
      <div *ngIf="historialMode && productHistorial.length > 0">
        <app-card title="Historial de cambios" [elevated]="true">
          <div class="historial-list">
            <div *ngFor="let item of productHistorial" class="historial-item">
              <div class="historial-date">{{ item.changedAt | date:'short' }}</div>
              <div class="historial-data">
                <div *ngIf="item.price" class="change">
                  📌 Precio: {{ item.price | currency }}
                </div>
                <div *ngIf="item.stock !== undefined" class="change">
                  📦 Stock: {{ item.stock }}
                </div>
                <div *ngIf="item.namePt" class="change">
                  📝 Nombre: {{ item.namePt }}
                </div>
              </div>
            </div>
          </div>

          <div class="card-footer">
            <app-btn
              label="Volver"
              (onClick)="historialMode = false" />
          </div>
        </app-card>
      </div>

      <div *ngIf="historialMode && productHistorial.length === 0" class="empty-state">
        <p>No hay historial disponible para este producto</p>
        <app-btn
          label="Volver"
          (onClick)="historialMode = false" />
      </div>
    </div>
  `,
  styles: [`
    .container {
      max-width: 1000px;
      margin: 0 auto;
      padding: 24px;
      font-family: var(--font-sans);
      color: var(--text-primary);
    }

    .header {
      margin-bottom: 32px;
      text-align: center;
    }

    .header h1 {
      font-size: 28px;
      font-weight: 500;
      margin: 0 0 8px 0;
    }

    .header p {
      color: var(--text-secondary);
      margin: 0;
    }

    .search-row {
      display: grid;
      grid-template-columns: 1fr auto;
      gap: 12px;
      align-items: flex-end;
    }

    .results {
      margin-top: 24px;
      display: flex;
      flex-direction: column;
      gap: 24px;
    }

    .product-detail {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
    }

    .detail-row {
      display: flex;
      justify-content: space-between;
      padding: 12px;
      border-bottom: 0.5px solid var(--border);
    }

    .detail-row:last-child {
      border-bottom: none;
    }

    .label {
      font-weight: 500;
      color: var(--text-secondary);
      font-size: 13px;
    }

    .value {
      text-align: right;
      color: var(--text-primary);
    }

    .card-footer {
      display: flex;
      gap: 8px;
      margin-top: 16px;
      padding-top: 16px;
      border-top: 0.5px solid var(--border);
    }

    .edit-section {
      margin-top: 24px;
    }

    .form-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
      margin-bottom: 16px;
    }

    .diff-comparison {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 24px;
    }

    .diff-column h4 {
      margin: 0 0 12px 0;
      font-size: 14px;
      font-weight: 500;
      color: var(--text-secondary);
    }

    .diff-values {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .diff-item {
      padding: 12px;
      background: var(--surface-1);
      border-radius: var(--radius);
      font-size: 13px;
    }

    .diff-item .label {
      display: block;
      margin-bottom: 4px;
      color: var(--text-secondary);
    }

    .diff-item .value {
      display: block;
      font-weight: 500;
      color: var(--text-primary);
    }

    .historial-list {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .historial-item {
      padding: 12px;
      border-left: 3px solid var(--fill-accent);
      background: var(--surface-1);
      border-radius: var(--radius);
    }

    .historial-date {
      font-size: 12px;
      font-weight: 500;
      color: var(--text-secondary);
      margin-bottom: 8px;
    }

    .change {
      font-size: 13px;
      color: var(--text-primary);
      margin: 4px 0;
    }

    .loading, .error {
      padding: 12px;
      border-radius: var(--radius);
      font-size: 14px;
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

    .empty-state {
      text-align: center;
      padding: 24px;
      color: var(--text-secondary);
      font-size: 14px;
    }
  `],
  providers: [ProductoService]
})
export class ProductosTestComponent implements OnInit {
  searchCode: string = '';
  currentProduct: Producto | null = null;
  editedProduct: Partial<Producto> = {};
  editMode = false;
  loading = false;
  error: string | null = null;
  productDiff: ProductDiff | null = null;
  productHistorial: ProductHistorial[] = [];
  historialMode = false;

  constructor(private productoService: ProductoService) {}

  ngOnInit(): void {
    // Inicializar
  }

  searchProduct(): void {
    if (!this.searchCode.trim()) {
      this.error = 'Ingresa un código de producto';
      return;
    }

    this.loading = true;
    this.error = null;
    this.currentProduct = null;
    this.productDiff = null;
    this.productHistorial = [];
    this.historialMode = false;

    this.productoService.getProductByCode(this.searchCode).subscribe({
      next: (product) => {
        this.currentProduct = product;
        this.editedProduct = { ...product };
        this.loading = false;
      },
      error: (err) => {
        this.error = `No encontrado: ${err.message}`;
        this.loading = false;
      }
    });
  }

  loadDiff(): void {
    if (!this.currentProduct) return;

    this.productoService.getProductDiff(this.currentProduct.code).subscribe({
      next: (diff) => {
        this.productDiff = diff;
        this.historialMode = false;
      },
      error: (err) => {
        this.error = `Error cargando diff: ${err.message}`;
      }
    });
  }

  loadHistorial(): void {
    if (!this.currentProduct) return;

    this.historialMode = true;
    this.productoService.getProductHistorial(this.currentProduct.code).subscribe({
      next: (historial) => {
        this.productHistorial = historial;
      },
      error: (err) => {
        this.error = `Error cargando historial: ${err.message}`;
        this.historialMode = false;
      }
    });
  }

  toggleEditMode(): void {
    this.editMode = !this.editMode;
    if (!this.editMode) {
      this.editedProduct = { ...this.currentProduct };
    }
  }

  saveProduct(): void {
    if (!this.currentProduct) return;

    this.loading = true;
    this.productoService.updateProduct(this.currentProduct.code, this.editedProduct).subscribe({
      next: (updated) => {
        this.currentProduct = updated;
        this.editMode = false;
        this.error = null;
        this.loading = false;
      },
      error: (err) => {
        this.error = `Error guardando: ${err.message}`;
        this.loading = false;
      }
    });
  }
}
