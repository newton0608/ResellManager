# Importación temporal de productos mediante API administrativa

**Estado: implementada e integrada en main y develop; desplegada en producción el 15/10/2026.** Es una herramienta operativa restringida, deshabilitada por defecto. No es una API pública de Virtuosa Store ni una integración permanente de terceros.

## Objetivo y límites

Importar definiciones de productos y hasta ocho imágenes por producto mediante `IProductoService`, `IProductoConImagenService.CrearGaleriaAsync` e `ICategoriaService`. La API no consulta directamente SQLite ni crea unidades de inventario, compras, pedidos, ventas o saldos. La aparición en el catálogo público sigue dependiendo de las reglas normales de inventario.

El primer alcance no actualiza ni elimina productos existentes, no cambia sus precios y no reemplaza sus fotografías. Las coincidencias se reportan para revisión. Los archivos se adjuntan; no se descargan URLs remotas. No hay migraciones ni dependencias nuevas.

## Contrato implementado

Todas las rutas usan el prefijo `/api/admin/importacion-productos`:

| Método y ruta | Entrada | Resultado |
| --- | --- | --- |
| `GET /estado` | Autorización temporal local | Habilitación y vencimiento, sin secreto ni datos de negocio. |
| `POST /validar` | JSON: arreglo de 1 a 100 objetos `ProductoInput`, propiedades camelCase. | HTTP 200 con `valido` y `resultados[]`: `indice`, `valido`, `errores[]`, `advertencias[]`. No escribe. |
| `POST /crear` | Multipart con un campo `producto` (JSON `ProductoInput`), cero a ocho archivos `imagenes` y `portadaIndice` opcional, desde cero. Encabezado `X-Importacion-Confirmada: true`. | HTTP 201 con `id`, `codigoInterno`, `imagenes` e `inventarioCreado: false`; las validaciones de negocio devuelven 409. |

`ProductoInput` conserva el contrato [existente](../../src/ResellManager.Application/DTOs/MasterDataDtos.cs). No se generan valores comerciales para campos desconocidos. Precio sugerido cero está permitido; una ausencia de información comercial debe resolverse al revisar el manifiesto. La descripción y otros campos opcionales pueden quedar vacíos.

Límites:

- 256 KiB para la solicitud de validación y para los metadatos JSON del producto.
- 8 MiB por imagen y 64 MiB para la solicitud completa, incluido el envoltorio multipart.
- Solo los campos y nombres de partes documentados; portada existente y dentro de rango.
- Límite de lectura efectivo aunque falte `Content-Length`; el tamaño declarado excesivo devuelve 413. Una parte multipart inválida o superior al límite por imagen devuelve 400.
- Archivos corruptos o no admitidos se rechazan mediante el procesador existente. No se confía en extensión ni MIME declarados.

La API devuelve 400 para estructura inválida, 409 para conflictos o falta de confirmación, 413 para cuerpo excesivo y 429 cuando se supera la frecuencia. Las respuestas de los endpoints llevan `Cache-Control: no-store`. La limitación de frecuencia se aplica antes del filtro de acceso y puede devolver 429 también a una solicitud no autorizada.

## Validación y repetición

La revisión previa comprueba nombre obligatorio de hasta 200 caracteres, precio no negativo, categoría existente y jerarquía raíz/hija, coherencia de la principal declarada, medidas positivas, exclusión entre ml y gramos y presentación de hasta 100 caracteres.

- Un código de barras existente, comparado sin espacios extremos y sin distinguir mayúsculas, bloquea la creación. Se almacena sin espacios extremos y vuelve a verificarse dentro del servicio de galería.
- Una definición idéntica por nombre, marca, modelo, color, talla, contenido, peso y presentación bloquea la creación. El precio y la descripción no distinguen variantes ni autorizan sobrescribir el registro.
- El mismo nombre con datos de variante diferentes genera advertencia cuando ya existe en el sistema. No se fusionan registros.
- También se detectan códigos y definiciones repetidos dentro del lote.
- Las creaciones de esta herramienta se serializan dentro del proceso y se revalidan después de esperar, evitando duplicados entre solicitudes de importación simultáneas y reintentos.
- Un reintento de un producto ya creado responde 409. No constituye una operación de actualización ni devuelve automáticamente el resultado anterior. El operador conserva los identificadores obtenidos para reanudar.

Esta serialización cubre la herramienta dentro de una instancia de aplicación. No introduce unicidad global por nombre ni una garantía entre múltiples procesos o altas realizadas por otros flujos administrativos.

## Frontera de seguridad

Se requiere simultáneamente:

- `ImportacionProductos:Habilitada = true`.
- `ImportacionProductos:Token`: secreto aleatorio de alta entropía, entre 32 y 512 caracteres.
- `ImportacionProductos:ExpiraUtc`: vencimiento absoluto en formato ISO 8601 UTC, futuro y como máximo a 24 horas.
- Encabezado `X-Resell-Import-Key` válido; comparación mediante hashes y tiempo constante.
- Origen de loopback y Host `localhost`, `127.0.0.1` o `[::1]`, sin `X-Forwarded-For`, `X-Forwarded-Host` ni `X-Original-For`.

La configuración ausente, malformada, revocada o caducada cierra el acceso con 404. El filtro comprueba la autorización antes de leer el cuerpo. La comprobación se realiza después de Forwarded Headers y conserva la política general de proxies de confianza.

El uso previsto es una solicitud local a `localhost:8080` desde la red del propio contenedor. No se publican puertos nuevos ni se modifica Caddy. El dominio público no es una vía operativa de importación. El despliegue debe admitir explícitamente `localhost` en `AllowedHosts` además de los dominios actuales; no se modifica ni se deshabilita el filtrado de Host desde esta implementación. La ruta de importación con origen y Host de loopback evita la redirección HTTPS, incluso si el host fuerza el puerto 443; las demás rutas y el acceso externo conservan la redirección habitual. La publicación real exige comprobar el rechazo desde fuera.

El secreto se conserva en configuración privada del servidor: nunca Git, chat, documentación, JS, URL ni logs. La auditoría de esta herramienta registra ruta y código HTTP, sin tokens ni cuerpos completos. Se permiten 20 solicitudes por minuto por IP, sin cola. La autorización no concede permisos generales de Identity ni acceso a clientes o finanzas.

Cambiar una configuración recargable se evalúa en la siguiente solicitud; la caducidad también se evalúa por solicitud. Si se usa configuración por variables de entorno, la revocación requiere aplicar el cambio al proceso/contenedor, o esperar el vencimiento: editar un archivo de entorno por sí solo no modifica un proceso activo.

## Flujo operativo

1. Reunir un manifiesto con datos revisados y archivos de imagen. La API no recupera adjuntos de otro chat.
2. Obtener categorías vigentes y preparar los identificadores correspondientes sin inventar relaciones.
3. Validar el lote sin escrituras; revisar errores, coincidencias, variantes y datos comerciales faltantes.
4. Confirmar explícitamente qué productos se crearán y enviarlos de uno en uno, conservando el resultado por ítem.
5. Comprobar registros y galerías desde la interfaz administrativa; no asumir inventario ni visibilidad pública.
6. Revocar el acceso temporal y conservar el resumen sin credenciales.

## Implementación y verificación

[Endpoints](../../src/ResellManager.Web/Endpoints/ImportacionTemporalProductosEndpoints.cs), registrados desde `Program.cs`. La persistencia, transacción de galería, normalización de imágenes y compensación ante fallos siguen en los servicios existentes.

Pruebas: `ImportacionTemporalProductosTests` e `ImportacionTemporalEndpointsIntegracionTests`, con SQLite y almacenamiento temporales aislados. Cubren acceso deshabilitado, clave incorrecta/ausente, vencimiento, origen externo, dominio y proxy; validación sin escrituras; medidas, categorías y variantes; creación con cero, dos y ocho fotos, portada, reintentos y concurrencia; imágenes inválidas/excesivas, solicitudes sin longitud, frecuencia, revocación, operación HTTP local con HTTPS forzado y rollback con limpieza tras un fallo de almacenamiento. `ProductoGaleriaTests` aporta la cobertura del fallo de commit y cancelación del servicio reutilizado.

Verificación aislada del **2026-10-15**: build de la solución sin advertencias ni errores; **39/39** pruebas específicas y **981/981** en la suite .NET completa, sin omitidas. Ejecutadas en contenedor SDK .NET 10 con red desactivada, código de entrada en solo lectura y almacenamiento temporal de pruebas; sin montar datos de producción.

El despliegue del 15/10/2026 verificó la operación local y el rechazo a través del dominio público. Futuras actualizaciones requieren respaldo previo, compatibilidad con producción, autorización explícita, configuración privada y comprobación de aislamiento y revocación. La API pública `/api/catalogo/productos` permanece exclusivamente de lectura.

## Registro de despliegue — 15/10/2026

- Integración: PR #22 a develop y PR #23 a main; main en el despliegue: `046a181244ebbb61b9e1391cb1fd8b176339b0bf`.
- Fuente construida: `5dea8bb39ab791f6e5c9b212ae7f65ff195939da`, con árbol idéntico al main de despliegue y el hotfix de fotografías transparentes conservado. La versión combinada pasó **983/983** pruebas .NET, sin omitidas, y build sin advertencias ni errores. La imagen Release también se construyó correctamente.
- Imagen desplegada: `resellmanager:import-5dea8bb`, también etiquetada `resellmanager:v1`; ID `sha256:030821bb70ee970ce8490791db7e820002378adff4248a6d4eedec8b8e3c5b7d`.
- Imagen anterior preservada: `resellmanager:pre-import-api-20261015T155359Z`, ID `sha256:1dd80ee86d6a12011cd5568330a5e9a123d8252708b981dfa0c78166b3f619a4`. No hay migraciones nuevas.
- Respaldo consistente con la aplicación detenida: `/opt/resellmanager/backups/import-api-20261015T155359Z/data-20261015T155359Z.tar.gz`, 3 673 882 bytes, con checksum acompañante. Incluye database, comprobantes, dataprotection y productos; el SQLite extraído en una ruta temporal pasó `PRAGMA integrity_check`. El directorio operativo también conserva la configuración anterior y un manifiesto privado, sin publicar secretos en Git.
- Se recreó únicamente ResellManager; Caddy, sus certificados y los otros servicios no se reiniciaron. Se conservaron los cuatro montajes persistentes y no se publicó un puerto de aplicación.
- Verificaciones reales: `/health` respondió OK; login, catálogo administrativo y Virtuosa Store respondieron 200. Desde loopback: estado 200, lote vacío 400, creación sin confirmación 409 y clave inválida 404. Por el dominio público, estado, validación y creación devolvieron 404 aun usando la clave válida y un X-Forwarded-For falsificado. Estas sondas no crearon productos ni inventario.
- Configuración operativa: localhost añadido a AllowedHosts, credencial privada y autorización hasta **2026-10-16T15:54:01Z** (16/10/2026, 09:54, Guatemala). Es una ventana histórica concreta; comprobar configuración y estado actuales antes de reutilizarla. La API conserva la deshabilitación predeterminada y deniega acceso al vencer. La clave no está en este documento.
- Límites: no se importó el lote real de productos/fotografías; no se ejecutó una restauración ni un rollback real de aplicación en este despliegue. La imagen y configuración anteriores quedaron preparadas para recuperación, lo que no sustituye ensayar ese procedimiento.
