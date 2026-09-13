# V3 — Candidatos pendientes

Documento de planificación; ninguna de estas funciones está implementada en V1.

## Conteo físico / toma de inventario

Candidato principal para V3: contrastar existencias físicas con el inventario esperado.

- Iniciar una sesión de conteo con un snapshot lógico del inventario esperado.
- Buscar o escanear unidades y marcarlas como encontradas; puede aprovechar el scanner previsto en V2.
- Mostrar esperadas frente a encontradas y detectar faltantes/sobrantes.
- Admitir conteos totales y parciales/cíclicos.
- Revisar diferencias antes de modificar inventario, considerando movimientos ocurridos durante el conteo.
- Exigir confirmación y motivo para cualquier ajuste.
- Mantener trazabilidad y auditoría del conteo, revisiones y ajustes.

Nunca modificar inventario automáticamente solo porque el conteo difiera. El conteo detecta diferencias; su resolución es una operación explícita y revisada.
