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
    // Coincide con la elegibilidad de venta general y la validación definitiva de VentaService.
    // Las ventas canceladas son historial y no bloquean una unidad liberada.
    private IQueryable<Producto> ProductosDisponibles() => db.Productos.AsNoTracking()
        .Where(p => p.UnidadesInventario.Any(u =>
            u.Estado == EstadoUnidadInventario.Disponible
            && u.DetallePedidoReservaId == null
            && !u.DetallesVenta.Any(d => d.Venta.Estado == EstadoVenta.Registrada)));

    public async Task<IReadOnlyList<ProductoCatalogoDto>> ListarAsync(
        string? termino = null, int? categoriaId = null, CancellationToken ct = default)
    {
        var productos = ProductosDisponibles();
        var texto = termino?.Trim().ToLowerInvariant() ?? string.Empty;
        if (texto.Length > 0)
        {
            // Mismos campos y normalización que IProductoService.BuscarAsync.
            productos = productos.Where(p => p.Nombre.ToLower().Contains(texto)
                || p.CodigoInterno.ToLower().Contains(texto)
                || (p.CodigoBarras != null && p.CodigoBarras.ToLower().Contains(texto)));
        }
        if (categoriaId.HasValue)
            productos = productos.Where(p => p.CategoriaId == categoriaId.Value);

        return await productos
            .OrderByDescending(p => p.CodigoInterno.ToLower() == texto
                || (p.CodigoBarras != null && p.CodigoBarras.ToLower() == texto))
            .ThenBy(p => p.Nombre).ThenBy(p => p.Id)
            .Select(p => new ProductoCatalogoDto(
                p.Id, p.Nombre, p.CategoriaId, p.Categoria.Nombre, p.PrecioSugerido,
                p.ImagenPrincipalRuta != null && p.ImagenPrincipalRuta.Trim() != "",
                true))
            .ToListAsync(ct);
    }

    public async Task<ServiceResult<ProductoCatalogoDetalleDto>> ObtenerPorIdAsync(
        int productoId, CancellationToken ct = default)
    {
        var producto = await ProductosDisponibles().Where(p => p.Id == productoId)
            .Select(p => new ProductoCatalogoDetalleDto(
                p.Id, p.Nombre, p.Descripcion, p.Marca, p.Modelo, p.Color, p.Talla,
                p.ContenidoMl, p.PesoGramos, p.Presentacion,
                p.CategoriaId, p.Categoria.Nombre, p.PrecioSugerido,
                p.ImagenPrincipalRuta != null && p.ImagenPrincipalRuta.Trim() != "",
                true))
            .FirstOrDefaultAsync(ct);

        return producto is null
            ? ServiceResult<ProductoCatalogoDetalleDto>.Failure("Producto no encontrado.")
            : ServiceResult<ProductoCatalogoDetalleDto>.Ok(producto);
    }

    public async Task<ServiceResult<ImagenProductoLectura>> AbrirImagenPrincipalAsync(
        int productoId, CancellationToken ct = default)
    {
        var ruta = await ProductosDisponibles().Where(p => p.Id == productoId)
            .Select(p => p.ImagenPrincipalRuta).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(ruta)
            || !ruta.StartsWith($"productos/{productoId}/", StringComparison.Ordinal))
            return ServiceResult<ImagenProductoLectura>.Failure("Imagen no disponible.");

        // El almacenamiento existente valida el formato de la ruta y abre únicamente WebP procesados.
        return await almacenamiento.AbrirLecturaAsync(ruta, ct);
    }
}
