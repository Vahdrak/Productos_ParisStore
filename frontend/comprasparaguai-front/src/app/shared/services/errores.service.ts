import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { HttpService } from './http.service';
import { Destino } from './envio.service';

export interface ErrorTipoResumen {
  codigo: string;
  titulo: string;
  queHacer: string;
  reintentable: boolean;
  total: number;
}

export interface ErrorGrupo {
  id: number | null;
  nombre: string;
  total: number;
}

export interface ErrorProducto {
  code: string;
  nombre: string | null;
  marca: string | null;
  categoria: string | null;
  tipo: string;
  tipoTitulo: string;
  /** "envio" o "retiro". */
  etapa: string;
  mensaje: string | null;
  updatedAt: string;
}

export interface ErroresResultado {
  tipos: ErrorTipoResumen[];
  porDepartamento: ErrorGrupo[];
  porMarca: ErrorGrupo[];
  items: ErrorProducto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface ErroresFiltro {
  tipo?: string;
  marcaId?: number | null;
  categoriaId?: number | null;
  q?: string;
}

/** Productos en Error por destino, agrupados por tipo, marca y departamento; y reintento. */
@Injectable({ providedIn: 'root' })
export class ErroresService {
  constructor(private http: HttpService) {}

  getErrores(target: Destino, filtro: ErroresFiltro, page = 1, pageSize = 25): Observable<ErroresResultado> {
    const params = [`target=${target}`, `page=${page}`, `pageSize=${pageSize}`];
    if (filtro.tipo) params.push(`tipo=${encodeURIComponent(filtro.tipo)}`);
    if (filtro.marcaId) params.push(`marcaId=${filtro.marcaId}`);
    if (filtro.categoriaId) params.push(`categoriaId=${filtro.categoriaId}`);
    if (filtro.q) params.push(`q=${encodeURIComponent(filtro.q)}`);
    return this.http.get<ErroresResultado>(`/errores?${params.join('&')}`);
  }

  /** Vuelve a Pendiente los códigos indicados, o todo lo que cumpla el filtro. */
  reintentar(target: Destino, filtro: ErroresFiltro, codes?: string[]): Observable<{ reiniciados: number }> {
    return this.http.post<{ reiniciados: number }>('/errores/reintentar', {
      target,
      tipo: filtro.tipo || null,
      marcaId: filtro.marcaId || null,
      categoriaId: filtro.categoriaId || null,
      q: filtro.q || null,
      codes: codes?.length ? codes : null
    });
  }
}
