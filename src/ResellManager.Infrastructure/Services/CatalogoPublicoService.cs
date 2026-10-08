using Microsoft.EntityFrameworkCore;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;

namespace ResellManager.Infrastructure.Services;

public sealed class CatalogoPublicoService(
    ResellManagerDbContext db,
    IAlmacenamientoImagenesProducto almacenamiento) : ICatalogoPublicoService
{
    // Misma unidad Disponible, sin reserva ni venta Registrada; cancelaciones no bloquean.
    private IQueryable<Producto> ProductosDisponibles() => db.Productos.AsNoTracking()
        .Where(p => p.UnidadesInventario.Any(u =>
            u.Estado == EstadoUnidadInventario.Disponible
            && u.DetallePedidoReservaId == null
            && !u.DetallesVenta.Any(d => d.Venta.Estado == EstadoVenta.Registrada)));

    public async Task<IReadOnlyList<ProductoCatalogoDto>> ListarAsync(
        string? termino = null, int? categoriaId = null, CancellationToken ct = default, string? marca = null)
    {
        var productos = ProductosDisponibles();
        var texto = termino?.Trim().ToLowerInvariant() ?? string.Empty;
        if (texto.Length > 0)
        {
            productos = productos.Where(p => p.Nombre.ToLower().Contains(texto)
                || p.CodigoInterno.ToLower().Contains(texto)
                || (p.CodigoBarras != null && p.CodigoBarras.ToLower().Contains(texto)));
        }
        if (categoriaId.HasValue)
            productos = productos.Where(p => p.CategoriaId == categoriaId.Value
                || p.Categoria.CategoriaPadreId == categoriaId.Value);

        var resultado = await productos
            .OrderByDescending(p => p.CodigoInterno.ToLower() == texto
                || (p.CodigoBarras != null && p.CodigoBarras.ToLower() == texto))
            .ThenBy(p => p.Nombre).ThenBy(p => p.Id)
            .Select(p => new ProductoCatalogoDto(
                p.Id, p.Nombre, p.CategoriaId, p.Categoria.Nombre, p.PrecioSugerido,
                p.ImagenPrincipalRuta != null && p.ImagenPrincipalRuta.Trim() != "",
                true, p.Marca, p.Categoria.CategoriaPadreId,
                p.Categoria.CategoriaPadre == null ? null : p.Categoria.CategoriaPadre.Nombre))
            .ToListAsync(ct);
        var marcaNormalizada = marca?.Trim();
        // SQLite lower/NOCASE no normaliza Unicode; comparar aquí conserva acentos y evita matching difuso.
        return string.IsNullOrWhiteSpace(marcaNormalizada) ? resultado
            : resultado.Where(p => string.Equals(p.Marca?.Trim(), marcaNormalizada,
                StringComparison.OrdinalIgnoreCase)).ToList();
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
