using GymTrack.Domain.Enums;

namespace GymTrack.Domain.Entities;

/// <summary>Entidades con token de concurrencia optimista (se renueva en cada guardado).</summary>
public interface IVersioned
{
    Guid Version { get; set; }
}

public class MachineCategory
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public ICollection<Machine> Machines { get; set; } = new List<Machine>();
}

public class MuscleGroup
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public ICollection<Machine> Machines { get; set; } = new List<Machine>();
    public ICollection<Exercise> Exercises { get; set; } = new List<Exercise>();
}

public class Machine : IVersioned
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? Instrucciones { get; set; }
    public int CategoryId { get; set; }
    public MachineCategory? Category { get; set; }
    public int MuscleGroupId { get; set; }
    public MuscleGroup? MuscleGroup { get; set; }
    public TrainingType TipoEntrenamiento { get; set; } = TrainingType.Fuerza;
    public string? ImagenRuta { get; set; }
    public MachineStatus Estado { get; set; } = MachineStatus.Disponible;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();
}

public class MaintenanceRecord
{
    public int Id { get; set; }
    public int MachineId { get; set; }
    public Machine? Machine { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
    public DateTime? FechaFin { get; set; }
    public MaintenanceStatus Estado { get; set; } = MaintenanceStatus.Abierto;
    public string? Observaciones { get; set; }
}
