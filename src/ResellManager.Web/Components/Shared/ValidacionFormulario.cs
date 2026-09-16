using System.ComponentModel.DataAnnotations;

namespace ResellManager.Web.Components.Shared;

public static class ValidacionFormulario
{
    public static string? PrimerError(object modelo)
    {
        var errores = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo), errores, true);
        return errores.FirstOrDefault()?.ErrorMessage;
    }
}
