# Importación temporal de productos mediante API administrativa

**Estado: especificación acordada; implementación y despliegue pendientes.** Esta capacidad es una herramienta operativa restringida, no una API pública de Virtuosa Store ni un mecanismo permanente de integración de terceros.

## Objetivo y límites

Permitir que un agente autorizado importe definiciones de productos y hasta ocho imágenes por producto sin manipular SQLite directamente. Utilizar `IProductoService`, `IProductoConImagenService.CrearGaleriaAsync` y `ICategoriaService`; nunca crear `UnidadInventario`, compras, pedidos, ventas ni saldos como efecto de la importación. La existencia en el catálogo público sigue dependiendo de las reglas normales de inventario.

No aceptar cambios de precio ni sobrescritura de imágenes de un producto existente durante el primer alcance: las coincidencias se reportan para revisión manual. No aceptar URLs remotas para descargar imágenes en esta API; los archivos deben adjuntarse y pasar por el procesador/almacenamiento existentes. No agregar migraciones, dependencias o cambios de reglas de negocio.

## Contrato propuesto (a validar en pruebas)

- `POST /api/admin/importacion-productos/validar`: recibe lote de metadatos; verifica estructura, campos obligatorios, categoría existente, jerarquía, precio sugerido y duplicados evidentes. **No escribe**. Devuelve resultados por posición, errores y advertencias.
- `POST /api/admin/importacion-productos/crear`: crea **un producto por solicitud**, con metadatos y de cero a ocho imágenes adjuntas; requiere revisión previa del lote por el operador. Devuelve el identificador generado, o un error por producto. No implementar actualización ni borrado en esta fase.
- `GET /api/admin/importacion-productos/estado`: estado mínimo de habilitación para el operador autorizado; no revelar secretos, datos de clientes ni información interna.

El formato final del multipart, DTOs, códigos HTTP, límites de tamaño y reglas de repetición deben reflejar los contratos reales de `ProductoInput` y del almacenamiento de galería. Validar también los duplicados en el instante de crear, no solo en la vista previa. El código de barras, cuando exista, es una señal fuerte; coincidencias por nombre/marca/presentación son **advertencias**, no autorizan fusionar registros automáticamente. No inventar datos faltantes: dejar opcionales vacíos y marcar obligatorios ausentes.

## Frontera de seguridad

- **Deshabilitada por defecto**, con configuración explícita y expiración absoluta de la autorización.
- Credencial aleatoria de alta entropía almacenada **solo** en configuración privada del servidor; jamás en Git, chat, documentación, JS, URL o logs. Comparar sin filtraciones de tiempo y permitir revocación inmediata.
- **Solo acceso desde el propio servidor** por una ruta operativa local; denegar solicitudes externas incluso si presentan la credencial. Validar la dirección de origen después de `UseForwardedHeaders`; no confiar en `X-Forwarded-For` arbitrario ni publicar nuevos puertos. Evitar exponer una ruta escribible mediante Caddy.
- Aplicar límites de tamaño, cantidad y frecuencia; rechazar solicitudes sin credencial antes de leer archivos grandes; desactivar caché; no registrar tokens ni datos completos de productos en logs.
- Registrar auditoría mínima de intentos y resultados sin secretos. No confundir la autenticación de esta herramienta con permisos administrativos generales de Identity.
- Tras la importación: desactivar/revocar credencial y verificar desde fuera que el endpoint no admite escrituras.

## Flujo operativo

1. Preparar un manifiesto local con productos y archivos de imagen. El chat donde están las fotografías debe exportar o proporcionar los archivos: la API no recupera automáticamente adjuntos de otros chats.
2. Ejecutar validación sin escrituras y revisar categorías, precios, datos faltantes y coincidencias.
3. Confirmar qué productos se van a crear y enviar solicitudes idempotentes o con comprobación de duplicados, guardando el resultado por ítem para reanudar sin duplicar.
4. Verificar en la interfaz administrativa los registros y las galerías; no asumir que aparecen públicamente sin unidades disponibles.
5. Revocar el acceso temporal y conservar únicamente el resumen de importación sin credenciales.

## Pruebas y salida

Cubrir deshabilitación predeterminada, credencial inválida/caducada, origen externo, ausencia de inventario creado, validación sin escrituras, código de barras repetido, categorías inválidas, límite de imágenes, archivos corruptos, reintento, fallo de persistencia y limpieza de temporales. Validar suite .NET y flujo de producción con backup previo, rollback documentado y autorización explícita de despliegue.

La API pública `/api/catalogo/productos` permanece exclusivamente de lectura. La nueva API administrativa no debe reutilizar ni ampliar permisos de ese grupo.
