using GymTrack.Domain.Enums;

namespace GymTrack.Domain.Entities;

public class UserProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public GoalType? Objetivo { get; set; }
    public ExperienceLevel? Nivel { get; set; }
    public TrainingType? PreferenciaEntrenamiento { get; set; }
}

public class QuestionnaireResponse
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public GoalType Objetivo { get; set; }
    public ExperienceLevel Nivel { get; set; }
    public int DiasPorSemana { get; set; }
    public int DuracionSesionMin { get; set; }
    public TrainingType TipoPreferido { get; set; }
    /// <summary>Identificadores de grupos musculares separados por coma.</summary>
    public string GruposMuscularesIds { get; set; } = string.Empty;
    public string? EjerciciosAEvitar { get; set; }
    public string? Limitaciones { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}

public class BodyAssessment
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public AssessmentStatus Estado { get; set; }
    public string? ResultadoAnalisis { get; set; }
    public string? Indicadores { get; set; }
    public string? Observaciones { get; set; }
    public ICollection<BodyPhoto> Fotos { get; set; } = new List<BodyPhoto>();
}

public class BodyPhoto
{
    public int Id { get; set; }
    public int BodyAssessmentId { get; set; }
    public BodyAssessment? BodyAssessment { get; set; }
    /// <summary>Clave relativa dentro del almacenamiento privado (nunca una URL pública).</summary>
    public string RutaPrivada { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/jpeg";
    public PhotoType Tipo { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public DateTime ConsentimientoFecha { get; set; }
}

public class ProgressRecord
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public double PesoKg { get; set; }
    public double? CinturaCm { get; set; }
    public double? PechoCm { get; set; }
    public double? CaderaCm { get; set; }
    public double? BrazoCm { get; set; }
    public double? MusloCm { get; set; }
    public string? ObjetivoAlcanzado { get; set; }
    public string? Observaciones { get; set; }
}

public class AdminAuditLog
{
    public int Id { get; set; }
    public string AdminUserId { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public string Entidad { get; set; } = string.Empty;
    public string? EntidadId { get; set; }
    public string? Detalle { get; set; }
    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
}
