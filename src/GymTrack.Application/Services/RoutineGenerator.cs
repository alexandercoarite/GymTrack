using GymTrack.Application.Common;
using GymTrack.Domain.Enums;

namespace GymTrack.Application.Services;

public class Prescription
{
    public int Series { get; set; } = 3;
    public string Repeticiones { get; set; } = "10-12";
    public int DescansoSeg { get; set; } = 60;
    public int CardioMin { get; set; }
}

/// <summary>Reglas configurables (se pueden sobrescribir desde la sección "RoutineRules" de appsettings).</summary>
public class RoutineRules
{
    public Dictionary<GoalType, Prescription> Prescriptions { get; set; } = new()
    {
        [GoalType.GanarMasaMuscular] = new Prescription { Series = 4, Repeticiones = "8-12", DescansoSeg = 90, CardioMin = 10 },
        [GoalType.AumentarFuerza] = new Prescription { Series = 4, Repeticiones = "3-6", DescansoSeg = 150, CardioMin = 0 },
        [GoalType.PerderGrasa] = new Prescription { Series = 3, Repeticiones = "12-15", DescansoSeg = 45, CardioMin = 20 },
        [GoalType.MejorarResistencia] = new Prescription { Series = 3, Repeticiones = "15-20", DescansoSeg = 30, CardioMin = 20 },
        [GoalType.CondicionGeneral] = new Prescription { Series = 3, Repeticiones = "10-12", DescansoSeg = 60, CardioMin = 10 },
    };

    public int WarmupMinutes { get; set; } = 10;
    public int SecondsPerSet { get; set; } = 40;
    public int MinExercisesPerSession { get; set; } = 3;
    public int MaxExercisesPerSession { get; set; } = 8;
}

public class RoutineRequest
{
    public GoalType Objetivo { get; set; }
    public ExperienceLevel Nivel { get; set; }
    public int DiasPorSemana { get; set; } = 3;
    public int DuracionSesionMin { get; set; } = 45;
    public TrainingType TipoPreferido { get; set; } = TrainingType.Combinado;
    public IReadOnlyList<int> GruposMuscularesIds { get; set; } = Array.Empty<int>();
    public string? EjerciciosAEvitar { get; set; }
    public bool TieneLimitaciones { get; set; }
}

public record MachineOption(int Id, string Nombre);

public record ExerciseCandidate(
    int Id, string Nombre, int MuscleGroupId, string GrupoMuscular, TrainingType Tipo,
    bool UsaMaquina, ExperienceLevel NivelMinimo, IReadOnlyList<MachineOption> Maquinas);

public record DraftExercise(
    int ExerciseId, string Nombre, string GrupoMuscular, int? MachineId, string? Maquina,
    int Series, string Repeticiones, int DescansoSeg, int Orden, int Dia);

public class RoutineDraft
{
    public string Nombre { get; set; } = string.Empty;
    public int Dias { get; set; }
    public int TiempoEstimadoMin { get; set; }
    public string GruposMusculares { get; set; } = string.Empty;
    public string Recomendaciones { get; set; } = string.Empty;
    public List<string> Advertencias { get; set; } = new();
    public List<DraftExercise> Ejercicios { get; set; } = new();
}

public interface IRoutineGenerator
{
    RoutineDraft Generate(RoutineRequest request, IReadOnlyList<ExerciseCandidate> catalog);
}

public class RoutineGenerator : IRoutineGenerator
{
    private readonly RoutineRules _rules;

    public RoutineGenerator(RoutineRules rules) => _rules = rules;

    public RoutineDraft Generate(RoutineRequest req, IReadOnlyList<ExerciseCandidate> catalog)
    {
        var draft = new RoutineDraft();
        var presc = _rules.Prescriptions.TryGetValue(req.Objetivo, out var p) ? p : new Prescription();
        var series = req.Nivel == ExperienceLevel.Principiante ? Math.Max(2, presc.Series - 1) : presc.Series;
        var days = Math.Clamp(req.DiasPorSemana, 1, 7);
        var avoid = ParseTerms(req.EjerciciosAEvitar);

        // Solo ejercicios del nivel adecuado, sin los que el usuario quiere evitar
        // y cuyas máquinas existan y estén disponibles.
        var eligible = catalog
            .Where(e => e.NivelMinimo <= req.Nivel)
            .Where(e => !avoid.Any(t => e.Nombre.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .Where(e => !e.UsaMaquina || e.Maquinas.Count > 0)
            .ToList();

        var strength = eligible.Where(e => e.Tipo == TrainingType.Fuerza).ToList();
        var cardio = eligible.Where(e => e.Tipo == TrainingType.Cardiovascular).ToList();

        var wantsStrength = req.TipoPreferido != TrainingType.Cardiovascular;
        var wantsCardio = req.TipoPreferido != TrainingType.Fuerza
                          || req.Objetivo is GoalType.PerderGrasa or GoalType.MejorarResistencia;
        var cardioMin = req.TipoPreferido == TrainingType.Cardiovascular ? 15 : Math.Max(10, presc.CardioMin);

        var allGroups = strength.Select(e => e.MuscleGroupId).Distinct().OrderBy(id => id).ToList();
        var groupIds = req.GruposMuscularesIds.Count > 0
            ? allGroups.Where(g => req.GruposMuscularesIds.Contains(g)).ToList()
            : allGroups;
        if (groupIds.Count == 0 && allGroups.Count > 0)
        {
            groupIds = allGroups;
            draft.Advertencias.Add("No hay ejercicios disponibles para los grupos musculares elegidos; se usaron todos los grupos disponibles.");
        }

        var restAvg = presc.DescansoSeg;
        var perExerciseMin = series * (_rules.SecondsPerSet + restAvg) / 60.0;
        var availableMin = req.DuracionSesionMin - _rules.WarmupMinutes - (wantsCardio && wantsStrength ? cardioMin : 0);
        var slots = Math.Clamp((int)(availableMin / perExerciseMin), _rules.MinExercisesPerSession, _rules.MaxExercisesPerSession);

        var used = new HashSet<int>();
        var maxSessionMin = 0.0;
        var cardioWarned = false;

        for (var d = 0; d < days; d++)
        {
            var order = 1;
            var sessionMin = (double)_rules.WarmupMinutes;

            if (wantsStrength && strength.Count > 0)
            {
                var dayGroups = PickGroups(groupIds, d, days);
                var queues = dayGroups
                    .Select(g => strength.Where(e => e.MuscleGroupId == g)
                        .OrderBy(e => used.Contains(e.Id) ? 1 : 0)
                        .ThenBy(e => (e.Id + d) % 7)
                        .ToList())
                    .Where(q => q.Count > 0)
                    .ToList();

                var index = new int[queues.Count];
                var picks = new List<ExerciseCandidate>();
                while (picks.Count < slots)
                {
                    var added = false;
                    for (var q = 0; q < queues.Count && picks.Count < slots; q++)
                    {
                        if (index[q] < queues[q].Count)
                        {
                            picks.Add(queues[q][index[q]++]);
                            added = true;
                        }
                    }
                    if (!added) break;
                }

                foreach (var e in picks)
                {
                    used.Add(e.Id);
                    var machine = e.UsaMaquina ? e.Maquinas[(d + e.Id) % e.Maquinas.Count] : null;
                    draft.Ejercicios.Add(new DraftExercise(
                        e.Id, e.Nombre, e.GrupoMuscular, machine?.Id, machine?.Nombre,
                        series, presc.Repeticiones, presc.DescansoSeg, order++, d + 1));
                    sessionMin += series * (_rules.SecondsPerSet + presc.DescansoSeg) / 60.0;
                }
            }

            if (wantsCardio)
            {
                if (cardio.Count == 0)
                {
                    if (!cardioWarned)
                    {
                        draft.Advertencias.Add("No hay equipos de cardio disponibles en este momento; la rutina no incluye cardio.");
                        cardioWarned = true;
                    }
                }
                else
                {
                    var blocks = req.TipoPreferido == TrainingType.Cardiovascular
                        ? Math.Clamp((req.DuracionSesionMin - _rules.WarmupMinutes) / cardioMin, 1, 4)
                        : 1;
                    var ordered = cardio.OrderBy(e => (e.Id + d) % 5).ToList();
                    for (var b = 0; b < blocks; b++)
                    {
                        var e = ordered[b % ordered.Count];
                        var machine = e.UsaMaquina ? e.Maquinas[(d + e.Id) % e.Maquinas.Count] : null;
                        draft.Ejercicios.Add(new DraftExercise(
                            e.Id, e.Nombre, e.GrupoMuscular, machine?.Id, machine?.Nombre,
                            1, $"{cardioMin} min", 0, order++, d + 1));
                        sessionMin += cardioMin;
                    }
                }
            }

            maxSessionMin = Math.Max(maxSessionMin, sessionMin);
        }

        draft.Dias = draft.Ejercicios.Select(e => e.Dia).Distinct().Count();
        draft.TiempoEstimadoMin = (int)Math.Round(maxSessionMin);
        draft.GruposMusculares = string.Join(", ",
            draft.Ejercicios.Where(e => e.GrupoMuscular.Length > 0).Select(e => e.GrupoMuscular).Distinct());
        draft.Nombre = $"Rutina {EnumLabels.GoalShort(req.Objetivo)} · {req.Nivel.Label()} · {days} día(s)";
        draft.Recomendaciones = BuildRecommendations(req);
        if (draft.Ejercicios.Count == 0)
            draft.Advertencias.Add("No se encontraron ejercicios compatibles con tus respuestas y las máquinas disponibles.");
        return draft;
    }

    private static List<int> PickGroups(List<int> groups, int day, int days)
    {
        var n = groups.Count;
        if (n == 0) return new List<int>();
        // Pocos días o pocos grupos: cuerpo completo en cada sesión.
        if (days <= 2 || n <= 2) return groups.ToList();
        // Más días: cada grupo se entrena aproximadamente dos veces por semana.
        var perDay = Math.Min(n, Math.Max(1, (int)Math.Ceiling(n * 2.0 / days)));
        var result = new List<int>();
        for (var i = 0; i < perDay; i++) result.Add(groups[(day * perDay + i) % n]);
        return result.Distinct().ToList();
    }

    private static List<string> ParseTerms(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? new List<string>()
            : text.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Where(t => t.Length >= 3).ToList();

    private static string BuildRecommendations(RoutineRequest req)
    {
        var lines = new List<string>
        {
            "Calienta de 5 a 10 minutos antes de cada sesión y termina con estiramientos suaves.",
            "Prioriza la técnica antes que el peso y aumenta la carga de forma gradual.",
            "Respeta los tiempos de descanso indicados y mantén una buena hidratación.",
            req.Objetivo switch
            {
                GoalType.GanarMasaMuscular => "Para ganar masa muscular, busca terminar cada serie cerca del límite sin perder la técnica.",
                GoalType.PerderGrasa => "Combina fuerza y cardio; la constancia semanal importa más que la intensidad de un solo día.",
                GoalType.MejorarResistencia => "Aumenta poco a poco la duración del cardio y reduce los descansos cuando te sientas cómodo.",
                GoalType.AumentarFuerza => "En ejercicios de fuerza pesada, pide supervisión o ayuda y descansa lo suficiente entre series.",
                _ => "Mantén una rutina regular y ajusta la carga según cómo te sientas."
            },
            "Esta rutina es una orientación general y no constituye una prescripción médica."
        };
        if (req.TieneLimitaciones)
            lines.Add("Indicaste molestias o limitaciones físicas: consulta a un profesional de la salud antes de realizar ejercicios que puedan representar un riesgo.");
        return string.Join("\n", lines);
    }
}
