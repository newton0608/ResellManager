# Medidas y presentación de producto

**Estado: diseño aprobado, pendiente de implementación.**

Se prevé implementar esta mejora inmediatamente después del cierre documental de V1.0.1, como una entrega funcional tipo **V1.1.0**. La versión exacta de publicación queda por confirmar. Los campos, validaciones, controles de UI y restricciones de persistencia descritos aquí **todavía no existen en la implementación actual**.

Este documento registra el diseño futuro. No modifica el modelo vigente de V1.0.1 ni acredita una funcionalidad liberada. La presentación pública se aplicará cuando se implemente la tienda virtual, prevista en [V2.4](19_V2_Pendientes.md#v24--canal-público--tienda-en-línea); no adelanta esa tienda a V1.1.

## Propósito

Describir el volumen o la masa de un producto y su presentación comercial mediante atributos específicos, sin mezclar medidas con descripción, talla u otros datos del producto. La captura administrativa podrá usar unidades cómodas, mientras que la persistencia conservará un único valor canónico por medida y la tienda calculará sus equivalencias al mostrarlo.

## Modelo propuesto

Se incorporarán a `Producto` estas propiedades opcionales:

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
| 5 lb | `PesoGramos = 2267.96185` |

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

## UI administrativa prevista

### Exclusión entre entradas

- Si la usuaria introduce volumen, la entrada de peso se bloqueará o deshabilitará.
- Si introduce peso, la entrada de volumen se bloqueará o deshabilitará.
- Si elimina el valor introducido, se volverá a habilitar la alternativa.
- Dejar ambas medidas vacías será válido.
- Un valor inválido deberá señalarse; el bloqueo visual no sustituirá la validación del servidor.

### Unidades de entrada

La entrada de volumen ofrecerá selector **ml / L**. Antes de persistir, los litros se convertirán a mililitros multiplicando por 1000.

| Entrada | Valor a persistir |
| --- | --- |
| 100 ml | `ContenidoMl = 100` |
| 2 L | `ContenidoMl = 2000` |

La entrada de peso ofrecerá selector **g / kg / lb**. Antes de persistir, los kilogramos se multiplicarán por 1000 y las libras por 453.59237.

| Entrada | Valor a persistir |
| --- | --- |
| 500 g | `PesoGramos = 500` |
| 1.5 kg | `PesoGramos = 1500` |
| 5 lb | `PesoGramos = 2267.96185` |

Estos selectores serán una comodidad de entrada. No requieren añadir a la entidad `Producto` propiedades persistidas para la unidad elegida ni conservar valores duplicados. No se prevé entrada en onzas fluidas en este diseño; su equivalencia podrá calcularse para presentación pública.

## Validaciones de aplicación y persistencia previstas

La implementación deberá aplicar reglas equivalentes en tres niveles:

1. **UI administrativa:** exclusión entre entradas, positividad y longitud propuesta de presentación.
2. **Servidor / casos de uso de Producto:** validar `ContenidoMl > 0` cuando tenga valor, `PesoGramos > 0` cuando tenga valor y rechazar siempre ambos simultáneamente, incluso si se omiten los controles visuales. Validar también el máximo propuesto de 100 caracteres de `Presentacion`.
3. **Base de datos:** planificar columnas opcionales y restricciones `CHECK` equivalentes para las medidas.

Expresiones conceptuales de los `CHECK` previstos, aún no implementados:

```sql
CHECK (ContenidoMl IS NULL OR PesoGramos IS NULL)
CHECK (ContenidoMl IS NULL OR ContenidoMl > 0)
CHECK (PesoGramos IS NULL OR PesoGramos > 0)
```

La primera expresión permite ambos valores nulos y prohíbe que ambos estén informados. Las otras dos exigen positividad únicamente cuando exista la medida.

La persistencia normalizada conservará mililitros o gramos y el texto opcional de presentación. No almacenará litros, kilogramos, libras ni onzas fluidas como equivalencias duplicadas.

La configuración EF, los tipos y precisión/escala efectivos en SQLite y la migración correspondiente se definirán y comprobarán durante la implementación. Deberán permitir los valores canónicos del diseño, incluido `2267.96185` g; este documento no decide una política adicional de redondeo para almacenamiento. **Todavía no se crea ni ejecuta ninguna migración ni se altera el esquema o los datos.**

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
PesoGramos = 2267.96185
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

- Confirmar la versión exacta de entrega; la previsión es una mejora funcional tipo V1.1.0 tras el cierre documental de V1.0.1.
- Concretar la precisión/escala y representación de persistencia compatibles con SQLite y las conversiones canónicas descritas.
- Definir el redondeo y formato visual exactos de las medidas y equivalencias en la futura tienda.

La documentación del modelo implementado, requisitos/reglas vigentes, DER, diagrama de clases y changelog se actualizarán cuando corresponda a la implementación y liberación real. Este diseño no presenta los campos como disponibles en V1.0.1.

La entrada de planificación se encuentra en [ROADMAP](../ROADMAP.md). La mejora administrativa se prevé para V1.1; no se incorpora artificialmente al alcance de V2. La tienda básica permanece planificada en V2.4.
