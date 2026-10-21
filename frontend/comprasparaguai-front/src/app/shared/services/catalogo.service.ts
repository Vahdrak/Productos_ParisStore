import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { HttpService } from './http.service';

export interface Marca {
  id: number;
  nombre: string;
  activo: boolean;
  productos: number;
  pendientesCompras: number;
  pendientesCompara: number;
  /** Id de la pausa activa de esta marca (sola, sin categoría) por destino; null si no está pausada. */
  pausaIdCompras?: number | null;
  pausaIdCompara?: number | null;
}

export interface MarcaList {
  items: Marca[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface CategoriaNodo {
  id: number;
  parentId: number | null;
  nombre: string;
  nivel: number;
  ruta: string;
  activo: boolean;
  /** Productos del nodo y de todos sus descendientes. */
  productos: number;
  pendientesCompras: number;
  pendientesCompara: number;
  /** Id de la pausa activa de ESTE nodo (solo categoría) por destino; null si no está pausado. */
  pausaIdCompras?: number | null;
  pausaIdCompara?: number | null;
  /** Pausado porque un nivel superior tiene una pausa de categoría activa. */
  heredadaCompras: boolean;
  heredadaCompara: boolean;
  hijos: CategoriaNodo[];
}

/** Catálogo normalizado: marcas y árbol de categorías. */
@Injectable({ providedIn: 'root' })
export class CatalogoService {
  constructor(private http: HttpService) {}

  getMarcas(search?: string, page = 1, pageSize = 50): Observable<MarcaList> {
    let url = `/marcas?page=${page}&pageSize=${pageSize}`;
    if (search) url += `&search=${encodeURIComponent(search)}`;
    return this.http.get<MarcaList>(url);
  }

  getArbolCategorias(): Observable<CategoriaNodo[]> {
    return this.http.get<CategoriaNodo[]>('/categorias/arbol');
  }
}
