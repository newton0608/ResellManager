using ResellManager.Application.Common;
using ResellManager.Application.DTOs;

namespace ResellManager.Web.Components.Productos;

// Solo estado temporal del formulario; no consulta proveedores ni persiste.
public sealed class ProductoLookupImportacion
{
    private ProductoFormModel? anterior;
    public bool PuedeDeshacer => anterior is not null;

    public void Aplicar(ProductoFormModel modelo, ProductoLookupCandidato candidato)
    {
        anterior = modelo.CrearInstantanea();
        var datos = ProductoLookupDatos.Normalizar(candidato);
        if (datos.Nombre is not null) modelo.Nombre = datos.Nombre;
        if (datos.Descripcion is not null) modelo.Descripcion = datos.Descripcion;
        if (datos.Marca is not null) modelo.Marca = datos.Marca;
        if (datos.Modelo is not null) modelo.Modelo = datos.Modelo;
        if (datos.Color is not null) modelo.Color = datos.Color;
        if (datos.Talla is not null) modelo.Talla = datos.Talla;
        if (datos.Presentacion is not null) modelo.Presentacion = datos.Presentacion;
        if (datos.ContenidoMl.HasValue)
        {
            modelo.Peso = null;
            modelo.ContenidoMl = datos.ContenidoMl;
        }
        else if (datos.PesoGramos.HasValue)
        {
            modelo.Volumen = null;
            modelo.PesoGramos = datos.PesoGramos;
        }
        if (datos.ImagenUrl is not null && modelo.Galeria.Count == 0 && modelo.ImagenArchivo is null && modelo.ImagenContenido is null)
            modelo.ImagenExternaUrl = datos.ImagenUrl;
        // El código aprobado, precio y categoría local se conservan.
    }

    public void Deshacer(ProductoFormModel modelo)
    {
        if (anterior is null) return;
        modelo.Restaurar(anterior);
        anterior = null;
    }
}
