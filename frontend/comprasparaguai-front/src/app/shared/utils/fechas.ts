/** ¿La fecha cae dentro del rango [desde, hasta] (días completos)? Sin fecha o sin rango = true. */
export function isDateInRange(date: string | Date | null | undefined, desde: string, hasta: string): boolean {
  if (!date) return true;
  if (!desde && !hasta) return true;

  const d = new Date(date);
  if (desde) {
    const desdeDate = new Date(desde);
    desdeDate.setHours(0, 0, 0, 0);
    if (d < desdeDate) return false;
  }
  if (hasta) {
    const hastaDate = new Date(hasta);
    hastaDate.setHours(23, 59, 59, 999);
    if (d > hastaDate) return false;
  }
  return true;
}

/** Rango rápido ('hoy' | 'hora' | 'semana' | 'mes' | 'todo') como fechas yyyy-MM-dd. */
export function quickRange(range: string): { desde: string; hasta: string } {
  const now = new Date();
  const today = now.toISOString().split('T')[0];
  const diasAtras = (dias: number) => new Date(now.getTime() - dias * 24 * 60 * 60 * 1000).toISOString().split('T')[0];

  switch (range) {
    case 'hoy':
      return { desde: today, hasta: today };
    case 'hora': // última hora: desde hace 60 minutos
      return { desde: new Date(now.getTime() - 60 * 60 * 1000).toISOString().split('T')[0], hasta: today };
    case 'semana':
      return { desde: diasAtras(7), hasta: today };
    case 'mes':
      return { desde: diasAtras(30), hasta: today };
    default:
      return { desde: '', hasta: '' };
  }
}
