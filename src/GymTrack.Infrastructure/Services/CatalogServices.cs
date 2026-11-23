using GymTrack.Application.Common;
using GymTrack.Application.DTOs;
using GymTrack.Application.Interfaces;
using GymTrack.Domain.Entities;
using GymTrack.Domain.Enums;
using GymTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymTrack.Infrastructure.Services;

public class MachineService : IMachineService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ICurrentUser _current;
    private readonly IAuditService _audit;
    private readonly IMachineImageStorage _images;

    public MachineService(IDbContextFactory<AppDbContext> factory, ICurrentUser current, IAuditService audit, IMachineImageStorage images)
    {
        _factory = factory;
        _current = current;
        _audit = audit;
        _images = images;
    }

    private static MachineDto ToDto(Machine m) => new(
        m.Id, m.Nombre, m.Descripcion, m.Instrucciones, m.CategoryId, m.Category?.Nombre ?? string.Empty,
        m.MuscleGroupId, m.MuscleGroup?.Nombre ?? string.Empty, m.TipoEntrenamiento, m.Estado,
        m.ImagenRuta, m.FechaRegistro, m.Version);

    // Consulta pública (catálogo y página principal): no requiere permisos de administrador.
    public async Task<IReadOnlyList<MachineDto>> SearchAsync(MachineFilter? filter = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var query = db.Machines.AsNoTracking().Include(m => m.Category).Include(m => m.MuscleGroup).AsQueryable();

        if (filter != null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Texto))
            {
                var text = filter.Texto.Trim().ToLower();
                query = query.Where(m => m.Nombre.ToLower().Contains(text));
            }
            if (filter.MuscleGroupId.HasValue) query = query.Where(m => m.MuscleGroupId == filter.MuscleGroupId);
            if (filter.CategoryId.HasValue) query = query.Where(m => m.CategoryId == filter.CategoryId);
            if (filter.Tipo.HasValue) query = query.Where(m => m.TipoEntrenamiento == filter.Tipo);
            if (filter.Estado.HasValue) query = query.Where(m => m.Estado == filter.Estado);
        }

        var list = await query.OrderBy(m => m.Nombre).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<MachineDto?> GetAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var m = await db.Machines.AsNoTracking().Include(x => x.Category).Include(x => x.MuscleGroup)
            .FirstOrDefaultAsync(x => x.Id == id);
        return m == null ? null : ToDto(m);
    }

    public async Task<int> CreateAsync(MachineEditModel model)
    {
        await _current.EnsureAdminAsync();
        ModelValidator.ValidateOrThrow(model);
        await using var db = await _factory.CreateDbContextAsync();
        await EnsureReferencesAsync(db, model);
        await EnsureUniqueNameAsync(db, model.Nombre, null);

        var machine = new Machine
        {
            Nombre = model.Nombre.Trim(),
            Descripcion = model.Descripcion.Trim(),
            Instrucciones = model.Instrucciones?.Trim(),
            CategoryId = model.CategoryId,
            MuscleGroupId = model.MuscleGroupId,
            TipoEntrenamiento = model.Tipo,
            ImagenRuta = model.ImagenRuta,
            Estado = MachineStatus.Disponible,
            FechaRegistro = DateTime.UtcNow
        };
        db.Machines.Add(machine);
        await db.SaveChangesAsync();
        await _audit.LogAsync("Crear", nameof(Machine), machine.Id.ToString(), machine.Nombre);
        return machine.Id;
    }

    public async Task UpdateAsync(MachineEditModel model)
    {
        await _current.EnsureAdminAsync();
        ModelValidator.ValidateOrThrow(model);
        await using var db = await _factory.CreateDbContextAsync();
        var machine = await db.Machines.FirstOrDefaultAsync(m => m.Id == model.Id)
                      ?? throw new ServiceException("La máquina no existe.");
        await EnsureReferencesAsync(db, model);
        await EnsureUniqueNameAsync(db, model.Nombre, model.Id);

        var oldImage = machine.ImagenRuta;
        db.Entry(machine).Property(m => m.Version).OriginalValue = model.Version;
        machine.Nombre = model.Nombre.Trim();
        machine.Descripcion = model.Descripcion.Trim();
        machine.Instrucciones = model.Instrucciones?.Trim();
        machine.CategoryId = model.CategoryId;
        machine.MuscleGroupId = model.MuscleGroupId;
        machine.TipoEntrenamiento = model.Tipo;
        machine.ImagenRuta = model.ImagenRuta;

        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ServiceException("La máquina fue modificada por otra persona. Recarga la página e inténtalo de nuevo.");
        }

        if (oldImage != model.ImagenRuta) _images.Delete(oldImage);
        await _audit.LogAsync("Editar", nameof(Machine), machine.Id.ToString(), machine.Nombre);
    }

    public async Task DeleteAsync(int id)
    {
        await _current.EnsureAdminAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var machine = await db.Machines.FirstOrDefaultAsync(m => m.Id == id)
                      ?? throw new ServiceException("La máquina no existe.");
        var image = machine.ImagenRuta;
        var name = machine.Nombre;
        // Las rutinas que la usaban conservan el ejercicio (MachineId pasa a NULL); el mantenimiento se elimina en cascada.
        db.Machines.Remove(machine);
        await db.SaveChangesAsync();
        _images.Delete(image);
        await _audit.LogAsync("Eliminar", nameof(Machine), id.ToString(), name);
    }

    public async Task ChangeStatusAsync(int id, MachineStatus status)
    {
        await _current.EnsureAdminAsync();
        if (status == MachineStatus.EnMantenimiento)
            throw new ServiceException("Para poner una máquina en mantenimiento usa el módulo de mantenimiento y describe el problema.");

        await using var db = await _factory.CreateDbContextAsync();
        var machine = await db.Machines.FirstOrDefaultAsync(m => m.Id == id)
                      ?? throw new ServiceException("La máquina no existe.");
        machine.Estado = status;

        if (status == MachineStatus.Disponible)
        {
            var open = await db.MaintenanceRecords
                .Where(r => r.MachineId == id && r.Estado == MaintenanceStatus.Abierto).ToListAsync();
            foreach (var r in open)
            {
                r.Estado = MaintenanceStatus.Resuelto;
                r.FechaFin = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();
        await _audit.LogAsync("CambiarEstado", nameof(Machine), id.ToString(), status.ToString());
    }

    private static async Task EnsureReferencesAsync(AppDbContext db, MachineEditModel model)
    {
        if (!await db.MachineCategories.AnyAsync(c => c.Id == model.CategoryId))
            throw new ServiceException("La categoría seleccionada no existe.");
        if (!await db.MuscleGroups.AnyAsync(g => g.Id == model.MuscleGroupId))
            throw new ServiceException("El grupo muscular seleccionado no existe.");
    }

    private static async Task EnsureUniqueNameAsync(AppDbContext db, string name, int? ignoreId)
    {
        var normalized = name.Trim().ToLower();
        var exists = await db.Machines.AnyAsync(m => m.Nombre.ToLower() == normalized && m.Id != (ignoreId ?? 0));
        if (exists) throw new ServiceException("Ya existe una máquina con ese nombre.");
    }
}

public class LookupService : ILookupService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ICurrentUser _current;
    private readonly IAuditService _audit;

    public LookupService(IDbContextFactory<AppDbContext> factory, ICurrentUser current, IAuditService audit)
    {
        _factory = factory;
        _current = current;
        _audit = audit;
    }

    public async Task<IReadOnlyList<LookupDto>> GetCategoriesAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.MachineCategories.AsNoTracking().OrderBy(c => c.Nombre)
            .Select(c => new { c.Id, c.Nombre, c.Descripcion, Usos = c.Machines.Count }).ToListAsync();
        return rows.Select(r => new LookupDto(r.Id, r.Nombre, r.Descripcion, r.Usos)).ToList();
    }

    public async Task<IReadOnlyList<LookupDto>> GetMuscleGroupsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.MuscleGroups.AsNoTracking().OrderBy(g => g.Nombre)
            .Select(g => new { g.Id, g.Nombre, Usos = g.Machines.Count + g.Exercises.Count }).ToListAsync();
        return rows.Select(r => new LookupDto(r.Id, r.Nombre, null, r.Usos)).ToList();
    }

    public async Task<int> SaveCategoryAsync(LookupEditModel model)
    {
        await _current.EnsureAdminAsync();
        ModelValidator.ValidateOrThrow(model);
        await using var db = await _factory.CreateDbContextAsync();
        var name = model.Nombre.Trim();
        var lower = name.ToLower();
        if (await db.MachineCategories.AnyAsync(c => c.Nombre.ToLower() == lower && c.Id != model.Id))
            throw new ServiceException("Ya existe una categoría con ese nombre.");

        MachineCategory entity;
        if (model.Id == 0)
        {
            entity = new MachineCategory();
            db.MachineCategories.Add(entity);
        }
        else
        {
            entity = await db.MachineCategories.FirstOrDefaultAsync(c => c.Id == model.Id)
                     ?? throw new ServiceException("La categoría no existe.");
        }
        entity.Nombre = name;
        entity.Descripcion = model.Descripcion?.Trim();
        await db.SaveChangesAsync();
        await _audit.LogAsync(model.Id == 0 ? "Crear" : "Editar", nameof(MachineCategory), entity.Id.ToString(), name);
        return entity.Id;
    }

    public async Task DeleteCategoryAsync(int id)
    {
        await _current.EnsureAdminAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await db.MachineCategories.FirstOrDefaultAsync(c => c.Id == id)
                     ?? throw new ServiceException("La categoría no existe.");
        var count = await db.Machines.CountAsync(m => m.CategoryId == id);
        if (count > 0) throw new ServiceException($"No se puede eliminar: tiene {count} máquina(s) asociada(s).");
        db.MachineCategories.Remove(entity);
        await db.SaveChangesAsync();
        await _audit.LogAsync("Eliminar", nameof(MachineCategory), id.ToString(), entity.Nombre);
    }

    public async Task<int> SaveMuscleGroupAsync(LookupEditModel model)
    {
        await _current.EnsureAdminAsync();
        ModelValidator.ValidateOrThrow(model);
        await using var db = await _factory.CreateDbContextAsync();
        var name = model.Nombre.Trim();
        var lower = name.ToLower();
        if (await db.MuscleGroups.AnyAsync(g => g.Nombre.ToLower() == lower && g.Id != model.Id))
            throw new ServiceException("Ya existe un grupo muscular con ese nombre.");

        MuscleGroup entity;
        if (model.Id == 0)
        {
            entity = new MuscleGroup();
            db.MuscleGroups.Add(entity);
        }
        else
        {
            entity = await db.MuscleGroups.FirstOrDefaultAsync(g => g.Id == model.Id)
                     ?? throw new ServiceException("El grupo muscular no existe.");
        }
        entity.Nombre = name;
        await db.SaveChangesAsync();
        await _audit.LogAsync(model.Id == 0 ? "Crear" : "Editar", nameof(MuscleGroup), entity.Id.ToString(), name);
        return entity.Id;
    }

    public async Task DeleteMuscleGroupAsync(int id)
    {
        await _current.EnsureAdminAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await db.MuscleGroups.FirstOrDefaultAsync(g => g.Id == id)
                     ?? throw new ServiceException("El grupo muscular no existe.");
        var machines = await db.Machines.CountAsync(m => m.MuscleGroupId == id);
        var exercises = await db.Exercises.CountAsync(e => e.MuscleGroupId == id);
        if (machines + exercises > 0)
            throw new ServiceException($"No se puede eliminar: tiene {machines} máquina(s) y {exercises} ejercicio(s) asociados.");
        db.MuscleGroups.Remove(entity);
        await db.SaveChangesAsync();
        await _audit.LogAsync("Eliminar", nameof(MuscleGroup), id.ToString(), entity.Nombre);
    }
}

public class MaintenanceService : IMaintenanceService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ICurrentUser _current;
    private readonly IAuditService _audit;

    public MaintenanceService(IDbContextFactory<AppDbContext> factory, ICurrentUser current, IAuditService audit)
    {
        _factory = factory;
        _current = current;
        _audit = audit;
    }

    public async Task<IReadOnlyList<MaintenanceDto>> ListAsync(bool soloAbiertos)
    {
        await _current.EnsureAdminAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var query = db.MaintenanceRecords.AsNoTracking().Include(r => r.Machine).AsQueryable();
        if (soloAbiertos) query = query.Where(r => r.Estado == MaintenanceStatus.Abierto);
        var rows = await query.OrderByDescending(r => r.FechaInicio).Take(200).ToListAsync();
        return rows.Select(r => new MaintenanceDto(r.Id, r.MachineId, r.Machine?.Nombre ?? "(eliminada)",
            r.Descripcion, r.FechaInicio, r.FechaFin, r.Estado, r.Observaciones)).ToList();
    }

    public async Task<int> RegisterAsync(MaintenanceCreateModel model)
    {
        await _current.EnsureAdminAsync();
        ModelValidator.ValidateOrThrow(model);
        await using var db = await _factory.CreateDbContextAsync();
        var machine = await db.Machines.FirstOrDefaultAsync(m => m.Id == model.MachineId)
                      ?? throw new ServiceException("La máquina no existe.");

        var record = new MaintenanceRecord
        {
            MachineId = machine.Id,
            Descripcion = model.Descripcion.Trim(),
            FechaInicio = DateTime.UtcNow,
            Estado = MaintenanceStatus.Abierto
        };
        db.MaintenanceRecords.Add(record);
        machine.Estado = MachineStatus.EnMantenimiento;
        await db.SaveChangesAsync();
        await _audit.LogAsync("RegistrarMantenimiento", nameof(MaintenanceRecord), record.Id.ToString(), machine.Nombre);
        return record.Id;
    }

    public async Task ResolveAsync(int recordId, string? observaciones)
    {
        await _current.EnsureAdminAsync();
        if (observaciones is { Length: > 1000 })
            throw new ServiceException("Las observaciones no pueden superar 1000 caracteres.");

        await using var db = await _factory.CreateDbContextAsync();
        var record = await db.MaintenanceRecords.Include(r => r.Machine).FirstOrDefaultAsync(r => r.Id == recordId)
                     ?? throw new ServiceException("El registro de mantenimiento no existe.");
        if (record.Estado == MaintenanceStatus.Resuelto)
            throw new ServiceException("Este mantenimiento ya fue cerrado.");

        record.Estado = MaintenanceStatus.Resuelto;
        record.FechaFin = DateTime.UtcNow;
        record.Observaciones = observaciones?.Trim();
        await db.SaveChangesAsync();

        var stillOpen = await db.MaintenanceRecords.AnyAsync(r =>
            r.MachineId == record.MachineId && r.Estado == MaintenanceStatus.Abierto);
        if (!stillOpen && record.Machine != null && record.Machine.Estado == MachineStatus.EnMantenimiento)
        {
            record.Machine.Estado = MachineStatus.Disponible;
            await db.SaveChangesAsync();
        }

        await _audit.LogAsync("CerrarMantenimiento", nameof(MaintenanceRecord), recordId.ToString(), record.Machine?.Nombre);
    }
}

public class OpeningHourService : IOpeningHourService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ICurrentUser _current;
    private readonly IAuditService _audit;
    private readonly IGymClock _clock;

    public OpeningHourService(IDbContextFactory<AppDbContext> factory, ICurrentUser current, IAuditService audit, IGymClock clock)
    {
        _factory = factory;
        _current = current;
        _audit = audit;
        _clock = clock;
    }

    public async Task<IReadOnlyList<OpeningHourModel>> GetWeekAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.OpeningHours.AsNoTracking().ToListAsync();
        return DayLabels.WeekOrder.Select(day =>
        {
            var r = rows.FirstOrDefault(x => x.Dia == day);
            return new OpeningHourModel
            {
                Dia = day,
                Abierto = r?.Abierto ?? false,
                Apertura = r?.Apertura,
                Cierre = r?.Cierre
            };
        }).ToList();
    }

    public async Task SaveWeekAsync(IReadOnlyList<OpeningHourModel> week)
    {
        await _current.EnsureAdminAsync();
        foreach (var h in week) ValidateTimes(DayLabels.Of(h.Dia), h.Abierto, h.Apertura, h.Cierre);

        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.OpeningHours.ToListAsync();
        foreach (var h in week)
        {
            var row = rows.FirstOrDefault(r => r.Dia == h.Dia);
            if (row == null)
            {
                row = new OpeningHour { Dia = h.Dia };
                db.OpeningHours.Add(row);
            }
            row.Abierto = h.Abierto;
            row.Apertura = h.Abierto ? h.Apertura : null;
            row.Cierre = h.Abierto ? h.Cierre : null;
        }
        await db.SaveChangesAsync();
        await _audit.LogAsync("Editar", nameof(OpeningHour), null, "Horario semanal");
    }

    public async Task<IReadOnlyList<SpecialOpeningHourModel>> GetSpecialAsync(bool soloProximos)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var query = db.SpecialOpeningHours.AsNoTracking().AsQueryable();
        if (soloProximos)
        {
            var today = DateOnly.FromDateTime(_clock.Now);
            query = query.Where(s => s.Fecha >= today);
        }
        var rows = await query.OrderBy(s => s.Fecha).ToListAsync();
        return rows.Select(s => new SpecialOpeningHourModel
        {
            Id = s.Id, Fecha = s.Fecha, Abierto = s.Abierto, Apertura = s.Apertura, Cierre = s.Cierre, Descripcion = s.Descripcion
        }).ToList();
    }

    public async Task<int> SaveSpecialAsync(SpecialOpeningHourModel model)
    {
        await _current.EnsureAdminAsync();
        ModelValidator.ValidateOrThrow(model);
        ValidateTimes("El horario especial", model.Abierto, model.Apertura, model.Cierre);

        await using var db = await _factory.CreateDbContextAsync();
        if (await db.SpecialOpeningHours.AnyAsync(s => s.Fecha == model.Fecha && s.Id != model.Id))
            throw new ServiceException("Ya existe un horario especial para esa fecha.");

        SpecialOpeningHour entity;
        if (model.Id == 0)
        {
            entity = new SpecialOpeningHour();
            db.SpecialOpeningHours.Add(entity);
        }
        else
        {
            entity = await db.SpecialOpeningHours.FirstOrDefaultAsync(s => s.Id == model.Id)
                     ?? throw new ServiceException("El horario especial no existe.");
        }
        entity.Fecha = model.Fecha;
        entity.Abierto = model.Abierto;
        entity.Apertura = model.Abierto ? model.Apertura : null;
        entity.Cierre = model.Abierto ? model.Cierre : null;
        entity.Descripcion = model.Descripcion.Trim();
        await db.SaveChangesAsync();
        await _audit.LogAsync(model.Id == 0 ? "Crear" : "Editar", nameof(SpecialOpeningHour), entity.Id.ToString(), entity.Descripcion);
        return entity.Id;
    }

    public async Task DeleteSpecialAsync(int id)
    {
        await _current.EnsureAdminAsync();
        await using var db = await _factory.CreateDbContextAsync();
        var entity = await db.SpecialOpeningHours.FirstOrDefaultAsync(s => s.Id == id)
                     ?? throw new ServiceException("El horario especial no existe.");
        db.SpecialOpeningHours.Remove(entity);
        await db.SaveChangesAsync();
        await _audit.LogAsync("Eliminar", nameof(SpecialOpeningHour), id.ToString(), entity.Descripcion);
    }

    public async Task<GymStatusDto> GetTodayStatusAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var now = _clock.Now;
        var today = DateOnly.FromDateTime(now);
        var time = TimeOnly.FromDateTime(now);

        var special = await db.SpecialOpeningHours.AsNoTracking().FirstOrDefaultAsync(s => s.Fecha == today);
        bool open; TimeOnly? desde; TimeOnly? hasta; string? note = null;
        if (special != null)
        {
            open = special.Abierto; desde = special.Apertura; hasta = special.Cierre; note = special.Descripcion;
        }
        else
        {
            var day = await db.OpeningHours.AsNoTracking().FirstOrDefaultAsync(h => h.Dia == now.DayOfWeek);
            open = day?.Abierto ?? false; desde = day?.Apertura; hasta = day?.Cierre;
        }

        if (!open || desde == null || hasta == null)
            return new GymStatusDto(false, note != null ? $"Hoy cerrado: {note}" : "Hoy el gimnasio está cerrado.");

        var isOpenNow = time >= desde && time < hasta;
        var range = $"{desde:HH\\:mm} a {hasta:HH\\:mm}";
        var message = isOpenNow ? $"Abierto ahora · hoy de {range}" : $"Cerrado ahora · hoy atendemos de {range}";
        if (note != null) message += $" ({note})";
        return new GymStatusDto(isOpenNow, message);
    }

    private static void ValidateTimes(string label, bool open, TimeOnly? desde, TimeOnly? hasta)
    {
        if (!open) return;
        if (desde == null || hasta == null) throw new ServiceException($"{label}: indica la hora de apertura y de cierre.");
        if (hasta <= desde) throw new ServiceException($"{label}: la hora de cierre debe ser posterior a la de apertura.");
    }
}
