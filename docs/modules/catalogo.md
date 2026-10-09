# Catálogo público de Virtuosa Store

**Estado: implementado como catálogo de lectura.** Comparte productos/inventario
con la administración. No hay carrito, checkout, pedidos web ni pagos online;
la consulta por WhatsApp abre un enlace externo y no registra pedidos.
La ampliación hacia una tienda está [planeada para V2.4](../19_V2_Pendientes.md#v24--canal-público--tienda-en-línea).

**V1.4 implementada en esta rama, sin release ni despliegue:** [Galería (máximo 8 fotos), zoom,
subcategorías de dos niveles, filtro por marca, reconexión discreta pública,
indicador verde y consulta por WhatsApp](catalogo-v1-4.md). Se desarrolla en una rama separada
partiendo de `v1.3.0`; no incluye ni fusiona V2.1.

**Ajustes aprobados pendientes de implementación:** [portada con carruseles
por categoría raíz, paginación real, navegación raíz→subcategorías,
progreso de fotos y aviso mínimo de reconexión](catalogo-v1-4-ajustes-ux.md).
El listado actual todavía descarga todos los productos elegibles; el nuevo
contrato lo sustituirá sin alterar la elegibilidad ni el carácter solo lectura.

## Contratos y límites

- [Backend público](../25_CatalogoPublicoBackend.md) es la referencia de campos,
  disponibilidad comercial, lecturas HTTP y protección de imágenes.
- [Primera UI](../26_CatalogoPublicoUI.md) conserva arquitectura/validación histórica;
  [Virtuosa Store](../27_VirtuosaStore.md) describe la identidad visual vigente.
- Publicar exige una misma unidad Disponible, sin reserva y sin venta Registrada.
  La UI consume esa decisión del backend; no calcula stock ni crea reservas.
- Este catálogo público **no** es `TipoPedido.Catalogo`/`OrigenCompra.Catalogo`,
  que gestiona artículos bajo pedido sin inventario físico.
- [Dominios](../deployment/domains.md) distingue `virtuosagt.com` (público),
  `app.resellmanager.tech` (administración) y `preview.newtonlab.dev` (pruebas).
  En producción, `/` y `/producto/{id}` son las URLs públicas operativas mediante
  redirects/rewrites de Caddy. El código Blazor sigue declarando `/catalogo` y
  `/catalogo/{id}`; preview conserva esas rutas. No confundir routing externo
  con un `@page /producto/{id}` inexistente.

## Dónde trabajar y validar

- [ICatalogoPublicoService](../../src/ResellManager.Application/Interfaces/ICatalogoPublicoService.cs),
  [CatalogoPublicoService](../../src/ResellManager.Infrastructure/Services/CatalogoPublicoService.cs)
  y [endpoints](../../src/ResellManager.Web/Endpoints/CatalogoPublicoEndpoints.cs).
- [Componentes públicos](../../src/ResellManager.Web/Components/Catalogo/),
  [cliente de lectura](../../src/ResellManager.Web/Catalogo/CatalogoPublicoClient.cs)
  y [JS del catálogo](../../src/ResellManager.Web/wwwroot/catalogo-publico.js).
- Pruebas: `CatalogoPublicoTests`, `CatalogoPublicoEndpointsTests`,
  `CatalogoUiTests` y `VirtuosaStoreUiTests`, en
  [ResellManager.Tests](../../tests/ResellManager.Tests/).

Conserva URLs relativas al origen para poder probar el catálogo en preview.
Cambiar un enlace público no autoriza tocar proxy/DNS ni exponer datos privados.

Configuración del contacto, migración y validación de V1.4 se mantienen en
[su contrato](catalogo-v1-4.md#implementación-y-configuración-v14).
