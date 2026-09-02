# Fase 5.8 — Compras, proveedores y comprobantes

## Estado

**Implementada.**

Este documento registra las decisiones y el resultado implementado de la Fase 5.8.

La fase debe completar la experiencia de Compras sobre la lógica de backend ya existente, manteniendo las reglas actuales de inventario, orígenes de compra, recepción y comprobantes.

---

## 1. Objetivo de la fase

La Fase 5.8 debe permitir gestionar desde Blazor el flujo real de abastecimiento del negocio:

- listar compras;
- registrar una compra;
- consultar detalle de compra;
- seleccionar y administrar proveedores;
- registrar múltiples productos por compra;
- registrar costo unitario por detalle;
- seleccionar el origen de compra;
- adjuntar un comprobante real desde teléfono o computadora;
- consultar el comprobante posteriormente;
- integrar la compra con la creación de inventario físico según las reglas existentes.

El módulo debe ser responsive y cómodo de usar desde teléfono.

---

## 2. Código interno de Compra

El código técnico de una `Compra` será generado automáticamente por ResellManager.

La usuaria no debe inventar ni escribir códigos internos.

Formato inicial:

```text
COM-<GUID>
```

Ejemplo:

```text
COM-7F0DB2E5457B45ADBF30DF01EC037F02
```

La estrategia debe preservar la validación de unicidad del backend.

No se usarán esquemas frágiles como:

- `MAX()+1`;
- lectura del último código;
- contador mantenido por la UI;
- `Random` simple;
- fecha/hora como única fuente de unicidad.

### Código interno vs. referencia externa

Se mantiene la regla general documentada para ResellManager:

```text
Código interno
→ generado por el sistema

Referencia externa
→ capturada manualmente cuando proviene de un tercero
```

Por tanto:

- `Compra.CodigoInterno` → automático;
- número de factura/comprobante → manual y opcional según el documento real;
- referencias propias del proveedor → datos externos, no códigos internos del sistema.

---

## 3. Reglas existentes de origen e inventario

La UI de Fase 5.8 no debe duplicar ni sustituir las reglas de `CompraService`.

### Importación

```text
Compra
→ crea UnidadInventario
→ Estado = Comprada
→ FechaIngreso = null
→ recepción posterior
→ Disponible
```

La fecha real de ingreso se registra mediante el caso de uso de recepción de mercancía.

### Compra local

```text
Compra
→ crea UnidadInventario
→ Estado = Disponible
→ FechaIngreso obligatoria
```

### Envío del hermano

```text
Compra/registro de abastecimiento
→ crea UnidadInventario
→ Estado = Disponible
→ FechaIngreso obligatoria
```

Se registra cuando la mercancía ya fue recibida.

### Catálogo

```text
Compra Catálogo
→ DetalleCompra
→ NO crea UnidadInventario
```

Catálogo permanece completamente fuera del inventario físico.

La UI no debe introducir una opción que permita crear unidades físicas para una compra de catálogo.

---

## 4. Comprobante de compra

En V1 una `Compra` puede tener **cero o un comprobante**.

La relación existente se conserva:

```text
Compra 1 → 0..1 ComprobanteCompra
```

No se implementará todavía una colección de múltiples archivos por compra.

Si el uso real demuestra posteriormente que una compra necesita varias fotografías/documentos, se diseñará una evolución explícita del modelo.

---

## 5. La usuaria no escribe rutas

`ComprobanteCompra.RutaDocumento` es un detalle técnico de persistencia.

La UI nunca debe pedir a la usuaria escribir una ruta como:

```text
/uploads/comprobantes/factura123.jpg
```

El flujo será:

```text
Nueva compra
→ Adjuntar comprobante
→ cámara / galería / archivos
→ procesamiento seguro
→ almacenamiento
→ ResellManager guarda RutaDocumento
```

La ruta se genera y administra internamente.

---

## 6. Tipos de archivo admitidos

V1 admitirá comprobantes en los siguientes formatos:

### Imágenes

- JPG / JPEG;
- PNG;
- WebP.

### Documentos

- PDF.

No se debe aceptar cualquier archivo basándose únicamente en su nombre o extensión.

La implementación debe validar de forma controlada el tipo admitido y el tamaño antes de almacenarlo.

---

## 7. Límites de tamaño

El límite de entrada será independiente del tamaño final almacenado.

### Entrada

Máximo inicial recomendado:

```text
10 MB por archivo
```

Motivo:

Una fotografía original tomada con un teléfono moderno puede pesar varios megabytes. El sistema debe poder recibirla para posteriormente optimizarla sin obligar a la usuaria a editarla manualmente.

### PDF

Los PDF tendrán un máximo de:

```text
10 MB
```

En V1 se conservarán sin recomprimir ni modificar su contenido.

---

## 8. Optimización de imágenes

Las imágenes de comprobantes deben almacenarse con calidad suficiente para ampliar y leer datos como:

- número de documento;
- fecha;
- productos;
- precios;
- totales.

Al mismo tiempo, no se deben conservar fotografías originales innecesariamente grandes.

Configuración objetivo inicial:

```text
lado más largo: máximo aproximado 1800 px
calidad JPEG objetivo: aproximadamente 85 %
```

El sistema debe mantener la relación de aspecto y evitar ampliar imágenes que ya sean menores.

El objetivo no es producir una miniatura, sino una copia legible y razonablemente compacta.

Un comprobante típico debería quedar muy por debajo de una fotografía original de cámara, sin degradación visible relevante para su lectura.

Los valores podrán ajustarse durante pruebas reales si se detecta que determinados comprobantes pequeños o con texto fino necesitan mayor resolución.

---

## 9. Metadatos de imagen

Cuando el procesamiento utilizado lo permita de forma segura y sencilla, la copia almacenada no debe conservar metadatos innecesarios de la fotografía original, por ejemplo EXIF.

Para el negocio interesa el contenido visible del comprobante, no información como:

- ubicación GPS;
- modelo del teléfono;
- información de captura innecesaria.

No se agregará complejidad desproporcionada únicamente para este objetivo; debe resolverse como parte natural del proceso de re-encode de la imagen.

---

## 10. Nombre de archivo seguro

El nombre original enviado por el navegador no será utilizado como nombre físico definitivo.

Ejemplo:

```text
Archivo elegido:
IMG_4837.jpg

Archivo interno:
CMP-<GUID>.jpg
```

El sistema generará un nombre propio y seguro.

No se debe construir una ruta a partir de texto proporcionado por la usuaria.

No se aceptarán secuencias de directorios ni nombres destinados a controlar dónde se escribe el archivo.

---

## 11. Almacenamiento fuera de SQLite

Los binarios de imágenes y PDF no se guardarán dentro de SQLite.

La base de datos conserva únicamente la referencia necesaria, actualmente representada por:

```text
ComprobanteCompra.RutaDocumento
```

Los archivos se almacenarán en un directorio administrado por la aplicación.

Esta separación evita inflar innecesariamente la base de datos y prepara una posible evolución futura a almacenamiento externo sin tener que rediseñar la entidad de Compra.

---

## 12. Abstracción de almacenamiento

Blazor no debe conocer directamente:

- rutas físicas del servidor;
- reglas de nombres;
- compresión;
- resize;
- validaciones internas de almacenamiento.

La responsabilidad debe quedar encapsulada detrás de un servicio dedicado, por ejemplo conceptualmente:

```csharp
public interface IAlmacenamientoComprobantes
{
    Task<ServiceResult<ArchivoComprobanteDto>> GuardarAsync(
        Stream contenido,
        string nombreOriginal,
        string contentType,
        CancellationToken ct = default);
}
```

El contrato final podrá ajustarse durante implementación según la arquitectura existente, pero debe conservar esta separación de responsabilidades.

Flujo conceptual:

```text
Blazor
  ↓
servicio de almacenamiento de comprobantes
  ↓
validación
optimización si es imagen
nombre seguro
persistencia de archivo
  ↓
RutaDocumento
```

No se debe inyectar ni utilizar `DbContext` directamente desde componentes Blazor para resolver el upload.

---

## 13. Dependencia de procesamiento de imágenes

No se añadirá una biblioteca de procesamiento de imágenes arbitrariamente.

Antes de incorporar una dependencia se debe revisar:

- compatibilidad con .NET actual del proyecto;
- licencia;
- mantenimiento;
- necesidad de dependencias nativas;
- comportamiento en Windows y futuro servidor Linux;
- capacidad real de resize/re-encode requerida por V1.

La librería concreta debe elegirse durante implementación y quedar aislada detrás del servicio de almacenamiento.

De esta forma una futura sustitución no modifica los componentes Blazor ni el dominio.

---

## 14. Consistencia entre archivo y Compra

Guardar un archivo y guardar una Compra en SQLite no forman automáticamente una única transacción ACID.

La implementación debe manejar explícitamente fallos parciales.

No debe ocurrir de forma silenciosa:

```text
archivo definitivo existe
Compra no existe
```

ni:

```text
Compra apunta a comprobante
archivo no existe
```

Estrategia objetivo:

```text
1. recibir y validar archivo
2. procesarlo en almacenamiento temporal/controlado
3. registrar Compra y ComprobanteCompra
4. confirmar/mover archivo al almacenamiento definitivo
```

Si falla la creación de Compra:

```text
→ eliminar archivo temporal
```

Si falla el procesamiento/almacenamiento del comprobante:

```text
→ no registrar una Compra que afirme tener ese comprobante
```

La implementación concreta puede variar si existe una estrategia equivalente más segura, pero debe incluir limpieza y manejo explícito del fallo parcial.

No se debe simular que el sistema de archivos participa en la transacción de EF Core.

---

## 15. Comprobante opcional

El comprobante continúa siendo opcional para una Compra.

Por tanto:

```text
Compra sin archivo
→ válida si las demás reglas de negocio se cumplen
```

```text
Compra con archivo
→ archivo debe almacenarse correctamente antes de considerar válido ese comprobante
```

La UI debe diferenciar claramente entre:

- no adjuntar comprobante;
- intentar adjuntar uno y que la carga falle.

Un error de carga no debe interpretarse silenciosamente como "sin comprobante".

---

## 16. Datos del comprobante

Cuando exista comprobante, la UI podrá capturar los datos ya soportados por el modelo:

- `NumeroDocumento` opcional;
- `Fecha`;
- `Observaciones` opcionales;
- archivo real adjunto.

`RutaDocumento` no será un campo visible/editable de formulario.

---

## 17. Seguridad mínima del upload

La implementación debe incluir como mínimo:

- límite de tamaño del stream;
- lista blanca de formatos admitidos;
- nombre físico generado por el sistema;
- no confiar en rutas/nombres del cliente;
- evitar sobrescritura de archivos existentes;
- guardar únicamente dentro del directorio configurado para comprobantes;
- mensajes de error controlados para archivos rechazados;
- ninguna ejecución ni interpretación activa de archivos subidos.

Los comprobantes deben servirse únicamente como contenido de consulta dentro del contexto autenticado de ResellManager; no se debe diseñar el directorio de almacenamiento como un repositorio público anónimo por defecto.

---

## 18. UX móvil

El módulo se diseñará pensando en que una compra puede registrarse desde teléfono.

La captura del comprobante debe funcionar con el selector estándar de archivos del navegador, permitiendo que el dispositivo ofrezca cámara, galería o archivos según sus capacidades.

La usuaria debe ver al menos:

- nombre/tipo del archivo seleccionado de forma amigable;
- estado de carga/procesamiento cuando corresponda;
- error claro si supera el límite o el formato no es admitido;
- indicación de que el comprobante es opcional;
- acceso posterior al comprobante desde el detalle de Compra.

---

## 19. Puntos críticos de Fase 5.8

Durante revisión de implementación se deberá verificar especialmente:

1. `Compra.CodigoInterno` ya no depende de entrada manual de la usuaria.
2. El número de factura/comprobante continúa separado del código interno.
3. Catálogo nunca crea `UnidadInventario`.
4. Importación no se marca como recibida durante la compra.
5. Compra local y EnvíoHermano respetan su fecha de ingreso.
6. `CompraService` continúa siendo autoridad sobre creación de inventario.
7. Blazor no modifica estados de inventario manualmente para simular una Compra.
8. `RutaDocumento` nunca se captura como texto por la usuaria.
9. Los archivos subidos se validan y almacenan con nombre seguro.
10. Las imágenes se optimizan sin perder legibilidad relevante.
11. Los PDF no se transforman en V1 y respetan el límite de tamaño.
12. Los archivos no se guardan como BLOB dentro de SQLite.
13. El sistema limpia archivos temporales ante fallos.
14. Una Compra no puede quedar referenciando un archivo inexistente por un fallo controlable del flujo.
15. No se introduce una dependencia de imágenes sin revisar licencia y portabilidad.
16. No se usa `DbContext` directamente desde Blazor.

---

## 20. Fuera del alcance de esta fase

No se implementará en Fase 5.8:

- OCR;
- extracción automática de datos de facturas;
- múltiples archivos por Compra;
- almacenamiento en servicios cloud externos;
- edición avanzada de imágenes;
- firma digital de comprobantes;
- integración automática con proveedores;
- cálculo automático de comisiones de catálogo mientras las reglas del negocio sigan sin confirmarse;
- roles/permisos nuevos;
- Dashboard.

---

## 21. Relación con el roadmap

Con Fase 5.8 completa, el flujo principal queda conectado desde abastecimiento hasta cobro:

```text
Compra
  ↓
UnidadInventario, cuando aplica
  ↓
Pedido
  ↓
Venta
  ↓
Pago
```

Después de Fase 5.8 se retomará el Dashboard y posteriormente el pulido final de V1.

Roles y permisos permanecen como evolución posterior mientras los usuarios de confianza puedan compartir el mismo nivel de acceso.

---

## 22. Resultado implementado

### Rutas y UX

- `/compras`: historial ordenado por backend, tabla de escritorio y cards móviles.
- `/compras/nueva`: proveedor/productos reales, múltiples detalles, subtotal y total visual, reglas reactivas por origen y comprobante opcional.
- `/compras/{id}`: resumen, detalles, total backend, contexto de recepción y datos del comprobante sin mostrar `RutaDocumento`.
- `/proveedores` y `/proveedores/nuevo`: consulta y registro responsive.
- `/comprobantes/{compraId}`: lectura privada con autorización, tipo seguro, `X-Content-Type-Options: nosniff` y CSP `sandbox`.

Los formularios de compra y proveedor deshabilitan el submit mientras la operación está activa. El formulario no contiene inputs para `Compra.CodigoInterno` ni `ComprobanteCompra.RutaDocumento`.

### Códigos y reglas por origen

`CodigosInternos.CrearCodigoCompra()` crea una vez por flujo un valor `COM-<GUID N>` en mayúsculas. La validación de unicidad de `CompraService` y el índice único EF continúan activos.

`CompraService` no fue reemplazado: sigue calculando el total, creando `DetalleCompra` y, cuando corresponde, una `UnidadInventario` por cantidad con el costo del detalle.

- `Importacion`: unidades `Comprada`, `FechaIngreso = null`; recepción posterior por `IInventarioService.RegistrarRecepcionAsync`.
- `CompraLocal`: unidades `Disponible` y fecha de ingreso obligatoria.
- `EnvioHermano`: unidades `Disponible` y fecha de ingreso obligatoria.
- `Catalogo`: detalles sin ninguna `UnidadInventario`.

### Biblioteca de imágenes

- Nombre: **SkiaSharp**.
- Versión: **4.151.1**.
- Licencia revisada: **MIT**.
- Paquetes: `SkiaSharp` y `SkiaSharp.NativeAssets.Linux.NoDependencies`, ambos 4.151.1.
- Motivo: API mantenida y multiplataforma para decodificar/re-encodear JPG/JPEG, PNG y WebP sin usar `System.Drawing.Common`.
- Despliegue: usa binarios nativos. Windows llega mediante los activos de `SkiaSharp`; Linux incluye la variante `NoDependencies`, que excluye Fontconfig y depende únicamente de bibliotecas base de glibc. Debe publicarse para una arquitectura Linux soportada.

Las imágenes se validan primero por firma y después por decodificación, se limitan a 50 millones de píxeles, conservan la relación de aspecto, no se amplían y se reducen a un lado máximo de 1800 px. JPG y WebP se codifican a calidad 85; PNG se conserva lossless. El re-encode elimina metadatos no necesarios como efecto natural. Los PDF solo se validan, limitan y copian.

### Almacenamiento y configuración

La sección de configuración es:

```json
"AlmacenamientoComprobantes": {
  "DirectorioBase": "App_Data"
}
```

Una ruta relativa se resuelve desde el content root de la aplicación. Con el valor por defecto:

```text
src/ResellManager.Web/App_Data/
├── .temporales-comprobantes/
└── comprobantes/
    └── CMP-<GUID>.<extensión-validada>
```

La carpeta está fuera de `wwwroot`, se excluye de Git y los tests sustituyen la configuración por directorios temporales aislados. SQLite solo almacena una ruta controlada como `comprobantes/CMP-....pdf`.

### Estrategia de fallo parcial

El flujo real es:

```text
stream con límite real de 10 MB
→ copia RAW temporal
→ firma/decodificación y re-encode cuando es imagen
→ archivo preparado TMP-<GUID>
→ confirmación/movimiento a CMP-<GUID>
→ CompraService.RegistrarAsync con la ruta relativa
```

Se invierte deliberadamente confirmación y guardado DB respecto al flujo conceptual inicial: si falla la confirmación todavía no existe Compra, por lo que SQLite nunca queda apuntando a un archivo ausente. Si el backend rechaza la Compra, el archivo definitivo se elimina. Ante una excepción ambigua se consulta el listado por el código único: una compra persistida conserva su archivo y se recupera; una compra inexistente provoca limpieza. Si esa verificación tampoco está disponible, el archivo se conserva y se registra un error crítico para evitar destruir un documento que pudiera estar referenciado.

No se simula una transacción distribuida. La estrategia es compensatoria, explícita y está cubierta por pruebas de fallo de procesamiento, confirmación, registro backend y recuperación posterior al commit.

### Pruebas

La suite pasó con **195 pruebas**: las 161 anteriores más 34 nuevas. Cubre códigos, orígenes, múltiples detalles, total backend, entidades reales, formatos admitidos, 10 MB, nombres/rutas seguras, resize, limpieza, unicidad 1:0..1, autorización, lectura real, ausencia de `DbContext` en Blazor, responsive y doble submit.

Dashboard, roles, permisos, OCR, nube, edición/eliminación de compras y múltiples comprobantes permanecen fuera de alcance.
