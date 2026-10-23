import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input, inject } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

/** Trazos (viewBox 0 0 24 24) de cada icono; se dibujan con stroke=currentColor. */
const ICONOS: Record<string, string> = {
  check: '<path d="M4 12.5l5 5L20 6.5"/>',
  x: '<path d="M6 6l12 12M18 6L6 18"/>',
  warn: '<path d="M12 3.5L2.5 20h19L12 3.5z"/><path d="M12 10v5M12 17.8v.2"/>',
  clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3.5 2"/>',
  refresh: '<path d="M20 11a8 8 0 0 0-14.5-4.5L4 8"/><path d="M4 4v4h4"/><path d="M4 13a8 8 0 0 0 14.5 4.5L20 16"/><path d="M20 20v-4h-4"/>',
  box: '<path d="M12 3l8.5 4.5v9L12 21l-8.5-4.5v-9L12 3z"/><path d="M3.5 7.5L12 12l8.5-4.5M12 12v9"/>',
  send: '<path d="M21 3L10.5 13.5"/><path d="M21 3l-6.5 18-4-7.5L3 9.5 21 3z"/>',
  ban: '<circle cx="12" cy="12" r="9"/><path d="M5.6 5.6l12.8 12.8"/>',
  ticket: '<path d="M3 8a2 2 0 0 0 0 8v1.5A1.5 1.5 0 0 0 4.5 19h15a1.5 1.5 0 0 0 1.5-1.5V16a2 2 0 0 1 0-8V6.5A1.5 1.5 0 0 0 19.5 5h-15A1.5 1.5 0 0 0 3 6.5V8z"/><path d="M14 5v2.5M14 11v2M14 16.5V19"/>',
  calendar: '<rect x="3.5" y="5" width="17" height="15.5" rx="2"/><path d="M3.5 10h17M8 3v4M16 3v4"/>',
  tag: '<path d="M3 12.5V4.5a1.5 1.5 0 0 1 1.5-1.5h8L21 11.5 12.5 20 3 12.5z"/><circle cx="8" cy="8" r="1.3"/>',
  trash: '<path d="M4 7h16M10 11v6M14 11v6"/><path d="M6 7l1 12.5A1.5 1.5 0 0 0 8.5 21h7a1.5 1.5 0 0 0 1.5-1.5L18 7"/><path d="M9 7V4.5A1.5 1.5 0 0 1 10.5 3h3A1.5 1.5 0 0 1 15 4.5V7"/>',
  history: '<path d="M3.5 12a8.5 8.5 0 1 0 2.6-6.1L3.5 8.5"/><path d="M3.5 3.5v5h5"/><path d="M12 7.5V12l3 2"/>',
  plus: '<path d="M12 5v14M5 12h14"/>',
  info: '<circle cx="12" cy="12" r="9"/><path d="M12 11v6M12 7.5v.2"/>',
  lock: '<rect x="4.5" y="10.5" width="15" height="10" rx="2"/><path d="M8 10.5V7.5a4 4 0 0 1 8 0v3"/><path d="M12 15v2"/>',
  search: '<circle cx="10.5" cy="10.5" r="6.5"/><path d="M15.5 15.5L21 21"/>',
  chart: '<path d="M3.5 3.5v17h17"/><path d="M8 16v-5M12.5 16V7.5M17 16v-8.5"/>',
  user: '<circle cx="12" cy="8" r="4"/><path d="M4 20.5a8 8 0 0 1 16 0"/>'
};

const CACHE = new Map<string, SafeHtml>(); // misma referencia en cada detección de cambios

/** Icono inline (línea, hereda el color del texto). Un nombre desconocido no dibuja nada. */
@Component({
  selector: 'app-icon',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<svg *ngIf="trazos"
    xmlns="http://www.w3.org/2000/svg"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="2"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
    focusable="false"
    [attr.width]="size"
    [attr.height]="size"
    [innerHTML]="trazos"></svg>`,
  styles: [`svg { vertical-align: -0.125em; flex: none; }`],
  imports: [CommonModule]
})
export class IconComponent {
  @Input() name = '';
  @Input() size = '1em';

  private readonly sanitizer = inject(DomSanitizer);

  /** Los trazos son constantes de este archivo (no vienen del usuario), por eso es seguro confiar en ellos. */
  get trazos(): SafeHtml | null {
    const crudo = ICONOS[this.name];
    if (!crudo) return null;
    let seguro = CACHE.get(this.name);
    if (!seguro) {
      seguro = this.sanitizer.bypassSecurityTrustHtml(crudo);
      CACHE.set(this.name, seguro);
    }
    return seguro;
  }
}
