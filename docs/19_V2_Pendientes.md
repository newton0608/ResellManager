# V2 — Pendientes y mejoras futuras

Este documento reúne decisiones funcionales que quedan explícitamente fuera de V1 y se consideran para una V2 posterior.

## Lectura de códigos de barras con la cámara

La lectura de códigos de barras mediante la cámara del dispositivo queda planificada para **V2**. No forma parte del alcance de V1.

### Objetivo

Permitir que la usuaria pueda escanear `Producto.CodigoBarras` desde un teléfono o dispositivo con cámara para localizar productos rápidamente y reducir la captura manual durante la operación diaria.

### Alcance previsto

- Usar la cámara del dispositivo para leer códigos de barras compatibles.
- Buscar el producto mediante `Producto.CodigoBarras` después de una lectura exitosa.
- Integrar el escaneo principalmente con **Venta Directa** e **Inventario**.
- Evaluar su uso también en búsquedas y selección de productos donde aporte velocidad real al flujo.
- Mantener siempre la búsqueda/captura manual como alternativa cuando no haya cámara, no exista permiso o el código no pueda leerse.
- Mostrar errores claros cuando el código leído no corresponda a ningún producto registrado.

### Regla de datos

`Producto.CodigoBarras` continúa siendo una referencia externa del producto. El lector no genera ni modifica códigos de barras: únicamente captura mediante cámara un valor existente para utilizarlo en búsquedas y selección.

El comportamiento de `Producto.CodigoInterno` es independiente de esta funcionalidad y debe seguir la decisión vigente documentada para los códigos internos del sistema.

### Consideraciones técnicas para V2

Antes de implementarlo se deberá evaluar:

- compatibilidad de cámara en navegadores móviles;
- permisos y experiencia cuando el usuario deniega acceso a la cámara;
- soporte real de formatos de códigos utilizados por la mercancía del negocio;
- comportamiento en conexiones lentas o inestables;
- rendimiento del lector en dispositivos móviles;
- seguridad y privacidad: la cámara solo debe activarse por acción explícita de la usuaria;
- una solución web compatible con la arquitectura Blazor existente, evitando dependencias innecesarias.

### Criterio de experiencia esperado

Flujo objetivo para Venta Directa en V2:

1. La usuaria abre **Venta Directa**.
2. Selecciona **Escanear código**.
3. La aplicación solicita/usa la cámara.
4. Se lee `Producto.CodigoBarras`.
5. Se localiza el producto correspondiente.
6. La usuaria confirma o agrega el producto a la operación.
7. Si no puede escanearse, puede buscarse manualmente sin bloquear la venta.

Esta mejora debe acelerar la operación, no convertir el escaneo en requisito obligatorio para vender o consultar inventario.
