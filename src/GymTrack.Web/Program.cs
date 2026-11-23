using GymTrack.Application;
using GymTrack.Application.Common;
using GymTrack.Application.Services;
using GymTrack.Infrastructure;
using GymTrack.Web.Services;
using GymTrack.Web.Components;
using GymTrack.Web.Config;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    // Las fotografías capturadas con la cámara viajan por SignalR como base64.
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 10 * 1024 * 1024);

builder.Services.UseAuth(builder.Configuration, builder.Environment);

builder.Services.Configure<GymInfo>(builder.Configuration.GetSection("Gym"));
builder.Services.Configure<RoutineRules>(builder.Configuration.GetSection("RoutineRules"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<RoutineRules>>().Value);

var webRoot = builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.ContentRootPath, webRoot);

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

app.UseAPIEndpoints();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.SeedAuth();

app.Run();
