# Imagen principal opcional de producto

Cada producto puede guardar una sola ruta relativa nullable en `Producto.ImagenPrincipalRuta`. Los registros anteriores siguen sin imagen. No se guarda el binario en SQLite.

`IAlmacenamientoImagenesProducto` recibe el flujo sin usar el nombre original. Admite JPEG, PNG y WebP validados por firma y decodificación con SkiaSharp. El límite de entrada es 8 MB y 25 millones de píxeles. Se aplica la orientación codificada, se conserva la proporción, se reduce el lado mayor a 1200 píxeles y se convierte a WebP (calidad 82). HEIC/HEIF no está soportado en esta versión: requeriría dependencias o soporte nativo adicional en Linux.

El archivo procesado pasa por `.temporales/` y se confirma como `productos/{id}/imagen-principal-{guid}.webp`. La base solo conserva esa ruta relativa. El directorio físico se configura mediante `AlmacenamientoImagenesProducto:DirectorioBase` (en Docker, `/app/data/productos`), fuera de `wwwroot`. La lectura usa `GET /productos/{id}/imagen` con autenticación y valida la ruta antes de abrir el archivo. No se publican rutas del servidor.

El servicio de producto con imagen prepara el archivo, guarda el producto y la nueva referencia dentro de una transacción SQLite, y compensa eliminando temporales o archivos confirmados cuando el guardado falla. En el reemplazo conserva la imagen anterior hasta confirmar la nueva referencia y después la elimina. Al quitarla, limpia la referencia y después elimina el archivo. Como SQLite y el sistema de archivos no comparten transacción, un error físico al borrar después del commit se registra como crítico para limpieza operativa; no se elimina antes un archivo que aún pueda estar referenciado.

Producción requiere el bind mount `/opt/resellmanager/data/productos:/app/data/productos`. Staging requiere `/opt/resellmanager-staging/data/productos:/app/data/productos`. El directorio debe ser persistente y escribible por el usuario del contenedor. El backup debe incluir conjuntamente `database/`, `comprobantes/`, `dataprotection/` y `productos/`; el script versionado ya enumera los cuatro, pero su copia instalada debe actualizarse durante el despliegue. No se ha cambiado el VPS.

## Imágenes externas importadas desde la búsqueda

La búsqueda conserva la URL aceptada en `ProductoFormModel.ImagenExternaUrl`
hasta «Guardar producto». Una imagen manual tiene prioridad. El alta asistida
descarga la imagen, reutiliza la validación y conversión a WebP de este
almacenamiento y persiste únicamente `ImagenPrincipalRuta`. La ruta lógica
`productos/{id}/imagen-principal-{guid}.webp` se resuelve físicamente como
`{DirectorioBase}/{id}/imagen-principal-{guid}.webp`.

`ImagenProductoExternaService` sigue manualmente **301, 302, 303, 307 y 308**,
con un máximo de **tres redirecciones** (cuatro peticiones contando la inicial).
Resuelve `Location` relativa contra la URL de la petición actual y detecta ciclos,
incluidos cambios que solo afectan al fragmento. Un `Location` ausente o inválido,
un ciclo, un salto adicional o un destino inseguro producen el fallo controlado.

Antes de cada petición aplica `DestinoImagenProductoSeguro.UrlPermitida`:
HTTPS/443, sin credenciales, sin nombres locales ni IP privadas/reservadas.
El handler conserva `AllowAutoRedirect = false`, sin proxy ni cookies.
Su `ConnectCallback` valida todas las IP resueltas por DNS y fija la conexión a
una IP pública ya validada; también se conserva esa protección en cada salto.
El presupuesto de **10 segundos** cubre toda la cadena y el cuerpo final,
sin reiniciarse por redirección. Se mantiene el máximo de **8 MiB**, MIME
JPEG/PNG/WebP y la posterior comprobación de firma, decodificación, dimensiones
y conversión por Skia. No se consumen los cuerpos de las redirecciones.

Los logs de descarga incluyen motivo y estado HTTP cuando está disponible,
sin URL, query, credenciales ni cuerpo remoto. Un 403/404, contenido inválido,
destino bloqueado o fallo de archivo conserva el alta sin imagen con el aviso
existente y sin huérfanos; no se eluden restricciones del CDN.

El detalle y listado administrativos consumen `/productos/{id}/imagen`.
Para un producto comercialmente disponible, la API pública proyecta
`TieneImagenPrincipal` y `ImagenCatalogo` carga
`/api/catalogo/productos/{id}/imagen`. Tener imagen no publica por sí solo un
producto: las [reglas de disponibilidad](25_CatalogoPublicoBackend.md#disponibilidad-exacta)
y la autenticación de la imagen administrativa se conservan.

Regresiones: `ImagenExternaRedireccionesTests` prueba HTTP simulado, destinos,
límites, ciclos, cancelación, timeout total y cuerpos acotados;
`FlujoImagenExternaTests` recorre adaptadores HTTP → candidato → formulario →
Guardar → SQLite/WebP → endpoints administrativos/públicos y `ImagenCatalogo`,
incluyendo la exclusión pública antes de registrar una unidad disponible.
Las pruebas automatizadas no consultan Internet ni usan imágenes externas reales.
