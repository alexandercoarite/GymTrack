using GymTrack.Application;
using GymTrack.Application.Common;
using GymTrack.Application.Services;
using GymTrack.Infrastructure;
using GymTrack.Infrastructure.Data;
using GymTrack.Web;
using GymTrack.Web.Components;
using GymTrack.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    // Las fotografías capturadas con la cámara viajan por SignalR como base64.
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 10 * 1024 * 1024);

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/cuenta/iniciar-sesion";
    options.AccessDeniedPath = "/cuenta/acceso-denegado";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", p => p.RequireRole(AppRoles.Admin))
    .AddPolicy("ClientOnly", p => p.RequireRole(AppRoles.Client));

builder.Services.Configure<GymInfo>(builder.Configuration.GetSection("Gym"));
builder.Services.Configure<RoutineRules>(builder.Configuration.GetSection("RoutineRules"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<RoutineRules>>().Value);

var webRoot = builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.ContentRootPath, webRoot);

if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("GymTrack:EphemeralKeys"))
    builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=()";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGymTrackEndpoints();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await DbSeeder.InitializeAsync(app.Services);

app.Run();
