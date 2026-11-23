using GymTrack.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GymTrack.Application;

public static class DependencyInjection
{
    /// <summary>Registra la lógica de negocio pura. RoutineRules debe estar registrado por el host.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IRoutineGenerator, RoutineGenerator>();
        return services;
    }
}
