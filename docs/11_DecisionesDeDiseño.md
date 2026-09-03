# Decisiones

## 001 La aplicación será una Web App.

Debe funcionar desde iPhone, Android y computadora sin desarrollar aplicaciones nativas.

Alternativas consideradas:
- MAUI
- Flutter
- Aplicación iOS

Resultado:
ASP.NET Core + Blazor

## 002 SQLite como base de datos inicial

Es sencilla, ligera y suficiente para la primera versión.

En el futuro:
Migrar a PostgreSQL si el proyecto crece

## 003 El sistema debe permitir múltiples orígenes de abastecimiento.

- Importación EE.UU.
- Compra local.
- Catálogo (Ágora / Andrea)
- Envíos del hijo

## 004 No desarrollar una aplicación móvil nativa.

Motivo:
Una Web App cubre todos los dispositivos.

Resultado:
Blazor.

## 005 Las ventas por catálogo no generan inventario.

Motivo:
En el flujo vigente del negocio, Catálogo se gestiona como pedido, venta y deuda/pago sin que el producto pase por inventario físico.

Resultado:
Una venta de catálogo registra `DetalleVenta` con Producto, `CostoUnitario` y `PrecioFinal`, sin `UnidadInventario`.

Restricción:
Solo los pedidos de tipo Catálogo generan ventas sin `UnidadInventario`; no existe una opción para convertir ese flujo en inventario físico.

## 006 Una compra de catálogo nunca genera unidades de inventario.

Motivo:
No tiene sentido crear UnidadInventario cuando el producto de catálogo no pasa físicamente por el inventario del negocio.

Resultado:
Una compra de catálogo registra `DetalleCompra` y nunca genera `UnidadInventario`. Esta regla es absoluta en el modelo actual y no admite recepción física ni opciones de UI que la alteren.

## 007 La recepción de mercancía será un caso de uso explícito.

Motivo:
La fecha de compra y la fecha real de ingreso al inventario pueden ser distintas, especialmente en importaciones.

Resultado:
Se debe implementar un caso de uso para registrar recepción de mercancía y mover unidades compradas o en tránsito a disponibles.

## 008 Las ventas canceladas conservan historial.

Motivo:
El sistema debe conservar trazabilidad de lo que ocurrió, pero también permitir revender una unidad si la venta fue cancelada antes de la entrega.

Resultado:
Si una venta se cancela antes de entregar, sus unidades pueden volver a Disponible y los DetalleVenta quedan como historial. Si ya existen unidades Entregadas, la cancelación simple no debe permitirse y debe manejarse con un flujo futuro de devolución o cambio.

## 009 La reserva comercial no es un estado físico

Motivo:
Una unidad puede estar `EnTransito` o `Disponible` y, al mismo tiempo, reservada para un cliente. Un único enum no puede representar ambos conceptos sin perder información.

Resultado:
`EstadoUnidadInventario` conserva solo el ciclo físico y `UnidadInventario.DetallePedidoReservaId` identifica opcionalmente la reserva activa. No se crea una entidad `Reserva` independiente en V1. Los pedidos `Catalogo` no reservan inventario físico; cualquier otro tipo de pedido puede reservarlo cuando el flujo lo requiera.

## 010 Una venta se registra completa e inmutable en sus artículos

Motivo:
Agregar artículos después del registro rompe la correspondencia con el pedido y dificulta proteger inventario, saldo e historial.

Resultado:
Las cantidades vendidas deben coincidir exactamente por `ProductoId` con el pedido. Solo entonces el pedido pasa a `Completado`. Una venta registrada no permite agregar detalles; una compra posterior genera otro pedido y otra venta.

## 011 Cancelar una venta no puede producir saldo negativo

Motivo:
Los pagos pertenecen globalmente al cliente. Excluir una venta ya pagada puede dejar más pagos que deuda registrada.

Resultado:
Antes de cancelar se calcula el saldo sin esa venta. Si resulta negativo, se rechaza la operación hasta ajustar o devolver pagos. Las unidades entregadas continúan requiriendo devolución/cambio futuro.

## 012 Precio sugerido y precio real permanecen separados

Motivo:
El negocio necesita una referencia de captura sin restringir negociaciones ni distorsionar utilidad.

Resultado:
`Producto.PrecioSugerido` es referencia/default y debe ser no negativo. `DetalleVenta.PrecioFinal` es el precio real y puede diferir. La utilidad usa el costo transaccional y `PrecioFinal`.

## 013 Devoluciones y cambios quedan fuera de esta fase

Resultado:
Devoluciones y cambios se diseñarán como casos de uso futuros; no se habilitan mediante cambios arbitrarios de estado ni mediante UI.

## 014 Las comisiones de catálogo deben ser configurables y conservar historial

Motivo:
Los proveedores de catálogo muestran directamente el precio final al cliente y dentro de ese importe ya se encuentra incluida la ganancia de la vendedora. El porcentaje de comisión cambia según el tipo o categoría del producto; se recuerdan al menos grupos diferenciados para ropa y calzado, electrodomésticos y productos de limpieza. Los porcentajes exactos y la fórmula/base usada por cada proveedor todavía no están confirmados.

Resultado:
No se codifican porcentajes fijos mientras no exista información confirmada. La automatización futura debe permitir configurar reglas de comisión por proveedor y categoría. Cuando se implemente, cada venta de catálogo deberá conservar como snapshot el porcentaje y/o monto aplicado en ese momento, para que cambios posteriores de configuración no alteren la utilidad histórica.

Mientras las reglas no estén confirmadas, las ventas de catálogo seguirán registrando explícitamente `CostoUnitario` y `PrecioFinal`, que permiten llevar deuda y utilidad sin depender de porcentajes asumidos.

## 015 El saldo requiere endurecimiento de concurrencia después de completar la UI

Motivo:
El saldo de un cliente se calcula a partir de ventas registradas y pagos globales. Operaciones como registrar una venta, registrar un pago/abono o cancelar una venta pueden competir sobre el mismo estado lógico si se ejecutan al mismo tiempo. La validación secuencial actual es suficiente para la etapa de desarrollo y uso inicial, pero no debe asumirse que las tareas asíncronas se ejecutarán en un orden o velocidad determinados.

Resultado:
Una vez completada la UI y validados los flujos reales, se realizará una fase específica de endurecimiento de concurrencia. La sección crítica no se define como la lectura del número de saldo en sí, sino como el conjunto de operaciones que leen y modifican datos que determinan el saldo de un mismo cliente.

La solución deberá cumplir:
- exclusión mutua para operaciones incompatibles sobre el mismo cliente;
- independencia respecto a la velocidad relativa, orden o temporización de procesos/tareas asíncronas;
- progreso: procesos ajenos a la sección crítica no deben impedir innecesariamente que otro proceso avance;
- espera acotada: una operación no puede ser postergada indefinidamente para entrar a su sección crítica;
- granularidad por cliente siempre que sea posible, evitando bloquear globalmente operaciones de clientes distintos.

Se aplicará doble protección: una capa de coordinación a nivel de aplicación y una segunda garantía a nivel de persistencia/transacción o mecanismo de concurrencia apropiado para la base de datos utilizada. La implementación concreta se decidirá cuando estén terminados los flujos de UI y antes de considerar el sistema listo para escenarios con concurrencia real.

## 016 La aplicación es privada y usa ASP.NET Core Identity

Motivo:
ResellManager administra información familiar y no necesita adquisición pública de usuarios.

Resultado:
La Fase 5.1 implementa login, logout, cookies seguras y protección de rutas con ASP.NET Core Identity. La autorización actual exige únicamente un usuario autenticado. No se exponen `MapIdentityApi` ni endpoints o páginas de registro anónimo.

### Roles y permisos futuros

Mientras ResellManager sea utilizado únicamente por la propietaria del negocio o por personas de confianza con el mismo nivel de acceso, no se necesita separar permisos por rol.

Si en el futuro el negocio evoluciona a una tienda física con trabajadores, sí se deberá incorporar autorización por roles o permisos. Ejemplos posibles:

- `Administrador` o `Propietaria`: acceso completo, configuración, reportes, usuarios y operaciones sensibles.
- `Vendedor`: registrar ventas, consultar clientes e inventario y registrar cobros según las reglas permitidas.
- `Bodega` o `Inventario`: registrar recepción, consultar inventario y actualizar operaciones físicas autorizadas, sin acceso necesariamente a información financiera completa.

Los nombres y permisos exactos no se consideran definidos todavía; deberán diseñarse a partir de las responsabilidades reales de los trabajadores. Las tablas de roles de ASP.NET Core Identity se conservan para permitir esa evolución sin reemplazar el sistema de autenticación.

### Administración de usuarios futura

"Administración de usuarios" significa disponer de una pantalla o módulo privado para que una cuenta autorizada pueda gestionar las cuentas que entran a ResellManager. Sería útil cuando existan varios trabajadores y ya no sea práctico crear cuentas manualmente mediante configuración.

Ese módulo podría permitir:

- crear una cuenta para un trabajador;
- deshabilitar o bloquear una cuenta cuando una persona deje de trabajar en el negocio;
- restablecer o cambiar credenciales de acceso;
- asignar o retirar roles;
- consultar qué cuentas existen y si están activas.

No significa administrar `Cliente`; se refiere exclusivamente a las cuentas de acceso al sistema. En V1 no es necesario porque existe un usuario inicial configurable y no hay autorregistro público.

### Usuario inicial

El seed es opcional e idempotente: crea una cuenta solo cuando no existe y están configurados tanto el correo como la contraseña. Nunca reemplaza la contraseña de una cuenta existente. Si falta cualquier credencial, la aplicación inicia normalmente sin crear usuarios.

En desarrollo se configura con User Secrets desde la raíz del repositorio:

```powershell
dotnet user-secrets set "UsuarioInicial:Correo" "administrador@ejemplo.com" --project src/ResellManager.Web
dotnet user-secrets set "UsuarioInicial:Contrasena" "<contraseña-segura>" --project src/ResellManager.Web
```

En despliegue se pueden usar variables de entorno:

```text
UsuarioInicial__Correo=administrador@ejemplo.com
UsuarioInicial__Contrasena=<contraseña-segura>
```

La contraseña debe tener al menos 12 caracteres e incluir mayúscula, minúscula, número y carácter no alfanumérico. Identity almacena únicamente su hash. Una vez creada la primera cuenta, las credenciales de seed deben retirarse de la configuración del entorno.

## 017 Los comprobantes se almacenan como archivos privados administrados

Motivo:
Los comprobantes contienen información privada, no pertenecen a `wwwroot` y guardar sus binarios como BLOB inflaría SQLite. El sistema de archivos y SQLite tampoco comparten una transacción ACID.

Resultado:
Los archivos se validan detrás de `IAlmacenamientoComprobantes`, se preparan en una carpeta temporal y se confirman con un nombre `CMP-<GUID>` antes de invocar a `CompraService`. La base conserva únicamente una ruta relativa `comprobantes/CMP-...`; la lectura se realiza por `/comprobantes/{compraId}`, que exige autenticación y resuelve el archivo mediante el servicio.

La confirmación se realiza antes del guardado de la compra para impedir que SQLite quede apuntando a un archivo inexistente. Si `CompraService` devuelve un fallo, se elimina el archivo confirmado. Si ocurre una excepción ambigua, se busca la compra por su código único usando el contrato existente: si quedó persistida se conserva el archivo y se recupera el resultado; si no existe se elimina. Si no es posible verificar el estado, se conserva el archivo y se registra un evento crítico, priorizando no romper una referencia que pudiera haber quedado confirmada.

Las imágenes se re-encodean mediante SkiaSharp 4.151.1, con lado máximo de 1800 px y calidad 85 para formatos con pérdida. Los PDF se conservan sin transformación. SkiaSharp y sus activos Linux usan licencia MIT; el despliegue Linux incorpora `SkiaSharp.NativeAssets.Linux.NoDependencies`.
