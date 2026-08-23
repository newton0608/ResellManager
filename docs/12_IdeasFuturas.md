# Ideas Futuras

## Compartir directamente a WhatsApp.
## Notificaciones.
## Dashboard avanzado.
## Estadísticas.
## Código de barras.
## Sincronización offline.
## Modo oscuro.
## Multiusuario.

## Verificación de correo para escenarios multiusuario

En la versión actual no se requiere confirmación de correo por enlace, porque ResellManager es una aplicación privada de administración y las cuentas serán creadas de forma controlada por el propio negocio. En un escenario con trabajadores, lo normal sería que la empresa proporcione o gestione directamente las cuentas utilizadas para acceder al sistema, en lugar de depender de correos personales.

Si en el futuro ResellManager evoluciona para usarse en varios locales, negocios o por un número mayor de usuarios administrados de forma más independiente, considerar habilitar confirmación de correo mediante ASP.NET Core Identity.

Posible flujo futuro:

- Crear la cuenta con `EmailConfirmed = false`.
- Generar un token de confirmación de correo.
- Enviar un enlace de confirmación al usuario.
- Marcar el correo como confirmado únicamente cuando se valide el token.
- Mantener esta verificación como opcional/configurable según el tipo de despliegue y las políticas del negocio.

No es un requisito de la V1 ni del escenario actual de uso privado/familiar.

## Endurecimiento de concurrencia del saldo

Después de completar la UI, revisar las operaciones que determinan el saldo global de cada cliente como una sección crítica lógica.

Operaciones inicialmente identificadas:

- Registrar venta.
- Registrar pago o abono.
- Cancelar venta.

Objetivos de concurrencia:

- Solo una operación incompatible sobre el saldo del mismo cliente debe ejecutar su sección crítica a la vez.
- No depender de suposiciones sobre la velocidad relativa, orden o temporización de procesos/tareas asíncronas.
- Procesos ajenos a la sección crítica no deben impedir el progreso de los procesos que desean entrar.
- Ningún proceso debe ser postergado indefinidamente para entrar a su propia sección crítica.
- Evitar un bloqueo global: operaciones sobre clientes distintos deberían poder avanzar de forma independiente cuando sea seguro.
- Aplicar doble seguridad: coordinación a nivel de aplicación más una garantía de persistencia/transacción o control de concurrencia en base de datos.
- Agregar pruebas específicas de concurrencia y revisar si la UI revela otras secciones críticas además del saldo.

## Asistente IA local mediante Ollama.

Detectar automáticamente:

- Marca.

- Tipo.

- Color.

- Categoría.

- Generar descripción.

- Sugerir precio.

- Crear publicación.

Fotografía de comprobante

↓

Ollama

↓

Lee productos

↓

Llena automáticamente

Proveedor

Fecha

Productos

Costos

## Kardex

Registrar el movimiento del inventario (MovimientoInventario)

Pj:

Compra

+5 Splash

Venta
-1 Splash

Cambio

-1 Zapato

+1 Zapato

## V2

- Gestión de envíos (Cargo Expreso u otros).
- Registro de número de guía.
- Estado del envío.
- Facturación al cliente (si el negocio lo requiere).
