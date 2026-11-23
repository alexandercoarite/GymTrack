using GymTrack.Application.Interfaces;
using GymTrack.Infrastructure.Data;
using GymTrack.Infrastructure.Identity;
using GymTrack.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GymTrack.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration, string contentRootPath, string webRootPath)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'DefaultConnection'.");

        var csb = new SqliteConnectionStringBuilder(connectionString);
        var dbPath = Path.GetFullPath(csb.DataSource, contentRootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        csb.DataSource = dbPath;
        var finalConnection = csb.ToString();

        // Los servicios usan una fábrica (un contexto corto por operación): es lo recomendado en Blazor Server.
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(finalConnection));
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
                options.Lockout.AllowedForNewUsers = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddRoles<IdentityRole>()
            .AddErrorDescriber<SpanishIdentityErrorDescriber>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

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
