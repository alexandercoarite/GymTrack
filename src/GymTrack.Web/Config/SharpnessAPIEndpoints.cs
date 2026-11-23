namespace GymTrack.Web.Config;

public interface IEndpoints
{
    void MapEndpoints(IEndpointRouteBuilder app);
}

public static class SharpnessAPIEndpoints
{
    public static void UseAPIEndpoints(this WebApplication app)
    {
        var modules = typeof(SharpnessAPIEndpoints).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IEndpoints).IsAssignableFrom(type));

        foreach (var type in modules)
            ((IEndpoints)Activator.CreateInstance(type)!).MapEndpoints(app);
    }
}
