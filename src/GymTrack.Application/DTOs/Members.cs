using System.ComponentModel.DataAnnotations;
using GymTrack.Domain.Enums;

namespace GymTrack.Application.DTOs;

public record UserListItem(string Id, string NombreCompleto, string Email, string UserName, bool Activo, string Rol, DateTime FechaRegistro);

public record UserDetailDto(
    string Id, string Nombre, string Apellido, string Email, string UserName, string? Telefono,
    DateTime? FechaNacimiento, DateTime FechaRegistro, bool Activo, string Rol, string? Objetivo, string? Nivel);

public class ProfileModel
{
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(80, ErrorMessage = "El nombre no puede superar 80 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [StringLength(80, ErrorMessage = "El apellido no puede superar 80 caracteres.")]
    public string Apellido { get; set; } = string.Empty;

    [Phone(ErrorMessage = "El teléfono no es válido.")]
    [StringLength(30, ErrorMessage = "El teléfono no puede superar 30 caracteres.")]
    public string? Telefono { get; set; }

    public DateTime? FechaNacimiento { get; set; }
}

public class QuestionnaireModel : IValidatableObject
{
    public GoalType Objetivo { get; set; } = GoalType.CondicionGeneral;
    public ExperienceLevel Nivel { get; set; } = ExperienceLevel.Principiante;

    [Range(1, 7, ErrorMessage = "Los días por semana deben estar entre 1 y 7.")]
    public int DiasPorSemana { get; set; } = 3;

    public int DuracionSesionMin { get; set; } = 45;
    public TrainingType TipoPreferido { get; set; } = TrainingType.Combinado;
    public List<int> GruposMusculares { get; set; } = new();

    [StringLength(500, ErrorMessage = "Los ejercicios a evitar no pueden superar 500 caracteres.")]
    public string? EjerciciosAEvitar { get; set; }

    [StringLength(1000, ErrorMessage = "El texto de limitaciones no puede superar 1000 caracteres.")]
    public string? Limitaciones { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DuracionSesionMin is not (30 or 45 or 60 or 90))
            yield return new ValidationResult("Selecciona una duración de sesión válida.");
    }
}

public record PhotoDto(int Id, PhotoType Tipo, DateTime Fecha);

public record AssessmentDto(
    int Id, DateTime Fecha, AssessmentStatus Estado, string? Resultado, string? Indicadores,
    string? Observaciones, IReadOnlyList<PhotoDto> Fotos);

public class PhotoUpload
{
    public PhotoType Tipo { get; set; }
    public byte[] Contenido { get; set; } = Array.Empty<byte>();
}

public class AssessmentCreateModel
{
    public bool Consentimiento { get; set; }
    public bool ConservarFotos { get; set; } = true;

    [StringLength(1000, ErrorMessage = "Las observaciones no pueden superar 1000 caracteres.")]
    public string? Observaciones { get; set; }

    public List<PhotoUpload> Fotos { get; set; } = new();
}

public record PhotoContent(Stream Contenido, string ContentType);

public class ProgressModel
{
    public DateTime Fecha { get; set; } = DateTime.Today;

    [Range(20, 400, ErrorMessage = "El peso debe estar entre 20 y 400 kg.")]
    public double PesoKg { get; set; }

    [Range(20, 300, ErrorMessage = "La cintura debe estar entre 20 y 300 cm.")]
    public double? CinturaCm { get; set; }

    [Range(20, 300, ErrorMessage = "El pecho debe estar entre 20 y 300 cm.")]
    public double? PechoCm { get; set; }

    [Range(20, 300, ErrorMessage = "La cadera debe estar entre 20 y 300 cm.")]
    public double? CaderaCm { get; set; }

    [Range(10, 150, ErrorMessage = "El brazo debe estar entre 10 y 150 cm.")]
    public double? BrazoCm { get; set; }

    [Range(20, 200, ErrorMessage = "El muslo debe estar entre 20 y 200 cm.")]
    public double? MusloCm { get; set; }

    [StringLength(300, ErrorMessage = "El objetivo alcanzado no puede superar 300 caracteres.")]
    public string? ObjetivoAlcanzado { get; set; }

    [StringLength(500, ErrorMessage = "Las observaciones no pueden superar 500 caracteres.")]
    public string? Observaciones { get; set; }
}

public record ProgressDto(
    int Id, DateTime Fecha, double PesoKg, double? CinturaCm, double? PechoCm, double? CaderaCm,
    double? BrazoCm, double? MusloCm, string? ObjetivoAlcanzado, string? Observaciones);
