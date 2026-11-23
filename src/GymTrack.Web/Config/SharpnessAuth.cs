using GymTrack.Application.Common;
using GymTrack.Infrastructure.Config;
using GymTrack.Infrastructure.Data;
using GymTrack.Infrastructure.Identity;
using GymTrack.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace GymTrack.Web.Config;

public static class SharpnessAuth
{
    public static IServiceCollection UseAuth(this IServiceCollection services, IConfiguration config, IWebHostEnvironment environment)
    {
        services.UseDB(config, environment.ContentRootPath);
        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        }).AddIdentityCookies();

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
        }).AddRoles<IdentityRole>()
            .AddErrorDescriber<SpanishIdentityErrorDescriber>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/cuenta/iniciar-sesion";
            options.AccessDeniedPath = "/cuenta/acceso-denegado";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
        });
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));
        services.AddAuthorizationBuilder()
            .AddPolicy("AdminOnly", p => p.RequireRole(AppRoles.Admin))
            .AddPolicy("ClientOnly", p => p.RequireRole(AppRoles.Client));

        if (environment.IsDevelopment() && config.GetValue<bool>("GymTrack:EphemeralKeys"))
            services.AddDataProtection().UseEphemeralDataProtectionProvider();

        return services;
    }

    public static Task SeedAuth(this WebApplication app) => DbSeeder.InitializeAsync(app.Services);
}
