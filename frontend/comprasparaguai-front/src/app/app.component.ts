import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClientModule } from '@angular/common/http';
import { ProductosTestComponent } from './pages/productos-test/productos-test.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, HttpClientModule, ProductosTestComponent],
  template: `
    <div class="app-container">
      <app-productos-test></app-productos-test>
    </div>
  `,
  styles: [`
    .app-container {
      min-height: 100vh;
      background: var(--surface-0);
    }
  `]
})
export class AppComponent {
  title = 'Compras Paraguai Frontend';
}
