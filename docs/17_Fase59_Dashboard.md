# Fase 5.9 — Dashboard

## Estado

**Implementada.**

La ruta privada `/` presenta un resumen operativo construido por `IDashboardService` con datos reales
del backend. Blazor no lista módulos completos para recalcular métricas ni utiliza `DbContext`.

## Métricas principales

### Total adeudado

```text
SUM DetalleVenta.PrecioFinal de ventas Registrada
- SUM Pago.Monto
```

Las ventas `Cancelada` no forman parte de la deuda. La definición se comparte con el saldo de Cliente
y Pago mediante una consulta común de Infrastructure. El Dashboard no aplica `Math.Max` ni oculta un
resultado negativo si existieran datos históricos inconsistentes.

### Inventario disponible

```text
ValorInventarioDisponible =
SUM UnidadInventario.Costo WHERE Estado == Disponible

UnidadesDisponibles =
COUNT UnidadInventario WHERE Estado == Disponible
```

No se usa `Producto.PrecioSugerido`. Los estados `Comprada`, `EnTransito`, `Vendida` y `Entregada`
quedan excluidos.

### Pedidos activos

En V1 un pedido sigue operativo mientras su estado sea `Pendiente` o `Confirmado`. Por tanto:

```text
PedidosActivos = Pendiente + Confirmado
```

`Cancelado` y `Completado` son terminales para esta tarjeta y no se cuentan.

## Actividad reciente

Los últimos pagos respetan `cantidadRecientes` y se ordenan por `Fecha` descendente y después `Id`
descendente. Muestran cliente, fecha, monto y método, con acceso al detalle del cliente.

Las últimas ventas representan actividad comercial vigente, por lo que incluyen únicamente
`EstadoVenta.Registrada`. Se ordenan por `Fecha` descendente y después `Id` descendente. Muestran
código, cliente, fecha, total, canal y acceso al detalle. El historial completo, incluidas las ventas
canceladas, permanece en `/ventas`.

## Métricas por CanalVenta

`CanalVenta` continúa perteneciendo exclusivamente a `Pedido`; no se duplicó en `Venta` ni en
`VentaDto`. El Dashboard usa un DTO específico y relaciona `Venta → Pedido → CanalVenta`.

Siempre se devuelven los cinco canales reales del enum, incluso cuando sus valores son cero:

- `Presencial`;
- `WhatsApp`;
- `Facebook`;
- `Web`;
- `Otro`.

Las definiciones son:

```text
CantidadPedidos = COUNT Pedido por canal WHERE Estado != Cancelado

CantidadVentas = COUNT Venta por canal WHERE Estado == Registrada

MontoVentas = SUM DetalleVenta.PrecioFinal por canal
              WHERE Venta.Estado == Registrada
```

La cantidad de pedidos es una métrica histórica de origen comercial: conserva pedidos pendientes,
confirmados y completados, pero excluye demanda cancelada. Las ventas canceladas no cuentan ni suman
monto.

## Utilidad por periodo

`IDashboardService.ObtenerUtilidadAsync` valida que `desde <= hasta`. Un rango inválido devuelve
`ServiceResult` fallido y un periodo sin ventas devuelve `0`.

El filtro es inclusivo en ambos extremos:

```text
desde <= Venta.Fecha <= hasta
```

La fórmula se aplica por detalle únicamente a ventas `Registrada`:

```text
Utilidad = SUM(DetalleVenta.PrecioFinal - DetalleVenta.CostoUnitario)
```

La utilidad es comercial, no flujo de caja. No usa `PrecioSugerido`, `Compra.Total`, saldos ni pagos.
Si un dato histórico carece de costo unitario, el servicio registra el problema y devuelve un error
controlado en vez de inventar un costo.

La UI inicia con el primer día del mes actual y la fecha actual y ejecuta una primera consulta para
ese rango. Después, las fechas pueden modificarse sin recalcular en cada cambio; el nuevo periodo se
consulta al presionar **Consultar utilidad**. Un error de utilidad no elimina ni reemplaza el resto
del Dashboard.

## Consultas y errores

`DashboardService` ejecuta consultas EF Core proyectadas y agregadas de forma secuencial sobre el
mismo `DbContext`; no utiliza `Task.WhenAll`. Los fallos inesperados se registran en backend. La UI
muestra mensajes controlados y permite reintentar la carga general.

## Presentación V1

El Dashboard usa cards para métricas, una tabla sencilla para canales y tablas/cards responsive para
actividad reciente. Cuando no existen datos muestra valores cero, **Sin pagos recientes** o
**Sin ventas recientes**.

No incluye gráficas decorativas, porcentajes, tendencias, comparaciones o un mejor canal sin una base
real. Tampoco incorpora librerías JavaScript de gráficas.

## Fuera de alcance

La fase no modifica roles, permisos, administración de usuarios ni concurrencia V2. Tampoco agrega
devoluciones, OCR, IA, ecommerce, APIs de Facebook/WhatsApp, predicciones, forecasting o exportaciones.
