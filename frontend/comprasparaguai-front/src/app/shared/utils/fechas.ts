/**
 * Fechas del panel. Todo se guarda en UTC en la BD; algunas fechas llegan con zona ("...Z") y otras sin ella
 * (columnas timestamp). Las sin zona SON UTC. Todo se muestra y se filtra en hora de Paraguay (America/Asuncion,
 * UTC-3 fijo desde octubre de 2024), sin importar la zona horaria de la PC que mira el panel.
 */
export const ZONA_PARAGUAY = 'America/Asuncion';

/** Interpreta un valor de la API como instante: sin zona explícita se asume UTC. */
export function parseFecha(valor: string | Date | null | undefined): Date | null {
  if (!valor) return null;
  if (valor instanceof Date) return isNaN(valor.getTime()) ? null : valor;
  const texto = valor.trim();
  const conZona = /([zZ]|[+-]\d{2}:?\d{2})$/.test(texto);
  const d = new Date(conZona ? texto : texto.replace(' ', 'T') + 'Z');
  return isNaN(d.getTime()) ? null : d;
}

const formateador = new Intl.DateTimeFormat('en-GB', {
  timeZone: ZONA_PARAGUAY,
  year: 'numeric', month: '2-digit', day: '2-digit',
  hour: '2-digit', minute: '2-digit', second: '2-digit',
  hourCycle: 'h23'
});

function partes(d: Date): Record<string, string> {
  const r: Record<string, string> = {};
  for (const p of formateador.formatToParts(d)) r[p.type] = p.value;
  return r;
}

/** Formatea en hora de Paraguay. Tokens: yyyy MM dd HH mm ss (por defecto 'yyyy-MM-dd HH:mm:ss'). */
export function fechaPy(valor: string | Date | null | undefined, formato = 'yyyy-MM-dd HH:mm:ss'): string {
  const d = parseFecha(valor);
  if (!d) return '';
  const p = partes(d);
  return formato
    .replace('yyyy', p['year']).replace('MM', p['month']).replace('dd', p['day'])
    .replace('HH', p['hour']).replace('mm', p['minute']).replace('ss', p['second']);
}

/** Día (yyyy-MM-dd) de un instante, en hora de Paraguay. */
export function diaPy(valor: string | Date | null | undefined): string {
  return fechaPy(valor, 'yyyy-MM-dd');
}

/** Un día cualquiera "hace N días" respecto a hoy en Paraguay, como yyyy-MM-dd. */
function diaPyHaceDias(dias: number): string {
  return diaPy(new Date(Date.now() - dias * 24 * 60 * 60 * 1000));
}

/** ¿La fecha cae dentro del rango [desde, hasta] (días completos de Paraguay)? Sin fecha o sin rango = true. */
export function isDateInRange(date: string | Date | null | undefined, desde: string, hasta: string): boolean {
  if (!date) return true;
  if (!desde && !hasta) return true;

  const dia = diaPy(date);
  if (!dia) return true;
  if (desde && dia < desde) return false;
  if (hasta && dia > hasta) return false;
  return true;
}

/** Rango rápido ('hoy' | 'hora' | 'semana' | 'mes' | 'todo') como fechas yyyy-MM-dd de Paraguay. */
export function quickRange(range: string): { desde: string; hasta: string } {
  const hoy = diaPyHaceDias(0);
  switch (range) {
    case 'hoy':
      return { desde: hoy, hasta: hoy };
    case 'hora': // última hora: el día de hace 60 minutos hasta hoy
      return { desde: diaPy(new Date(Date.now() - 60 * 60 * 1000)), hasta: hoy };
    case 'semana':
      return { desde: diaPyHaceDias(7), hasta: hoy };
    case 'mes':
      return { desde: diaPyHaceDias(30), hasta: hoy };
    default:
      return { desde: '', hasta: '' };
  }
}
