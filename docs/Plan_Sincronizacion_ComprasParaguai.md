# Plan de Proyecto — Sincronización de Productos hacia Compras Paraguai

**Autor:** Mario
**Fecha:** 13 de julio de 2026
**Objetivo:** Construir un servicio en .NET que mantenga actualizado el catálogo de la tienda (Toku Importados) en el comparador **Compras Paraguai**, leyendo los productos desde el sistema **Cangrejo** y enviando solo lo que cambia.

---

## 1. La idea

La tienda vive en un sistema ajeno llamado **Cangrejo** (base de datos Oracle). Ellos nos dieron acceso de **solo lectura** a una vista: `SENSEI.PARAGUAY_COMPRAS_VIEW` (~109.000 productos).

Por otro lado, **Compras Paraguai** es un **comparador de precios** (no vende) orientado a compradores brasileños y paraguayos. Ofrece una API para que las tiendas suban y mantengan actualizado su catálogo.

Nuestro trabajo es el **puente automático** entre ambos: leer de la vista de Cangrejo, guardar en una base de datos propia, y enviar los productos nuevos/modificados a la API de Compras Paraguai, cada 15 minutos mientras la tienda está abierta.

```
Cangrejo (Oracle)            Nuestro servicio (.NET)              Compras Paraguai
┌───────────────────┐        ┌───────────────────────┐           ┌──────────────────┐
│ PARAGUAY_COMPRAS_ │  read  │  BD propia (PostgreSQL)│   POST    │ API de productos │
│ VIEW (~109k)      │ ─────► │  + Worker de sync      │ ────────► │ /products/import │
└───────────────────┘        └───────────────────────┘           └──────────────────┘
      solo lectura              detecta cambios + enriquece            token en header
```

---

## 2. La estrategia (por qué se hace así)

### 2.1. Una base de datos propia = memoria + escudo
La vista de Cangrejo **no dice qué cambió**. Por eso guardamos una **copia propia** de cada producto con una **huella (hash)** de sus datos. En cada corrida comparamos la vista contra nuestra copia:
- **Nuevo:** está en la vista, no en nuestra BD → se envía.
- **Modificado:** está en ambos, pero el hash cambió → se envía.
- **Baja:** está en nuestra BD, ya no en la vista → se envía con `stock = 0`.

Además, nuestra BD es un **escudo para no sobrecargar a Cangrejo**: todo el trabajo pesado (comparaciones, reintentos, reportes) ocurre contra nuestra copia, no contra su sistema.

### 2.2. Solo viajan los deltas
La API es un **upsert** (inserta o actualiza por `code`), así que no hace falta consultarle qué tiene. Solo enviamos lo pendiente. En régimen normal son unas decenas de productos por ciclo, no 109.000.

### 2.3. Dos velocidades
- **Carga inicial** (una sola vez): subir todo el catálogo.
- **Ciclo recurrente** (cada 15 min): solo los cambios.
- **Reconciliación diaria** (una vez al día): un escaneo completo para detectar las bajas que la lectura incremental no ve.

### 2.4. Empieza simple, evoluciona sin reescribir
Se arranca con **"escaneo completo + hash"** (funciona hoy). Cuando Cangrejo agregue una columna de **versión/fecha de modificación**, se cambia solo la forma de leer (a incremental) y el resto queda igual. Por eso el lector se construye detrás de una interfaz intercambiable.

### 2.5. Capa de enriquecimiento
Nuestra BD no solo espeja a Cangrejo; también **completa lo que Cangrejo no tiene** (traducción al portugués, descripciones, links). Regla de oro:

> **Separar los campos "de Cangrejo" (se refrescan en cada sync) de los campos "propios" (se preservan y nunca se pisan).**

El hash se calcula sobre el **payload final** (Cangrejo + enriquecimiento), así cualquier cambio —venga de donde venga— dispara el reenvío.

---

## 3. Mapeo de campos (Vista → API)

`code` es el **SKU** (código de barras / EAN), confirmado en la prueba real.

| Campo API | Obligatorio | Origen en la vista | Regla / Nota |
|---|---|---|---|
| `code` | ✅ | `SKU` | Tal cual. Es la llave del upsert. |
| `name.pt` | ✅ | *(propio)* | Portugués. MVP: duplicar el español. Luego: traducir. |
| `name.es` | ⬜ | `NOMBRE` | Se refresca desde Cangrejo. |
| `price` | ✅ | `PRECIO_NORMAL` | Debe estar entre **0.05 y 99999**. Si es inválido/vacío → saltar y registrar. |
| `price_iva` | ⬜ | `PRECIO_NORMAL` + IVA | IVA Paraguay 10%. Opcional. |
| `stock` | ✅ | `INVENTARIO` | Negativos → **0**. `0` = no disponible. |
| `brand` | ⬜ | `VALOR_ATRIBUTO_1` (cuando `NOMBRE_ATRIBUTO_1` = "Marca") | Se refresca. En la BD vive en la tabla `marcas` (`productos.marca_id`); vacío/NULL = `BRANDLESS`. Ver "Catálogo normalizado" (sección 4). |
| `description.pt` | ✅ | *(propio)* | No existe en la vista. MVP: nombre + categoría normalizada sin la empresa (`NOMBRE - COCINA > VAJILLAS > PLATILLOS`). |
| `description.es` | ⬜ | *(propio)* | Idem. |
| `url_image` | ✅ | base + `IMAGENES` | Anteponer la URL base. Si es `sinimagen.jpg` → **imagen por defecto**. |
| `link` | ✅ | *(propio)* patrón de tienda + `code` | Definir patrón de URL del producto. |
| `link_purchase` | ⬜ | *(propio)* patrón de tienda | Opcional. |
| `force_image_update` | ⬜ | fijo `false` | `true` solo para forzar recarga de imagen. |

**Leyenda:** ✅ obligatorio · ⬜ opcional · *(propio)* = campo de enriquecimiento (no viene de Cangrejo).

### Calidad de datos a vigilar
- **Stock negativo** (`-2`, `-1`) → forzar a `0`.
- **Precios raros** (`600`, `799` sin decimales) o **vacíos** → validar rango; saltar y registrar los inválidos.
- **`sinimagen.jpg`** → usar imagen por defecto (el campo `url_image` es obligatorio).

---

## 4. Diseño de la base de datos propia (PostgreSQL)

> **Motor:** PostgreSQL (UTF-8 nativo, sin `NVARCHAR`). Convención de nombres `snake_case`.

### Tabla `productos`
```sql
CREATE TABLE productos (
    id                 BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    -- ===== Campos que exige la API =====
    code               VARCHAR(255)  NOT NULL UNIQUE,   -- API: code (= SKU)
    name_es            VARCHAR(255),                    -- API: name.es (de Cangrejo)
    name_pt            VARCHAR(255)  NOT NULL,          -- API: name.pt (propio)
    price              NUMERIC(12,2),                   -- API: price (0.05 - 99999)
    price_iva          NUMERIC(12,2),                   -- API: price_iva
    stock              INT           NOT NULL DEFAULT 0,-- API: stock (nunca negativo)
    brand              VARCHAR(255),                    -- API: brand (de Cangrejo)
    description_es     TEXT,                            -- API: description.es (propio)
    description_pt     TEXT,                            -- API: description.pt (propio)
    url_image          VARCHAR(1000),                   -- API: url_image
    link               VARCHAR(1000),                   -- API: link (propio)
    link_purchase      VARCHAR(1000),                   -- API: link_purchase (propio)
    force_image_update BOOLEAN       NOT NULL DEFAULT FALSE,
    categoria_id       INT,                             -- FK a categorias
    -- ===== Control de sincronización =====
    source_hash        CHAR(64),          -- hash del payload final
    sync_status        SMALLINT   NOT NULL DEFAULT 0,   -- 0=Pendiente, 1=Enviado, 2=Error
    exists_in_view     BOOLEAN    NOT NULL DEFAULT TRUE,-- ¿sigue en la vista?
    last_seen_at       TIMESTAMPTZ,       -- última vez visto en la vista
    last_changed_at    TIMESTAMPTZ,       -- cuándo detectamos el último cambio
    last_sent_at       TIMESTAMPTZ,       -- último envío EXITOSO
    first_sent_at      TIMESTAMPTZ,       -- primer envío (vacío = nunca subido = nuevo)
    last_error         VARCHAR(2000),
    raw_json           JSONB,             -- fila original de la vista (para auditar)
    created_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at         TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX ix_productos_sync_status ON productos(sync_status);
```

### Tabla `categorias` y tabla `marcas` (catálogo normalizado, migración 009)

> El esquema de `productos` de arriba es el original. Desde la migración 009 `productos.brand` (texto)
> se reemplazó por `productos.marca_id → marcas`, y `categorias` pasó de lista plana a **árbol**.
> Ver "Catálogo normalizado" más abajo.

```sql
CREATE TABLE categorias (               -- árbol: cada nodo conoce a su padre
    id         INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    parent_id  INTEGER REFERENCES categorias(id),   -- NULL = raíz ("TOKU IMPORTADOS")
    nombre     VARCHAR(255)  NOT NULL,              -- "PLATILLOS PLASTICO"
    nivel      SMALLINT      NOT NULL,              -- 1 empresa, 2 departamento, 3 sección, 4 familia, 5+ subfamilia
    ruta       VARCHAR(1000) NOT NULL UNIQUE,       -- "TOKU IMPORTADOS>COCINA>VAJILLAS>PLATILLOS PLASTICO"
    activo     BOOLEAN       NOT NULL DEFAULT TRUE, -- tiene productos propios o de descendientes
    created_at TIMESTAMP     NOT NULL DEFAULT now()
);
CREATE TABLE marcas (
    id         INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre     VARCHAR(255) NOT NULL,               -- único sin distinguir mayúsculas; "BRANDLESS" = sin marca
    activo     BOOLEAN      NOT NULL DEFAULT TRUE,  -- tiene productos
    created_at TIMESTAMP    NOT NULL DEFAULT now()
);
CREATE TABLE categoria_alias (          -- memoria del sync: texto crudo de Oracle -> nodo
    ruta_origen  TEXT PRIMARY KEY,
    categoria_id INTEGER NOT NULL REFERENCES categorias(id)
);
```

### Catálogo normalizado: cómo se llena y se mantiene

`PARAGUAY_COMPRAS_VIEW.CATEGORIAS` entrega la categoría como un único texto separado por `>` y con
dos problemas: Cangrejo **repite el nivel** cuando no lo usa (`COCINA>VAJILLAS>VAJILLAS>PLATILLOS`) y algunas
letras llegan como katakana de ancho medio (`NIﾑAS` en vez de `NIÑAS`). Como referencia, la vista interna
`CLASIFICACION_PRODUCTOS_TOKU_VIEW` (sin acceso) confirma la jerarquía Empresa > Departamento > Sección > Familia > Subfamilia.

**Regla de normalización** (`fn_normalizar_ruta_categoria`, espejo en C#: `CategoriaTexto`):
1. Corregir la codificación (U+FF61–U+FF9F → Latin-1, `cp - 0xFF61 + 0xA1`).
2. Recortar cada segmento, colapsar espacios y descartar vacíos.
3. Colapsar segmentos consecutivos iguales.

**Cómo aprende el sync** (`UpsertProductsAsync`): cada texto crudo se normaliza una sola vez y queda en
`categoria_alias`; en una corrida normal todo se resuelve con ese join. Solo una ruta nueva crea nodos
(`fn_asegurar_categoria` crea los prefijos que falten, nivel por nivel). Las marcas se resuelven igual por nombre
(sin distinguir mayúsculas; vacío = `BRANDLESS`). El log informa `Categorías nuevas: N, marcas nuevas: M`.

**Ciclo de vida:** el sync nunca borra categorías ni marcas, así se conservarán los flags de envío que se les
configuren. La reconciliación diaria recalcula `activo` (con productos o sin ellos).

**Pedido a Cangrejo** (mejora futura): exponer un código de categoría (`CODIGO_COMPUESTO` de
`CLASIFICACION_PRODUCTOS_TOKU_VIEW`) en `PARAGUAY_COMPRAS_VIEW` o dar `SELECT` sobre esa vista, y corregir la
codificación de la columna `CATEGORIAS`. Con eso el árbol podría copiarse tal cual y enlazar por código en lugar de por texto.

### Tabla `sync_runs` (bitácora)
```sql
CREATE TABLE sync_runs (
    id            BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    started_at    TIMESTAMPTZ NOT NULL,
    finished_at   TIMESTAMPTZ,
    rows_in_view  INT,   -- filas leídas de la vista
    new_count     INT,   -- productos nuevos
    updated_count INT,   -- productos modificados
    sent          INT,   -- enviados OK
    failed        INT,
    notes         VARCHAR(2000)
);
```

### 4.1. Auditoría — historial "del antes" (trigger + tabla)

Para poder ver **cómo estaba un producto antes de cada cambio**, se guarda una copia de la fila
**previa** a cada `UPDATE`. Se implementa con un **trigger PL/pgSQL** (no en la app), así queda
garantizado aunque el cambio venga de cualquier lado.

**Tabla `productos_historial`:**
```sql
CREATE TABLE productos_historial (
    historial_id   BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    producto_id    BIGINT       NOT NULL,   -- referencia a productos.id
    code           VARCHAR(255) NOT NULL,
    -- snapshot de los campos relevantes ANTES del cambio:
    name_es        VARCHAR(255),
    name_pt        VARCHAR(255),
    price          NUMERIC(12,2),
    price_iva      NUMERIC(12,2),
    stock          INT,
    brand          VARCHAR(255),
    url_image      VARCHAR(1000),
    source_hash    CHAR(64),
    sync_status    SMALLINT,
    -- metadatos de auditoría:
    changed_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    operation      CHAR(1)     NOT NULL DEFAULT 'U'  -- 'U'=update, 'D'=delete (opcional)
);
CREATE INDEX ix_hist_producto ON productos_historial(producto_id, changed_at DESC);
```

**Función + trigger `AFTER UPDATE`** — solo audita si cambió el `source_hash` (evita ruido de
timestamps y reintentos), guardando los valores **`OLD`**:
```sql
CREATE OR REPLACE FUNCTION fn_audit_producto() RETURNS trigger AS $$
BEGIN
    IF OLD.source_hash IS DISTINCT FROM NEW.source_hash THEN
        INSERT INTO productos_historial(
            producto_id, code, name_es, name_pt, price, price_iva,
            stock, brand, url_image, source_hash, sync_status, operation)
        VALUES (OLD.id, OLD.code, OLD.name_es, OLD.name_pt, OLD.price, OLD.price_iva,
                OLD.stock, OLD.brand, OLD.url_image, OLD.source_hash, OLD.sync_status, 'U');
    END IF;
    RETURN NEW;
END; $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_audit_producto
    AFTER UPDATE ON productos
    FOR EACH ROW EXECUTE FUNCTION fn_audit_producto();
```

**Rendimiento:** como el trigger solo dispara cuando cambia `source_hash`, el historial queda
acotado a **cambios reales** (no a cada reescritura). En régimen normal son decenas de filas por
ciclo. Si la **carga inicial** masiva generara ruido, se puede desactivar el trigger
(`ALTER TABLE productos DISABLE TRIGGER trg_audit_producto;`) durante esa carga y reactivarlo
después. El front (Sección 11) usa `productos_historial` como fuente del **"antes"** para el diff.

---

## 5. Flujo de trabajo (ciclo de sincronización)

Cada 15 minutos, en horario comercial:

1. **Leer la vista** de Cangrejo en *streaming* (`DataReader`), sin cargar todo a memoria de golpe.
2. Por cada fila, **mapear** al formato de la API y **limpiar** (stock negativo→0, validar precio, armar `url_image`, enriquecer).
3. Calcular el **`source_hash`** (SHA-256 del payload final).
4. **Comparar** contra la BD, cargando todos los `(code, source_hash)` en memoria en **una sola consulta** (nada de un SELECT por producto):
   - No existe → `INSERT`, `sync_status = Pendiente`.
   - Hash distinto → `UPDATE`, `sync_status = Pendiente`, `last_changed_at = now`.
   - Hash igual → no tocar.
5. **Escribir los cambios en bloque** con `Npgsql` binary `COPY` hacia una tabla *staging* y luego
   `INSERT ... ON CONFLICT (code) DO UPDATE` (upsert nativo de PostgreSQL). No fila por fila. Los
   `UPDATE` de este paso disparan el trigger de auditoría (Sección 4.1).
6. Tomar los **`Pendiente`**, partir en **lotes de máximo 500** y hacer `POST /api/products/import/`.
7. Leer la respuesta **`207`**:
   - OK → `sync_status = Enviado`, `last_sent_at = now`, `first_sent_at` si estaba vacío.
   - `validation_errors` → `sync_status = Error`, guardar mensaje en `last_error`, reintentar luego.
8. Registrar la corrida en **`sync_runs`**.

**Reconciliación diaria (una vez al día):** escaneo completo de la vista → los productos en la BD que ya no aparecen se marcan `exists_in_view = FALSE`, `stock = 0`, `Pendiente` (para darlos de baja en el comparador).

### Rendimiento esperado (109k productos)
| Paso | Tiempo aprox. |
|---|---|
| Leer la vista (streaming + hash) | 5–20 s (según la vista) |
| Comparar en memoria | < 2 s |
| Guardar cambios (COPY a staging + upsert) | 1–2 s |
| Enviar deltas | segundos |
| **Total ciclo normal** | **< 1 min** |

La **carga inicial** (200 lotes de 500) tarda unos minutos, pero ocurre una sola vez.

---

## 6. Guía de uso de la API (Compras Paraguai)

**Base URL:** `https://products.comprasparaguai.com.br`
**Autenticación:** header llamado `token` (requerido). El valor lo provee Compras Paraguai (Vidal). Si diera `403`, probar también `Authorization: Bearer <token>`.

### 6.1. Importar productos — `POST /api/products/import/`
Inserta o actualiza (upsert por `code`).

**Headers:** `token: <token>` · `Content-Type: application/json`
**Body:** array de productos (máximo **500** por lote).

**Reglas de envío (según la documentación):**
- Enviar solo los productos con cambios (primer envío del día, o cambios posteriores al último envío exitoso).
- Cada 15 minutos mientras la tienda esté abierta.
- Lotes de máximo 500 productos.
- `stock = 0` para marcar un producto como no disponible.

**Ejemplo de body (probado con éxito):**
```json
[
  {
    "code": "8806097656784",
    "name": { "pt": "Samsung SM-A075F 4GB/64GB Black", "es": "Samsung SM-A075F 4GB/64GB Negro" },
    "price": 97,
    "price_iva": 97,
    "stock": 10,
    "brand": "Samsung",
    "description": { "pt": "Celular Samsung SM-A075F 4GB/64GB Black", "es": "Celular Samsung SM-A075F 4GB/64GB Negro" },
    "url_image": "https://.../imagen.png",
    "link": "https://tu-tienda.com/producto/8806097656784"
  }
]
```

**Respuestas:**
- **`207` Multi-Status** → procesado. Revisar `products_processed`, `products_failed` y `validation_errors`.
- **`403`** → token inválido o mal ubicado (debe ir en el header, no en la URL).
- **`500`** → error del lado de Compras Paraguai.

**Ejemplo de respuesta exitosa (real):**
```json
{ "success": true, "message": "Import completed.", "products_processed": 1, "products_failed": 0, "validation_errors": [] }
```

### 6.2. Listar / verificar — `GET /api/products/list/`
Devuelve productos ya cargados (paginado). El `token` va en el **header**; los filtros van como **query params**.

**Filtros:** `code`, `name`, `available`, `with_stock`, `page`.

**Ejemplo:** `GET /api/products/list/?code=8806097656784` (con header `token`).
Devuelve `name_pt`, `name_es`, `price`, `price_iva`, `stock`, `available`, `link`, `created_at`, `updated_at`, etc. (no devuelve `url_image`).

> ⚠️ **Importante:** el `token` SIEMPRE va en los **Headers**, nunca como parámetro en la URL (eso da `403`).

---

## 7. Herramientas

| Área | Herramienta | Para qué |
|---|---|---|
| Backend (sync) | **.NET 8 — Worker Service** | Proceso en segundo plano con temporizador (cada 15 min). No es Web API. |
| Base de datos propia | **PostgreSQL** | Copia + enriquecimiento + control de sync + auditoría (trigger). |
| Acceso a Oracle | **Oracle.ManagedDataAccess** + **Dapper** | Leer la vista de Cangrejo en streaming. |
| Acceso a PostgreSQL | **Npgsql** + **Dapper** | Leer/escribir la BD propia. |
| Escritura masiva | **Npgsql `COPY`** + `INSERT ... ON CONFLICT` | Guardar los cambios en bloque (upsert). |
| Cliente HTTP | **IHttpClientFactory** + `HttpClient` tipado | Llamar a la API con el token. |
| Resiliencia | **Polly** | Reintentos, timeout, circuit breaker. |
| Logging | **Serilog** | Registrar corridas y errores. |
| API de monitoreo | **ASP.NET Core (Web API)** | Endpoints de solo lectura para el front (Sección 11). |
| Front de monitoreo | **Angular** + **Angular CLI** | Panel para ver estado de sync y diff antes/después. |
| Secretos | **User Secrets** (dev) / variables de entorno (prod) | Token y cadenas de conexión — nunca en el código. |
| Pruebas de API | **Postman** | Ya usado para validar los endpoints. |

---

## 8. Plan por fases

| Fase | Qué se hace | Tiempo |
|---|---|---|
| **0. Definiciones** | Enviar pedido a Cangrejo · token de la API · datos de conexión Oracle · cerrar mapeo (IVA 10%, patrón de link, URL base de imágenes, imagen por defecto) | 1–2 días |
| **1. Tu BD** | Crear PostgreSQL: `productos`, `categorias`, `sync_runs` + índices | 1 día |
| **2. Proyecto base** | Worker Service .NET 8: config, secretos, logging | 0.5–1 día |
| **3. Lectura + mapeo** | Conexión Oracle en streaming · mapear vista→API · limpieza · hash | 2–3 días |
| **4. Detección de cambios** | Comparar en memoria · `COPY` a staging + `ON CONFLICT` · marcar pendientes | 2 días |
| **5. Envío a la API** | Cliente HTTP con token · lotes de 500 · parsear `207` · reintentos | 2 días |
| **6. Orquestación** | Timer 15 min · carga inicial aparte · reconciliación diaria · bitácora | 1 día |
| **7. Pruebas + arranque** | Probar con pocos productos · carga inicial · monitoreo | 2–3 días |
| **8. Auditoría** | Tabla `productos_historial` + función/trigger `AFTER UPDATE` · probar con un `UPDATE` | 0.5 día |
| **9. Front + API** | Web API de lectura (productos, historial, diff, dashboard) + app Angular (3 pantallas) | 3–5 días |

**Total estimado:** ~3 a 4 semanas de trabajo efectivo para el MVP (con front y auditoría incluidos).

**Orden práctico:** empezar por Fases 1–3 sin esperar a Cangrejo (la lógica funciona con escaneo completo). Cuando llegue la columna de versión, cambiar solo el lector.

---

## 9. Pedido a Cangrejo (para enviar)

> Para poder sincronizar los productos:
> - **Columna de número de versión** (o fecha de modificación) — Muy importante, que se actualice cada vez que cambie cualquier dato del producto (precio, stock, nombre, imagen, etc.).
> - **Columna numérica de categoría** (Código / Código compuesto) indexada — además del texto actual.
> - **URL base de las imágenes** — el campo `IMAGENES` trae solo el nombre del archivo (`prod_976990101.webp`); necesito la dirección completa.

> **Pendiente adicional a considerar:** cómo detectar bajas (que no borren la fila, sino que marquen un flag). Si no se pide, se resuelve con la reconciliación diaria del lado propio.

---

## 10. Estado y próximos pasos

**✅ Ya validado:**
- Acceso a la vista de Cangrejo (109.276 productos).
- Estructura de la vista y mapeo de campos.
- `code` = SKU (confirmado en prueba real).
- API de Compras Paraguai: `import` (POST) y `list` (GET) funcionando, token en header.
- Envío real probado: `207` en el POST, cambio reflejado en el GET.

**⏭️ Próximos pasos:**
1. Confirmar con Vidal si el token es de pruebas o producción.
2. Obtener: URL base de imágenes, patrón de `link`, definir imagen por defecto.
3. Levantar **PostgreSQL** local y crear la BD propia (Fase 1) y el Worker Service (Fase 2).
4. Implementar la lectura de la vista + mapeo (Fase 3), empezando con escaneo completo + hash.
5. Crear la **auditoría** (`productos_historial` + trigger, Fase 8).
6. Hacer *scaffold* de la **Web API + Angular** para el panel de monitoreo (Fase 9).
7. Enviar el pedido de mejoras a Cangrejo (columna de versión, etc.).

---

## 11. Front de monitoreo (Angular + Web API de lectura)

Un panel interno para **ver qué se sincronizó y cómo estaba antes** de cada cambio. Como el Worker
no expone HTTP, se suman **dos proyectos** a la solución.

> **Nota de scope:** Angular es una SPA y necesita una Web API que la alimente. Si se busca lo
> mínimo, el **Plan B** (final de esta sección) usa Razor Pages / Blazor Server y evita la API y la
> SPA separadas. El backend PostgreSQL y la auditoría son los mismos en ambos casos.

### 11.1. `ComprasParaguai.Api` — ASP.NET Core (solo lectura)
Lee la BD propia con **Npgsql + Dapper**. No reenvía nada a Compras Paraguai, así que **no maneja el
token** de esa API. CORS habilitado para el origen de Angular.

| Endpoint | Para qué |
|---|---|
| `GET /api/productos` | Lista paginada. Filtros: `sync_status`, `code`, `name`, `changed` (solo con historial). |
| `GET /api/productos/{code}` | Detalle actual del producto. |
| `GET /api/productos/{code}/historial` | Versiones previas (la auditoría de `productos_historial`). |
| `GET /api/productos/{code}/diff` | Compara la fila actual contra la última del historial → `campo → {antes, después}`. |
| `GET /api/sync-runs` | Bitácora de corridas (`sync_runs`) para el dashboard. |
| `GET /api/dashboard` | Contadores: pendientes / enviados / error + última corrida. |

### 11.2. `comprasparaguai-front` — Angular (standalone components)
| Pantalla | Contenido |
|---|---|
| **Dashboard** | Tarjetas con contadores + tabla de últimas corridas (`sync_runs`). |
| **Productos** | Tabla con `code`, nombre, precio, stock, badge de `sync_status` y marca de "modificado" si tiene historial. Filtros arriba. |
| **Detalle** | Datos actuales + **diff "antes/después"** (endpoint `/diff`) + *timeline* del `/historial`. |

### 11.3. Plan B (más liviano, sin Angular)
Reemplazar 11.1 + 11.2 por **Razor Pages / Blazor Server** dentro de la misma solución .NET:
server-rendered, sin CORS ni SPA separada. Mismas vistas (dashboard, productos, detalle con diff),
mismo backend PostgreSQL y misma auditoría.

---

*Documento de planificación — puede evolucionar a medida que se confirmen los pendientes.*
