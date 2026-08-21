using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;

namespace ResellManager.Application.Interfaces;

public interface IClienteService
{
    Task<ServiceResult<ClienteDto>> CrearAsync(ClienteInput input, CancellationToken ct = default);
    Task<ServiceResult<ClienteDto>> EditarAsync(
        int id,
        ClienteInput input,
        CancellationToken ct = default
    );
    Task<ServiceResult<ClienteDto>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ClienteDto>> ListarAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ClienteDto>> BuscarAsync(string termino, CancellationToken ct = default);
    Task<ServiceResult<decimal>> ObtenerSaldoAsync(int clienteId, CancellationToken ct = default);
    Task<ServiceResult<ClienteHistorialDto>> ObtenerHistorialAsync(
        int clienteId,
        CancellationToken ct = default
    );
}

public interface ICategoriaService
{
    Task<ServiceResult<CategoriaDto>> CrearAsync(
        CategoriaInput input,
        CancellationToken ct = default
    );
    Task<ServiceResult<CategoriaDto>> EditarAsync(
        int id,
        CategoriaInput input,
        CancellationToken ct = default
    );
    Task<ServiceResult<CategoriaDto>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<CategoriaDto>> ListarAsync(CancellationToken ct = default);
}

public interface IProductoService
{
    Task<ServiceResult<ProductoDto>> CrearAsync(
        ProductoInput input,
        CancellationToken ct = default
    );
    Task<ServiceResult<ProductoDto>> EditarAsync(
        int id,
        ProductoInput input,
        CancellationToken ct = default
    );
    Task<ServiceResult<ProductoDto>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ProductoDto>> ListarAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProductoDto>> BuscarAsync(string termino, CancellationToken ct = default);
}

public interface IProveedorService
{
    Task<ServiceResult<ProveedorDto>> CrearAsync(
        ProveedorInput input,
        CancellationToken ct = default
    );
    Task<ServiceResult<ProveedorDto>> EditarAsync(
        int id,
        ProveedorInput input,
        CancellationToken ct = default
    );
    Task<ServiceResult<ProveedorDto>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ProveedorDto>> ListarAsync(CancellationToken ct = default);
}

public interface ICompraService
{
    Task<ServiceResult<CompraDto>> RegistrarAsync(
        CompraInput input,
        CancellationToken ct = default
    );
    Task<ServiceResult<CompraDto>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<CompraDto>> ListarAsync(CancellationToken ct = default);
    Task<ServiceResult<ComprobanteCompraDto>> ObtenerComprobanteAsync(
        int compraId,
        CancellationToken ct = default
    );
}

public interface IInventarioService
{
    Task<IReadOnlyList<UnidadInventarioDto>> ListarAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UnidadInventarioDto>> ListarDisponiblesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UnidadInventarioDto>> BuscarAsync(
        string? termino,
        EstadoUnidadInventario? estado,
        CancellationToken ct = default
    );
    Task<ServiceResult<IReadOnlyList<UnidadInventarioDto>>> RegistrarRecepcionAsync(
        RecepcionMercanciaInput input,
        CancellationToken ct = default
    );
    Task<ServiceResult<UnidadInventarioDto>> ReservarAsync(
        int unidadInventarioId,
        int detallePedidoId,
        CancellationToken ct = default
    );
    Task<ServiceResult<UnidadInventarioDto>> CancelarReservaAsync(
        int unidadInventarioId,
        CancellationToken ct = default
    );
    Task<ServiceResult<UnidadInventarioDto>> CambiarEstadoAsync(
        int id,
        EstadoUnidadInventario estado,
        CancellationToken ct = default
    );
}

public interface IPedidoService
{
    Task<ServiceResult<PedidoDto>> CrearAsync(PedidoInput input, CancellationToken ct = default);
    Task<ServiceResult<PedidoDto>> AgregarDetalleAsync(
        int pedidoId,
        DetallePedidoInput input,
        CancellationToken ct = default
    );
    Task<ServiceResult<PedidoDto>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<PedidoDto>> ListarAsync(CancellationToken ct = default);
    Task<ServiceResult> CancelarAsync(int id, CancellationToken ct = default);
}

public interface IVentaService
{
    Task<ServiceResult<VentaDto>> RegistrarDesdePedidoAsync(
        VentaInput input,
        CancellationToken ct = default
    );
    Task<ServiceResult<VentaDto>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<VentaDto>> ListarAsync(CancellationToken ct = default);
    Task<ServiceResult<decimal>> CalcularTotalAsync(int ventaId, CancellationToken ct = default);
    Task<ServiceResult> CancelarAsync(int id, CancellationToken ct = default);
}

public interface IPagoService
{
    Task<ServiceResult<PagoDto>> RegistrarAsync(PagoInput input, CancellationToken ct = default);
    Task<ServiceResult<PagoDto>> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<PagoDto>> ListarPorClienteAsync(
        int clienteId,
        CancellationToken ct = default
    );
}

public interface IDashboardService
{
    Task<DashboardDto> ObtenerAsync(int cantidadRecientes = 5, CancellationToken ct = default);
    Task<ServiceResult<decimal>> ObtenerUtilidadAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default
    );
}
