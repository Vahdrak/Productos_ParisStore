import { Component } from '@angular/core';

@Component({
  selector: 'app-api',
  standalone: true,
  template: `
    <div class="fade-in">
      <div class="section-card">
        <h3 class="section-title">🧪 Pruebas de API</h3>
        <p class="text-dim">Endpoints disponibles:</p>
        <ul class="text-dim" style="padding-left: 20px; margin: 12px 0;">
          <li><code>GET /api/dashboard</code> - Estado de ambos destinos</li>
          <li><code>GET /api/productos?target=compras</code> - Productos por destino</li>
          <li><code>GET /api/productos/pending?target=compras</code> - Pendientes</li>
          <li><code>GET /api/productos/errors?target=compras</code> - Errores</li>
          <li><code>GET /api/workers/send-runs?target=compras</code> - Historial de envíos</li>
          <li><code>GET /api/workers/verify-runs</code> - Historial de verificaciones</li>
        </ul>
      </div>
    </div>
  `
})
export class ApiComponent {}
