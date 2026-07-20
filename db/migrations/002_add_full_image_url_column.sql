-- Agregar columna para URL completa de imagen
-- Si no tiene imagen o es "sinimagen.jpg", quedará vacío
ALTER TABLE productos
ADD COLUMN url_image_full VARCHAR(1000);

-- Crear índice para búsquedas rápidas
CREATE INDEX ix_productos_url_image_full ON productos(url_image_full)
WHERE url_image_full IS NOT NULL;
