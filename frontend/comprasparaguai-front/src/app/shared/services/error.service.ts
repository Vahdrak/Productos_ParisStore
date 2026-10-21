import { Injectable } from '@angular/core';

/** Mensaje de error global: las páginas lo publican y AppComponent lo muestra. */
@Injectable({ providedIn: 'root' })
export class ErrorService {
  message = '';

  show(message: string) {
    this.message = message;
  }

  clear() {
    this.message = '';
  }
}
