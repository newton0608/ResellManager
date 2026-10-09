using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;

namespace ResellManager.Infrastructure.Services;

public sealed class CatalogoPublicoService : ICatalogoPublicoService
{
    private const string ComparacionMarca = "RM_CATALOGO_MARCA";
    private const int MaximoPagina = 16;
    private const int MaximoSecciones = 3;
    private const int MaximoCarrusel = 10;
    private readonly ResellManagerDbContext db;
    private readonly IAlmacenamientoImagenesProducto almacenamiento;

    public CatalogoPublicoService(ResellManagerDbContext db, IAlmacenamientoImagenesProducto almacenamiento)
    {
        this.db = db;
        this.almacenamiento = almacenamiento;
        // La comparación se ejecuta dentro de SQLite, antes de LIMIT. No modifica valores ni esquema.
        // SQLite NOCASE sólo cubre ASCII; esta collation conserva el contrato Trim + OrdinalIgnoreCase.
        if (db.Database.GetDbConnection() is SqliteConnection conexion)
            conexion.CreateCollation(ComparacionMarca, (a, b) =>
                string.Compare(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    // Misma unidad Disponible, sin reserva ni venta Registrada; cancelaciones no bloquean.
    private IQueryable<Producto> ProductosDisponibles() => db.Productos.AsNoTracking()
        .Where(p => p.UnidadesInventario.Any(u =>
            u.Estado == EstadoUnidadInventario.Disponible
            && u.DetallePedidoReservaId == null
            && !u.DetallesVenta.Any(d => d.Venta.Estado == EstadoVenta.Registrada)));

    private IQueryable<Producto> ProductosFiltrados(string? termino, int? categoriaId, string? marca)
    {
        var productos = ProductosDisponibles();
        var texto = termino?.Trim().ToLowerInvariant() ?? string.Empty;
        if (texto.Length > 0)
            productos = productos.Where(p => p.Nombre.ToLower().Contains(texto)
                || p.CodigoInterno.ToLower().Contains(texto)
                || (p.CodigoBarras != null && p.CodigoBarras.ToLower().Contains(texto)));
        if (categoriaId.HasValue)
            productos = productos.Where(p => p.CategoriaId == categoriaId.Value
                || p.Categoria.CategoriaPadreId == categoriaId.Value);
        var marcaNormalizada = marca?.Trim();
        if (!string.IsNullOrWhiteSpace(marcaNormalizada))
            productos = productos.Where(p => p.Marca != null
                && EF.Functions.Collate(p.Marca, ComparacionMarca) == marcaNormalizada);
        return productos;
    }

    private static IQueryable<ProductoCatalogoDto> Proyectar(IQueryable<Producto> productos) =>
        productos.Select(p => new ProductoCatalogoDto(
            p.Id, p.Nombre, p.CategoriaId, p.Categoria.Nombre, p.PrecioSugerido,
            p.ImagenPrincipalRuta != null && p.ImagenPrincipalRuta.Trim() != "",
            true, p.Marca, p.Categoria.CategoriaPadreId,
            p.Categoria.CategoriaPadre == null ? null : p.Categoria.CategoriaPadre.Nombre));

    public async Task<IReadOnlyList<ProductoCatalogoDto>> ListarAsync(
        string? termino = null, int? categoriaId = null, CancellationToken ct = default, string? marca = null)
    {
        // Contrato antiguo: conserva array completo y orden por coincidencia exacta/nombre/id.
        var texto = termino?.Trim().ToLowerInvariant() ?? string.Empty;
        return await Proyectar(ProductosFiltrados(termino, categoriaId, marca)
            .OrderByDescending(p => p.CodigoInterno.ToLower() == texto
                || (p.CodigoBarras != null && p.CodigoBarras.ToLower() == texto))
            .ThenBy(p => p.Nombre).ThenBy(p => p.Id)).ToListAsync(ct);
    }

    public async Task<CatalogoProductosPaginaDto> ListarPaginaAsync(
        string? termino = null, int? categoriaId = null, string? marca = null,
        int? cursor = null, int tamano = MaximoPagina, CancellationToken ct = default)
    {
        ValidarPagina(cursor, tamano, MaximoPagina);
        var productos = ProductosFiltrados(termino, categoriaId, marca);
        if (cursor.HasValue) productos = productos.Where(p => p.Id > cursor.Value);
        var items = await Proyectar(productos.OrderBy(p => p.Id).Take(tamano + 1)).ToListAsync(ct);
        var hayMas = items.Count > tamano;
        if (hayMas) items.RemoveAt(tamano);
        return new(items, hayMas, hayMas ? items[^1].Id : null);
    }

    private IQueryable<Categoria> RaicesPublicas()
    {
        var productos = ProductosDisponibles();
        return db.Categorias.AsNoTracking().Where(c => c.CategoriaPadreId == null
            && productos.Any(p => p.CategoriaId == c.Id || p.Categoria.CategoriaPadreId == c.Id));
    }

    public async Task<CatalogoCategoriasPaginaDto> LeerRaicesAsync(
        int? cursor = null, int tamano = MaximoPagina, CancellationToken ct = default)
    {
        ValidarPagina(cursor, tamano, MaximoPagina);
        var categorias = RaicesPublicas();
        if (cursor.HasValue) categorias = categorias.Where(c => c.Id > cursor.Value);
        return await PaginaCategoriasAsync(categorias, tamano, ct);
    }

    public async Task<CatalogoEscaparatePaginaDto> LeerEscaparateAsync(
        int? cursor = null, int tamano = MaximoSecciones, CancellationToken ct = default)
    {
        ValidarPagina(cursor, tamano, MaximoSecciones);
        var raices = await LeerRaicesAsync(cursor, tamano, ct);
        if (raices.Items.Count == 0) return new([], false, null);
        var ids = raices.Items.Select(c => c.Id).ToArray();
        var disponibles = ProductosDisponibles();
        // Una consulta limitada para todos los carruseles del bloque: rango Id por raíz, sin N+1.
        // EXISTS busca sólo el décimo anterior elegible; evita contar todos los anteriores de cada fila.
        var productos = await Proyectar(disponibles
            .Where(p => ids.Contains(p.Categoria.CategoriaPadreId ?? p.CategoriaId)
                && !disponibles.Where(anterior => anterior.Id < p.Id
                    && (anterior.Categoria.CategoriaPadreId ?? anterior.CategoriaId)
                        == (p.Categoria.CategoriaPadreId ?? p.CategoriaId))
                    .OrderBy(anterior => anterior.Id).Skip(MaximoCarrusel - 1).Any())
            .OrderBy(p => p.Id)).ToListAsync(ct);
        var secciones = raices.Items.Select(c => new CatalogoSeccionDto(c,
            productos.Where(p => (p.CategoriaPadreId ?? p.CategoriaId) == c.Id).ToArray()))
            .Where(s => s.Productos.Count > 0).ToArray();
        return new(secciones, raices.HasMore, raices.NextCursor);
    }

    public async Task<CatalogoMarcasPaginaDto> LeerMarcasAsync(
        string? cursor = null, int tamano = MaximoPagina, CancellationToken ct = default)
    {
        ValidarPagina(null, tamano, MaximoPagina);
        if (cursor is not null && string.IsNullOrWhiteSpace(cursor))
            throw new ArgumentException("Cursor de marca inválido.", nameof(cursor));
        // GROUP BY usa la misma comparación Unicode que el filtro. MIN binario selecciona
        // una representación estable entre variantes; el recorte visual sólo afecta el bloque obtenido.
        var marcas = ProductosDisponibles().Where(p => p.Marca != null
                && EF.Functions.Collate(p.Marca, ComparacionMarca) != "")
            .GroupBy(p => EF.Functions.Collate(p.Marca!, ComparacionMarca))
            .Select(g => g.Min(p => p.Marca)!);
        if (cursor is not null)
            marcas = marcas.Where(m => EF.Functions.Collate(m, ComparacionMarca).CompareTo(cursor) > 0);
        var items = await marcas.OrderBy(m => EF.Functions.Collate(m, ComparacionMarca))
            .ThenBy(m => m).Take(tamano + 1).ToListAsync(ct);
        var hayMas = items.Count > tamano;
        if (hayMas) items.RemoveAt(tamano);
        return new(items.Select(m => m.Trim()).ToArray(), hayMas, hayMas ? items[^1].Trim() : null);
    }

    public async Task<ServiceResult<CatalogoCategoriaContextoDto>> LeerContextoCategoriaAsync(
        int categoriaId, CancellationToken ct = default)
    {
        var productos = ProductosDisponibles();
        var seleccionada = await db.Categorias.AsNoTracking().Where(c => c.Id == categoriaId
                && productos.Any(p => p.CategoriaId == c.Id || p.Categoria.CategoriaPadreId == c.Id))
            .Select(c => new CategoriaCatalogoDto(c.Id, c.Nombre, c.CategoriaPadreId)).SingleOrDefaultAsync(ct);
        if (seleccionada is null)
            return ServiceResult<CatalogoCategoriaContextoDto>.Failure("Categoría no encontrada.");
        var raiz = seleccionada.CategoriaPadreId.HasValue
            ? await RaicesPublicas().Where(c => c.Id == seleccionada.CategoriaPadreId.Value)
                .Select(c => new CategoriaCatalogoDto(c.Id, c.Nombre, null)).SingleOrDefaultAsync(ct)
            : seleccionada;
        if (raiz is null)
            return ServiceResult<CatalogoCategoriaContextoDto>.Failure("Categoría no encontrada.");
        return ServiceResult<CatalogoCategoriaContextoDto>.Ok(new(raiz, seleccionada,
            await LeerSubcategoriasAsync(raiz.Id, ct: ct)));
    }

    public async Task<CatalogoCategoriasPaginaDto> LeerSubcategoriasAsync(
        int raizId, int? cursor = null, int tamano = MaximoPagina, CancellationToken ct = default)
    {
        ValidarPagina(cursor, tamano, MaximoPagina);
        var productos = ProductosDisponibles();
        var categorias = db.Categorias.AsNoTracking().Where(c => c.CategoriaPadreId == raizId
            && c.CategoriaPadre!.CategoriaPadreId == null
            && productos.Any(p => p.CategoriaId == c.Id));
        if (cursor.HasValue) categorias = categorias.Where(c => c.Id > cursor.Value);
        return await PaginaCategoriasAsync(categorias, tamano, ct);
    }

    private static async Task<CatalogoCategoriasPaginaDto> PaginaCategoriasAsync(
        IQueryable<Categoria> categorias, int tamano, CancellationToken ct)
    {
        var items = await categorias.OrderBy(c => c.Id).Take(tamano + 1)
            .Select(c => new CategoriaCatalogoDto(c.Id, c.Nombre, c.CategoriaPadreId)).ToListAsync(ct);
        var hayMas = items.Count > tamano;
        if (hayMas) items.RemoveAt(tamano);
        return new(items, hayMas, hayMas ? items[^1].Id : null);
    }

    private static void ValidarPagina(int? cursor, int tamano, int maximo)
    {
        if (cursor is <= 0) throw new ArgumentOutOfRangeException(nameof(cursor), "Cursor inválido.");
        if (tamano < 1 || tamano > maximo)
            throw new ArgumentOutOfRangeException(nameof(tamano), $"Tamaño permitido: 1–{maximo}.");
    }

    public async Task<ServiceResult<ProductoCatalogoDetalleDto>> ObtenerPorIdAsync(
        int productoId, CancellationToken ct = default)
    {
        var producto = await ProductosDisponibles().Where(p => p.Id == productoId)
            .Include(p => p.Categoria).Include(p => p.Imagenes).FirstOrDefaultAsync(ct);
        if (producto is null)
            return ServiceResult<ProductoCatalogoDetalleDto>.Failure("Producto no encontrado.");
        IReadOnlyList<ImagenCatalogoDto> imagenes = producto.Imagenes.OrderBy(i => i.Orden).ThenBy(i => i.Id)
            .Select(i => new ImagenCatalogoDto(i.Id.ToString("N"), i.Orden,
                i.RutaRelativa == producto.ImagenPrincipalRuta)).ToArray();
        // Compatibilidad de referencias anteriores al backfill, sin duplicar archivos ni exponer rutas.
        if (imagenes.Count == 0 && !string.IsNullOrWhiteSpace(producto.ImagenPrincipalRuta))
            imagenes = [new ImagenCatalogoDto("principal", 0, true)];
        return ServiceResult<ProductoCatalogoDetalleDto>.Ok(new ProductoCatalogoDetalleDto(
            producto.Id, producto.Nombre, producto.Descripcion, producto.Marca, producto.Modelo,
            producto.Color, producto.Talla, producto.ContenidoMl, producto.PesoGramos, producto.Presentacion,
            producto.CategoriaId, producto.Categoria.Nombre, producto.PrecioSugerido,
            !string.IsNullOrWhiteSpace(producto.ImagenPrincipalRuta), true, imagenes));
    }

    public async Task<ServiceResult<ImagenProductoLectura>> AbrirImagenPrincipalAsync(
        int productoId, CancellationToken ct = default)
    {
        var ruta = await ProductosDisponibles().Where(p => p.Id == productoId)
            .Select(p => p.ImagenPrincipalRuta).FirstOrDefaultAsync(ct);
        return await AbrirRutaAsync(productoId, ruta, ct);
    }

    public async Task<ServiceResult<ImagenProductoLectura>> AbrirImagenAsync(
        int productoId, string imagenId, CancellationToken ct = default)
    {
        if (imagenId == "principal") return await AbrirImagenPrincipalAsync(productoId, ct);
        if (!Guid.TryParse(imagenId, out var id))
            return ServiceResult<ImagenProductoLectura>.Failure("Imagen no disponible.");
        var ruta = await ProductosDisponibles().Where(p => p.Id == productoId)
            .SelectMany(p => p.Imagenes).Where(i => i.Id == id)
            .Select(i => i.RutaRelativa).FirstOrDefaultAsync(ct);
        return await AbrirRutaAsync(productoId, ruta, ct);
    }

    private async Task<ServiceResult<ImagenProductoLectura>> AbrirRutaAsync(
        int productoId, string? ruta, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ruta)
            || !ruta.StartsWith($"productos/{productoId}/", StringComparison.Ordinal))
            return ServiceResult<ImagenProductoLectura>.Failure("Imagen no disponible.");
        return await almacenamiento.AbrirLecturaAsync(ruta, ct);
    }
}
