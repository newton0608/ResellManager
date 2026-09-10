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

## 3. Códigos internos del cierre original

Registro histórico: la clasificación manual de Producto de esta sección fue sustituida por la decisión posterior de la sección 13. La referencia vigente es `15_CodigosYCanalesVenta.md`.

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

## 12. Ajuste UX posterior al cierre técnico

Se conserva el cierre V1 anterior; este ajuste no reabre la fase ni cambia reglas de negocio, servicios, esquema o almacenamiento.

- **Inicio compacto (ajuste original):** resumen con las cuatro métricas entonces existentes (total por cobrar, unidades disponibles, pedidos activos e inventario al costo), dos columnas móviles y CTA Venta directa. La composición vigente posterior se describe en la sección 15.
- **Ganancia total:** nuevo texto visible para Utilidad, dentro de una sola card con fechas, botón Consultar y resultado compacto. El cálculo sigue intacto. Se corrigió que editar fechas sin consultar reetiquetara el importe anterior: ahora el resultado conserva el rango de la consulta que lo produjo.
- **Controles responsive:** los grids implícitos de formularios/listas y los mínimos de las columnas podían propagar el ancho intrínseco de los controles. Se definieron tracks `minmax(0, 1fr)`, hijos encogibles y límites de ancho para inputs/selects/textarea; los ajustes WebKit de fecha conservan el selector nativo. No se oculta el overflow del body.
- **Nueva compra:** el encabezado de productos y Agregar producto se apilan en móvil; campos, líneas y botones respetan el contenedor. Se reutiliza el CSS compartido, sin modificar CompraService.
- **Alertas prematuras:** ClienteEdicion, ProductoEdicion y CategoriaEdicion pasaban `ErrorMessage="ErrorGuardado"` como literal Razor. Se corrigió a `ErrorMessage="@ErrorGuardado"`; las condiciones existentes de los formularios solo muestran errores reales. La advertencia amarilla cuando faltan categorías se conserva.
- **Código de producto (histórico):** en ese ajuste se mantuvo manual, obligatorio y único, y solo se aclararon placeholder y mensaje requerido. Esta decisión fue sustituida posteriormente por la sección 13.
- **Regresiones:** seis casos de formularios (render inicial sin alertas/validaciones prematuras, errores reales, reintentos y referencia manual) y dos de Ganancia (rango/importe estable y consulta pendiente sin duplicación). Se usa la infraestructura existente, SQLite aislada y renderizado Razor; no son pruebas visuales de CSS ni se añadió una librería de UI testing.

**Verificación del ajuste:** `dotnet build ResellManager.sln` correcto, 0 errores y 0 warnings; `dotnet test ResellManager.sln`: **272 aprobadas** (264 anteriores + 8 nuevas), 0 fallidas y 0 omitidas. `git diff --check` correcto. Los primeros intentos de build/test quedaron bloqueados por el ejecutable de una instancia local en ejecución; una vez liberado, ambos comandos normales finalizaron correctamente, sin cambiar la configuración de compilación ni relajar pruebas.

**Validación automática visual no disponible; requiere comprobación manual.** El navegador falló al inicializarse (`trusted Node process exited unexpectedly`); no se reintentó. Quedan pendientes escritorio y 390 × 844 CSS px en Inicio/Ganancia, Cliente nuevo, Producto nuevo y Nueva compra, incluyendo Safari/iPhone, ausencia de scroll horizontal y controles dentro de las cards. La revisión de código y las pruebas de render no sustituyen esa aceptación visual.

## 13. Decisiones posteriores a validación manual: pedidos y productos

- VentaDirecta se retiró del selector de Nuevo pedido: su pedido nace desde el flujo específico de Venta Directa. Importacion, Catalogo y Apartado comparten una lista permitida en Application, usada por el selector, el modelo y `PedidoService.CrearManualAsync`. Este contrato rechaza VentaDirecta e incluso valores de enum desconocidos sin persistir un pedido.
- `TipoPedido.VentaDirecta` sigue en dominio con el mismo valor. El flujo directo conserva CanalVenta.Presencial, pedido `PED-VD-`, venta `VEN-VD-` y reintentos existentes; no se cambió VentaService.
- `Producto.CodigoInterno` pasa a identificador técnico automático `PRO-<GUID>` (GUID N de 32 hexadecimales en mayúsculas). ProductoService lo genera una vez por nueva entidad mediante CodigosInternos, no desde la UI.
- ProductoInput elimina CodigoInterno tanto para crear como editar. El formulario compartido ya no lo solicita; ProductoDto, detalle, búsquedas y selectores lo conservan. Editar nunca reasigna ni normaliza el código, incluidos los históricos manuales.
- CodigoBarras continúa externo, manual y opcional. No se generaron códigos de barras, migraciones ni cambios de esquema; se conserva columna requerida e índice único de Producto.
- Regresiones: lista manual y enum persistido, rechazo directo en servicio sin depender de UI, tipos manuales admitidos, selector renderizado, formato y unicidad PRO, creación sin código, edición de nuevos e históricos sin alterar código, código de barras y búsquedas. Las pruebas antiguas de captura manual se adaptan a la nueva decisión, preservando validación, errores reales y reintentos. SQLite aislada por prueba; no se usan datos reales.
- Este ajuste no incluye cambios del Dashboard ni implementa el roadmap V2. `19_V2_Pendientes.md` se conserva íntegro. La aceptación visual sigue pendiente de comprobación manual; las pruebas de render no equivalen a validación en iPhone.

**Verificación de este ajuste (07/09/2026):** build correcto con 0 errores y 0 warnings; 286 pruebas .NET aprobadas (274 anteriores adaptadas donde cambió la decisión + 12 nuevas), 0 fallidas y 0 omitidas. También pasan las 8 pruebas JavaScript existentes de reconexión. `git diff --check` correcto. El primer build encontró DLL bloqueadas por la instancia local; se detuvo únicamente esa instancia y se repitieron los comandos normales con éxito. La reconciliación con origin conservó los 12 archivos de trabajo parcial mediante stash, fast-forward de los dos commits documentales y reaplicación sin conflictos; el roadmap V2 no se modificó.

## 14. Productividad posterior: productos dentro de Nueva compra

Nueva compra ya no carga el catálogo completo en selectores. ProductoBuscador reutiliza IProductoService.BuscarAsync por nombre, CodigoInterno y CodigoBarras: mínimo 2 caracteres, debounce de 300 ms, cancelación de búsquedas anteriores y hasta 12 resultados limitados en SQL. Las coincidencias exactas de código se ordenan primero; la comparación ignora mayúsculas dentro del soporte de LOWER de SQLite (no promete normalización de acentos). Las consultas normales sin límite conservan todos sus resultados.

Los resultados son botones accesibles mediante Tab/Enter, táctil y lector de pantalla, con etiqueta y estado anunciados. Escape oculta resultados. Cada búsqueda usa un scope independiente para no solapar consultas en el DbContext del circuito. No se añadió librería frontend ni lector/cámara.

ProductoAltaPanel reutiliza ProductoForm fuera del EditForm de compra, evitando formularios anidados. Crear llama a ProductoService; el código PRO- sigue siendo exclusivamente backend. El callback selecciona el nuevo ProductoDto en la línea original y conserva el modelo de compra, proveedor, fechas, detalles, cantidades, costos y comprobante. El resto del formulario queda temporalmente deshabilitado. No se promete recuperación después de recargar o perder el circuito.

Sin categorías se mantiene la advertencia; el enlace las abre en otra pestaña y permite actualizar la lista al volver, sin navegación que descarte la compra. No hay una cadena de altas anidadas. Las regresiones usan SQLite aislada y prueban búsquedas, límites/prioridad, debounce, selección y alta integrada preservando la compra.

Verificación técnica de Compra: build con 0 errores y 0 warnings; 294 pruebas .NET aprobadas (286 anteriores + 8 nuevas). Validación visual automática no disponible por la limitación previamente registrada; requiere comprobación manual, especialmente a 390 px y con teclado.

## 15. Productividad posterior: Dashboard y filtros contextuales

- El resumen conserva cuatro cards compactas: Total por cobrar, Unidades disponibles, Pendiente de entregar e Inventario al costo. PendientesEntrega se agrega al DTO y se calcula en DashboardService como COUNT de unidades Vendida. Comprada, EnTransito, Disponible y Entregada no cuentan. PedidosActivos permanece en DTO/servicio como Pendiente + Confirmado, sin renombrarlo ni eliminarlo.
- Deuda, inventario al costo y ganancia mantienen sus fórmulas. Total por cobrar enlaza a `/clientes?saldo=pendiente`, usando el saldo calculado por ClienteService y filtrando > 0. Buscar cliente sin filtro sigue en `/clientes`.
- `/pedidos?estado=activos` incluye Pendiente/Confirmado y excluye Cancelado/Completado. El listado ofrece un acceso visible a activos. Inventario procesa `estado=vendida` y `estado=disponible` reutilizando su servicio de búsqueda. Cada filtro puede quitarse y la navegación al mismo componente vuelve a aplicar los parámetros.
- Acciones rápidas reemplaza el CTA individual con Registrar abono (`/pagos`), Venta directa (`/ventas/nueva?modo=directa`), Registrar pedido (`/pedidos/nuevo`) y Buscar cliente (`/clientes`). Grid 2 × 2, una columna hasta 21rem; Venta directa conserva énfasis. Ganancia y sus fechas no se rediseñaron.
- Se agregan regresiones de saldo/estados con SQLite aislada y renderizado autenticado de rutas con query strings, además de las pruebas existentes de fórmulas y movimientos. No hay EF en Razor ni migraciones.
- `19_V2_Pendientes.md` permanece intacto; no se implementó cámara, lector, ecommerce ni otro alcance V2. La aceptación visual real queda pendiente: no se reintentó el navegador del entorno. Verificar manualmente Compra, Dashboard y filtros a 390 px y escritorio; las pruebas de render no demuestran apariencia visual.

Verificación final de ambos ajustes (08/09/2026): `dotnet build ResellManager.sln` con 0 errores y 0 warnings; `dotnet test ResellManager.sln` con 306 aprobadas, 0 fallidas y 0 omitidas (286 anteriores + 8 de Compra + 12 de Dashboard/filtros). Las 8 pruebas JavaScript existentes también pasan. `git diff --check` correcto. Entrega separada en commits de productividad de compras y de acciones/métricas/filtros del Dashboard, sin merge.

## 16. Ajustes finales derivados de validación manual

### Sincronización y alcance

Se partió de la rama `feature/ajustes-ux-movil-dashboard` limpia en `04e4421`. Tras fetch se incorporó por fast-forward `fdeea3e` (`docs: add business reports to V2 roadmap`), sin stash ni descarte de cambios. `19_V2_Pendientes.md` conserva íntegra la sección Informes del negocio en V2.2; no se duplicó ni implementó. Se mantienen PRO-, restricciones del pedido manual, flujo Venta Directa, cálculos, almacenamiento, seguridad y fixes responsive anteriores. No hay migración ni nuevo esquema.

### Compra y búsqueda contextual

- Proveedor se busca por nombre/teléfono usando `IProveedorService.BuscarAsync`, con 2 caracteres mínimos, debounce de 300 ms y hasta 12 resultados en SQL. No se carga la lista completa. Una coincidencia exacta de teléfono tiene prioridad.
- Si una búsqueda válida queda sin resultados, aparece Agregar proveedor. El panel reutiliza `ProveedorForm` (también usado por Nuevo proveedor) y llama a `CrearAsync`. El callback asigna el DTO/ProveedorId a la compra sin reemplazar su modelo. Se puede crear el primer proveedor sin salir de Compra.
- Producto mantiene su buscador/backend y alta con PRO- automático; se elimina el botón permanente de registro. Agregar producto aparece solo tras una búsqueda válida sin resultados, nunca mientras carga, ante error o con coincidencias. La referencia de la línea de origen sigue determinando la autoselección; otras líneas permanecen intactas.
- Los paneles de alta están fuera del EditForm de compra. Se conservan fechas, origen, observaciones, cantidades, costos, detalles y archivo seleccionado; el resto del formulario queda deshabilitado durante el alta. Sin categorías se conserva el enlace en otra pestaña y la posibilidad de actualizar categorías al volver.
- Total visual y Comprobante opcional quedan separados por 1.25rem (gap de 1rem más margen contextual de .25rem), sin alterar date inputs ni breakpoints.
- Revisar compra muestra proveedor, origen, fechas, productos, cantidades, costos unitarios, subtotales, total y nombre del comprobante opcional. Revisar no abre el archivo ni llama al registro. Editar conserva el mismo formulario y archivo; Confirmar usa `IRegistroCompraConComprobanteService.RegistrarAsync` y el flujo existente. Guardando y el diálogo bloquean confirmaciones simultáneas.

### Pagos, Clientes y Dashboard

- Pagos reemplaza el select masivo por búsqueda de nombre completo/teléfono con la misma pauta incremental. Cliente no tiene CodigoInterno: se confirmó utilizar sus campos existentes, sin agregar columnas. `IClienteService.BuscarAsync` limita opcionalmente antes de obtener saldos; consultas normales sin límite siguen completas.
- `?cliente=id` obtiene ese cliente por ID, sin listar todo el catálogo, y carga saldo/historial mediante los servicios existentes. IDs inválidos o inexistentes dejan un mensaje controlado y permiten buscar otro cliente. Seleccionar un resultado actualiza saldo/historial.
- Revisar abono muestra cliente, saldo actual, monto, saldo previsto (`SaldoActual - Monto`), fecha, método y referencia. Editar no registra; Confirmar llama a PagoService y recarga saldo/historial desde backend. La estimación visual no sustituye la validación de saldo.
- Clientes ofrece Todos / Con deuda; Saldo > 0 usa el valor entregado por ClienteService. La búsqueda se combina con el filtro y se conserva en `buscar` al alternar; quitar deuda no borra la búsqueda. Sin búsqueda, las rutas son `/clientes` y `/clientes?saldo=pendiente`.
- Las cuatro acciones rápidas conservan texto, rutas y grid; su altura mínima pasa a 5rem (80px con fuente base de 16px). No se modifican las cuatro cards ni Ganancia total.

### Confirmaciones de impacto

Decisiones formales 021–022 en `11_DecisionesDeDiseño.md`. `ConfirmacionOperacion` usa un diálogo nativo, contenido por RenderFragment, Editar/Cancelar, Confirmar, errores controlados, foco contenido/restaurado y bloqueo mientras envía. Permite scroll vertical y contenido largo con wrap. No contiene reglas de un módulo.

Además de Compra y Pago, se incorpora en Pedido manual (cliente, tipo/canal, detalles y total estimado) y Venta Directa (cliente, unidades/precios, total y saldo estimado; abonos por separado). Revisar no crea Pedido/Venta; Confirmar conserva servicios, códigos PED-VD-/VEN-VD-, Presencial y reintentos con pedido parcial.

Inventario: revisión de recepción de una o varias unidades con fecha y conservación de reservas; revisión de entrega Vendida → Entregada con advertencia de su efecto en cancelación. En tránsito continúa directo. Cancelaciones de venta, pedido y reserva conservan las confirmaciones previas, sin duplicarlas. Altas/ediciones simples de Cliente, Producto, Categoría y Proveedor no reciben pasos extra. Un maestro guardado inline existe aunque después se abandone la compra; no se ofrece rollback de ese alta.

### Evidencia y aceptación pendiente

Regresiones con SQLite aislada por prueba: búsqueda de proveedor/cliente, prioridad y límites, cero proveedores y alta/autoselección, estado/archivo de compra preservados, CTA de Producto solo sin coincidencias, debounce/cancelación, revisión sin persistencia y confirmación única, query de Pagos, saldo previsto/recarga, filtros combinados, Pedido/Venta Directa e Inventario. Se adaptó la expectativa antigua de Pagos para su nuevo estado inicial sin listado completo, conservando la cobertura de navegación y estado vacío. Las pruebas de JS cubren apertura única, Escape durante envío y restauración de foco; se conservan las de reconexión.

**Validación automática visual no disponible; requiere comprobación manual.** No se reintentó el navegador que fallaba en este entorno. Quedan por comprobar en iPhone (~390px) y escritorio: búsqueda/altas/autoselección de Compra y su espaciado, revisión/Editar/Confirmar, búsqueda y query de Pagos, Todos/Con deuda, altura de acciones, y los diálogos de Pedido, Venta Directa, recepción y entrega. Las pruebas de componentes/renderizado no acreditan apariencia visual, foco real ni ausencia de overflow en Safari.

Verificación técnica final (09/09/2026): `dotnet build ResellManager.sln` correcto, 0 errores y 0 warnings; `dotnet test ResellManager.sln`: **332 aprobadas** (306 anteriores, adaptadas donde cambia la UX, + 26 nuevas), 0 fallidas y 0 omitidas. JavaScript: **11 aprobadas** (8 de reconexión + 3 del diálogo). `git diff --check` y revisión del stage correctos. Se utilizaron únicamente SQLite en memoria/hosts temporales aislados del arnés; no se tocaron datos reales. Entrega en commits separados de confirmaciones, Compra, Pagos, filtros/acciones y documentación, sin merge.
