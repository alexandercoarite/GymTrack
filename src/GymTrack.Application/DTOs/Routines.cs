using GymTrack.Domain.Enums;

namespace GymTrack.Application.DTOs;

public record PlanExerciseDto(int Dia, int Orden, string Ejercicio, string GrupoMuscular, string? Maquina, int Series, string Repeticiones, int DescansoSeg);

public record RoutineDto(
    int Id, string Nombre, GoalType Objetivo, ExperienceLevel Nivel, int Dias, int TiempoEstimadoMin,
    string GruposMusculares, string Recomendaciones, DateTime FechaCreacion, DateTime? FechaFin,
    PlanStatus Estado, IReadOnlyList<PlanExerciseDto> Ejercicios);

public record ClientDashboardDto(
    string Nombre, string? Objetivo, string? Nivel, DateTime? UltimaEvaluacion, bool TieneCuestionario,
    RoutineDto? RutinaActiva, IReadOnlyList<ProgressDto> Progreso);

public record RecentUserDto(string Nombre, string Email, DateTime FechaRegistro);

public record AdminDashboardDto(
    int Clientes, int Maquinas, int Disponibles, int EnMantenimiento, int FueraDeServicio,
    int AltasUltimos30Dias, IReadOnlyList<RecentUserDto> Recientes, int MantenimientosAbiertos);
