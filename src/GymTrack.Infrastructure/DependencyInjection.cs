using GymTrack.Application.Interfaces;
using GymTrack.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GymTrack.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration, string contentRootPath, string webRootPath)
    {
        var storage = new StorageOptions
        {
            PrivatePhotosPath = Path.GetFullPath(
                configuration["Storage:PrivatePhotosPath"] ?? "App_Data/private-photos", contentRootPath),
            MachineImagesPath = Path.Combine(webRootPath, "uploads", "machines")
        };
        services.AddSingleton(storage);

        services.AddSingleton<IGymClock, GymClock>();
        services.AddScoped<ICurrentUser, CurrentUserService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddSingleton<IEmailService, LoggingEmailService>();
        services.AddSingleton<IMachineImageStorage, MachineImageStorage>();
        services.AddSingleton<IPhotoStorage, PrivatePhotoStorage>();
        services.AddSingleton<IBodyAnalysisService, NotConfiguredBodyAnalysisService>();

        services.AddScoped<IMachineService, MachineService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddScoped<IOpeningHourService, OpeningHourService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IQuestionnaireService, QuestionnaireService>();
        services.AddScoped<IRoutineService, RoutineService>();
        services.AddScoped<IBodyAssessmentService, BodyAssessmentService>();
        services.AddScoped<IProgressService, ProgressService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
