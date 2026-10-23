import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';

/** Paginador «‹ Pág x/y › » (usa las clases globales .pagination / .page-info). No se dibuja con una sola página. */
@Component({
  selector: 'app-paginador',
  standalone: true,
  imports: [CommonModule],
  template: `
    <nav *ngIf="totalPages > 1" aria-label="Paginación">
      <div class="pagination">
        <button type="button" (click)="ir(1)" [disabled]="page <= 1" aria-label="Primera página">«</button>
        <button type="button" (click)="ir(page - 1)" [disabled]="page <= 1" aria-label="Página anterior">‹</button>
        <span class="page-info" aria-current="page">Pág {{ page }}/{{ totalPages }}<ng-container *ngIf="info"> · {{ info }}</ng-container></span>
        <button type="button" (click)="ir(page + 1)" [disabled]="page >= totalPages" aria-label="Página siguiente">›</button>
        <button type="button" (click)="ir(totalPages)" [disabled]="page >= totalPages" aria-label="Última página">»</button>
      </div>
    </nav>
  `
})
export class PaginadorComponent {
  @Input() page = 1;
  @Input() totalPages = 1;
  @Input() info?: string;
  @Output() go = new EventEmitter<number>();

  ir(destino: number): void {
    const objetivo = Math.min(Math.max(1, destino), Math.max(1, this.totalPages));
    this.go.emit(objetivo);
  }
}
