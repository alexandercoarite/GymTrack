using GymTrack.Domain.Enums;

namespace GymTrack.Domain.Entities;

public class Exercise
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int MuscleGroupId { get; set; }
    public MuscleGroup? MuscleGroup { get; set; }
    public TrainingType TipoEjercicio { get; set; } = TrainingType.Fuerza;
    /// <summary>Si es true, el ejercicio necesita una máquina o equipo del gimnasio.</summary>
    public bool UsaMaquina { get; set; }
    /// <summary>Texto que debe contener el nombre de la máquina requerida (opcional).</summary>
    public string? MaquinaReferencia { get; set; }
    public ExperienceLevel NivelMinimo { get; set; } = ExperienceLevel.Principiante;
}

public class TrainingPlan
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public GoalType Objetivo { get; set; }
    public ExperienceLevel Nivel { get; set; }
    public int DiasEntrenamiento { get; set; }
    public int TiempoEstimadoMin { get; set; }
    public string GruposMusculares { get; set; } = string.Empty;
    public string Recomendaciones { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaFin { get; set; }
    public PlanStatus Estado { get; set; } = PlanStatus.Activo;
    public ICollection<PlanExercise> Ejercicios { get; set; } = new List<PlanExercise>();
}

public class PlanExercise
{
    public int Id { get; set; }
    public int TrainingPlanId { get; set; }
    public TrainingPlan? TrainingPlan { get; set; }
    public int ExerciseId { get; set; }
    public Exercise? Exercise { get; set; }
    public int? MachineId { get; set; }
    public Machine? Machine { get; set; }
    public int Dia { get; set; }
    public int Orden { get; set; }
    public int Series { get; set; }
    public string Repeticiones { get; set; } = string.Empty;
    public int DescansoSeg { get; set; }
}
