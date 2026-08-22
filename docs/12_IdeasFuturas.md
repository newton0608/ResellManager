# Ideas Futuras

## Compartir directamente a WhatsApp.
## Notificaciones.
## Dashboard avanzado.
## Estadísticas.
## Código de barras.
## Sincronización offline.
## Modo oscuro.
## Multiusuario.

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
