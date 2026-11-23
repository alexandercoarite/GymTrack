using GymTrack.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GymTrack.Infrastructure.Config;

public static class SharpnessDB
{
    public static IServiceCollection UseDB(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        var connection = configuration.GetConnectionString("DefaultConnection");
        var sqlite = new SqliteConnectionStringBuilder(connection ?? string.Empty);
        if (string.IsNullOrWhiteSpace(sqlite.DataSource))
            sqlite.DataSource = configuration["Database:Source"] ?? "App_Data/gymtrack.db";

        sqlite.DataSource = Path.GetFullPath(sqlite.DataSource, contentRootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(sqlite.DataSource)!);
        var connectionString = sqlite.ToString();

        return services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(connectionString));
    }
}
