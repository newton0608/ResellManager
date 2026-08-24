using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Clientes;

namespace ResellManager.Tests;

public sealed class ClientePresentacionTests
{
    [Fact]
    public void Moneda_YNombreCompleto_UsanPresentacionEsperada()
    {
        var cliente = new ClienteDto(1, "Ana", "López", "5555-0101", null, null, 1250m);

        Assert.Equal("Ana López", ClientePresentacion.NombreCompleto(cliente));
        Assert.Equal("Q 1,250.00", ClientePresentacion.Moneda(cliente.Saldo));
    }

    [Fact]
    public void Formulario_SoloExigeNombresYTelefono()
    {
        var invalido = new ClienteFormModel();
        var errores = new List<ValidationResult>();

        var esValido = Validator.TryValidateObject(
            invalido,
            new ValidationContext(invalido),
            errores,
            validateAllProperties: true);

        Assert.False(esValido);
        Assert.Equal(2, errores.Count);

        var valido = new ClienteFormModel
        {
            Nombres = "Ana",
            Telefono = "5555-0101",
        };

        Assert.True(Validator.TryValidateObject(
            valido,
            new ValidationContext(valido),
            [],
            validateAllProperties: true));
    }
}

public sealed class ClienteServicioModuloTests
{
    [Fact]
    public async Task CrearBuscarEditar_UsaElContratoExistente()
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = new ClienteService(test.Db);

        var creado = await servicio.CrearAsync(
            new ClienteInput("María", "Pérez", "5555-0202", null, null));
        var encontrados = await servicio.BuscarAsync("Pérez");
        var editado = await servicio.EditarAsync(
            creado.Value!.Id,
            new ClienteInput("María Elena", "Pérez", "5555-0303", "Guatemala", "Preferente"));

        Assert.True(creado.IsSuccess, creado.ErrorMessage);
        Assert.Contains(encontrados, cliente => cliente.Id == creado.Value.Id);
        Assert.True(editado.IsSuccess, editado.ErrorMessage);
        Assert.Equal("María Elena", editado.Value!.Nombres);
        Assert.Equal("5555-0303", editado.Value.Telefono);
        Assert.Equal(0m, editado.Value.Saldo);
    }
}

public sealed class ClienteModuloIntegracionTests(AplicacionAutenticacionFactory factory)
    : IClassFixture<AplicacionAutenticacionFactory>
{
    [Theory]
    [InlineData("/clientes")]
    [InlineData("/clientes/nuevo")]
    [InlineData("/clientes/7")]
    [InlineData("/clientes/7/editar")]
    public async Task RutasDeClientes_RequierenAutenticacion(string ruta)
    {
        using var cliente = CrearCliente(factory);

        var respuesta = await cliente.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/login", respuesta.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task ListadoYDetalle_MuestranSaldosEHistorialProvistosPorIClienteService()
    {
        var servicio = new ClienteServiceFalso();
        using var aplicacion = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IClienteService>();
                services.AddSingleton<IClienteService>(servicio);
            });
        });
        using var cliente = CrearCliente(aplicacion);
        await IniciarSesionAsync(cliente);

        var listado = WebUtility.HtmlDecode(await cliente.GetStringAsync("/clientes"));
        var detalle = WebUtility.HtmlDecode(await cliente.GetStringAsync("/clientes/7"));

        Assert.Contains("Cliente Prueba", listado);
        Assert.Contains("Q 987.65", listado);
        Assert.Contains("Q 1,250.00", detalle);
        Assert.Contains("VEN-SERVICIO", detalle);
        Assert.Contains("Q 500.00", detalle);
        Assert.Contains("Pagos y abonos", detalle);
        Assert.Contains("Q 125.00", detalle);
        Assert.True(servicio.ObtenerSaldoFueInvocado);
        Assert.True(servicio.ObtenerHistorialFueInvocado);
    }

    private static HttpClient CrearCliente(WebApplicationFactory<Program> aplicacion) =>
        aplicacion.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

    private static async Task IniciarSesionAsync(HttpClient cliente)
    {
        var paginaLogin = await cliente.GetAsync("/login");
        var token = await ObtenerTokenAntiforgeryAsync(paginaLogin);
        using var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["correo"] = AplicacionAutenticacionFactory.CorreoUsuario,
            ["contrasena"] = AplicacionAutenticacionFactory.ContrasenaValida,
            ["__RequestVerificationToken"] = token,
        });

        var respuesta = await cliente.PostAsync("/account/login", formulario);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
    }

    private static async Task<string> ObtenerTokenAntiforgeryAsync(HttpResponseMessage respuesta)
    {
        respuesta.EnsureSuccessStatusCode();
        var contenido = await respuesta.Content.ReadAsStringAsync();
        var etiqueta = Regex.Match(
            contenido,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>",
            RegexOptions.IgnoreCase);
        var valor = Regex.Match(etiqueta.Value, "value=\"([^\"]+)\"", RegexOptions.IgnoreCase);

        Assert.True(valor.Success);
        return WebUtility.HtmlDecode(valor.Groups[1].Value);
    }

    private sealed class ClienteServiceFalso : IClienteService
    {
        private readonly ClienteDto cliente =
            new(7, "Cliente", "Prueba", "5555-0707", "Ciudad de Guatemala", "Observación", 987.65m);

        public bool ObtenerSaldoFueInvocado { get; private set; }
        public bool ObtenerHistorialFueInvocado { get; private set; }

        public Task<ServiceResult<ClienteDto>> CrearAsync(
            ClienteInput input,
            CancellationToken ct = default) =>
            Task.FromResult(ServiceResult<ClienteDto>.Failure("No disponible en esta prueba."));

        public Task<ServiceResult<ClienteDto>> EditarAsync(
            int id,
            ClienteInput input,
            CancellationToken ct = default) =>
            Task.FromResult(ServiceResult<ClienteDto>.Failure("No disponible en esta prueba."));

        public Task<ServiceResult<ClienteDto>> ObtenerPorIdAsync(
            int id,
            CancellationToken ct = default) =>
            Task.FromResult(ServiceResult<ClienteDto>.Ok(cliente));

        public Task<IReadOnlyList<ClienteDto>> ListarAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ClienteDto>>([cliente]);

        public Task<IReadOnlyList<ClienteDto>> BuscarAsync(
            string termino,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ClienteDto>>([cliente]);

        public Task<ServiceResult<decimal>> ObtenerSaldoAsync(
            int clienteId,
            CancellationToken ct = default)
        {
            ObtenerSaldoFueInvocado = true;
            return Task.FromResult(ServiceResult<decimal>.Ok(1250m));
        }

        public Task<ServiceResult<ClienteHistorialDto>> ObtenerHistorialAsync(
            int clienteId,
            CancellationToken ct = default)
        {
            ObtenerHistorialFueInvocado = true;
            var venta = new VentaDto(
                10,
                "VEN-SERVICIO",
                new DateOnly(2026, 8, 20),
                EstadoVenta.Registrada,
                "Venta desde el servicio",
                20,
                cliente.Id,
                "Cliente Prueba",
                500m,
                []);
            var pago = new PagoDto(
                11,
                cliente.Id,
                "Cliente Prueba",
                new DateOnly(2026, 8, 21),
                125m,
                MetodoPago.Efectivo,
                "REF-SERVICIO",
                "Abono desde el servicio");

            return Task.FromResult(
                ServiceResult<ClienteHistorialDto>.Ok(
                    new ClienteHistorialDto(cliente, [venta], [pago])));
        }
    }
}
