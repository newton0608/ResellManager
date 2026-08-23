# Backlog

## Backend V1 completado

- [x] Separar reserva comercial del estado físico de inventario.
- [x] Exigir correspondencia exacta entre cantidades de pedido y venta.
- [x] Registrar ventas completas sin adición posterior de artículos.
- [x] Proteger cancelaciones frente a pagos que producirían saldo negativo.
- [x] Endurecer transiciones físicas y recepción de mercancía.

## Fase 5.1: autenticación

- [x] Implementar login y logout con ASP.NET Core Identity.
- [x] Proteger globalmente las páginas de negocio.
- [x] Mantener el login público y eliminar el flujo de autorregistro.
- [x] Crear el usuario inicial mediante configuración segura opcional.
- [x] Cubrir autenticación y sesión con pruebas de integración.
- [ ] Incorporar roles y permisos si el negocio evoluciona a una tienda física con trabajadores.
- [ ] Incorporar administración de usuarios desde la aplicación cuando existan varias cuentas que deban crearse, deshabilitarse, restablecerse o asignarse a roles.

## Endurecimiento de concurrencia después de completar la UI

- [ ] Revisar todas las operaciones que afectan el saldo global de un cliente como sección crítica lógica: registrar venta, registrar pago/abono y cancelar venta.
- [ ] Implementar exclusión mutua para impedir que dos operaciones incompatibles sobre el saldo del mismo cliente se ejecuten simultáneamente.
- [ ] Añadir una segunda capa de seguridad a nivel de persistencia/transacción o control de concurrencia, además de la coordinación a nivel de aplicación.
- [ ] La solución no debe depender de suposiciones sobre la velocidad relativa, orden de ejecución o temporización de tareas/procesos asíncronos.
- [ ] Garantizar progreso: procesos u operaciones que no participan en la sección crítica de un cliente no deben bloquear indefinidamente a los que sí desean entrar.
- [ ] Garantizar espera acotada: ninguna operación que solicite acceso a la sección crítica debe poder ser postergada indefinidamente.
- [ ] Definir el alcance del bloqueo por cliente, evitando un bloqueo global del sistema cuando dos clientes distintos puedan procesarse de manera independiente.
- [ ] Agregar pruebas de concurrencia para pagos, ventas y cancelaciones simultáneas sobre el mismo cliente.
- [ ] Revisar si existen otras secciones críticas además del saldo una vez que la UI y los flujos reales estén completos.

## Catálogo: información pendiente del negocio

- [ ] Confirmar porcentajes de comisión por categoría para cada proveedor de catálogo.
- [ ] Confirmar la base exacta o fórmula que utiliza cada proveedor para calcular la comisión.
- [ ] Diseñar configuración de comisiones por proveedor y categoría.
- [ ] Al automatizar comisiones, guardar snapshot del porcentaje y/o monto aplicado en cada venta para preservar historial.

## Flujos futuros

- [ ] Diseñar devoluciones y cambios para unidades entregadas.

---

## Clientes

- [ ] Registrar cliente

- [ ] Editar cliente

- [ ] Eliminar cliente

- [ ] Buscar cliente

- [ ] Ver historial

- [ ] Consultar saldo

---

## Producto

- [ ] CRUD Producto

---

## UnidadInventario

- [ ] Registrar UnidadInventario

- [ ] Editar UnidadInventario

- [ ] Buscar UnidadInventario

- [ ] Cambiar estado

---

## Ventas

- [ ] Registrar venta

- [ ] Registrar apartado

- [ ] Cancelar apartado

---

## Pagos

- [ ] Registrar abono

- [ ] Registrar pago contado

- [ ] Historial de pagos

---

## Dashboard

- [ ] Total adeudado

- [ ] Inventario

- [ ] Productos apartados

- [ ] Últimos pagos

## Compras

- [ ] Registrar Comprobante de compra

- [ ] Registrar proveedor

- [ ] Adjuntar Fotografía de comprobante

- [ ] Consultar Comprobante de compra

- [ ] Ver historial de compras

## Categorías
- [ ] Crear categoría

- [ ] Editar categoría

- [ ] Listar categorías
