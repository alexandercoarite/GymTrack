using GymTrack.Application.Common;
using GymTrack.Application.DTOs;
using GymTrack.Application.Interfaces;
using GymTrack.Application.Services;
using GymTrack.Domain.Entities;
using GymTrack.Domain.Enums;
using GymTrack.Infrastructure.Data;
using GymTrack.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GymTrack.Infrastructure.Services;

public class UserAdminService : IUserAdminService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ICurrentUser _current;
    private readonly IAuditService _audit;

    public UserAdminService(IDbContextFactory<AppDbContext> factory, UserManager<ApplicationUser> users, ICurrentUser current, IAuditService audit)
    {
        _factory = factory;
        _users = users;
        _current = current;
        _audit = audit;
    }

    public async Task<IReadOnlyList<UserListItem>> SearchAsync(string? texto)
    {
        await _current.EnsureAdminAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var adminIds = await AdminIdsAsync(db);

        var query = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            query = query.Where(u => u.Nombre.ToLower().Contains(t) || u.Apellido.ToLower().Contains(t)
                                     || (u.Email != null && u.Email.ToLower().Contains(t))
                                     || (u.UserName != null && u.UserName.ToLower().Contains(t)));
        }

        var users = await query.OrderByDescending(u => u.FechaRegistro).Take(300).ToListAsync();
        return users.Select(u => new UserListItem(
            u.Id, u.NombreCompleto, u.Email ?? string.Empty, u.UserName ?? string.Empty, u.Activo,
            adminIds.Contains(u.Id) ? AppRoles.Admin : AppRoles.Client, u.FechaRegistro)).ToList();
    }

    public async Task<UserDetailDto?> GetAsync(string id)
    {
        await _current.EnsureAdminAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (u == null) return null;
        var adminIds = await AdminIdsAsync(db);
        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == id);
        return new UserDetailDto(u.Id, u.Nombre, u.Apellido, u.Email ?? string.Empty, u.UserName ?? string.Empty,
            u.PhoneNumber, u.FechaNacimiento, u.FechaRegistro, u.Activo,
            adminIds.Contains(u.Id) ? AppRoles.Admin : AppRoles.Client,
            profile?.Objetivo?.Label(), profile?.Nivel?.Label());
    }

    public async Task SetActiveAsync(string id, bool activo)
    {
        await _current.EnsureAdminAsync();
        var me = await _current.RequireUserIdAsync();
        if (me == id && !activo) throw new ServiceException("No puedes desactivar tu propia cuenta.");

        var user = await _users.FindByIdAsync(id) ?? throw new ServiceException("El usuario no existe.");
        user.Activo = activo;
        await _users.UpdateAsync(user);
        await _users.SetLockoutEnabledAsync(user, true);
        await _users.SetLockoutEndDateAsync(user, activo ? null : DateTimeOffset.MaxValue);
        // Invalida las sesiones abiertas del usuario desactivado.
        await _users.UpdateSecurityStampAsync(user);
        await _audit.LogAsync(activo ? "ActivarCuenta" : "DesactivarCuenta", nameof(ApplicationUser), id, user.Email);
    }

    public async Task SetRoleAsync(string id, string rol)
    {
        await _current.EnsureAdminAsync();
        if (rol != AppRoles.Admin && rol != AppRoles.Client) throw new ServiceException("Rol no válido.");
        var me = await _current.RequireUserIdAsync();
        if (me == id) throw new ServiceException("No puedes cambiar tu propio rol.");

        var user = await _users.FindByIdAsync(id) ?? throw new ServiceException("El usuario no existe.");
        var current = await _users.GetRolesAsync(user);
        if (current.Contains(rol) && current.Count == 1) return;

        if (current.Count > 0)
        {
            var removed = await _users.RemoveFromRolesAsync(user, current);
            if (!removed.Succeeded) throw new ServiceException("No se pudo actualizar el rol.");
        }
        var added = await _users.AddToRoleAsync(user, rol);
        if (!added.Succeeded) throw new ServiceException("No se pudo actualizar el rol.");
        await _users.UpdateSecurityStampAsync(user);
        await _audit.LogAsync("CambiarRol", nameof(ApplicationUser), id, rol);
    }

    // El administrador ve el historial y los resultados, pero no las fotografías (solo las ve su propietario).
    public async Task<IReadOnlyList<AssessmentDto>> GetAssessmentsAsync(string userId)
    {
        await _current.EnsureAdminAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.BodyAssessments.AsNoTracking().Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Fecha).Include(a => a.Fotos).ToListAsync();
        await _audit.LogAsync("ConsultarEvaluaciones", nameof(BodyAssessment), userId);
        return rows.Select(a => new AssessmentDto(a.Id, a.Fecha, a.Estado, a.ResultadoAnalisis, a.Indicadores,
            a.Observaciones, Array.Empty<PhotoDto>())).ToList();
    }

    private static async Task<HashSet<string>> AdminIdsAsync(AppDbContext db)
    {
        var roleId = await db.Roles.Where(r => r.Name == AppRoles.Admin).Select(r => r.Id).FirstOrDefaultAsync();
        if (roleId == null) return new HashSet<string>();
        var ids = await db.UserRoles.Where(ur => ur.RoleId == roleId).Select(ur => ur.UserId).ToListAsync();
        return ids.ToHashSet();
    }
}

public class ProfileService : IProfileService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ICurrentUser _current;

    public ProfileService(UserManager<ApplicationUser> users, ICurrentUser current)
    {
        _users = users;
        _current = current;
    }

    public async Task<ProfileModel> GetAsync()
    {
        var user = await GetUserAsync();
        return new ProfileModel
        {
            Email = user.Email ?? string.Empty, UserName = user.UserName ?? string.Empty,
            Nombre = user.Nombre, Apellido = user.Apellido, Telefono = user.PhoneNumber,
            FechaNacimiento = user.FechaNacimiento
        };
    }

    public async Task UpdateAsync(ProfileModel model)
    {
        ModelValidator.ValidateOrThrow(model);
        if (model.FechaNacimiento is { } d && (d > DateTime.Today || d < DateTime.Today.AddYears(-120)))
            throw new ServiceException("La fecha de nacimiento no es válida.");
        var user = await GetUserAsync();
        user.Nombre = model.Nombre.Trim();
        user.Apellido = model.Apellido.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(model.Telefono) ? null : model.Telefono.Trim();
        user.FechaNacimiento = model.FechaNacimiento;
        var result = await _users.UpdateAsync(user);
        if (!result.Succeeded) throw new ServiceException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    public async Task ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var user = await GetUserAsync();
        var result = await _users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded) throw new ServiceException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    private async Task<ApplicationUser> GetUserAsync()
    {
        var id = await _current.RequireUserIdAsync();
        return await _users.FindByIdAsync(id) ?? throw new ServiceException("No se encontró tu cuenta.");
    }
}

public class QuestionnaireService : IQuestionnaireService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ICurrentUser _current;

    public QuestionnaireService(IDbContextFactory<AppDbContext> factory, ICurrentUser current)
    {
        _factory = factory;
        _current = current;
    }

    public async Task<QuestionnaireModel?> GetLatestAsync()
    {
        var userId = await _current.RequireUserIdAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var q = await db.QuestionnaireResponses.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Fecha).FirstOrDefaultAsync();
        if (q == null) return null;
        return new QuestionnaireModel
        {
            Objetivo = q.Objetivo, Nivel = q.Nivel, DiasPorSemana = q.DiasPorSemana,
            DuracionSesionMin = q.DuracionSesionMin, TipoPreferido = q.TipoPreferido,
            GruposMusculares = ParseIds(q.GruposMuscularesIds),
            EjerciciosAEvitar = q.EjerciciosAEvitar, Limitaciones = q.Limitaciones
        };
    }

    public async Task SaveAsync(QuestionnaireModel model)
    {
        var userId = await _current.RequireUserIdAsync();
        ModelValidator.ValidateOrThrow(model);
        await using var db = await _factory.CreateDbContextAsync();

        var validGroups = await db.MuscleGroups.Select(g => g.Id).ToListAsync();
        var groups = model.GruposMusculares.Distinct().Where(id => validGroups.Contains(id)).ToList();

        // Se conserva el historial: cada envío crea una respuesta nueva.
        db.QuestionnaireResponses.Add(new QuestionnaireResponse
        {
            UserId = userId, Objetivo = model.Objetivo, Nivel = model.Nivel, DiasPorSemana = model.DiasPorSemana,
            DuracionSesionMin = model.DuracionSesionMin, TipoPreferido = model.TipoPreferido,
            GruposMuscularesIds = string.Join(",", groups),
            EjerciciosAEvitar = string.IsNullOrWhiteSpace(model.EjerciciosAEvitar) ? null : model.EjerciciosAEvitar.Trim(),
            Limitaciones = string.IsNullOrWhiteSpace(model.Limitaciones) ? null : model.Limitaciones.Trim(),
            Fecha = DateTime.UtcNow
        });

        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null)
        {
            profile = new UserProfile { UserId = userId };
            db.UserProfiles.Add(profile);
        }
        profile.Objetivo = model.Objetivo;
        profile.Nivel = model.Nivel;
        profile.PreferenciaEntrenamiento = model.TipoPreferido;
        await db.SaveChangesAsync();
    }

    internal static List<int> ParseIds(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? new List<int>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Select(s => int.TryParse(s, out var n) ? n : 0).Where(n => n > 0).ToList();
}

public class RoutineService : IRoutineService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ICurrentUser _current;
    private readonly IRoutineGenerator _generator;

    public RoutineService(IDbContextFactory<AppDbContext> factory, ICurrentUser current, IRoutineGenerator generator)
    {
        _factory = factory;
        _current = current;
        _generator = generator;
    }

    public async Task<RoutineDto?> GetActiveAsync()
    {
        var userId = await _current.RequireUserIdAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var plan = await PlanQuery(db).Where(p => p.UserId == userId && p.Estado == PlanStatus.Activo)
            .OrderByDescending(p => p.FechaCreacion).FirstOrDefaultAsync();
        return plan == null ? null : ToDto(plan);
    }

    public async Task<IReadOnlyList<RoutineDto>> GetHistoryAsync()
    {
        var userId = await _current.RequireUserIdAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var plans = await PlanQuery(db).Where(p => p.UserId == userId)
            .OrderByDescending(p => p.FechaCreacion).Take(20).ToListAsync();
        return plans.Select(ToDto).ToList();
    }

    public async Task<RoutineDto> GenerateAsync()
    {
        var userId = await _current.RequireUserIdAsync();
        await using var db = await _factory.CreateDbContextAsync();

        var q = await db.QuestionnaireResponses.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Fecha).FirstOrDefaultAsync()
            ?? throw new ServiceException("Primero completa el cuestionario de personalización.");

        var availableMachines = await db.Machines.AsNoTracking()
            .Where(m => m.Estado == MachineStatus.Disponible).ToListAsync();
        var exercises = await db.Exercises.AsNoTracking().Include(e => e.MuscleGroup).ToListAsync();

        var candidates = exercises.Select(e =>
        {
            IReadOnlyList<MachineOption> options = e.UsaMaquina
                ? availableMachines
                    .Where(m => m.MuscleGroupId == e.MuscleGroupId && m.TipoEntrenamiento == e.TipoEjercicio)
                    .Where(m => string.IsNullOrWhiteSpace(e.MaquinaReferencia)
                                || m.Nombre.Contains(e.MaquinaReferencia, StringComparison.OrdinalIgnoreCase))
                    .Select(m => new MachineOption(m.Id, m.Nombre)).ToList()
                : Array.Empty<MachineOption>();
            return new ExerciseCandidate(e.Id, e.Nombre, e.MuscleGroupId, e.MuscleGroup?.Nombre ?? string.Empty,
                e.TipoEjercicio, e.UsaMaquina, e.NivelMinimo, options);
        }).ToList();

        var draft = _generator.Generate(new RoutineRequest
        {
            Objetivo = q.Objetivo, Nivel = q.Nivel, DiasPorSemana = q.DiasPorSemana,
            DuracionSesionMin = q.DuracionSesionMin, TipoPreferido = q.TipoPreferido,
            GruposMuscularesIds = QuestionnaireService.ParseIds(q.GruposMuscularesIds),
            EjerciciosAEvitar = q.EjerciciosAEvitar,
            TieneLimitaciones = !string.IsNullOrWhiteSpace(q.Limitaciones)
        }, candidates);

        if (draft.Ejercicios.Count == 0)
            throw new ServiceException(string.Join(" ", draft.Advertencias));

        // Las rutinas anteriores se conservan como historial.
        var previous = await db.TrainingPlans.Where(p => p.UserId == userId && p.Estado == PlanStatus.Activo).ToListAsync();
        foreach (var p in previous)
        {
            p.Estado = PlanStatus.Finalizado;
            p.FechaFin = DateTime.UtcNow;
        }

        var recommendations = draft.Recomendaciones;
        if (draft.Advertencias.Count > 0)
            recommendations += "\n" + string.Join("\n", draft.Advertencias.Select(a => "Aviso: " + a));

        var plan = new TrainingPlan
        {
            UserId = userId, Nombre = draft.Nombre, Objetivo = q.Objetivo, Nivel = q.Nivel,
            DiasEntrenamiento = draft.Dias, TiempoEstimadoMin = draft.TiempoEstimadoMin,
            GruposMusculares = draft.GruposMusculares, Recomendaciones = recommendations,
            FechaCreacion = DateTime.UtcNow, Estado = PlanStatus.Activo,
            Ejercicios = draft.Ejercicios.Select(e => new PlanExercise
            {
                ExerciseId = e.ExerciseId, MachineId = e.MachineId, Dia = e.Dia, Orden = e.Orden,
                Series = e.Series, Repeticiones = e.Repeticiones, DescansoSeg = e.DescansoSeg
            }).ToList()
        };
        db.TrainingPlans.Add(plan);
        await db.SaveChangesAsync();

        var saved = await PlanQuery(db).FirstAsync(p => p.Id == plan.Id);
        return ToDto(saved);
    }

    internal static IQueryable<TrainingPlan> PlanQuery(AppDbContext db) =>
        db.TrainingPlans.AsNoTracking()
            .Include(p => p.Ejercicios).ThenInclude(e => e.Exercise).ThenInclude(x => x!.MuscleGroup)
            .Include(p => p.Ejercicios).ThenInclude(e => e.Machine);

    internal static RoutineDto ToDto(TrainingPlan p) => new(
        p.Id, p.Nombre, p.Objetivo, p.Nivel, p.DiasEntrenamiento, p.TiempoEstimadoMin, p.GruposMusculares,
        p.Recomendaciones, p.FechaCreacion, p.FechaFin, p.Estado,
        p.Ejercicios.OrderBy(e => e.Dia).ThenBy(e => e.Orden)
            .Select(e => new PlanExerciseDto(e.Dia, e.Orden, e.Exercise?.Nombre ?? "Ejercicio",
                e.Exercise?.MuscleGroup?.Nombre ?? string.Empty, e.Machine?.Nombre,
                e.Series, e.Repeticiones, e.DescansoSeg)).ToList());
}

public class BodyAssessmentService : IBodyAssessmentService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ICurrentUser _current;
    private readonly IPhotoStorage _storage;
    private readonly IBodyAnalysisService _analysis;

    public BodyAssessmentService(IDbContextFactory<AppDbContext> factory, ICurrentUser current,
        IPhotoStorage storage, IBodyAnalysisService analysis)
    {
        _factory = factory;
        _current = current;
        _storage = storage;
        _analysis = analysis;
    }

    public bool AnalysisAvailable => _analysis.IsConfigured;

    public async Task<IReadOnlyList<AssessmentDto>> ListAsync()
    {
        var userId = await _current.RequireUserIdAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.BodyAssessments.AsNoTracking().Include(a => a.Fotos)
            .Where(a => a.UserId == userId).OrderByDescending(a => a.Fecha).ToListAsync();
        return rows.Select(a => new AssessmentDto(a.Id, a.Fecha, a.Estado, a.ResultadoAnalisis, a.Indicadores,
            a.Observaciones, a.Fotos.Select(f => new PhotoDto(f.Id, f.Tipo, f.Fecha)).ToList())).ToList();
    }

    public async Task<int> CreateAsync(AssessmentCreateModel model)
    {
        var userId = await _current.RequireUserIdAsync();
        ModelValidator.ValidateOrThrow(model);
        if (!model.Consentimiento)
            throw new ServiceException("Necesitas dar tu consentimiento explícito para usar o guardar fotografías.");
        if (model.Fotos.Count == 0) throw new ServiceException("Agrega al menos una fotografía.");
        if (model.Fotos.Count > 4) throw new ServiceException("Puedes subir como máximo 4 fotografías por evaluación.");
        if (!_analysis.IsConfigured && !model.ConservarFotos)
            throw new ServiceException("Sin un servicio de análisis configurado, las fotografías solo pueden guardarse como fotografías de progreso. Marca la opción de conservarlas o cancela la evaluación.");

        var prepared = new List<(PhotoUpload Upload, string Extension)>();
        foreach (var photo in model.Fotos)
            prepared.Add((photo, ImageValidator.ValidateOrThrow(photo.Contenido, ImageValidator.MaxPhotoBytes)));

        var assessment = new BodyAssessment
        {
            UserId = userId, Fecha = DateTime.UtcNow, Observaciones = model.Observaciones?.Trim(),
            Estado = AssessmentStatus.AnalisisNoDisponible
        };

        if (_analysis.IsConfigured)
        {
            var inputs = prepared.Select(p => new BodyPhotoInput(p.Upload.Tipo, p.Upload.Contenido,
                ImageValidator.ContentTypeFor(p.Extension))).ToList();
            var result = await _analysis.AnalyzeAsync(inputs);
            assessment.Estado = result.Success ? AssessmentStatus.Completado : AssessmentStatus.Fallido;
            assessment.ResultadoAnalisis = result.Success ? result.Resumen : result.Error;
            assessment.Indicadores = result.IndicadoresJson;
        }

        var savedKeys = new List<string>();
        try
        {
            if (model.ConservarFotos)
            {
                var consentDate = DateTime.UtcNow;
                foreach (var (upload, ext) in prepared)
                {
                    var key = await _storage.SaveAsync(userId, upload.Contenido, ext);
                    savedKeys.Add(key);
                    assessment.Fotos.Add(new BodyPhoto
                    {
                        RutaPrivada = key, ContentType = ImageValidator.ContentTypeFor(ext), Tipo = upload.Tipo,
                        Fecha = consentDate, ConsentimientoFecha = consentDate
                    });
                }
            }

            await using var db = await _factory.CreateDbContextAsync();
            db.BodyAssessments.Add(assessment);
            await db.SaveChangesAsync();
            return assessment.Id;
        }
        catch
        {
            foreach (var key in savedKeys) _storage.Delete(key);
            throw;
        }
    }

    public async Task DeleteAsync(int assessmentId)
    {
        var userId = await _current.RequireUserIdAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var assessment = await db.BodyAssessments.Include(a => a.Fotos)
            .FirstOrDefaultAsync(a => a.Id == assessmentId && a.UserId == userId)
            ?? throw new ServiceException("La evaluación no existe.");
        foreach (var photo in assessment.Fotos) _storage.Delete(photo.RutaPrivada);
        db.BodyAssessments.Remove(assessment);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAllAsync()
    {
        var userId = await _current.RequireUserIdAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var all = await db.BodyAssessments.Include(a => a.Fotos).Where(a => a.UserId == userId).ToListAsync();
        foreach (var photo in all.SelectMany(a => a.Fotos)) _storage.Delete(photo.RutaPrivada);
        db.BodyAssessments.RemoveRange(all);
        await db.SaveChangesAsync();
    }

    public async Task<PhotoContent?> OpenPhotoAsync(int photoId, string requestingUserId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var photo = await db.BodyPhotos.AsNoTracking().Include(p => p.BodyAssessment)
            .FirstOrDefaultAsync(p => p.Id == photoId);
        if (photo?.BodyAssessment == null || photo.BodyAssessment.UserId != requestingUserId) return null;
        try { return new PhotoContent(_storage.OpenRead(photo.RutaPrivada), photo.ContentType); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }
}

public class ProgressService : IProgressService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ICurrentUser _current;

    public ProgressService(IDbContextFactory<AppDbContext> factory, ICurrentUser current)
    {
        _factory = factory;
        _current = current;
    }

    internal static ProgressDto ToDto(ProgressRecord r) => new(r.Id, r.Fecha, r.PesoKg, r.CinturaCm, r.PechoCm,
        r.CaderaCm, r.BrazoCm, r.MusloCm, r.ObjetivoAlcanzado, r.Observaciones);

    public async Task<IReadOnlyList<ProgressDto>> ListAsync()
    {
        var userId = await _current.RequireUserIdAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.ProgressRecords.AsNoTracking().Where(r => r.UserId == userId)
            .OrderBy(r => r.Fecha).ThenBy(r => r.Id).ToListAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<int> AddAsync(ProgressModel model)
    {
        var userId = await _current.RequireUserIdAsync();
        ModelValidator.ValidateOrThrow(model);
        if (model.Fecha.Date > DateTime.Today.AddDays(1)) throw new ServiceException("La fecha no puede ser futura.");

        // Siempre se agrega un registro nuevo: el historial anterior nunca se reemplaza.
        await using var db = await _factory.CreateDbContextAsync();
        var record = new ProgressRecord
        {
            UserId = userId, Fecha = model.Fecha.Date, PesoKg = model.PesoKg, CinturaCm = model.CinturaCm,
            PechoCm = model.PechoCm, CaderaCm = model.CaderaCm, BrazoCm = model.BrazoCm, MusloCm = model.MusloCm,
            ObjetivoAlcanzado = string.IsNullOrWhiteSpace(model.ObjetivoAlcanzado) ? null : model.ObjetivoAlcanzado.Trim(),
            Observaciones = string.IsNullOrWhiteSpace(model.Observaciones) ? null : model.Observaciones.Trim()
        };
        db.ProgressRecords.Add(record);
        await db.SaveChangesAsync();
        return record.Id;
    }

    public async Task DeleteAsync(int id)
    {
        var userId = await _current.RequireUserIdAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var record = await db.ProgressRecords.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId)
                     ?? throw new ServiceException("El registro no existe.");
        db.ProgressRecords.Remove(record);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAllAsync()
    {
        var userId = await _current.RequireUserIdAsync();
        await using var db = await _factory.CreateDbContextAsync();
        await db.ProgressRecords.Where(r => r.UserId == userId).ExecuteDeleteAsync();
    }
}

public class DashboardService : IDashboardService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ICurrentUser _current;
    private readonly IRoutineService _routines;
    private readonly IGymClock _clock;

    public DashboardService(IDbContextFactory<AppDbContext> factory, ICurrentUser current, IRoutineService routines, IGymClock clock)
    {
        _factory = factory;
        _current = current;
        _routines = routines;
        _clock = clock;
    }

    public async Task<AdminDashboardDto> GetAdminAsync()
    {
        await _current.EnsureAdminAsync();
        await using var db = await _factory.CreateDbContextAsync();

        var adminRoleId = await db.Roles.Where(r => r.Name == AppRoles.Admin).Select(r => r.Id).FirstOrDefaultAsync();
        var adminIds = adminRoleId == null
            ? new List<string>()
            : await db.UserRoles.Where(ur => ur.RoleId == adminRoleId).Select(ur => ur.UserId).ToListAsync();

        var clients = db.Users.AsNoTracking().Where(u => !adminIds.Contains(u.Id));
        var clientCount = await clients.CountAsync();
        var since = DateTime.UtcNow.AddDays(-30);
        var recentCount = await clients.CountAsync(u => u.FechaRegistro >= since);
        var recent = await clients.OrderByDescending(u => u.FechaRegistro).Take(6).ToListAsync();

        var total = await db.Machines.CountAsync();
        var available = await db.Machines.CountAsync(m => m.Estado == MachineStatus.Disponible);
        var maintenance = await db.Machines.CountAsync(m => m.Estado == MachineStatus.EnMantenimiento);
        var outOfService = await db.Machines.CountAsync(m => m.Estado == MachineStatus.FueraDeServicio);
        var openMaintenance = await db.MaintenanceRecords.CountAsync(r => r.Estado == MaintenanceStatus.Abierto);

        return new AdminDashboardDto(clientCount, total, available, maintenance, outOfService, recentCount,
            recent.Select(u => new RecentUserDto(u.NombreCompleto, u.Email ?? string.Empty, u.FechaRegistro)).ToList(),
            openMaintenance);
    }

    public async Task<ClientDashboardDto> GetClientAsync()
    {
        var userId = await _current.RequireUserIdAsync();
        await using var db = await _factory.CreateDbContextAsync();

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
        var lastAssessment = await db.BodyAssessments.AsNoTracking().Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Fecha).Select(a => (DateTime?)a.Fecha).FirstOrDefaultAsync();
        var hasQuestionnaire = await db.QuestionnaireResponses.AnyAsync(q => q.UserId == userId);
        var progress = await db.ProgressRecords.AsNoTracking().Where(r => r.UserId == userId)
            .OrderByDescending(r => r.Fecha).ThenByDescending(r => r.Id).Take(12).ToListAsync();
        progress.Reverse();

        var routine = await _routines.GetActiveAsync();
        return new ClientDashboardDto(user?.Nombre ?? "Atleta", profile?.Objetivo?.Label(), profile?.Nivel?.Label(),
            lastAssessment, hasQuestionnaire, routine, progress.Select(ProgressService.ToDto).ToList());
    }
}
