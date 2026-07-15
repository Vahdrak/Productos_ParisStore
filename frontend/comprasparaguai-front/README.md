# Compras Paraguai - Frontend Angular

Frontend de monitoreo y prueba de productos para Compras Paraguai.

## Estructura de carpetas

```
src/
├── app/
│   ├── shared/
│   │   ├── components/      # Componentes reutilizables (librería)
│   │   │   ├── btn.component.ts         # Botón
│   │   │   ├── input.component.ts       # Campo de texto
│   │   │   ├── card.component.ts        # Contenedor tarjeta
│   │   │   ├── badge.component.ts       # Etiqueta de estado
│   │   │   └── index.ts                 # Barrel export
│   │   └── services/
│   │       ├── http.service.ts          # Cliente HTTP base
│   │       └── producto.service.ts      # Consumo API de productos
│   ├── pages/
│   │   └── productos-test/
│   │       └── productos-test.component.ts   # Página CRUD de prueba
│   └── app.component.ts      # Componente raíz
├── main.ts                   # Entry point
├── index.html               # Template HTML
├── styles.css               # Estilos globales
└── favicon.ico              # Favicon
```

## Componentes (Librería)

### BtnComponent
```html
<app-btn 
  label="Guardar" 
  variant="primary|danger|default"
  [disabled]="false"
  (onClick)="onSave()" />
```

### InputComponent
```html
<app-input
  label="Nombre"
  placeholder="Ingresa tu nombre"
  type="text|number|email"
  hint="Campo requerido"
  [(ngModel)]="value"
  (inputValue)="onValueChange($event)" />
```

### CardComponent
```html
<app-card 
  title="Mi tarjeta"
  [elevated]="true"
  [showFooter]="true">
  
  <!-- Contenido principal -->
  <p>Contenido de la tarjeta</p>
  
  <!-- Footer (opcional) -->
  <div card-footer>
    <app-btn label="Aceptar" />
  </div>
</app-card>
```

### BadgeComponent
```html
<app-badge 
  label="Pendiente" 
  variant="pending|sent|error|info" />
```

## Instalación

```bash
cd frontend/comprasparaguai-front
npm install
```

## Desarrollo

```bash
npm start
```

La aplicación estará disponible en `http://localhost:4200`

## API

Conecta a `http://localhost:5000/api`

Endpoints consumidos:
- GET `/productos/pending`
- GET `/productos/{code}`
- GET `/productos/{code}/historial`
- GET `/productos/{code}/diff`
- PUT `/productos/{code}`
