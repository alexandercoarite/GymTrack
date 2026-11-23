using GymTrack.Application.Services;
using GymTrack.Domain.Enums;

namespace GymTrack.Tests;

public class RoutineGeneratorTests
{
    private static readonly MachineOption Press = new(1, "Press de pecho");

    private static List<ExerciseCandidate> Catalog() => new()
    {
        new(1, "Press de pecho en máquina", 1, "Pecho", TrainingType.Fuerza, true, ExperienceLevel.Principiante, new[] { Press }),
        new(2, "Flexiones", 1, "Pecho", TrainingType.Fuerza, false, ExperienceLevel.Principiante, Array.Empty<MachineOption>()),
        new(3, "Remo sentado", 2, "Espalda", TrainingType.Fuerza, true, ExperienceLevel.Principiante, Array.Empty<MachineOption>()), // sin máquina disponible
        new(4, "Remo con mancuerna", 2, "Espalda", TrainingType.Fuerza, false, ExperienceLevel.Principiante, Array.Empty<MachineOption>()),
        new(5, "Press de banca con barra", 1, "Pecho", TrainingType.Fuerza, true, ExperienceLevel.Avanzado, new[] { Press }),
        new(6, "Bicicleta estática", 3, "Cardio", TrainingType.Cardiovascular, true, ExperienceLevel.Principiante, new[] { new MachineOption(9, "Bicicleta") }),
    };

    private static RoutineGenerator Generator() => new(new RoutineRules());

    private static RoutineRequest Request(Action<RoutineRequest>? tweak = null)
    {
        var r = new RoutineRequest { Objetivo = GoalType.GanarMasaMuscular, Nivel = ExperienceLevel.Principiante, DiasPorSemana = 3, DuracionSesionMin = 60 };
        tweak?.Invoke(r);
        return r;
    }

    [Fact]
    public void Generates_requested_number_of_days()
    {
        var draft = Generator().Generate(Request(), Catalog());
        Assert.Equal(3, draft.Dias);
    }

    [Fact]
    public void Excludes_exercises_whose_machine_is_unavailable()
    {
        var draft = Generator().Generate(Request(), Catalog());
        Assert.DoesNotContain(draft.Ejercicios, e => e.ExerciseId == 3);
    }

    [Fact]
    public void Excludes_exercises_above_user_level()
    {
        var draft = Generator().Generate(Request(), Catalog());
        Assert.DoesNotContain(draft.Ejercicios, e => e.ExerciseId == 5);
    }

    [Fact]
    public void Excludes_exercises_the_user_wants_to_avoid()
    {
        var draft = Generator().Generate(Request(r => r.EjerciciosAEvitar = "flexiones"), Catalog());
        Assert.DoesNotContain(draft.Ejercicios, e => e.ExerciseId == 2);
    }

    [Fact]
    public void Fat_loss_goal_adds_cardio()
    {
        var draft = Generator().Generate(Request(r => r.Objetivo = GoalType.PerderGrasa), Catalog());
        Assert.Contains(draft.Ejercicios, e => e.ExerciseId == 6);
    }

    [Fact]
    public void Strength_only_preference_without_fat_loss_has_no_cardio()
    {
        var draft = Generator().Generate(Request(r => r.TipoPreferido = TrainingType.Fuerza), Catalog());
        Assert.DoesNotContain(draft.Ejercicios, e => e.ExerciseId == 6);
    }

    [Fact]
    public void Beginner_gets_fewer_sets_than_intermediate()
    {
        var beginner = Generator().Generate(Request(), Catalog());
        var intermediate = Generator().Generate(Request(r => r.Nivel = ExperienceLevel.Intermedio), Catalog());
        Assert.True(beginner.Ejercicios.First(e => e.ExerciseId == 1 || e.ExerciseId == 2).Series
                    < intermediate.Ejercicios.First(e => e.ExerciseId == 1 || e.ExerciseId == 2).Series);
    }

    [Fact]
    public void Limitations_add_professional_advice()
    {
        var draft = Generator().Generate(Request(r => r.TieneLimitaciones = true), Catalog());
        Assert.Contains("profesional de la salud", draft.Recomendaciones);
        Assert.Contains("no constituye una prescripción médica", draft.Recomendaciones);
    }

    [Fact]
    public void Empty_catalog_returns_no_exercises_and_a_warning()
    {
        var draft = Generator().Generate(Request(), new List<ExerciseCandidate>());
        Assert.Empty(draft.Ejercicios);
        Assert.NotEmpty(draft.Advertencias);
    }
}
