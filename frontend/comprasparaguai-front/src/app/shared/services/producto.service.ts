import { Injectable } from '@angular/core';
import { HttpService } from './http.service';
import { Observable } from 'rxjs';

export interface Producto {
  code: string;
  nameEs?: string;
  namePt: string;
  price?: number;
  priceIva?: number;
  stock: number;
  brand?: string;
  descriptionEs?: string;
  descriptionPt?: string;
  urlImage?: string;
  link?: string;
  linkPurchase?: string;
  forceImageUpdate?: boolean;
  sourceHash?: string;
  syncStatus?: number;
}

export interface ProductHistorial {
  changedAt: string;
  nameEs?: string;
  namePt?: string;
  price?: number;
  priceIva?: number;
  stock?: number;
  brand?: string;
  urlImage?: string;
}

export interface ProductDiff {
  code: string;
  current: Producto;
  previous?: ProductHistorial;
  hasChanges: boolean;
}

export interface Dashboard {
  pendingCount: number;
  newCount: number;
  updatedCount: number;
  sentCount: number;
  errorCount: number;
  lastSyncAt?: string;
  status: string;
}

export interface SyncRun {
  id?: number;
  startedAt: string;
  finishedAt?: string;
  rowsInView?: number;
  newCount: number;
  updatedCount: number;
  sentCount: number;
  errorCount: number;
  notes?: string;
}

export interface ProductList {
  items: Producto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ChangeLog {
  code: string;
  productName?: string;
  field: string;
  before?: string;
  after?: string;
  changedAt?: string;
}

export interface ChangeLogList {
  items: ChangeLog[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface CycleSummary {
  timestamp?: string;
  rowsRead: number;
  newCount: number;
  updatedCount: number;
  sentCount: number;
  errorCount: number;
  notes?: string;
}

export interface TrendStatistics {
  totalNew: number;
  totalUpdated: number;
  totalSent: number;
  totalErrors: number;
  averageRowsPerCycle: number;
  successRate: number;
}

export interface SyncTrends {
  hours: number;
  totalCycles: number;
  cycles: CycleSummary[];
  statistics: TrendStatistics;
}

@Injectable({
  providedIn: 'root'
})
export class ProductoService {
  constructor(private http: HttpService) {}

  getPendingProducts(): Observable<Producto[]> {
    return this.http.get<Producto[]>('/productos/pending');
  }

  getErrorProducts(): Observable<any[]> {
    return this.http.get<any[]>('/productos/errors');
  }

  getProductByCode(code: string): Observable<Producto> {
    return this.http.get<Producto>(`/productos/${code}`);
  }

  getProductHistorial(code: string): Observable<ProductHistorial[]> {
    return this.http.get<ProductHistorial[]>(`/productos/${code}/historial`);
  }

  getProductDiff(code: string): Observable<ProductDiff> {
    return this.http.get<ProductDiff>(`/productos/${code}/diff`);
  }

  getDashboard(): Observable<Dashboard> {
    return this.http.get<Dashboard>('/dashboard');
  }

  getSyncRuns(): Observable<SyncRun[]> {
    return this.http.get<SyncRun[]>('/dashboard/sync-runs');
  }

  getSyncTrends(hours: number = 24): Observable<SyncTrends> {
    return this.http.get<SyncTrends>(`/dashboard/trends?hours=${hours}`);
  }

  getRecentChanges(page: number = 1, pageSize: number = 20): Observable<ChangeLogList> {
    return this.http.get<ChangeLogList>(`/productos/cambios/recientes?page=${page}&pageSize=${pageSize}`);
  }

  getProductos(filters?: any, page: number = 1, pageSize: number = 50): Observable<ProductList> {
    let url = `/productos?page=${page}&pageSize=${pageSize}`;
    if (filters?.code) url += `&code=${encodeURIComponent(filters.code)}`;
    if (filters?.name) url += `&name=${encodeURIComponent(filters.name)}`;
    if (filters?.syncStatus !== null && filters?.syncStatus !== undefined) url += `&syncStatus=${filters.syncStatus}`;
    return this.http.get<ProductList>(url);
  }

  getProductsByCategories(): Observable<any[]> {
    return this.http.get<any[]>('/productos/categories');
  }

  createProduct(producto: Producto): Observable<Producto> {
    return this.http.post<Producto>('/productos', producto);
  }

  updateProduct(code: string, producto: Partial<Producto>): Observable<Producto> {
    return this.http.put<Producto>(`/productos/${code}`, producto);
  }

  getCPProducts(token: string, code?: string): Observable<any> {
    let url = `/comprasparaguai/products?token=${encodeURIComponent(token)}`;
    if (code) {
      url += `&code=${encodeURIComponent(code)}`;
    }
    return this.http.get<any>(url);
  }

  importCPProducts(token: string, payload: any): Observable<any> {
    return this.http.post<any>(`/comprasparaguai/import?token=${encodeURIComponent(token)}`, payload);
  }

  verifyImageExists(imageUrl: string): Promise<boolean> {
    return new Promise((resolve) => {
      if (!imageUrl) {
        resolve(false);
        return;
      }
      const img = new Image();
      const timeout = setTimeout(() => {
        resolve(true);
      }, 3000);
      img.onload = () => {
        clearTimeout(timeout);
        resolve(true);
      };
      img.onerror = () => {
        clearTimeout(timeout);
        resolve(true);
      };
      img.src = imageUrl;
    });
  }
}
