# Códigos internos y canales de venta

Este documento registra decisiones y pendientes detectados durante la Fase 5.7 antes de continuar con las siguientes fases.

## 1. Regla general para códigos internos

Los códigos que existen únicamente para identificar registros dentro de ResellManager deben ser generados automáticamente por el sistema cuando sea técnicamente razonable.

La usuaria no debería tener que inventar, recordar ni coordinar manualmente códigos internos como parte de una operación normal del negocio.

### Distinción importante

**Código interno**

- Lo genera ResellManager.
- Identifica de forma única una entidad o transacción dentro del sistema.
- No debe confundirse con números o referencias de terceros.

**Referencia externa**

- Proviene de un proveedor, banco, factura, comprobante u otra fuente externa.
- Puede requerir captura manual porque representa información real fuera de ResellManager.

Ejemplo conceptual:

```text
Compra interna: COM-...
Factura del proveedor: FAC-001827
```

El primer valor pertenece al sistema; el segundo pertenece al documento o proveedor externo.

## 2. Estrategia de generación

En V1 se prefiere una estrategia que no dependa de contadores calculados en la UI, `MAX()+1` ni del supuesto de que solo existe un proceso activo.

Para los casos que actualmente necesitan generación desde la capa de presentación, puede utilizarse un prefijo legible más un GUID, por ejemplo:

```text
PED-VD-<GUID>
VEN-VD-<GUID>
```

Esta estrategia evita coordinación adicional y colisiones prácticas sin introducir todavía un servicio global de numeración.

En una evolución futura se podrá diseñar un generador centralizado de códigos más cortos y amigables para presentación, siempre que preserve unicidad y seguridad frente a concurrencia.

## 3. Fase 5.7 — código automático de venta directa

La venta presencial directa ya crea automáticamente un `Pedido` de tipo `VentaDirecta` con código `PED-VD-<GUID>`.

Se decide que el código interno de la `Venta` generada por este mismo flujo también debe ser automático.

### Motivo

Pedir a la usuaria un código técnico de venta no aporta valor al flujo real y agrega una causa evitable de error, especialmente códigos duplicados.

### Resultado esperado

En el modo `Venta directa` de `/ventas/nueva`:

- la usuaria no captura `CodigoInterno` de la venta;
- ResellManager genera automáticamente un código con prefijo distinguible, inicialmente `VEN-VD-<GUID>`;
- el código se conserva como identificador interno de la venta;
- el flujo `Desde pedido` existente no debe modificarse todavía más allá de lo necesario;
- no se modifica el esquema ni se vuelve opcional `Venta.CodigoInterno`;
- la validación de unicidad del backend permanece activa como segunda defensa.

La generación automática reduce una de las causas plausibles del estado parcial `Pedido creado / Venta no registrada`, aunque ese estado sigue siendo posible por concurrencia o fallos técnicos entre ambas operaciones.

## 4. Revisión futura de códigos internos

Antes de cerrar V1 se debe revisar de forma sistemática qué entidades aún piden códigos internos manuales y decidir cuáles deben automatizarse.

Candidatos principales:

- `Pedido.CodigoInterno`;
- `Venta.CodigoInterno`;
- `Compra.CodigoInterno` si aplica en el modelo actual;
- códigos internos de unidades físicas;
- cualquier otro identificador técnico capturado actualmente desde UI.

No deben automatizarse como si fueran códigos internos los datos que realmente son referencias externas, por ejemplo:

- número de factura;
- número de comprobante;
- referencia bancaria;
- código de barras del producto;
- referencias proporcionadas por proveedores.

La automatización de todos los códigos no se implementará de forma improvisada dentro de Fase 5.7; primero se revisarán contratos, reglas y uso real de cada entidad.

## 5. Nuevo concepto: CanalVenta

El negocio ya utiliza más de un canal comercial y debe poder distinguirse de `TipoPedido`.

`TipoPedido` responde a qué clase de operación se está realizando, por ejemplo:

- `Importacion`;
- `Catalogo`;
- `Apartado`;
- `VentaDirecta`.

`CanalVenta` responde a través de qué medio llegó o se originó comercialmente el pedido.

Son conceptos diferentes y no deben mezclarse.

### Valores iniciales propuestos

```csharp
public enum CanalVenta
{
    Presencial,
    WhatsApp,
    Facebook,
    Web,
    Otro
}
```

Los nombres finales se confirmarán al implementar.

### Motivo

Actualmente existen ventas y contactos comerciales por medios distintos. Registrar el canal permitirá medir qué medio realmente produce pedidos y ventas y preparar el sistema para nuevos canales sin alterar `TipoPedido`.

## 6. Ubicación conceptual de CanalVenta

La opción preferida es almacenar `CanalVenta` en `Pedido`.

Motivo:

El canal puede existir antes de que exista una venta registrada.

Ejemplo:

```text
Facebook
   ↓
Pedido / Apartado
   ↓
Venta
```

La `Venta` podrá conocer el canal a través de su `Pedido` y no será necesario duplicar el dato.

### Ejemplos

```text
TipoPedido = Apartado
CanalVenta = Facebook
```

```text
TipoPedido = VentaDirecta
CanalVenta = Presencial
```

```text
TipoPedido = Catalogo
CanalVenta = WhatsApp
```

En el futuro:

```text
TipoPedido = VentaDirecta / Apartado / otro flujo válido
CanalVenta = Web
```

## 7. Uso futuro de CanalVenta

El canal podrá utilizarse posteriormente en Dashboard y reportes para métricas como:

- número de pedidos por canal;
- ventas registradas por canal;
- monto vendido por canal;
- pedidos cancelados por canal;
- comparación entre Facebook, WhatsApp, presencial y futura web.

No se deben inventar métricas de conversión mientras el sistema no registre suficientes eventos para calcularlas correctamente.

## 8. Alcance del próximo cambio de CanalVenta

Antes de implementarlo se debe revisar el modelo actual de `Pedido`, DTOs, servicios, EF Core y migraciones.

La implementación prevista deberá contemplar como mínimo:

- agregar el enum `CanalVenta` en Domain;
- agregar la propiedad a `Pedido`;
- actualizar configuración EF Core y migración;
- actualizar `PedidoInput` y `PedidoDto`;
- actualizar `IPedidoService`/implementación según sea necesario;
- permitir seleccionar canal al crear pedidos manuales;
- asignar automáticamente `Presencial` a la venta directa presencial;
- mostrar el canal en detalle/listado donde aporte contexto;
- conservar `CanalVenta` independiente de `TipoPedido`;
- agregar tests de persistencia, creación y presentación;
- preparar el valor `Web` sin implementar todavía una tienda web ni integración automática.

No se implementará integración con Facebook, WhatsApp ni una tienda web en este cambio. El objetivo es únicamente registrar el origen comercial de manera estructurada.

## 9. Relación con V1 y V2

La generación automática del código de la venta directa pertenece al cierre de Fase 5.7 y debe realizarse antes del merge de esa fase.

`CanalVenta` es una ampliación pequeña del modelo motivada por un flujo real del negocio y conviene incorporarla antes de continuar con las siguientes fases, para que Compras, Dashboard y reportes futuros partan del modelo correcto.

Siguen pendientes para V2 las protecciones de concurrencia ya documentadas y la posible orquestación transaccional atómica `Pedido + Venta`.
