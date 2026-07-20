# Image Handling & Verification Guide

## 1. PostgreSQL Columns (Backend)

**Two image columns now exist:**

```sql
url_image      -- Just the filename: "producto123.jpg" (from Oracle)
url_image_full -- Complete URL: "https://www.toku.com.py/media/producto123.jpg"
               -- Built automatically, empty if no valid image
```

**How it works:**
- `url_image_full` is populated automatically when products sync from Oracle
- If `url_image` is empty or "sinimagen.jpg" → `url_image_full` = empty
- Otherwise → concatenates "https://www.toku.com.py/media/" + filename

---

## 2. Frontend Image Verification (Before Sending)

### Use Case
Before sending a product to Compras Paraguai API, verify the image exists:

```typescript
// In your component
async testBeforeSending(product: any) {
  // Check if image URL exists before sending
  const imageExists = await this.service.verifyImageExists(product.urlImageFull);
  
  if (!imageExists) {
    console.warn(`⚠️ Image not found: ${product.urlImageFull}`);
    return; // Don't send
  }
  
  console.log(`✅ Image verified, safe to send`);
  // Proceed with sending to Compras Paraguai API
}
```

### Implementation in Component

```typescript
import { ProductoService } from './shared/services/producto.service';

export class ProductosComponent {
  constructor(private service: ProductoService) {}

  async onSendProduct(code: string) {
    // 1. Get product
    const product = await this.service.getProductByCode(code).toPromise();
    
    // 2. Verify image exists (test mode)
    const hasImage = await this.service.verifyImageExists(product.urlImageFull);
    
    if (!hasImage) {
      alert('❌ Imagen no encontrada. No se puede enviar.');
      return;
    }
    
    // 3. Show image preview (optional)
    console.log('🖼️ Mostrando imagen:', product.urlImageFull);
    
    // 4. NOW safe to send
    console.log('✅ Enviando a Compras Paraguai...');
    // Call send API here
  }
}
```

### HTML Template Example

```html
<div class="product-test-panel">
  <div class="product-info">
    <h3>{{ product.code }}</h3>
    <p>{{ product.namePt }}</p>
  </div>

  <!-- Image Preview -->
  <div class="image-preview" *ngIf="product.urlImageFull">
    <img 
      [src]="product.urlImageFull" 
      [alt]="product.code"
      class="thumb"
      (error)="onImageLoadError($event)"
      (load)="onImageLoaded($event)" />
  </div>
  <div *ngIf="!product.urlImageFull" class="no-image">
    Sin imagen
  </div>

  <!-- Verification Button -->
  <button (click)="onSendProduct(product.code)" class="btn-send">
    🚀 Probar Envío (verifica imagen primero)
  </button>
</div>
```

---

## 3. Architecture: Worker vs Backend API

### The Flow

```
Oracle (Cangrejo) 
   ↓
   └─→ WORKER reads → PostgreSQL writes
       │
       ├─→ Sends directly to Compras Paraguai API ✅ (Worker's job)
       │
       └─→ Also saves to PostgreSQL (our DB)
                   ↓
            Backend API (read-only)
                   ↓
            Angular Frontend (displays data)
```

### Each Service's Responsibility

| Service | Port | Job |
|---------|------|-----|
| **Worker** | (background) | Read Oracle → Write PostgreSQL → Send to Compras Paraguai API |
| **Backend API** | 5000 | Read PostgreSQL → Serve to Angular frontend |
| **Frontend** | 4200 | Display data, test images, trigger UI actions |

### Key Point
- **Worker is INDEPENDENT** ✅
  - Runs every 15 minutes automatically
  - Doesn't need Backend API to run
  - Doesn't need Frontend to be open
  - Sends directly to Compras Paraguai API

- **Backend API is READ-ONLY** ✅
  - Never sends anything to Compras Paraguai
  - Only reads from PostgreSQL
  - Serves data to Angular
  - Can run independently or alongside Worker

---

## 4. Testing Image Verification

### Direct Test in Console

```typescript
// Get service
import { ProductoService } from './app/shared/services/producto.service';

// Test image URL
const service = new ProductoService(...);
const result = await service.verifyImageExists('https://www.toku.com.py/media/test.jpg');
console.log(result); // true or false
```

### Test in Component

```typescript
async ngOnInit() {
  const products = await this.service.getProductsByCategories().toPromise();
  
  for (const category of products) {
    for (const product of category.products) {
      const hasImage = await this.service.verifyImageExists(product.urlImageFull);
      console.log(`${product.code}: ${hasImage ? '✅' : '❌'}`);
    }
  }
}
```

---

## 5. Migration Applied

```sql
-- Migration 002_add_full_image_url_column.sql
ALTER TABLE productos ADD COLUMN url_image_full VARCHAR(1000);
CREATE INDEX ix_productos_url_image_full ON productos(url_image_full)
WHERE url_image_full IS NOT NULL;
```

Run this on your PostgreSQL to add the column.

---

## Summary

✅ **Backend**: url_image_full auto-built when syncing from Oracle  
✅ **Frontend**: verifyImageExists() checks if image URL is accessible  
✅ **Worker**: Independent, runs every 15min, sends directly to Compras Paraguai  
✅ **Backend API**: Read-only, serves data to Angular, doesn't send anything  

**Use case flow:**
1. User clicks "Probar Envío" button in Frontend
2. Frontend calls `verifyImageExists(product.urlImageFull)`
3. JavaScript Image API tests if URL loads
4. If image exists → safe to proceed with send
5. If not found → warn user, don't send
