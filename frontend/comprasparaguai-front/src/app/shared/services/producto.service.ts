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

@Injectable({
  providedIn: 'root'
})
export class ProductoService {
  constructor(private http: HttpService) {}

  getPendingProducts(): Observable<Producto[]> {
    return this.http.get<Producto[]>('/productos/pending');
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
    return this.http.get<SyncRun[]>('/sync-runs');
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
}
