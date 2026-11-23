using System.ComponentModel.DataAnnotations;
using GymTrack.Domain.Enums;

namespace GymTrack.Application.Common;

/// <summary>Error de validación o de regla de negocio con un mensaje apto para el usuario.</summary>
public class ServiceException : Exception
{
    public ServiceException(string message) : base(message) { }
}

/// <summary>El usuario actual no tiene permiso para la operación.</summary>
public class ForbiddenException : Exception
{
    public ForbiddenException() : base("No tienes permiso para realizar esta acción.") { }
}

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Client = "Client";
}

public static class ModelValidator
{
    public static void ValidateOrThrow(object model)
    {
        var results = new List<ValidationResult>();
        var ctx = new ValidationContext(model);
        if (!Validator.TryValidateObject(model, ctx, results, validateAllProperties: true))
        {
            throw new ServiceException(string.Join(" ", results.Select(r => r.ErrorMessage)));
        }
    }
}

public static class ImageValidator
{
    public const long MaxMachineImageBytes = 2 * 1024 * 1024;
    public const long MaxPhotoBytes = 5 * 1024 * 1024;

    /// <summary>Detecta el tipo real por los primeros bytes (no se confía en la extensión).</summary>
    public static string? DetectExtension(byte[] data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return ".jpg";
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47
            && data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A) return ".png";
        if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46
            && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50) return ".webp";
        return null;
    }

    public static string ContentTypeFor(string extension) => extension switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };

    public static string ValidateOrThrow(byte[] data, long maxBytes)
    {
        if (data.Length == 0) throw new ServiceException("El archivo está vacío.");
        if (data.Length > maxBytes)
            throw new ServiceException($"La imagen supera el tamaño máximo de {maxBytes / (1024 * 1024)} MB.");
        return DetectExtension(data)
               ?? throw new ServiceException("Formato no permitido. Usa imágenes JPG, PNG o WEBP.");
    }
}

public static class EnumLabels
{
    private static readonly Dictionary<string, string> Map = new()
    {
        ["GanarMasaMuscular"] = "Ganar masa muscular",
        ["PerderGrasa"] = "Perder grasa corporal",
        ["MejorarResistencia"] = "Mejorar la resistencia",
        ["AumentarFuerza"] = "Aumentar la fuerza",
        ["CondicionGeneral"] = "Mejorar la condición física general",
        ["EnMantenimiento"] = "En mantenimiento",
        ["FueraDeServicio"] = "Fuera de servicio",
        ["AnalisisNoDisponible"] = "Análisis no disponible",
    };

    public static string Label(this Enum value) =>
        Map.TryGetValue(value.ToString(), out var text) ? text : value.ToString();

    public static string GoalShort(GoalType goal) => goal switch
    {
        GoalType.GanarMasaMuscular => "Masa muscular",
        GoalType.PerderGrasa => "Pérdida de grasa",
        GoalType.MejorarResistencia => "Resistencia",
        GoalType.AumentarFuerza => "Fuerza",
        _ => "Condición general"
    };
}

public static class DayLabels
{
    public static string Of(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Lunes",
        DayOfWeek.Tuesday => "Martes",
        DayOfWeek.Wednesday => "Miércoles",
        DayOfWeek.Thursday => "Jueves",
        DayOfWeek.Friday => "Viernes",
        DayOfWeek.Saturday => "Sábado",
        _ => "Domingo"
    };

    /// <summary>Lunes a domingo.</summary>
    public static readonly DayOfWeek[] WeekOrder =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    };
}
