import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { HttpService } from './http.service';

export type Destino = 'compras' | 'compara';

export interface EnvioPausa {
  id: number;
  target: string;
  marcaId: number | null;
  marca: string | null;
  categoriaId: number | null;
  categoriaRuta: string | null;
  retirarDelDestino: boolean;
  activo: boolean;
  motivo: string | null;
  createdBy: string | null;
  updatedBy: string | null;
  createdAt: string;
  updatedAt: string;
  /** Productos que cubre la pausa. */
  productos: number;
}

export interface EnvioImpacto {
  productos: number;
  pendientes: number;
  publicados: number;
}

export interface EnvioExcepcion {
  accion: 'enviar' | 'retener';
  activo: boolean;
  motivo: string | null;
  updatedBy: string | null;
  updatedAt: string;
}

export interface EnvioEstadoProducto {
  target: Destino;
  excepcion: EnvioExcepcion | null;
  retenido: boolean;
  retenidoPor: string | null;
  retirado: boolean;
}

export interface ReglaHistorial {
  id: number;
  tabla: string;
  reglaId: number;
  accion: string;
  detalle: string | null;
  usuario: string | null;
  changedAt: string;
}

export interface ReglaHistorialList {
  items: ReglaHistorial[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

/** Un producto que hoy NO se envía al destino, y por qué. */
export interface Retenido {
  code: string;
  namePt: string | null;
  marca: string | null;
  categoriaRuta: string | null;
  stock: number;
  /** Estado de envío al destino: 0 pendiente, 1 enviado, 2 error. */
  estado: number;
  retenidoPor: string | null;
  /** True si lo retiene una excepción propia del SKU (se quita); false si lo retiene una pausa. */
  porExcepcion: boolean;
  motivo: string | null;
}

export interface RetenidosList {
  items: Retenido[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  porPausa: number;
  porExcepcion: number;
}

/** Lo que se aplica en bloque: los códigos dados o el filtro de la lista de productos. */
export interface LoteExcepcion {
  target: Destino | 'ambos';
  accion: 'retener' | 'enviar' | 'quitar';
  motivo?: string;
  codes?: string[];
  code?: string;
  name?: string;
  syncStatus?: number;
  filtroTarget?: string;
  marcaId?: number | null;
  categoriaId?: number | null;
  regimen?: boolean;
}

export interface MarcaCategoria {
  id: number;
  nombre: string;
  nivel: number;
  ruta: string;
  productos: number;
  /** Id de la pausa activa de "esta marca en esta categoría", si existe. */
  pausaId: number | null;
}

/** Pausas de envío por marca/categoría, excepciones por SKU y su historial. Escribir exige sesión. */
@Injectable({ providedIn: 'root' })
export class EnvioService {
  constructor(private http: HttpService) {}

  getPausas(target: Destino): Observable<EnvioPausa[]> {
    return this.http.get<EnvioPausa[]>(`/envio-pausas?target=${target}`);
  }

  getImpacto(target: Destino, marcaId: number | null, categoriaId: number | null): Observable<EnvioImpacto> {
    let url = `/envio-pausas/impacto?target=${target}`;
    if (marcaId) url += `&marcaId=${marcaId}`;
    if (categoriaId) url += `&categoriaId=${categoriaId}`;
    return this.http.get<EnvioImpacto>(url);
  }

  crearPausa(p: { target: Destino; marcaId?: number | null; categoriaId?: number | null; motivo?: string; retirarDelDestino?: boolean }): Observable<EnvioPausa> {
    return this.http.post<EnvioPausa>('/envio-pausas', p);
  }

  actualizarPausa(id: number, cambios: { activo?: boolean; motivo?: string; retirarDelDestino?: boolean }): Observable<void> {
    return this.http.patch<void>(`/envio-pausas/${id}`, cambios);
  }

  borrarPausa(id: number): Observable<void> {
    return this.http.delete<void>(`/envio-pausas/${id}`);
  }

  getHistorial(target: Destino | null, page = 1, pageSize = 25): Observable<ReglaHistorialList> {
    let url = `/envio-pausas/historial?page=${page}&pageSize=${pageSize}`;
    if (target) url += `&target=${target}`;
    return this.http.get<ReglaHistorialList>(url);
  }

  getCategoriasDeMarca(marcaId: number, target: Destino): Observable<MarcaCategoria[]> {
    return this.http.get<MarcaCategoria[]>(`/marcas/${marcaId}/categorias?target=${target}`);
  }

  getEstadoProducto(code: string): Observable<EnvioEstadoProducto[]> {
    return this.http.get<EnvioEstadoProducto[]>(`/productos/${encodeURIComponent(code)}/envio`);
  }

  guardarExcepcion(code: string, target: Destino, accion: 'enviar' | 'retener', motivo?: string): Observable<void> {
    return this.http.put<void>(`/productos/${encodeURIComponent(code)}/excepcion?target=${target}`, { accion, motivo });
  }

  /** Todo lo que hoy no se envía al destino (por pausa o por excepción). */
  getRetenidos(target: Destino, origen: '' | 'pausa' | 'excepcion', q: string, page = 1, pageSize = 50): Observable<RetenidosList> {
    let url = `/envio/retenidos?target=${target}&page=${page}&pageSize=${pageSize}`;
    if (origen) url += `&origen=${origen}`;
    if (q) url += `&q=${encodeURIComponent(q)}`;
    return this.http.get<RetenidosList>(url);
  }

  /** Retener / forzar el envío / quitar la excepción de muchos productos a la vez. Exige sesión. */
  excepcionesLote(p: LoteExcepcion): Observable<{ afectados: number }> {
    return this.http.post<{ afectados: number }>("/envio/excepciones/lote", {
      target: p.target,
      accion: p.accion,
      motivo: p.motivo ?? null,
      codes: p.codes?.length ? p.codes : null,
      code: p.code ?? null,
      name: p.name ?? null,
      syncStatus: p.syncStatus ?? null,
      filtroTarget: p.filtroTarget ?? null,
      marcaId: p.marcaId ?? null,
      categoriaId: p.categoriaId ?? null,
      regimen: p.regimen ?? null
    });
  }

  borrarExcepcion(code: string, target: Destino): Observable<void> {
    return this.http.delete<void>(`/productos/${encodeURIComponent(code)}/excepcion?target=${target}`);
  }
}
