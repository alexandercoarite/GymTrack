using GymTrack.Application.Interfaces;
using GymTrack.Domain.Entities;
using GymTrack.Domain.Enums;
using GymTrack.Infrastructure.Data;
using GymTrack.Infrastructure.Identity;
using GymTrack.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GymTrack.Tests;

public class TestDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly SqliteConnection _connection;
    public TestDbContextFactory(SqliteConnection connection) => _connection = connection;

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
}

public sealed class FakeCurrentUser : ICurrentUser
{
    public string? UserId { get; set; }
    public bool Admin { get; set; }

    public Task<string?> GetUserIdAsync() => Task.FromResult(UserId);
    public Task<string> RequireUserIdAsync() => Task.FromResult(UserId ?? throw new Application.Common.ForbiddenException());
    public Task<bool> IsAdminAsync() => Task.FromResult(Admin);
    public Task EnsureAdminAsync() => Admin ? Task.CompletedTask : throw new Application.Common.ForbiddenException();
}

public sealed class FakeClock : IGymClock
{
    public DateTime Now { get; set; } = new(2026, 11, 16, 10, 0, 0); // lunes
}

public sealed class FakeImageStorage : IMachineImageStorage
{
    public List<string> Deleted { get; } = new();
    public Task<string> SaveAsync(byte[] content) => Task.FromResult("/uploads/machines/test.jpg");
    public void Delete(string? path) { if (path != null) Deleted.Add(path); }
}

/// <summary>Base de datos SQLite en memoria con el esquema real.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    public TestDbContextFactory Factory { get; }
    public FakeCurrentUser User { get; } = new() { UserId = "user-1", Admin = true };

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Factory = new TestDbContextFactory(_connection);
        using var db = Factory.CreateDbContext();
        db.Database.EnsureCreated();
        db.Users.Add(new ApplicationUser
        {
            Id = "user-1", UserName = "user1", NormalizedUserName = "USER1", Email = "u1@test.com",
            NormalizedEmail = "U1@TEST.COM", Nombre = "Ana", Apellido = "Prueba"
        });
        db.MachineCategories.Add(new MachineCategory { Id = 1, Nombre = "Fuerza" });
        db.MuscleGroups.Add(new MuscleGroup { Id = 1, Nombre = "Pecho" });
        db.SaveChanges();
    }

    public AuditService Audit() => new(Factory, User);
    public MachineService Machines(FakeImageStorage? images = null) => new(Factory, User, Audit(), images ?? new FakeImageStorage());

    public void Dispose() => _connection.Dispose();
}
