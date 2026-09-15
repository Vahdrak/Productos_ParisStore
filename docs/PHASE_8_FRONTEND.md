# Fase 8: Frontend Angular + Component Library

Completada la estructura completa del frontend con librería de componentes reutilizables.

## 📦 Estructura creada

```
frontend/comprasparaguai-front/
├── src/
│   ├── app/
│   │   ├── shared/components/          # Librería de componentes
│   │   │   ├── btn.component.ts        # Botón reutilizable
│   │   │   ├── input.component.ts      # Input de texto
│   │   │   ├── card.component.ts       # Contenedor tarjeta
│   │   │   ├── badge.component.ts      # Etiqueta estado
│   │   │   └── index.ts                # Barrel export
│   │   ├── shared/services/
│   │   │   ├── http.service.ts         # Cliente HTTP
│   │   │   └── producto.service.ts     # API de productos
│   │   ├── pages/productos-test/
│   │   │   └── productos-test.component.ts  # Página CRUD
│   │   └── app.component.ts            # Raíz
│   ├── main.ts                          # Entry point
│   ├── index.html                       # Template
│   └── styles.css                       # Estilos globales
├── package.json
├── tsconfig.json
├── tsconfig.app.json
├── angular.json
└── README.md
```

## 🎨 Componentes de la librería

1. **BtnComponent** - Botón reutilizable
   - Variantes: default, primary, danger
   - Props: label, variant, disabled
   - Event: onClick

2. **InputComponent** - Campo de texto
   - Tipos: text, number, email
   - Props: label, placeholder, type, disabled, hint
   - Implementa ControlValueAccessor (NgModel compatible)
   - Events: inputValue

3. **CardComponent** - Contenedor/tarjeta
   - Props: title, elevated, showFooter
   - Soporte para footer con <ng-content select="[card-footer]">

4. **BadgeComponent** - Etiqueta de estado
   - Variantes: pending, sent, error, info
   - Props: label, variant

## 🔧 Servicios

### HttpService
- Base para todas las llamadas HTTP
- URL: `http://localhost:5000/api`
- Métodos: get, post, put, delete
- Manejo de errores centralizado

### ProductoService
Consumidor de los endpoints:
- `getPendingProducts()` → GET /productos/pending
- `getProductByCode(code)` → GET /productos/{code}
- `getProductHistorial(code)` → GET /productos/{code}/historial
- `getProductDiff(code)` → GET /productos/{code}/diff
- `getDashboard()` → GET /dashboard
- `createProduct(p)` → POST /productos
- `updateProduct(code, p)` → PUT /productos/{code}

## 📄 Página de Prueba (ProductosTestComponent)

Permite pruebas interactivas de CRUD:

### Funcionalidades

1. **Búsqueda**: Ingresa código y busca producto
2. **Visualización**: Muestra todos los campos del producto
3. **Edición**: Edita nombre, precio, stock, descripción, marca, URL
4. **Guardado**: POST/PUT para guardar cambios
5. **Diff**: Compara antes/después del último cambio
6. **Historial**: Timeline completo de cambios (productos_historial)

### Flujo de uso

```
1. Ingresa código (ej: PROD-001847)
2. Click "Buscar" → Carga producto
3. Ver detalles completos
4. Click "Editar" → Modo edición
5. Modifica campos
6. Click "Guardar cambios" → PUT a API
7. Click "Cargar Diff" → Ver antes/después
8. Click "Cargar Historial" → Ver timeline
```

## 🎯 Características de diseño

- **Standalone Components**: Sin módulos, componentes autónomos
- **CSS Variables**: Temas light/dark automáticos (prefers-color-scheme)
- **Responsive**: Grid adaptable a móvil/tablet/desktop
- **Reutilizable**: Componentes sin lógica de negocio
- **Tipado**: Interfaces definidas (Producto, ProductHistorial, ProductDiff, Dashboard)

## 📦 Dependencias principales

```json
{
  "@angular/core": "^18.0.0",
  "@angular/common": "^18.0.0",
  "@angular/forms": "^18.0.0",
  "@angular/platform-browser": "^18.0.0",
  "rxjs": "^7.8.1"
}
```

## 🚀 Próximos pasos

1. Instalar dependencias: `npm install`
2. Iniciar dev server: `npm start`
3. Navegar a `http://localhost:4200`
4. Asegurar que API está disponible en `http://localhost:5000`
5. Probar búsqueda, edición, diff e historial

## 📋 Rutas disponibles

- `/` (ProductosTestComponent) - Página principal de prueba

## 🔐 Notas

- No hay autenticación implementada (cliente de prueba)
- API token se configura en el header desde el Worker
- Frontend es solo lectura/escritura de productos via API
- Estado es local en el componente (sin state management)
- Listo para escalar: fácil agregar más páginas reutilizando componentes

