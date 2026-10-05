# Medidas y presentación de producto

**Estado al 05/10/2026: modelo, contratos, validación, persistencia y UI administrativa implementados; incluidos en el tag v1.1.0. El catálogo público actual muestra ml, gramos y Presentacion; equivalencias visuales adicionales pendientes.**

La fase técnica se implementó después de V1.0.1; existe `v1.1.0` (`832c172`, 27/09/2026). La presentación pública actual está en CatalogoPresentacion.cs y en los DTOs de detalle. La existencia del tag no certifica cuándo se desplegó.

Se conserva el diseño original y su validación. Los apartados de modelo/reglas describen requisitos ya implementados; la sección de equivalencias futuras sigue siendo una propuesta, no el formato público actual. La tienda con carrito/pedidos continúa planeada en [V2.4](19_V2_Pendientes.md#v24--canal-público--tienda-en-línea).

## Propósito

Describir el volumen o la masa de un producto y su presentación comercial mediante atributos específicos, sin mezclar medidas con descripción, talla u otros datos del producto. La captura administrativa podrá usar unidades cómodas, mientras que la persistencia conservará un único valor canónico por medida y la tienda calculará sus equivalencias al mostrarlo.

## Modelo propuesto

La fase técnica incorporó a `Producto` estas propiedades opcionales:

```csharp
decimal? ContenidoMl
decimal? PesoGramos
string? Presentacion
```

El diseño convivirá con los atributos actuales `CodigoInterno`, `CodigoBarras`, `Nombre`, `Descripcion`, `Marca`, `Modelo`, `Color`, `Talla`, `PrecioSugerido` y `Categoria` (relacionada mediante `CategoriaId`). No elimina ni redefine ninguno de ellos.

### ContenidoMl: volumen

- Opcional: `null` significa que no se ha informado volumen.
- Cuando tenga valor, deberá ser mayor que cero.
- La unidad canónica de almacenamiento será el mililitro.
- Litros y onzas fluidas no se almacenarán como segundos valores persistidos.

| Producto o medida | Valor canónico propuesto |
| --- | --- |
| Perfume de 100 ml | `ContenidoMl = 100` |
| Splash de 236 ml | `ContenidoMl = 236` |
| Botella de 1.5 L | `ContenidoMl = 1500` |

### PesoGramos: masa

- Opcional: `null` significa que no se ha informado masa.
- Cuando tenga valor, deberá ser mayor que cero.
- La unidad canónica de almacenamiento será el gramo.
- Kilogramos y libras no se almacenarán como valores duplicados.
- Referencia de conversión: **1 lb = 453.59237 g**.

| Medida | Valor canónico propuesto |
| --- | --- |
| 500 g | `PesoGramos = 500` |
| 1.5 kg | `PesoGramos = 1500` |
| 5 lb | `PesoGramos = 2267.96` |

### Presentacion: formato comercial

Será un texto corto opcional para describir formato comercial, empaque o agrupación, con **longitud máxima propuesta de 100 caracteres**.

Ejemplos válidos: `Pack x2`, `Set de 3 piezas`, `Caja x12`, `Frasco`, `Refill` y `Dúo`.

No sustituirá volumen, peso, talla, color, marca ni descripción. No será un campo genérico para guardar datos que ya tengan atributo propio: por ejemplo, el volumen de un frasco se registrará en `ContenidoMl`, no únicamente en `Presentacion`.

## Reglas de negocio propuestas

Un producto podrá tener **como máximo una de las dos medidas**:

| ContenidoMl | PesoGramos | Resultado |
| --- | --- | --- |
| Mayor que cero | `null` | Válido: solo volumen |
| `null` | Mayor que cero | Válido: solo masa |
| `null` | `null` | Válido: ninguna medida |
| Con valor | Con valor | Inválido: ambas medidas simultáneamente |

No es un XOR estricto: ambos campos pueden ser `null`. Cero y los valores negativos serán inválidos cuando se informe cualquiera de las medidas; no sustituyen a `null`.

`Presentacion` será independiente de esa exclusión y podrá acompañar cualquiera de las combinaciones válidas.

No se intentará convertir automáticamente **ml ↔ gramos**: volumen y masa son magnitudes diferentes y su conversión requiere conocer la densidad. El diseño no incorpora esa conversión.

## UI administrativa implementada

### Exclusión entre entradas

- Si se introduce volumen, la entrada de peso y su selector se deshabilitan.
- Si se introduce peso, la entrada de volumen y su selector se deshabilitan.
- Si se elimina el valor introducido, se vuelve a habilitar la alternativa.
- Dejar ambas medidas vacías es válido.
- Un valor inválido se señala durante la validación del formulario; el bloqueo visual no sustituye la validación del servidor.

### Unidades de entrada

La entrada de volumen ofrece selector **ml / L**. Antes de persistir, los litros se convierten a mililitros multiplicando por 1000.

| Entrada | Valor a persistir |
| --- | --- |
| 100 ml | `ContenidoMl = 100` |
| 2 L | `ContenidoMl = 2000` |

La entrada de peso ofrece selector **g / kg / lb**. Antes de persistir, los kilogramos se multiplican por 1000 y las libras por 453.59237. El resultado se redondea a dos decimales.

| Entrada | Valor a persistir |
| --- | --- |
| 500 g | `PesoGramos = 500` |
| 1.5 kg | `PesoGramos = 1500` |
| 5 lb | `PesoGramos = 2267.96` |

Estos selectores son una comodidad de entrada. No requieren añadir a la entidad `Producto` propiedades persistidas para la unidad elegida ni conservar valores duplicados. No se prevé entrada en onzas fluidas en este diseño; su equivalencia podrá calcularse para presentación pública.

## Validaciones de aplicación y persistencia

La implementación cubre los tres niveles previstos para la administración:

1. **UI administrativa (implementada):** entradas visibles para volumen, peso y presentación en alta y edición. Al informar una medida se deshabilitan la otra entrada y su selector; al vaciarla se rehabilitan. Los selectores ml/L y g/kg/lb son temporales. La carga para edición muestra ml o L y g o kg según el umbral de 1000.
2. **Servidor / casos de uso de Producto (implementado):** valida `ContenidoMl > 0` y `PesoGramos > 0` cuando tengan valor, rechaza ambos simultáneamente y limita `Presentacion` a 100 caracteres.
3. **Base de datos (implementado):** columnas opcionales y restricciones `CHECK` para positividad, exclusión de medidas y longitud de `Presentacion`.

Expresiones de los `CHECK` implementados para las medidas:

```sql
CHECK (ContenidoMl IS NULL OR PesoGramos IS NULL)
CHECK (ContenidoMl IS NULL OR ContenidoMl > 0)
CHECK (PesoGramos IS NULL OR PesoGramos > 0)
```

La primera expresión permite ambos valores nulos y prohíbe que ambos estén informados. Las otras dos exigen positividad únicamente cuando exista la medida.

La persistencia normalizada conservará mililitros o gramos y el texto opcional de presentación. No almacenará litros, kilogramos, libras ni onzas fluidas como equivalencias duplicadas.

La configuración EF declara `decimal(12,2)` para ambas medidas y la migración local añade columnas nullable y restricciones sin modificar datos existentes. SQLite no impone por sí mismo la escala declarada; el formulario convierte a la unidad canónica y redondea a dos decimales con `MidpointRounding.AwayFromZero` antes de enviar el contrato. La validación del servidor y las restricciones de la base de datos siguen siendo la autoridad. La migración se probó en una base de datos de prueba, pero no se aplicó a producción.

## Presentación futura en tienda virtual

La tienda calculará las conversiones desde `ContenidoMl` o `PesoGramos`. Los textos formateados y las equivalencias no se almacenarán duplicados en Producto.

### Volumen

La medida métrica principal se mostrará así:

- Menos de 1000 ml: mililitros.
- A partir de 1000 ml, incluido ese valor: litros.

Como equivalencia secundaria podrá mostrarse **US fl oz**, con la referencia aproximada **1 US fl oz ≈ 29.5735 ml**. La etiqueta deberá decir `fl oz`, no simplemente `oz`, porque representa volumen.

Ejemplos conceptuales:

| Valor canónico | Presentación posible |
| --- | --- |
| 100 ml | 100 ml / 3.4 fl oz |
| 1500 ml | 1.5 L / 50.7 fl oz |

### Peso

La medida métrica principal se mostrará así:

- Menos de 1000 g: gramos.
- A partir de 1000 g, incluido ese valor: kilogramos.

Como equivalencia secundaria podrá mostrarse `lb`, calculada desde gramos con la referencia de 453.59237 g por libra.

Ejemplos conceptuales:

| Valor canónico | Presentación posible |
| --- | --- |
| 454 g | 454 g / 1 lb aproximadamente |
| 1500 g | 1.5 kg / 3.31 lb |
| 2267.96 g | 2.27 kg / 5 lb |

Los ejemplos son orientativos: **las reglas exactas de redondeo y formato visual quedan pendientes de definición durante la implementación de la tienda**. El redondeo de presentación no deberá sustituir el valor canónico persistido.

La tienda podrá mostrar `Presentacion` como información de formato comercial junto a la medida disponible y los demás atributos del producto. Su incorporación pública seguirá el alcance futuro de la tienda.

## Ejemplos completos del modelo propuesto

```text
Perfume:
ContenidoMl = 100
PesoGramos = null
Presentacion = "Frasco"

Proteína:
ContenidoMl = null
PesoGramos = 2267.96
Presentacion = "Bolsa"

Camisa:
ContenidoMl = null
PesoGramos = null
Presentacion = null

Set:
ContenidoMl = null
PesoGramos = null
Presentacion = "Set de 3 piezas"
```

## Decisiones todavía abiertas y actualización posterior

- Versión de entrega identificada en Git: tag v1.1.0; la versión desplegada requiere evidencia operativa.
- Definir el redondeo y formato visual exactos de las medidas y equivalencias en la futura tienda.

La documentación del modelo implementado, requisitos/reglas vigentes, DER, diagrama de clases y changelog se actualizarán cuando corresponda a la implementación y liberación real. Este diseño no presenta los campos como disponibles en V1.0.1.

La entrada de planificación se encuentra en [ROADMAP](../ROADMAP.md). La mejora administrativa está etiquetada en v1.1.0; no se incorpora artificialmente al alcance de V2. El catálogo de lectura ya existe y la tienda ampliada permanece planificada en V2.4.
