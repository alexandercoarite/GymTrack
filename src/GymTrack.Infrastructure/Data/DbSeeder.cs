using GymTrack.Application.Common;
using GymTrack.Domain.Entities;
using GymTrack.Domain.Enums;
using GymTrack.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GymTrack.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var config = sp.GetRequiredService<IConfiguration>();
        var db = sp.GetRequiredService<AppDbContext>();

        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            logger.LogWarning("No hay migraciones en el proyecto: se crea la base de datos con EnsureCreated. " +
                              "Genera la migración inicial con scripts/crear-migracion (ver README) y borra App_Data/gymtrack.db antes de volver a ejecutar.");
            await db.Database.EnsureCreatedAsync();
        }

        try { await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;"); }
        catch (Exception ex) { logger.LogWarning(ex, "No se pudo activar el modo WAL de SQLite."); }

        await SeedRolesAsync(sp);
        await SeedAdminAsync(sp, config, logger);

        if (config.GetValue("Seed:DemoData", true))
        {
            await SeedCatalogAsync(db);
            await SeedExercisesAsync(db);
        }
        await SeedHoursAsync(db);
    }

    private static async Task SeedRolesAsync(IServiceProvider sp)
    {
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { AppRoles.Admin, AppRoles.Client })
        {
            if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole(role));
        }
    }

    // El administrador inicial se crea solo si se configuran correo y contraseña (user-secrets o variables de entorno).
    private static async Task SeedAdminAsync(IServiceProvider sp, IConfiguration config, ILogger logger)
    {
        var email = config["InitialAdmin:Email"];
        var password = config["InitialAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("No se creó un administrador inicial: configura InitialAdmin:Email e InitialAdmin:Password (ver README).");
            return;
        }

        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        if ((await users.GetUsersInRoleAsync(AppRoles.Admin)).Count > 0) return;

        var existing = await users.FindByEmailAsync(email);
        if (existing != null)
        {
            await users.AddToRoleAsync(existing, AppRoles.Admin);
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = config["InitialAdmin:UserName"] ?? "admin",
            Email = email,
            EmailConfirmed = true,
            Nombre = config["InitialAdmin:Nombre"] ?? "Administrador",
            Apellido = config["InitialAdmin:Apellido"] ?? "GymTrack",
            FechaRegistro = DateTime.UtcNow,
            Activo = true
        };
        var result = await users.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            logger.LogError("No se pudo crear el administrador inicial: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }
        await users.AddToRoleAsync(admin, AppRoles.Admin);
        logger.LogInformation("Administrador inicial creado: {Email}", email);
    }

    private static async Task SeedCatalogAsync(AppDbContext db)
    {
        if (await db.Machines.AnyAsync()) return;

        var categories = new[]
        {
            new MachineCategory { Nombre = "Máquinas de fuerza", Descripcion = "Máquinas guiadas para trabajo muscular seguro." },
            new MachineCategory { Nombre = "Peso libre", Descripcion = "Barras, bancos y mancuernas." },
            new MachineCategory { Nombre = "Cardio", Descripcion = "Equipos cardiovasculares." },
        };
        foreach (var c in categories)
            if (!await db.MachineCategories.AnyAsync(x => x.Nombre == c.Nombre)) db.MachineCategories.Add(c);

        foreach (var g in new[] { "Pecho", "Espalda", "Piernas", "Hombros", "Brazos", "Core", "Cardio" })
            if (!await db.MuscleGroups.AnyAsync(x => x.Nombre == g)) db.MuscleGroups.Add(new MuscleGroup { Nombre = g });
        await db.SaveChangesAsync();

        var cat = await db.MachineCategories.ToDictionaryAsync(c => c.Nombre, c => c.Id);
        var grp = await db.MuscleGroups.ToDictionaryAsync(g => g.Nombre, g => g.Id);

        Machine M(string name, string category, string group, TrainingType type, string desc, string how,
            MachineStatus status = MachineStatus.Disponible) => new()
        {
            Nombre = name, CategoryId = cat[category], MuscleGroupId = grp[group], TipoEntrenamiento = type,
            Descripcion = desc, Instrucciones = how, Estado = status, FechaRegistro = DateTime.UtcNow
        };

        const string F = "Máquinas de fuerza", P = "Peso libre", C = "Cardio";
        var machines = new List<Machine>
        {
            M("Prensa de piernas", F, "Piernas", TrainingType.Fuerza, "Trabaja cuádriceps, glúteos e isquiotibiales.", "Ajusta el asiento, apoya toda la planta del pie y empuja sin bloquear las rodillas."),
            M("Press de pecho en máquina", F, "Pecho", TrainingType.Fuerza, "Press guiado para pectoral y tríceps.", "Ajusta la altura del asiento para que las asas queden a la altura del pecho y empuja de forma controlada."),
            M("Jalón al pecho", F, "Espalda", TrainingType.Fuerza, "Polea alta para dorsales.", "Sujeta la barra más ancha que los hombros y llévala hacia el pecho sin balancear el torso."),
            M("Remo sentado", F, "Espalda", TrainingType.Fuerza, "Remo guiado para espalda media.", "Mantén la espalda recta y junta los omóplatos al tirar."),
            M("Press de hombros en máquina", F, "Hombros", TrainingType.Fuerza, "Press vertical para deltoides.", "Ajusta el asiento y empuja hacia arriba sin arquear la zona lumbar."),
            M("Extensión de cuádriceps", F, "Piernas", TrainingType.Fuerza, "Aislamiento de cuádriceps.", "Alinea la rodilla con el eje de la máquina y extiende sin impulso."),
            M("Curl femoral", F, "Piernas", TrainingType.Fuerza, "Aislamiento de isquiotibiales.", "Ajusta el rodillo sobre los tobillos y flexiona de forma lenta y controlada."),
            M("Polea alta de tríceps", F, "Brazos", TrainingType.Fuerza, "Polea para extensión de tríceps.", "Mantén los codos pegados al cuerpo y extiende por completo."),
            M("Máquina de abdominales", F, "Core", TrainingType.Fuerza, "Flexión de tronco guiada.", "Ajusta la carga y flexiona el tronco usando el abdomen, no los brazos."),
            M("Banco plano con barra", P, "Pecho", TrainingType.Fuerza, "Press de banca con barra.", "Pide ayuda para cargas altas, baja la barra al pecho y empuja de forma controlada."),
            M("Rack de sentadillas", P, "Piernas", TrainingType.Fuerza, "Rack con barra para sentadillas.", "Ajusta los soportes, coloca la barra sobre los trapecios y baja con la espalda neutra."),
            M("Cinta de correr", C, "Cardio", TrainingType.Cardiovascular, "Cinta con inclinación y velocidad ajustables.", "Colócate el cable de seguridad y empieza con velocidad baja."),
            M("Bicicleta estática", C, "Cardio", TrainingType.Cardiovascular, "Bicicleta con resistencia regulable.", "Ajusta la altura del asiento para que la rodilla quede casi extendida al pedalear."),
            M("Elíptica", C, "Cardio", TrainingType.Cardiovascular, "Cardio de bajo impacto.", "Mantén la postura erguida y usa las asas con suavidad.", MachineStatus.EnMantenimiento),
            M("Remo ergómetro", C, "Cardio", TrainingType.Cardiovascular, "Cardio de cuerpo completo.", "Empuja con las piernas, luego inclina el tronco y tira con los brazos."),
        };
        db.Machines.AddRange(machines);
        await db.SaveChangesAsync();

        var elliptical = machines.First(m => m.Nombre == "Elíptica");
        db.MaintenanceRecords.Add(new MaintenanceRecord
        {
            MachineId = elliptical.Id, Descripcion = "Ruido en el pedal derecho (registro de demostración).",
            FechaInicio = DateTime.UtcNow.AddDays(-2), Estado = MaintenanceStatus.Abierto
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedExercisesAsync(AppDbContext db)
    {
        if (await db.Exercises.AnyAsync()) return;
        var grp = await db.MuscleGroups.ToDictionaryAsync(g => g.Nombre, g => g.Id);
        if (grp.Count == 0) return;

        Exercise E(string name, string group, TrainingType type, bool machine, string? reference,
            ExperienceLevel level = ExperienceLevel.Principiante) => new()
        {
            Nombre = name, MuscleGroupId = grp[group], TipoEjercicio = type, UsaMaquina = machine,
            MaquinaReferencia = reference, NivelMinimo = level
        };

        db.Exercises.AddRange(
            E("Press de pecho en máquina", "Pecho", TrainingType.Fuerza, true, "Press de pecho"),
            E("Press de banca con barra", "Pecho", TrainingType.Fuerza, true, "Banco plano", ExperienceLevel.Intermedio),
            E("Flexiones de brazos", "Pecho", TrainingType.Fuerza, false, null),
            E("Jalón al pecho", "Espalda", TrainingType.Fuerza, true, "Jalón"),
            E("Remo sentado en máquina", "Espalda", TrainingType.Fuerza, true, "Remo sentado"),
            E("Remo con mancuerna", "Espalda", TrainingType.Fuerza, false, null),
            E("Prensa de piernas", "Piernas", TrainingType.Fuerza, true, "Prensa"),
            E("Extensión de cuádriceps", "Piernas", TrainingType.Fuerza, true, "Extensión de cuádriceps"),
            E("Curl femoral", "Piernas", TrainingType.Fuerza, true, "Curl femoral"),
            E("Sentadilla con barra", "Piernas", TrainingType.Fuerza, true, "Rack", ExperienceLevel.Intermedio),
            E("Zancadas", "Piernas", TrainingType.Fuerza, false, null),
            E("Press de hombros en máquina", "Hombros", TrainingType.Fuerza, true, "Press de hombros"),
            E("Elevaciones laterales con mancuernas", "Hombros", TrainingType.Fuerza, false, null),
            E("Extensión de tríceps en polea", "Brazos", TrainingType.Fuerza, true, "Polea"),
            E("Curl de bíceps con mancuernas", "Brazos", TrainingType.Fuerza, false, null),
            E("Plancha abdominal", "Core", TrainingType.Fuerza, false, null),
            E("Abdominales en máquina", "Core", TrainingType.Fuerza, true, "abdominales"),
            E("Caminata o carrera en cinta", "Cardio", TrainingType.Cardiovascular, true, "Cinta"),
            E("Bicicleta estática", "Cardio", TrainingType.Cardiovascular, true, "Bicicleta"),
            E("Elíptica", "Cardio", TrainingType.Cardiovascular, true, "Elíptica"),
            E("Remo en ergómetro", "Cardio", TrainingType.Cardiovascular, true, "ergómetro"),
            E("Saltar la cuerda", "Cardio", TrainingType.Cardiovascular, false, null));
        await db.SaveChangesAsync();
    }

    private static async Task SeedHoursAsync(AppDbContext db)
    {
        if (await db.OpeningHours.AnyAsync()) return;
        foreach (var day in DayLabels.WeekOrder)
        {
            var open = day != DayOfWeek.Sunday;
            var close = day == DayOfWeek.Saturday ? new TimeOnly(18, 0) : new TimeOnly(22, 0);
            var start = day == DayOfWeek.Saturday ? new TimeOnly(8, 0) : new TimeOnly(6, 0);
            db.OpeningHours.Add(new OpeningHour
            {
                Dia = day, Abierto = open, Apertura = open ? start : null, Cierre = open ? close : null
            });
        }
        await db.SaveChangesAsync();
    }
}
