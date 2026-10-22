import { Pipe, PipeTransform } from '@angular/core';
import { fechaPy } from './fechas';

/** {{ valor | fechaPy:'yyyy-MM-dd HH:mm' }} — muestra la fecha en hora de Paraguay. */
@Pipe({ name: 'fechaPy', standalone: true })
export class FechaPyPipe implements PipeTransform {
  transform(valor: string | Date | null | undefined, formato = 'yyyy-MM-dd HH:mm:ss'): string {
    return fechaPy(valor, formato);
  }
}
