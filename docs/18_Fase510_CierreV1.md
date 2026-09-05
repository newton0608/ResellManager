# Fase 5.10 — Cierre técnico de V1

## 1. Alcance y estado

Trabajo exclusivo en `feature/fase-5-10-cierre-v1`. Esta fase corrige inconsistencias verificadas, termina la experiencia de uso y agrega regresiones; no incorpora módulos nuevos ni cambia las reglas de negocio de V1.

La auditoría inicial y las decisiones de alcance ya estaban realizadas antes de implementar. Las continuaciones conservaron el trabajo correcto y atendieron únicamente pendientes. La base inicial fue de **216 pruebas aprobadas**; los resultados finales de build, suite y Git se registran en la sección 9.

La revisión de código y las pruebas automatizadas **no equivalen a validación visual**. El navegador automatizado falló con `trusted Node process exited unexpectedly`. Por indicación expresa no se reintentó; la revisión visual real de escritorio, tablet y móvil queda pendiente antes de terminar la aceptación de V1.

Referencias: [backlog](09_Backlog.md), [decisiones](11_DecisionesDeDiseño.md), [alcance V1](14_Alcance_V1.md) y [códigos y canales](15_CodigosYCanalesVenta.md). Las fases [5.8](16_Fase58_ComprasYComprobantes.md) y [5.9](17_Fase59_Dashboard.md) mantienen su contexto histórico; sus recuentos de pruebas no son el total de esta fase.

## 2. Auditoría inicial y correcciones

Se revisaron autenticación, layout/navegación, dashboard, clientes, productos, categorías, inventario, pedidos/reservas, ventas, pagos, proveedores y compras/comprobantes. Una casilla pendiente del backlog no se tomó como prueba de funcionalidad ausente.

| Hallazgo real | Resolución de V1 |
| --- | --- |
| Registrar/cancelar apartado figuraba pendiente aunque el modelo y los servicios ya lo resolvían. | Documentar Pedido + Reserva; no crear otra entidad o módulo. |
| Documentación desactualizada sobre pantallas completas, códigos de compra y canales del dashboard. | Conciliarla con la implementación, sin duplicar funcionalidades. |
| Pedido normal y venta desde pedido pedían código técnico manual. | Generación automática estable en la instancia del formulario; unicidad backend conservada. |
| Venta directa podía perder estado al cambiar de modo y generaba el código del pedido dentro del intento. | Conservar el componente inicializado, bloquear cambios durante la operación y mantener ambos códigos en reintentos. |
| Compras mostraba `$`; otros módulos dependían de la cultura del proceso. | Unificar `Q 1,234.56` y fechas visibles `dd/MM/yyyy`, sin alterar persistencia. |
| Orígenes con etiquetas divergentes, switches repetidos e IDs numéricos sin contexto. | Helpers por módulo, etiquetas amigables y eliminación de IDs visibles innecesarios, conservando trazabilidad útil. |
| Errores de venta/pago ocultos cuando no había entidad seleccionada. | Mensajes visibles y salida/reintento controlados, incluidos enlaces inválidos. |
| Edición con ID cero podía interpretarse como creación; rutas inexistentes carecían de salida útil. | Distinguir ausencia de ID de ID inválido; estados controlados y `NoEncontrado`. |
| Faltaban enlaces pedido→venta e inventario vacío→compra. | Completar navegación/CTA y permitir cancelar el pedido pendiente cuya venta ya está cancelada. |
| Confirmaciones permitían un segundo clic durante el diálogo. | Activar guardia antes del primer `await` y liberarla en `finally`. |
| Cargas en paralelo compartían el mismo `DbContext` scoped. | Secuenciar consultas; no implementar concurrencia fuerte V2. |
| Las migraciones dependían de la creación opcional del usuario inicial. | Migrar al arrancar aunque se hayan retirado las credenciales de inicialización. |
| La configuración permitía comprobantes bajo el directorio público. | Rechazar almacenamiento dentro de `wwwroot`. |
| Configuración capturada demasiado pronto podía ignorar overrides de hosts de integración. | Resolver conexión y almacenamiento desde la configuración final; reforzar aislamiento de factories. No borrar datos para hacer pasar tests. |

Se conservan las reglas de `VentaService`, saldo global, inventario, recepción, catálogo, cancelación y reserva. No hay cambio intencional de esquema ni migración nueva vacía.

## 3. Códigos internos finales

`<GUID>` representa 32 caracteres hexadecimales en mayúsculas, sin guiones internos.

| Campo/artefacto | Clasificación | Resultado |
| --- | --- | --- |
| Pedido normal | A: técnico | Automático `PED-<GUID>`. |
| Pedido de venta directa | A: técnico | Automático `PED-VD-<GUID>`. |
| Venta desde pedido | A: técnico | Automático `VEN-<GUID>`. |
| Venta directa | A: técnico | Automático `VEN-VD-<GUID>`. |
| Compra | A: técnico | Automático `COM-<GUID>`. |
| Nombre físico del comprobante | A: técnico; no es un nuevo campo de dominio `CodigoInterno` | `CMP-<GUID>.<extensión>`; persistencia de ruta relativa. |
| Unidad de inventario | A: técnico con trazabilidad útil | `<código-compra>-01-001`: compra, detalle y número de unidad. |
| `Producto.CodigoInterno` | B: referencia operativa significativa | Manual para identificar/buscar productos. No se presupone que todos sean SKU de proveedor. |
| Código de barras, número de documento, referencia de pago y código de país del proveedor | B: referencias externas | Captura manual según formularios existentes. |

Producto permanece manual por su uso real como referencia de búsqueda y selección; no se cambia por uniformidad estética. El código de unidad se crea dentro de la compra, conserva índice único y no se recalcula al reservar, recibir, vender o cancelar. La UI no lo solicita; su estrategia no se refactoriza.

Los códigos de formulario se crean una vez por instancia, no por render ni envío. Se reutilizan después de errores y reintentos. Recargar/iniciar un formulario nuevo comienza otro flujo: esto no constituye idempotencia persistente ni concurrencia fuerte. La unicidad del backend sigue vigente.

## 4. Apartados y cancelaciones

Registrar apartado es crear un `Pedido` de `TipoPedido.Apartado`, agregar uno o más productos y reservar unidades físicas desde el detalle. La relación existente es `UnidadInventario.DetallePedidoReservaId`.

Reservar no cambia el estado físico. Se puede reservar mercancía comprada, en tránsito o disponible respetando las validaciones; no una unidad vendida/entregada, de otro producto o reservada para otro detalle, ni superar la cantidad del detalle. **Catálogo no crea ni reserva `UnidadInventario`.**

- Liberar reserva elimina su vínculo con el detalle; el pedido continúa y la unidad conserva estado físico, fecha y compra.
- Cancelar pedido libera todas sus reservas y lo marca cancelado si no tiene venta registrada activa. La mercancía comprada sigue existiendo.

No se creó entidad `Apartado`, módulo adicional ni segundo mecanismo de reservas.

Cancelar una venta antes de entrega mantiene las reglas previas: venta cancelada, unidades vendidas nuevamente disponibles, pedido pendiente y saldo recalculado. No restaura reservas antiguas. Se rechaza si hay unidades entregadas o si los pagos dejarían saldo negativo. Los pagos siguen asociados al cliente. La restricción de una venta por pedido permanece: una reventa requiere otro pedido.

## 5. Rutas definitivas y navegación

| Módulo | Rutas |
| --- | --- |
| Dashboard | `/` |
| Clientes | `/clientes`, `/clientes/nuevo`, `/clientes/{id}`, `/clientes/{id}/editar` |
| Productos | `/productos`, `/productos/nuevo`, `/productos/{id}`, `/productos/{id}/editar` |
| Categorías | `/categorias`, `/categorias/nueva`, `/categorias/{id}/editar` |
| Inventario, recepción y cambios manuales permitidos | `/inventario` |
| Pedidos y reservas/apartados | `/pedidos`, `/pedidos/nuevo`, `/pedidos/{id}` |
| Ventas desde pedido y directas | `/ventas`, `/ventas/nueva`, `/ventas/{id}` |
| Pagos | `/pagos` |
| Compras | `/compras`, `/compras/nueva`, `/compras/{id}` |
| Proveedores | `/proveedores`, `/proveedores/nuevo` |
| Login | `/login`; envío `POST /account/login` |
| Logout | `POST /account/logout`, autenticado |
| Comprobantes privados | `GET /comprobantes/{compraId}`, autenticado |
| Estados controlados | `/no-encontrado`, `/Error` |

Los IDs de entidad tienen restricción entera en rutas. La navegación contextual admite `/ventas/nueva?pedido={id}` y `/pagos?cliente={id}`. Parámetros inválidos no crean una selección válida ni dejan carga eterna. El menú conserva los módulos V1, sin apartados duplicados, y el dashboard sigue en `/`.

`NoEncontrado` ofrece retorno al inicio y a pedidos en navegación interactiva y respuesta HTTP. El pipeline aplica la página de estado a `404` de `GET`/`HEAD`, no reejecuta mutaciones ni sustituye otros estados como `400` o `405`. Autenticación y autorización preceden a los endpoints; no se abre acceso anónimo a módulos de negocio. Las regresiones HTTP están en `Fase510UiTests`.

## 6. UX, responsive y accesibilidad básica

Se conserva la identidad visual. Los ajustes CSS extienden navegación/tarjetas hasta `64rem` (aproximadamente 1024 px), permiten wrap en códigos, nombres y acciones, y mejoran controles táctiles. Formularios y cards limitan anchos y usan `min-width: 0` donde contenido variable podía desbordar.

La moneda se presenta como `Q 1,234.56`, independiente de la cultura del proceso. Las fechas visibles usan `dd/MM/yyyy`; el valor ISO propio de controles HTML de fecha no cambia el dominio `DateOnly`.

Los helpers siguen separados por módulo. Se unifica «Compra local», «En tránsito», «Venta directa» y «Envío del hijo»; el valor persistido `EnvioHermano` no cambia. Los helpers presentan etiquetas controladas para valores no reconocidos, no números de enum.

Los listados de clientes, productos, unidades, pedidos, ventas, pagos, compras y proveedores muestran contexto vacío y acciones útiles cuando corresponde. Se mantienen validación, defaults razonables, opciones iniciales, feedback y navegación posterior. Se agregan asociaciones `aria-describedby`, señales de requerido y mensajes de estado/error; se preservan foco visible y botones reales para acciones.

Las operaciones mutables utilizan estado ocupado y botones deshabilitados; las guardias de cancelación cubren también la confirmación. Los errores esperables tienen mensajes de negocio; los inesperados se registran con mensaje controlado para la usuaria, sin stack trace visible.

### Validación visual pendiente

| Superficie | Evidencia disponible | Navegador |
| --- | --- | --- |
| Escritorio: dashboard, compra, pedido, venta y pago | Código revisado y cobertura funcional automatizada. | **Pendiente** por fallo de automatización. |
| Tablet: navegación, formularios, tablas/cards y textos largos | Breakpoints y contenedores ajustados. | **Pendiente**; no se afirma ausencia visual de overflow. |
| Móvil: rutas principales y controles táctiles | CSS/HTML y asociaciones básicas corregidos. | **Pendiente**; no se inventan capturas ni resultados. |

La aceptación visual debe recorrer las rutas de la sección 5 con registros, estados vacíos, textos largos, errores y confirmaciones. Debe comprobar scroll horizontal, foco, tamaño táctil y elementos fuera de viewport. No se realizó una auditoría WCAG completa.

## 7. Seguridad, arranque, almacenamiento y datos

- Las páginas de negocio heredan autorización. Login es público; logout y comprobantes requieren autenticación. No se añadió autorregistro, roles ni administración de usuarios.
- El arranque aplica migraciones independientemente de las credenciales opcionales del usuario inicial. La inicialización no reemplaza contraseñas de usuarios existentes.
- Conexión y almacenamiento se resuelven usando la configuración final del host; las opciones de almacenamiento se validan antes de migrar. Los hosts de integración se aíslan en bases/directorios propios y verifican explícitamente la conexión efectiva.
- Los flujos migran SQLite limpio, verifican `GetPendingMigrationsAsync()` vacío y `HasPendingModelChanges()` falso. Cada operación y lectura crea un contexto nuevo para comprobar persistencia real, no valores retenidos por `ChangeTracker`.
- Comprobantes fuera de `wwwroot`, límite 10 MB y formatos JPG/JPEG, PNG, WebP y PDF. Se conserva validación de contenido, orientación EXIF, ruta relativa, endpoint autenticado y limpieza/compensación de temporales de Fase 5.8.
- No se registran contraseñas, cookies ni archivos completos. La preparación registra el identificador de operación en vez del nombre original del comprobante; los códigos operativos se conservan cuando aportan trazabilidad.
- No se eliminaron datos para resolver fallos de pruebas. Los SHA-256 anteriores y posteriores de la base de desarrollo Web y la antigua base del directorio de salida de tests coinciden: la ejecución final no las modificó. Tampoco se crearon los archivos de fallback del proceso. Las pruebas temporales no certifican la migración de una base operativa distinta.

## 8. Evidencia end-to-end y regresiones

Los **siete casos de [Fase510FlujosTests](../tests/ResellManager.Tests/Fase510FlujosTests.cs) aprobaron**, tanto en la ejecución previa como en la suite final de 264 pruebas. Usan servicios reales y SQLite en memoria aislado, no factories HTTP. Las siete pruebas de arranque también aprobaron tras corregir resolución de configuración y verificar conexión efectiva. El resultado completo figura en la sección 9.

| Escenario | Evidencia |
| --- | --- |
| A — Compra local | Proveedor/categoría/producto/cliente mediante servicios → compra de dos unidades disponibles → pedido/apartado → reserva → venta → pagos 35 y 65. Stock 2→1, valor 80→40, deuda 0→100→65→0, utilidad 60, canal presencial. |
| B — Importación | Unidad comprada sin fecha → reserva → tránsito → recepción disponible con fecha → venta. Conserva reserva/código durante recepción; al vender se elimina el vínculo, pedido completado, deuda 100, utilidad 60. |
| C — Catálogo | Compra → pedido → venta → pago sin inventario físico. Cero unidades antes/después, detalle sin unidad, deuda 120→0, utilidad 75, canal Facebook. |
| D — Apartado | Dos productos y estados comprada, en tránsito y disponible → reservar → liberar una → reservar de nuevo → cancelar pedido. Conserva códigos/fechas/estados; libera reservas y permite reservar para otro pedido. |
| Cancelación válida | Venta física + otra venta de catálogo + pago global → cancelar primera antes de entrega. Unidad disponible sin reserva restaurada, pedido pendiente, deuda 250→150, utilidad 180→120. Repetir no duplica inventario ni altera pagos. |
| Cancelación con entrega | Rechazada sin cambios en venta registrada, pedido completado, unidad entregada, deuda o canal. |
| Cancelación con pagos impeditivos | Rechazada si produciría saldo negativo; conserva venta, pedido, unidad vendida y pago. |

Clases nuevas:

- [Fase510CodigosTests](../tests/ResellManager.Tests/Fase510CodigosTests.cs): formato/unicidad, reintentos, doble submit, códigos directos, selector de modo, permanencia del componente, enlaces inválidos y límite de observaciones.
- [Fase510UiTests](../tests/ResellManager.Tests/Fase510UiTests.cs): vacíos, IDs inválidos/inexistentes, selección inválida, rutas desconocidas, semántica HTTP, pedido con venta cancelada, moneda bajo varias culturas y guardias anteriores al diálogo.
- [Fase510ArranqueTests](../tests/ResellManager.Tests/Fase510ArranqueTests.cs): migración sin credenciales completas, usuario conservado tras reinicio y rechazo de almacenamiento público.
- [Fase510FlujosTests](../tests/ResellManager.Tests/Fase510FlujosTests.cs): los siete escenarios de la tabla anterior.

No se eliminan pruebas previas por conveniencia. Las expectativas UI se adaptan solo cuando cambia el texto técnico visible, preservando la trazabilidad útil.

## 9. Comprobación final y entrega

Resultados de la ejecución final sobre los cambios de Fase 5.10:

| Comprobación | Resultado final |
| --- | --- |
| `dotnet build ResellManager.sln` | Aprobado. |
| Errores / warnings | **0 errores, 0 warnings**. |
| `dotnet test ResellManager.sln` | **264 aprobadas, 0 fallos, 0 omitidas**; duración informada: 51 s. |
| Existentes + nuevas / total | **216 + 48 = 264**. Nuevas: Arranque 7, Códigos 16, Flujos 7, UI 18. |
| `git diff --check` | Código de salida **0**. Los avisos informativos LF→CRLF previos al indexado no son warnings de compilación ni errores de whitespace. |
| SQLite limpio / snapshot | Flujos migrados aprobados; sin diferencias en Domain/Persistence ni migración nueva. |
| Bases preexistentes sin cambios | SHA-256 anteriores/posteriores iguales; archivos de fallback no creados. |
| Validación visual | Pendiente por fallo del navegador, sin reintento por indicación expresa. |
| Rama | `feature/fase-5-10-cierre-v1`. |

La entrega Git utiliza el commit `refactor: finalize v1 user experience` y el destino `origin/feature/fase-5-10-cierre-v1`. Su SHA, el resultado efectivo del push y el `git status` posterior se reportan en el mensaje final de entrega; no se anticipa aquí un resultado no ejecutado ni se introduce un SHA autorreferencial en el propio commit.

## 10. Código real de los puntos críticos

Extractos del código vigente. Los enlaces llevan al archivo completo, con sus validaciones y manejo de errores.

### 1. Pedido normal

[CodigosInternos.cs](../src/ResellManager.Application/Common/CodigosInternos.cs):

```csharp
public const string PrefijoPedido = "PED-";

public static string CrearCodigoPedido() =>
    PrefijoPedido + Guid.NewGuid().ToString("N").ToUpperInvariant();
```

[PedidoNuevo.razor](../src/ResellManager.Web/Components/Pages/PedidoNuevo.razor), propiedad estable y uso:

```csharp
private string CodigoPedido { get; } = CodigosInternos.CrearCodigoPedido();
```

```csharp
var resultado = await PedidoService.CrearAsync(Modelo.ToInput(CodigoPedido));
```

### 2. Venta normal desde pedido

[CodigosInternos.cs](../src/ResellManager.Application/Common/CodigosInternos.cs):

```csharp
public const string PrefijoVenta = "VEN-";

public static string CrearCodigoVenta() =>
    PrefijoVenta + Guid.NewGuid().ToString("N").ToUpperInvariant();
```

[VentaNueva.razor](../src/ResellManager.Web/Components/Pages/VentaNueva.razor):

```csharp
private string CodigoVenta { get; } = CodigosInternos.CrearCodigoVenta();
```

```csharp
var input = new VentaInput(
    PedidoSeleccionado.Id,
    CodigoVenta,
    Modelo.Fecha,
    Modelo.Observaciones,
    Detalles.Select(x => x.ToInput(EsCatalogo)).ToArray());
var resultado = await VentaService.RegistrarDesdePedidoAsync(input);
```

### 3. Pedido de venta directa

[VentaPresentacion.cs](../src/ResellManager.Web/Components/Ventas/VentaPresentacion.cs):

```csharp
public const string PrefijoPedidoVentaDirecta = "PED-VD-";
```

```csharp
public static string CrearCodigoPedidoVentaDirecta() =>
    PrefijoPedidoVentaDirecta + Guid.NewGuid().ToString("N").ToUpperInvariant();
```

[VentaDirectaForm.razor](../src/ResellManager.Web/Components/Ventas/VentaDirectaForm.razor):

```csharp
private string CodigoPedidoDirecto { get; } = VentaPresentacion.CrearCodigoPedidoVentaDirecta();
```

```csharp
var pedidoInput = new PedidoInput(
    CodigoPedidoDirecto,
    Modelo.Fecha,
    TipoPedido.VentaDirecta,
    CanalVenta.Presencial,
    Modelo.ClienteId,
    ConstruirObservacionesPedido(),
    seleccionadas.Select(x => x.ToPedidoInput()).ToArray());
var pedido = await PedidoService.CrearAsync(pedidoInput);
```

### 4. Venta directa

[VentaPresentacion.cs](../src/ResellManager.Web/Components/Ventas/VentaPresentacion.cs):

```csharp
public const string PrefijoVentaDirecta = "VEN-VD-";
```

```csharp
public static string CrearCodigoVentaDirecta() =>
    PrefijoVentaDirecta + Guid.NewGuid().ToString("N").ToUpperInvariant();
```

[VentaDirectaForm.razor](../src/ResellManager.Web/Components/Ventas/VentaDirectaForm.razor):

```csharp
private string CodigoVentaDirecta { get; } = VentaPresentacion.CrearCodigoVentaDirecta();
```

```csharp
var ventaInput = new VentaInput(
    PedidoAutomaticoCreado.Id,
    CodigoVentaDirecta,
    Modelo.Fecha,
    Modelo.Observaciones,
    seleccionadas.Select(x => x.ToVentaInput()).ToArray());
var venta = await VentaService.RegistrarDesdePedidoAsync(ventaInput);
```

La creación del pedido solo ocurre dentro de `if (PedidoAutomaticoCreado is null)`: un pedido ya creado se reutiliza. Cambiar de modo no destruye el componente inicializado ni sus códigos.

### 5. Compra y archivo de comprobante

[CodigosInternos.cs](../src/ResellManager.Application/Common/CodigosInternos.cs) y [CompraNueva.razor](../src/ResellManager.Web/Components/Pages/CompraNueva.razor):

```csharp
public const string PrefijoCompra = "COM-";

public static string CrearCodigoCompra() =>
    PrefijoCompra + Guid.NewGuid().ToString("N").ToUpperInvariant();
```

```csharp
private readonly string CodigoInterno = CodigosInternos.CrearCodigoCompra();
```

[AlmacenamientoComprobantesLocal.cs](../src/ResellManager.Infrastructure/Storage/AlmacenamientoComprobantesLocal.cs):

```csharp
var nombreFinal = $"CMP-{Guid.NewGuid():N}".ToUpperInvariant() + tipo.Extension;
```

### 6. Unidad de inventario

[CompraInventarioServices.cs](../src/ResellManager.Infrastructure/Services/CompraInventarioServices.cs), bucle de detalle:

```csharp
if (generaInventario)
{
    for (var i = 1; i <= item.Cantidad; i++)
        detalle.UnidadesInventario.Add(
            new UnidadInventario
            {
                CodigoInterno = $"{compra.CodigoInterno}-{detailNumber:D2}-{i:D3}",
                Estado = estadoInicial,
                FechaIngreso =
                    estadoInicial == EstadoUnidadInventario.Disponible
                        ? input.FechaIngreso
                        : null,
                Costo = item.CostoUnitario,
                ProductoId = item.ProductoId,
            }
        );
}
```

### 7. Registrar apartado con el modelo existente

[Fase510FlujosTests.cs](../tests/ResellManager.Tests/Fase510FlujosTests.cs), apartado de varios productos:

```csharp
var pedido = Exito(await flujo.EjecutarAsync(db => new PedidoService(db).CrearAsync(
    new PedidoInput(CodigosInternos.CrearCodigoPedido(), Fecha, TipoPedido.Apartado,
        CanalVenta.Otro, flujo.Cliente.Id, null,
        [new(flujo.Producto.Id, 2, 100m, null), new(segundo.Id, 1, 100m, null)]))));
var antes = (await flujo.UnidadesAsync()).ToDictionary(x => x.Id);
foreach (var unidad in unidades)
{
    var detalle = pedido.Detalles.Single(x => x.ProductoId == unidad.ProductoId);
    Exito(await flujo.EjecutarAsync(db => new InventarioService(db)
        .ReservarAsync(unidad.Id, detalle.Id)));
}
```

Mutación final en `InventarioService.ReservarAsync`, después de validar:

```csharp
unidad.DetallePedidoReservaId = detallePedidoId;
await db.SaveChangesAsync(ct);
```

### 8. Liberar reserva y cancelar apartado

[InventarioService.CancelarReservaAsync](../src/ResellManager.Infrastructure/Services/CompraInventarioServices.cs), después de comprobar existencia:

```csharp
unidad.DetallePedidoReservaId = null;
await db.SaveChangesAsync(ct);
```

[PedidoService.CancelarAsync](../src/ResellManager.Infrastructure/Services/OperacionServices.cs):

```csharp
if (x.Venta is { Estado: EstadoVenta.Registrada })
    return ServiceResult.Failure("El pedido tiene una venta registrada.");

foreach (var unidad in x.Detalles.SelectMany(d => d.UnidadesReservadas))
    unidad.DetallePedidoReservaId = null;

x.Estado = EstadoPedido.Cancelado;
await db.SaveChangesAsync(ct);
return ServiceResult.Ok();
```

### 9. Protección relevante de doble submit

[PedidoNuevo.razor](../src/ResellManager.Web/Components/Pages/PedidoNuevo.razor), método completo:

```csharp
private async Task GuardarAsync()
{
    if (Guardando)
        return;

    ErrorGuardado = ValidarDetalles();
    if (ErrorGuardado is not null)
        return;

    Guardando = true;
    try
    {
        var resultado = await PedidoService.CrearAsync(Modelo.ToInput(CodigoPedido));
        if (!resultado.IsSuccess || resultado.Value is null)
        {
            ErrorGuardado = resultado.ErrorMessage ?? "No fue posible crear el pedido.";
            return;
        }

        Navigation.NavigateTo($"/pedidos/{resultado.Value.Id}?mensaje=pedido-creado");
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "No fue posible crear el pedido {CodigoPedido}.", CodigoPedido);
        ErrorGuardado = "Ocurrió un problema inesperado al guardar. Intenta nuevamente.";
    }
    finally
    {
        Guardando = false;
    }
}
```

En [PedidoDetalle.razor](../src/ResellManager.Web/Components/Pages/PedidoDetalle.razor), este inicio de método activa la guardia antes del diálogo:

```csharp
if (Procesando || !PuedeCancelarPedido)
    return;

CancelandoPedido = true;
ErrorOperacion = null;
try
{
    var confirmado = await JS.InvokeAsync<bool>(
        "confirm",
        "El pedido será cancelado y todas sus reservas se liberarán. La mercancía ya comprada seguirá existiendo en inventario.");
    if (!confirmado)
        return;
```

El `finally` del método libera `CancelandoPedido`; los botones usan `Procesando`. La regresión verifica dos clics antes de terminar el diálogo. Son guardias UX, no concurrencia backend.

### 10. Flujo crítico real: venta, pago y dashboard

Extracto de `CompraLocal_ReservaVentaPago_ActualizaInventarioSaldoCanalYUtilidad` en [Fase510FlujosTests.cs](../tests/ResellManager.Tests/Fase510FlujosTests.cs), después de crear compra, pedido y reserva:

```csharp
var venta = await flujo.VenderFisicaAsync(pedido, unidad.Id);
Assert.Equal(EstadoVenta.Registrada, venta.Estado);
Assert.Equal(40m, venta.Detalles.Single().CostoUnitario);
Assert.Equal(EstadoPedido.Completado, (await flujo.PedidoAsync(pedido.Id)).Estado);
var vendida = (await flujo.UnidadesAsync()).Single(x => x.Id == unidad.Id);
Assert.Equal(EstadoUnidadInventario.Vendida, vendida.Estado);
Assert.Null(vendida.DetallePedidoReservaId);
await flujo.VerificarDashboardAsync(100m, 1, 40m, 0, 60m);
await flujo.VerificarCanalAsync(CanalVenta.Presencial, 1, 1, 100m);

var pago = await flujo.PagarAsync(35m);
await flujo.VerificarDashboardAsync(65m, 1, 40m, 0, 60m);
var resumen = await flujo.EjecutarAsync(db => new DashboardService(db).ObtenerAsync());
Assert.Equal(pago.Id, Assert.Single(resumen.UltimosPagos).Id);
Assert.Equal(venta.Id, Assert.Single(resumen.UltimasVentas).Id);
Assert.Equal(CanalVenta.Presencial, resumen.UltimasVentas.Single().Canal);
await flujo.PagarAsync(65m);
await flujo.VerificarDashboardAsync(0m, 1, 40m, 0, 60m);
```

## 11. Pendientes V2 y preparación operativa

Deliberadamente **no implementados**: roles/permisos, administración de usuarios, concurrencia fuerte multiusuario, devoluciones/cambios, OCR, IA, ecommerce, APIs Facebook/WhatsApp, offline, PostgreSQL, despliegue productivo, CI/CD complejo, exportación y notificaciones. Los canales son clasificación interna, no integraciones externas.

La venta directa conserva Pedido → Venta; no incorpora transacción distribuida ni recuperación/idempotencia entre sesiones. Las restricciones de cancelación/pagos no se sustituyen por un módulo de devoluciones.

Antes de prueba real y posterior despliegue:

1. Completar aceptación visual escritorio/tablet/móvil con la usuaria, incluidos A–D y cancelación.
2. Preparar respaldo verificable de SQLite/comprobantes y confirmar directorios privados y permisos del proceso.
3. Configurar conexión, HTTPS y secretos sin credenciales reales en el repositorio; retirar las credenciales de inicialización después del alta.
4. Verificar migraciones sobre una copia de los datos objetivo y ensayar recuperación del respaldo. Las pruebas temporales no sustituyen esta verificación operativa.
5. Confirmar aceptación de negocio y resolver hallazgos reales antes de autorizar despliegue. Esta fase no despliega a producción.
