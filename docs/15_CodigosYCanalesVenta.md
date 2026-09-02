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

## 3. Fase 5.7 — código automático de venta directa (implementado)

La venta presencial directa crea automáticamente un `Pedido` de tipo `VentaDirecta` con código `PED-VD-<GUID>`.

El código interno de la `Venta` generada por este mismo flujo también se crea automáticamente como `VEN-VD-<GUID>`.

### Motivo

Pedir a la usuaria un código técnico de venta no aporta valor al flujo real y agrega una causa evitable de error, especialmente códigos duplicados.

### Resultado implementado

En el modo `Venta directa` de `/ventas/nueva`:

- la usuaria no captura `CodigoInterno` de la venta;
- ResellManager genera automáticamente un código con prefijo distinguible, inicialmente `VEN-VD-<GUID>`;
- el código se conserva como identificador interno de la venta;
- el flujo `Desde pedido` existente no debe modificarse todavía más allá de lo necesario;
- no se modifica el esquema ni se vuelve opcional `Venta.CodigoInterno`;
- la validación de unicidad del backend permanece activa como segunda defensa.

La generación automática reduce una de las causas plausibles del estado parcial `Pedido creado / Venta no registrada`, aunque ese estado sigue siendo posible por concurrencia o fallos técnicos entre ambas operaciones. El mismo código de venta y el mismo pedido se conservan durante los reintentos del flujo mientras permanece activo el componente.

Este cierre no cambia la política del modo `Desde pedido` ni implementa un generador general para otros módulos.

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

## 5. Concepto implementado: CanalVenta

**Estado: implementado.**

El negocio ya utiliza más de un canal comercial y debe poder distinguirse de `TipoPedido`.

`TipoPedido` responde a qué clase de operación se está realizando, por ejemplo:

- `Importacion`;
- `Catalogo`;
- `Apartado`;
- `VentaDirecta`.

`CanalVenta` responde a través de qué medio llegó o se originó comercialmente el pedido.

Son conceptos diferentes y no deben mezclarse.

### Valores implementados

```csharp
public enum CanalVenta
{
    Presencial = 1,
    WhatsApp = 2,
    Facebook = 3,
    Web = 4,
    Otro = 5
}
```

Los valores son explícitos porque se persisten en base de datos y su significado histórico no puede
depender del orden de declaración.

### Motivo

Actualmente existen ventas y contactos comerciales por medios distintos. Registrar el canal permitirá medir qué medio realmente produce pedidos y ventas y preparar el sistema para nuevos canales sin alterar `TipoPedido`.

## 6. Ubicación conceptual de CanalVenta

`CanalVenta` se almacena como propiedad requerida de `Pedido`.

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

La `Venta` conoce el canal a través de su `Pedido`; no existe una propiedad ni columna duplicada en
`Venta`, `VentaInput` o `VentaDto`.

Los pedidos existentes al aplicar la migración reciben `CanalVenta.Otro`, porque no existe información
confiable para reconstruir su origen. No se infiere el canal desde `TipoPedido`.

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

`Web` queda disponible como canal conceptual, pero no representa una tienda online implementada.

La Venta Directa representa una operación física y crea automáticamente su pedido con:

```text
TipoPedido = VentaDirecta
CanalVenta = Presencial
```

Este flujo no solicita el canal manualmente y conserva el mismo pedido y código de venta durante los
reintentos ya soportados.

## 7. Uso futuro de CanalVenta

El canal podrá utilizarse posteriormente en Dashboard y reportes para métricas como:

- número de pedidos por canal;
- ventas registradas por canal;
- monto vendido por canal;
- pedidos cancelados por canal;
- comparación entre Facebook, WhatsApp, presencial y futura web.

No se deben inventar métricas de conversión mientras el sistema no registre suficientes eventos para calcularlas correctamente.

## 8. Alcance implementado de CanalVenta

La implementación incluye:

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
- preparar el valor `Web` sin implementar una tienda web ni integración automática;
- migrar pedidos históricos a `Otro`.

No existe integración automática con Facebook o WhatsApp, ni tienda Web/ecommerce. El objetivo
implementado es únicamente registrar el origen comercial de manera estructurada.

## 9. Relación con V1 y V2

La generación automática de los códigos del pedido y la venta directa permanece sin cambios.
`CanalVenta` ya forma parte de V1 como dato del pedido.

Siguen pendientes para V2 las protecciones de concurrencia ya documentadas y la posible orquestación transaccional atómica `Pedido + Venta`.
También permanece pendiente el Dashboard por canal, con posibles métricas de ventas registradas por
canal y cantidad de pedidos por canal.
