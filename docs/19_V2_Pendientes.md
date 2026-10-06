# V2 — Pendientes y mejoras futuras

Este documento reúne planificación posterior al alcance original V1. Algunas piezas se adelantaron y ya existen en la base auditada el 05/10/2026; se distingue su estado para no repetir implementaciones.

La intención es evitar que V2 se convierta en una sola entrega demasiado grande. Por ello se divide conceptualmente en cuatro bloques:

- **V2.1 — Productividad y experiencia de uso**.
- **V2.2 — Operación del negocio**.
- **V2.3 — Escalabilidad y multiusuario**.
- **V2.4 — Canal público / tienda en línea**.

El orden puede ajustarse después de observar el uso real de V1, pero esta división sirve como guía de planificación.

---

# V2.1 — Productividad y experiencia de uso

Objetivo: reducir tiempo operativo y mejorar los flujos más frecuentes sin cambiar todavía la base del negocio.

## Imágenes de productos

**Foto principal ya implementada**: [contrato actual](24_ImagenPrincipalProducto.md). La evaluación de múltiples fotos y ampliaciones posteriores sigue planeada. Los puntos siguientes conservan el alcance considerado originalmente y no implican que toda esa ampliación exista.

- Producto ya muestra su imagen en listado/detalle y el catálogo público la reutiliza cuando corresponde. Sigue pendiente incorporarla donde aporte valor en Inventario, Venta Directa y otros buscadores operativos.
- Usar almacenamiento persistente; no guardar imágenes en la base de datos como blobs por defecto. Evaluar object storage para producción.
- Generar miniaturas y aplicar compresión; definir formatos permitidos y tamaño máximo.
- Permitir reemplazo y eliminación seguros, con limpieza de archivos sin referencias.
- Mostrar un fallback cuando no exista imagen.
- Validar contenido, permisos de acceso y privacidad; evitar exponer archivos no autorizados.

La foto principal tiene implementación y migración existentes; no volver a crearlas siguiendo esta planificación. Múltiples imágenes/object storage siguen pendientes.

## Búsqueda asistida externa al agregar Producto

**Ya implementada antes de iniciar V2.1.** En `/productos/nuevo`, un código escrito o confirmado por el scanner comprueba primero coincidencia local y luego puede consultar Open Facts y UPCitemdb. El resultado se revisa antes de importarse, puede deshacerse y nunca guarda el producto por sí solo. La imagen externa aceptada se descarga únicamente al guardar y se incorpora al almacenamiento administrado si pasa sus validaciones. Ver [contrato vigente](modules/productos-lookup-codigo-barras.md).

Esta ampliación no equivale a la integración operativa del lector en Venta Directa o Inventario y no debe volver a implementarse como parte de V2.1.

## Lectura de códigos de barras con la cámara

La captura de `Producto.CodigoBarras` con cámara durante alta/edición ya está implementada; el motor vigente y sus límites se documentan en [Scanner](29_BarcodeScanner.md). En el código actual, `ProductoForm` sigue siendo el consumidor funcional del componente `BarcodeScanner`; Venta Directa e Inventario todavía no lo integran. La integración operativa del lector en esos flujos y en búsquedas donde aporte velocidad permanece planificada para **V2.1**.

### Objetivo

Permitir que la usuaria pueda escanear `Producto.CodigoBarras` desde un teléfono o dispositivo con cámara para localizar productos rápidamente y reducir la captura manual durante la operación diaria.

### Alcance operativo previsto para V2.1

- Reutilizar el lector de cámara de V1.1 para leer códigos de barras compatibles.
- Buscar el producto mediante `Producto.CodigoBarras` después de una lectura exitosa.
- Integrar el escaneo principalmente con **Venta Directa** e **Inventario**.
- Evaluar su uso también en búsquedas y selección de productos donde aporte velocidad real al flujo.
- Mantener siempre la búsqueda/captura manual como alternativa cuando no haya cámara, no exista permiso o el código no pueda leerse.
- Mostrar errores claros cuando el código leído no corresponda a ningún producto registrado.

### Regla de datos

`Producto.CodigoBarras` continúa siendo una referencia externa del producto. El lector no genera códigos de barras: en V1.1 captura mediante cámara el valor de la referencia externa en el formulario de Producto, donde puede editarse manualmente antes de guardar; en V2.1 podrá utilizarse en búsquedas y selección.

El comportamiento de `Producto.CodigoInterno` es independiente de esta funcionalidad y debe seguir la decisión vigente documentada para los códigos internos del sistema.

### Consideraciones técnicas para la integración V2.1

Al extender el lector a flujos operativos se deberá evaluar:

- compatibilidad de cámara en navegadores móviles;
- permisos y experiencia cuando el usuario deniega acceso a la cámara;
- soporte real de formatos de códigos utilizados por la mercancía del negocio;
- comportamiento en conexiones lentas o inestables;
- rendimiento del lector en dispositivos móviles;
- seguridad y privacidad: la cámara solo debe activarse por acción explícita de la usuaria;
- una solución web compatible con la arquitectura Blazor existente, evitando dependencias innecesarias.

### Criterio de experiencia esperado

Flujo objetivo para Venta Directa:

1. La usuaria abre **Venta Directa**.
2. Selecciona **Escanear código**.
3. La aplicación solicita/usa la cámara.
4. Se lee `Producto.CodigoBarras`.
5. Se localiza el producto correspondiente.
6. La usuaria confirma o agrega el producto a la operación.
7. Si no puede escanearse, puede buscarse manualmente sin bloquear la venta.

Esta mejora debe acelerar la operación, no convertir el escaneo en requisito obligatorio para vender o consultar inventario.

## Analítica de uso y telemetría de producto

Incorporar medición del uso real de la aplicación para orientar futuras decisiones de UX con datos y no únicamente con intuición.

### Eventos útiles a evaluar

- páginas o módulos visitados;
- uso de acciones rápidas del Dashboard;
- inicio y finalización de Venta Directa;
- inicio y finalización de registro de pedidos;
- inicio y finalización de pagos/abonos;
- búsquedas de clientes y productos;
- frecuencia de uso por módulo;
- errores funcionales controlados;
- tiempos aproximados de operaciones relevantes;
- reconexiones o fallos técnicos que afecten la experiencia.

### Privacidad

La analítica no debe enviar información comercial sensible innecesaria. Evitar en los eventos:

- nombres de clientes;
- teléfonos;
- saldos individuales;
- notas privadas;
- nombres concretos de productos cuando no sean necesarios;
- montos individuales de ventas o pagos salvo que exista una razón analítica explícita y segura.

Preferir eventos genéricos como:

```text
QuickActionClicked / RegisterPayment
OperationCompleted / DirectSale
SearchPerformed / Customer
```

Antes de elegir una herramienta externa, evaluar si una telemetría propia y mínima es suficiente para el número real de usuarios de ResellManager.

## Mejoras de UX guiadas por uso real

Usar los datos anteriores y la observación directa de la operación para ajustar:

- acciones rápidas del Dashboard;
- accesos más frecuentes;
- orden de módulos;
- campos que casi nunca se utilizan;
- pasos redundantes en formularios;
- búsquedas y selección de clientes/productos.

Las decisiones deben priorizar los flujos que realmente ahorran tiempo a la usuaria.

---

# V2.2 — Operación del negocio

Objetivo: ampliar los procesos comerciales que V1 dejó deliberadamente fuera o simplificados.

## Informes del negocio

Incorporar un módulo formal de **Informes** para analizar la operación real del negocio a partir de los datos acumulados en ResellManager.

Los informes son distintos de la analítica de uso de la aplicación: aquí el objetivo es responder qué está pasando en el negocio, no cómo se utiliza la interfaz.

### Informes previstos inicialmente

- ventas por período;
- utilidad por período;
- cuentas por cobrar;
- clientes con mayor saldo pendiente;
- productos más vendidos;
- inventario actual;
- valor de inventario al costo;
- compras por período;
- compras por proveedor;
- compras por origen;
- pagos/abonos recibidos por período;
- pedidos pendientes o activos.

### Criterios de diseño

- reutilizar la lógica de negocio y consultas existentes cuando corresponda;
- permitir filtros por rangos de fecha cuando tenga sentido;
- mostrar totales y detalles de forma entendible para la usuaria;
- evitar duplicar fórmulas ya definidas en Dashboard o servicios de negocio;
- diferenciar claramente utilidad comercial, deuda, inventario y flujo de pagos;
- priorizar informes que respondan preguntas reales del negocio.

### Exportación

Evaluar exportación de informes a formatos como PDF y Excel una vez que los informes principales estén estables.

La exportación avanzada no es requisito para la primera entrega de V2.2 y puede implementarse posteriormente si aporta valor real.

## Devoluciones y cambios

Diseñar un flujo formal para devoluciones y cambios de mercancía ya vendida o entregada.

Debe preservar:

- historial de la venta original;
- trazabilidad de unidades físicas;
- impacto correcto en inventario;
- impacto correcto en saldo del cliente;
- costos y utilidad histórica;
- motivo del cambio o devolución.

No se debe resolver eliminando ventas o reescribiendo historial.

## Comisiones de catálogo

Cuando el negocio confirme las reglas reales de cada proveedor:

- configurar comisión por proveedor y/o categoría;
- definir la base exacta del cálculo;
- automatizar el cálculo de comisión cuando corresponda;
- guardar snapshot del porcentaje y/o monto aplicado en cada operación para preservar el histórico aunque la configuración cambie después.

## Edición avanzada de pedidos

Evaluar e implementar según necesidad real:

- edición de detalles de pedidos;
- eliminación controlada de detalles;
- reactivación de pedidos cancelados si el negocio realmente la necesita;
- historial más detallado de cambios y reservas.

Estas operaciones deben respetar reservas, inventario, ventas existentes y estados terminales.

## Desactivación segura de clientes y productos

**Decisión de dirección aceptada; implementación pendiente.** Clientes y productos no se eliminan físicamente desde la aplicación, tengan o no historial; la operación prevista siempre es desactivación/reactivación, según [decisión 025](11_DecisionesDeDiseño.md#025-preservar-clientes-y-productos-con-historial). No existe estado activo en las entidades actuales.

Falta definir el tratamiento de deuda, reservas u operaciones activas, qué operaciones admiten maestros inactivos, búsquedas/listados, efecto público e inventario, y reglas exactas de reactivación.

## Evolución futura del canal Web

`CanalVenta.Web` existe conceptualmente, pero V1 no implementa ecommerce.

La tienda pública básica se planifica como etapa final V2.4, separada de V2.2. Esta decisión sustituye la consideración anterior de tienda opcional fuera del compromiso de V2.

---

# V2.3 — Escalabilidad y multiusuario

Objetivo: preparar ResellManager para varios usuarios, mayor concurrencia y una operación más crítica.

## Concurrencia de saldo por cliente

Revisar las operaciones que afectan el saldo global de un cliente como sección crítica lógica:

- registrar venta;
- registrar pago/abono;
- cancelar venta.

La solución futura debe:

- impedir operaciones simultáneas incompatibles sobre el mismo cliente;
- evitar un bloqueo global cuando dos clientes distintos puedan procesarse de forma independiente;
- incluir protección a nivel de aplicación y persistencia/transacción;
- evitar depender de temporización o velocidad relativa de procesos;
- garantizar progreso y evitar espera indefinida.

## Concurrencia de reservas de inventario

Proteger la reserva por `UnidadInventarioId` para impedir doble reserva simultánea.

Debe contemplar al menos:

- dos intentos de reservar la misma unidad;
- reserva vs. cancelación de reserva;
- cancelación de pedido vs. reserva concurrente;
- protección de persistencia además de la validación actual del backend.

El bloqueo debe ser por unidad, no global.

## Usuarios, roles y permisos

Si el negocio evoluciona a varios trabajadores o usuarios:

- administrar usuarios desde la aplicación;
- crear/deshabilitar cuentas;
- restablecer acceso;
- asignar roles;
- restringir operaciones según permisos;
- mantener trazabilidad de quién realizó operaciones importantes cuando aporte valor real.

No se requiere para el escenario actual de V1.

## Robustez de Venta Directa

Evaluar una orquestación transaccional más fuerte para la creación de:

```text
Pedido de Venta Directa + Venta
```

especialmente si existen múltiples usuarios o instancias de la aplicación.

La meta sería evitar estados parciales y mejorar idempotencia/recuperación sin romper la regla de que toda Venta nace de un Pedido.

## Telemetría técnica

Separar la telemetría técnica de la analítica de producto.

Evaluar herramientas o estándares como OpenTelemetry / Application Insights o equivalentes para observar:

- excepciones;
- tiempos de respuesta;
- consultas lentas;
- consumo de recursos;
- disponibilidad;
- errores de infraestructura;
- comportamiento de múltiples instancias.

La finalidad es responder "qué parte del sistema está fallando o lenta", no "qué usa más la usuaria".

---

# V2.4 — Canal público / tienda en línea

Etapa final prevista de V2: ofrecer una tienda pública básica usando ResellManager como única fuente de verdad.

Alcance inicial:

- catálogo público, categorías, búsqueda y detalle de producto;
- imagen del producto cuando exista, con fallback cuando no tenga;
- disponibilidad derivada del inventario real, sin mantener inventario duplicado para la web;
- carrito o selección de productos y captura de datos del cliente;
- creación de una solicitud/pedido dentro de ResellManager reutilizando sus reglas y servicios actuales;
- revalidación de disponibilidad al procesar la solicitud, sin asumir que consultar un producto equivale a reservarlo.

No se requieren inicialmente pago en línea, cuentas completas de cliente, promociones complejas, integración WhatsApp ni logística avanzada. Pueden evaluarse después; no son requisitos de la tienda básica V2.4.

**Estado actual:** catálogo, categorías derivadas del listado, búsqueda, detalle, imagen y disponibilidad de lectura ya existen: [Catálogo público](modules/catalogo.md). La evolución hacia carrito/selección y pedidos web sigue siendo planificación V2.4. No está implementada ni autorizada por este roadmap; pagos online tampoco existen. Dominios y ruta canónica pendiente se describen en [Dominios](deployment/domains.md).

---

# Fuera de compromiso automático de V2

Estas ideas siguen siendo posibles expansiones, pero no se consideran obligatorias para completar V2 salvo que el negocio las justifique:

- ecommerce avanzado más allá de la tienda pública básica prevista en V2.4;
- integración automática con WhatsApp;
- integración automática con Facebook;
- OCR;
- funciones de IA;
- predicciones o forecasting;
- numeraciones humanas más cortas;
- exportaciones avanzadas más allá de los informes principales;
- otras integraciones externas.

Antes de incorporar cualquiera de estas funciones se debe comprobar que resuelva un problema real y que su costo de mantenimiento sea razonable.

---

# Criterio general para planificar V2

V1 debe utilizarse primero en el negocio real. La prioridad de V2 debería decidirse combinando:

1. problemas observados durante uso real;
2. frecuencia de los flujos;
3. tiempo que cada mejora puede ahorrar;
4. riesgo del problema actual;
5. complejidad de implementación y mantenimiento.

El objetivo de V2 no es agregar funciones por cantidad, sino hacer que ResellManager sea más rápido, seguro y útil conforme crezca el negocio.
