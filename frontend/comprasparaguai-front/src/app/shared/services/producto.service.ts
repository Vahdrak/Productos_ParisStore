import { Injectable } from '@angular/core';
import { HttpService } from './http.service';
import { Observable } from 'rxjs';

export interface Producto {
  code: string;
  nameEs?: string;
  namePt: string;
  price?: number;
  priceIva?: number;
  regimenTurismo?: boolean;
  precioExenta?: number;
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
  syncComparaStatus?: number;
  lastSentAt?: string;
  lastSentComparaAt?: string;
  lastError?: string;
  lastErrorCompara?: string;
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

export interface TargetStatus {
  target: string;
  pending: number;
  sent: number;
  error: number;
  total: number;
  lastSentAt: string;
}

export interface LastSyncRun {
  startedAt?: string;
  rowsInView?: number;
  newCount: number;
  updatedCount: number;
  notes?: string;
}

export interface Dashboard {
  targets: TargetStatus[];
  pendingCount: number;
  lastSyncAt?: string;
  lastSyncRun?: LastSyncRun;
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

export interface SendRun {
  id: number;
  target: string;
  mode: string;
  startedAt: string;
  finishedAt?: string;
  total: number;
  sent: number;
  failed: number;
  error?: string;
}

export interface VerifyRun {
  id: number;
  target: string;
  startedAt: string;
  finishedAt?: string;
  sampleSize: number;
  found: number;
  priceMatch: number;
  stockMatch: number;
  csvPath?: string;
}

export interface VerifyResult {
  code: string;
  namePt?: string;
  found: boolean;
  priceLocal?: number;
  priceApi?: number;
  stockLocal?: number;
  stockApi?: number;
  availableApi?: boolean;
  updatedAtApi?: string;
}

@Injectable({
  providedIn: 'root'
})
export class ProductoService {
  constructor(private http: HttpService) {}

  getPendingProducts(target?: string): Observable<Producto[]> {
    let url = '/productos/pending';
    if (target) url += `?target=${target}`;
    return this.http.get<Producto[]>(url);
  }

  getErrorProducts(target?: string): Observable<any[]> {
    let url = '/productos/errors';
    if (target) url += `?target=${target}`;
    return this.http.get<any[]>(url);
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
    if (filters?.target) url += `&target=${filters.target}`;
    return this.http.get<ProductList>(url);
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

  getSendRuns(target?: string, limit: number = 50): Observable<SendRun[]> {
    let url = `/workers/send-runs?limit=${limit}`;
    if (target) url += `&target=${target}`;
    return this.http.get<SendRun[]>(url);
  }

  getVerifyRuns(limit: number = 20): Observable<VerifyRun[]> {
    return this.http.get<VerifyRun[]>(`/workers/verify-runs?limit=${limit}`);
  }

  getVerifyResults(runId: number): Observable<VerifyResult[]> {
    return this.http.get<VerifyResult[]>(`/workers/verify-runs/${runId}`);
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
