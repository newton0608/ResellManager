# Modelo Del Negocio

## Procesos

- Publicación
- Abastecimiento
- Venta
- Cobro
- Entrega

## Modalidades de compra

- Compra para inventario.
- Compra para un cliente.

## Orígenes de abastecimiento

- Importación desde EE.UU.
- Compra local.
- Catálogo.
- Envío del hijo.

La etiqueta comercial «Envío del hijo» corresponde al identificador persistido `OrigenCompra.EnvioHermano`; no son dos orígenes distintos.

## Tipos de venta

- Contado.
- Crédito.
- Apartado.
- Catálogo.

## Formas de obtener productos

- Compra para inventario.
- Compra para cliente.
- Compra local.
- Recepción desde hijo.

## Estados físicos de UnidadInventario

- Comprada
- En tránsito
- Disponible
- Vendida
- Entregada
- Perdida

`Perdida` representa una pérdida previa a recepción: solo se admite desde `Comprada` o `EnTransito` y es irreversible en V1. Libera cualquier reserva, conserva el registro y excluye la unidad de recepción, reserva y venta; no equivale a mercancía recibida. Véanse las [reglas del negocio](08_ReglasDelNegocio.md#unidadinventario).

El apartado es una asociación comercial con DetallePedido y no un estado físico.


## Actores
- Vendedora
- Cliente
- Hijo
- Proveedor de catálogo

## Orígenes
- EE.UU.
- Compra local.
- Catálogo.
- Envío del Hijo.

## Categorías
- Es un catálogo configurable por el usuario.

## Publicación de productos
- Un producto publicado puede ser adquirido para un cliente específico.
- Un producto publicado puede ser adquirido para inventario.
- Un producto publicado puede no llegar a comprarse.