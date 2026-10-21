/** Estado de envío por destino: 0 = Pendiente, 1 = Enviado, 2 = Error. */
export function statusLabel(status?: number | null): string {
  return status === 0 ? '⏳ Pendiente' : status === 1 ? '✅ Enviado' : '❌ Error';
}

export function statusClass(status?: number | null): any {
  return {
    'badge-success': status === 1,
    'badge-error': status === 2
  };
}

export function statusField(target: string): 'syncStatus' | 'syncComparaStatus' {
  return target === 'compras' ? 'syncStatus' : 'syncComparaStatus';
}
