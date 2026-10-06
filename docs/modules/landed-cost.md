# Landed cost / costo puesto en Guatemala

**Estado: análisis pendiente de levantamiento y definición (05/10/2026).**
No hay modelo definitivo ni contrato aprobado de implementación.

## Punto de partida real

El flujo de importación ya existe: compra genera unidades Comprada, tránsito
opcional y recepción posterior. [Compras](compras.md) enlaza ese flujo y
[monedas de compra](../28_MonedasDeCompra.md) define la conversión GTQ/USD actual.
Esa conversión **excluye** flete, courier, impuestos/aranceles, prorrateo y costo
puesto en Guatemala. No debe presentarse como landed cost implementado.

## Información que falta definir

En el levantamiento habrá que confirmar con el negocio qué gastos se registran,
cuándo se conocen, cómo se asignan a artículos/unidades y cómo se preserva el
historial cuando ya hay recepción o ventas. Estos son temas para discutir,
no reglas, entidades, fórmulas ni requisitos aprobados.

No se fija una versión de entrega, algoritmo de reparto, esquema, migración o
recalculo de históricos. Cuando se acuerde el alcance, actualizar este documento
con estados explícitos y criterios de aceptación antes de pedir implementación.
