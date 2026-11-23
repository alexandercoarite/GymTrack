using System.Security.Claims;
using GymTrack.Application.Interfaces;
using GymTrack.Infrastructure.Identity;
using GymTrack.Web.Config;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GymTrack.Web;

public sealed class Endpoints : IEndpoints
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        // Cierre de sesión: POST con token antiforgery (la cookie no se puede borrar desde un circuito Blazor).
        app.MapPost("/cuenta/cerrar-sesion", async (SignInManager<ApplicationUser> signInManager, [FromForm] string? returnUrl) =>
        {
            await signInManager.SignOutAsync();
            return Results.LocalRedirect("~/");
        });

        // Fotografías corporales: solo las puede abrir su propietario autenticado.
        app.MapGet("/api/fotos/{id:int}", async (int id, HttpContext context, IBodyAssessmentService service) =>
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Results.Unauthorized();
            var photo = await service.OpenPhotoAsync(id, userId);
            if (photo == null) return Results.NotFound();
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.File(photo.Contenido, photo.ContentType);
        }).RequireAuthorization();
    }
}
