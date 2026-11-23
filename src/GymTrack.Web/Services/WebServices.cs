using System.Security.Claims;
using GymTrack.Application.Common;
using GymTrack.Infrastructure.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace GymTrack.Web.Services;

public class GymInfo
{
    public string Name { get; set; } = "GymTrack";
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

/// <summary>Revalida la sesión de los circuitos Blazor: cierra la sesión si el usuario fue desactivado o cambió su sello de seguridad.</summary>
internal sealed class IdentityRevalidatingAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IdentityOptions _options;

    public IdentityRevalidatingAuthenticationStateProvider(
        ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory, IOptions<IdentityOptions> options)
        : base(loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var principal = authenticationState.User;
        var user = await userManager.GetUserAsync(principal);
        if (user is null || !user.Activo) return false;
        if (!userManager.SupportsUserSecurityStamp) return true;
        var principalStamp = principal.FindFirst(_options.ClaimsIdentity.SecurityStampClaimType)?.Value;
        var userStamp = await userManager.GetSecurityStampAsync(user);
        return principalStamp == userStamp;
    }
}

/// <summary>Base de las páginas: captura errores del servicio y los muestra al usuario de forma centralizada.</summary>
public abstract class GymPageBase : ComponentBase
{
    [Inject] protected ILogger<GymPageBase> Logger { get; set; } = default!;

    protected string? Ok { get; set; }
    protected string? Error { get; set; }
    protected bool Busy { get; set; }

    protected async Task<bool> Safe(Func<Task> action, string? success = null, bool keepMessages = false)
    {
        if (!keepMessages)
        {
            Ok = null;
            Error = null;
        }
        Busy = true;
        try
        {
            await action();
            if (success != null) Ok = success;
            return true;
        }
        catch (ServiceException ex) { Error = ex.Message; }
        catch (ForbiddenException ex) { Error = ex.Message; }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error inesperado en la página {Page}", GetType().Name);
            Error = "Ocurrió un error inesperado. Inténtalo nuevamente.";
        }
        finally { Busy = false; }
        return false;
    }
}
