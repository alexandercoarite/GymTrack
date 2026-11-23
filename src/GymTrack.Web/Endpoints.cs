using System.Security.Claims;
using GymTrack.Application.Interfaces;
using GymTrack.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GymTrack.Web;

public static class Endpoints
{
    public static void MapGymTrackEndpoints(this WebApplication app)
    {
        // Cierre de sesión: POST con token antiforgery (la cookie no se puede borrar desde un circuito Blazor).
        app.MapPost("/cuenta/cerrar-sesion", async (SignInManager<ApplicationUser> signInManager, [FromForm] string? returnUrl) =>
        {
            await signInManager.SignOutAsync();
            return Results.LocalRedirect("~/");
        });

        // Fotografías corporales: solo las puede abrir su propietario autenticado.
        app.MapGet("/api/fotos/{id:int}", async (int id, ClaimsPrincipal user, IBodyAssessmentService service) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Results.Unauthorized();
            var photo = await service.OpenPhotoAsync(id, userId);
            if (photo == null) return Results.NotFound();
            return Results.File(photo.Contenido, photo.ContentType);
        }).RequireAuthorization();
    }
}
