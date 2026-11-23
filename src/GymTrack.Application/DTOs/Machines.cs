using System.ComponentModel.DataAnnotations;
using GymTrack.Domain.Enums;

namespace GymTrack.Application.DTOs;

public class MachineFilter
{
    public string? Texto { get; set; }
    public int? MuscleGroupId { get; set; }
    public int? CategoryId { get; set; }
    public TrainingType? Tipo { get; set; }
    public MachineStatus? Estado { get; set; }
}

public record MachineDto(
    int Id, string Nombre, string Descripcion, string? Instrucciones,
    int CategoryId, string Categoria, int MuscleGroupId, string GrupoMuscular,
    TrainingType Tipo, MachineStatus Estado, string? ImagenRuta, DateTime FechaRegistro, Guid Version);

public class MachineEditModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(1000, ErrorMessage = "La descripción no puede superar 1000 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Las instrucciones no pueden superar 2000 caracteres.")]
    public string? Instrucciones { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una categoría.")]
    public int CategoryId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un grupo muscular.")]
    public int MuscleGroupId { get; set; }

    public TrainingType Tipo { get; set; } = TrainingType.Fuerza;
    public string? ImagenRuta { get; set; }
    public Guid Version { get; set; }

    public static MachineEditModel From(MachineDto m) => new()
    {
        Id = m.Id, Nombre = m.Nombre, Descripcion = m.Descripcion, Instrucciones = m.Instrucciones,
        CategoryId = m.CategoryId, MuscleGroupId = m.MuscleGroupId, Tipo = m.Tipo,
        ImagenRuta = m.ImagenRuta, Version = m.Version
    };
}

public record LookupDto(int Id, string Nombre, string? Descripcion, int Usos);

public class LookupEditModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(80, ErrorMessage = "El nombre no puede superar 80 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "La descripción no puede superar 300 caracteres.")]
    public string? Descripcion { get; set; }
}

public record MaintenanceDto(
    int Id, int MachineId, string Maquina, string Descripcion,
    DateTime FechaInicio, DateTime? FechaFin, MaintenanceStatus Estado, string? Observaciones);

public class MaintenanceCreateModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona una máquina.")]
    public int MachineId { get; set; }

    [Required(ErrorMessage = "Describe el problema.")]
    [StringLength(1000, ErrorMessage = "La descripción no puede superar 1000 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;
}
