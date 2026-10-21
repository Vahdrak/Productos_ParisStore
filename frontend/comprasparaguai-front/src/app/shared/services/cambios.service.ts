import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { HttpService } from './http.service';

export interface CampoCambio {
  campo: string;
  antes: string | null;
  despues: string | null;
}

export interface Cambio {
  id: number;
  changedAt: string;
  code: string;
  nombre?: string;
  marca?: string;
  /** "Alta" (producto nuevo) o "Cambio". */
  operacion: string;
  origen?: string;
  usuario?: string;
  cambios: CampoCambio[];
}

export interface CambioList {
  items: Cambio[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface CambiosFiltro {
  code?: string;
  q?: string;
  marcaId?: number | null;
  categoriaId?: number | null;
  campo?: string;
  origen?: string;
  desde?: string;
  hasta?: string;
}

/** Historial de cambios de productos, con diff por campo. */
@Injectable({ providedIn: 'root' })
export class CambiosService {
  constructor(private http: HttpService) {}

  getCambios(filtro: CambiosFiltro, page = 1, pageSize = 50): Observable<CambioList> {
    const params: string[] = [`page=${page}`, `pageSize=${pageSize}`];
    if (filtro.code) params.push(`code=${encodeURIComponent(filtro.code)}`);
    if (filtro.q) params.push(`q=${encodeURIComponent(filtro.q)}`);
    if (filtro.marcaId) params.push(`marcaId=${filtro.marcaId}`);
    if (filtro.categoriaId) params.push(`categoriaId=${filtro.categoriaId}`);
    if (filtro.campo) params.push(`campo=${encodeURIComponent(filtro.campo)}`);
    if (filtro.origen) params.push(`origen=${encodeURIComponent(filtro.origen)}`);
    if (filtro.desde) params.push(`desde=${filtro.desde}`);
    if (filtro.hasta) params.push(`hasta=${filtro.hasta}`);
    return this.http.get<CambioList>(`/cambios?${params.join('&')}`);
  }
}
