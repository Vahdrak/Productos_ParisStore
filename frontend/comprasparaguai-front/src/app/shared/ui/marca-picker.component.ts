import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import { CatalogoService, Marca } from '../services/catalogo.service';

export interface MarcaSeleccion {
  /** Id de la marca elegida, o null si el texto no coincide con ninguna. */
  id: number | null;
  nombre: string;
}

/** Cuadro de búsqueda de marca con autocompletar (datalist). Emite el id cuando el texto coincide con una marca. */
@Component({
  selector: 'app-marca-picker',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <label class="sr-only" [attr.for]="inputId">{{ ariaLabel }}</label>
    <input type="text" class="form-input" [id]="inputId" [attr.list]="listId" [placeholder]="placeholder" [attr.aria-label]="ariaLabel"
           [(ngModel)]="texto" (ngModelChange)="onTexto($event)" autocomplete="off" />
    <datalist [id]="listId">
      <option *ngFor="let m of sugerencias" [value]="m.nombre">{{ m.productos }} productos</option>
    </datalist>
  `,
  styles: [`:host { display: contents; }`]
})
export class MarcaPickerComponent {
  private static contador = 0;

  @Input() placeholder = 'Marca...';
  /** Nombre accesible del cuadro (el placeholder no cuenta como etiqueta). */
  @Input() ariaLabel = 'Marca';
  @Output() marcaChange = new EventEmitter<MarcaSeleccion>();

  private readonly n = MarcaPickerComponent.contador++;
  readonly listId = `marcas-list-${this.n}`;
  readonly inputId = `marca-input-${this.n}`;
  texto = '';
  sugerencias: Marca[] = [];

  private idActual: number | null = null;
  private busqueda$ = new Subject<string>();

  constructor(private catalogo: CatalogoService) {
    this.busqueda$.pipe(
      debounceTime(250),
      distinctUntilChanged(),
      switchMap(t => this.catalogo.getMarcas(t, 1, 15).pipe(catchError(() => of(null)))),
      takeUntilDestroyed()
    ).subscribe(lista => {
      this.sugerencias = lista?.items ?? [];
      this.emitirSiCambio();   // el texto ya escrito puede coincidir con una sugerencia recién llegada
    });
  }

  onTexto(texto: string) {
    if (!texto.trim()) {
      this.sugerencias = [];
      this.idActual = null;
      this.marcaChange.emit({ id: null, nombre: '' });
      return;
    }
    this.emitirSiCambio();
    this.busqueda$.next(texto.trim());
  }

  /** Limpia el cuadro desde afuera. */
  limpiar() {
    this.texto = '';
    this.onTexto('');
  }

  private emitirSiCambio() {
    const exacta = this.sugerencias.find(m => m.nombre.toLowerCase() === this.texto.trim().toLowerCase());
    const id = exacta?.id ?? null;
    if (id !== this.idActual) {
      this.idActual = id;
      this.marcaChange.emit({ id, nombre: this.texto.trim() });
    }
  }
}
