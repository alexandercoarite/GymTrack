using System.ComponentModel.DataAnnotations;

namespace GymTrack.Application.DTOs;

public class OpeningHourModel
{
    public DayOfWeek Dia { get; set; }
    public bool Abierto { get; set; }
    public TimeOnly? Apertura { get; set; }
    public TimeOnly? Cierre { get; set; }
}

public class SpecialOpeningHourModel
{
    public int Id { get; set; }
    public DateOnly Fecha { get; set; }
    public bool Abierto { get; set; }
    public TimeOnly? Apertura { get; set; }
    public TimeOnly? Cierre { get; set; }

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(200, ErrorMessage = "La descripción no puede superar 200 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;
}

public record GymStatusDto(bool AbiertoAhora, string Mensaje);
