using System.Security.Claims;
using GymTrack.Application.Common;
using GymTrack.Application.Interfaces;
using GymTrack.Domain.Entities;
using GymTrack.Infrastructure.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GymTrack.Infrastructure.Services;

public class StorageOptions
{
    public string PrivatePhotosPath { get; set; } = "App_Data/private-photos";
    public string MachineImagesPath { get; set; } = string.Empty;
}

public class CurrentUserService : ICurrentUser
{
    private readonly AuthenticationStateProvider _auth;

    public CurrentUserService(AuthenticationStateProvider auth) => _auth = auth;

    private async Task<ClaimsPrincipal> PrincipalAsync() => (await _auth.GetAuthenticationStateAsync()).User;

    public async Task<string?> GetUserIdAsync()
    {
        var user = await PrincipalAsync();
        return user.Identity?.IsAuthenticated == true ? user.FindFirst(ClaimTypes.NameIdentifier)?.Value : null;
    }

    public async Task<string> RequireUserIdAsync() => await GetUserIdAsync() ?? throw new ForbiddenException();

    public async Task<bool> IsAdminAsync()
    {
        var user = await PrincipalAsync();
        return user.Identity?.IsAuthenticated == true && user.IsInRole(AppRoles.Admin);
    }

    public async Task EnsureAdminAsync()
    {
        if (!await IsAdminAsync()) throw new ForbiddenException();
    }
}

public class GymClock : IGymClock
{
    private readonly TimeZoneInfo _zone;

    public GymClock(IConfiguration configuration)
    {
        var id = configuration["Gym:TimeZoneId"] ?? "America/La_Paz";
        try { _zone = TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception) { _zone = TimeZoneInfo.Local; }
    }

    public DateTime Now => TimeZoneInfo.ConvertTime(DateTime.UtcNow, _zone);
}

public class AuditService : IAuditService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ICurrentUser _current;

    public AuditService(IDbContextFactory<AppDbContext> factory, ICurrentUser current)
    {
        _factory = factory;
        _current = current;
    }

    public async Task LogAsync(string accion, string entidad, string? entidadId = null, string? detalle = null)
    {
        var userId = await _current.GetUserIdAsync() ?? "sistema";
        await using var db = await _factory.CreateDbContextAsync();
        db.AuditLogs.Add(new AdminAuditLog
        {
            AdminUserId = userId, Accion = accion, Entidad = entidad, EntidadId = entidadId,
            Detalle = detalle is { Length: > 1000 } ? detalle[..1000] : detalle,
            FechaUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}

/// <summary>
/// No envía correos reales: escribe el contenido en el registro de la aplicación.
/// Sustituye esta clase por una implementación SMTP o de un proveedor de correo en producción.
/// </summary>
public class LoggingEmailService : IEmailService
{
    private readonly ILogger<LoggingEmailService> _logger;

    public LoggingEmailService(ILogger<LoggingEmailService> logger) => _logger = logger;

    public Task SendAsync(string to, string subject, string body)
    {
        _logger.LogWarning("CORREO NO ENVIADO (servicio de correo sin configurar). Para: {To}. Asunto: {Subject}. Contenido: {Body}",
            to, subject, body);
        return Task.CompletedTask;
    }
}

/// <summary>Sin proveedor de visión artificial: nunca se simula un resultado.</summary>
public class NotConfiguredBodyAnalysisService : IBodyAnalysisService
{
    public bool IsConfigured => false;

    public Task<BodyAnalysisResult> AnalyzeAsync(IReadOnlyList<BodyPhotoInput> photos, CancellationToken cancellationToken = default) =>
        Task.FromResult(new BodyAnalysisResult(false, null, null, "El análisis automático no está configurado."));
}

public class MachineImageStorage : IMachineImageStorage
{
    private readonly string _root;

    public MachineImageStorage(StorageOptions options) => _root = options.MachineImagesPath;

    public async Task<string> SaveAsync(byte[] content)
    {
        var extension = ImageValidator.ValidateOrThrow(content, ImageValidator.MaxMachineImageBytes);
        Directory.CreateDirectory(_root);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        await File.WriteAllBytesAsync(Path.Combine(_root, fileName), content);
        return $"/uploads/machines/{fileName}";
    }

    public void Delete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/uploads/machines/", StringComparison.Ordinal)) return;
        var full = Path.GetFullPath(Path.Combine(_root, Path.GetFileName(path)));
        if (full.StartsWith(Path.GetFullPath(_root), StringComparison.Ordinal) && File.Exists(full))
        {
            try { File.Delete(full); } catch (IOException) { /* se ignora: el archivo queda huérfano */ }
        }
    }
}

/// <summary>Fotografías corporales fuera de wwwroot: solo se sirven mediante un endpoint autorizado.</summary>
public class PrivatePhotoStorage : IPhotoStorage
{
    private readonly string _root;

    public PrivatePhotoStorage(StorageOptions options) => _root = Path.GetFullPath(options.PrivatePhotosPath);

    public async Task<string> SaveAsync(string userId, byte[] content, string extension)
    {
        var folder = Path.GetFileName(userId); // evita rutas relativas en el identificador
        var key = $"{folder}/{Guid.NewGuid():N}{extension}";
        var full = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, content);
        return key;
    }

    public Stream OpenRead(string key) => new FileStream(Resolve(key), FileMode.Open, FileAccess.Read, FileShare.Read);

    public void Delete(string key)
    {
        try
        {
            var full = Resolve(key);
            if (File.Exists(full)) File.Delete(full);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Se ignora: el registro de base de datos se elimina de todos modos.
        }
    }

    private string Resolve(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, key));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Ruta de fotografía no válida.");
        return full;
    }
}
