using GymTrack.Domain.Entities;
using GymTrack.Domain.Enums;
using GymTrack.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GymTrack.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<QuestionnaireResponse> QuestionnaireResponses => Set<QuestionnaireResponse>();
    public DbSet<BodyAssessment> BodyAssessments => Set<BodyAssessment>();
    public DbSet<BodyPhoto> BodyPhotos => Set<BodyPhoto>();
    public DbSet<MachineCategory> MachineCategories => Set<MachineCategory>();
    public DbSet<MuscleGroup> MuscleGroups => Set<MuscleGroup>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();
    public DbSet<OpeningHour> OpeningHours => Set<OpeningHour>();
    public DbSet<SpecialOpeningHour> SpecialOpeningHours => Set<SpecialOpeningHour>();
    public DbSet<TrainingPlan> TrainingPlans => Set<TrainingPlan>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<PlanExercise> PlanExercises => Set<PlanExercise>();
    public DbSet<ProgressRecord> ProgressRecords => Set<ProgressRecord>();
    public DbSet<AdminAuditLog> AuditLogs => Set<AdminAuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Los enums se guardan como texto para que la base de datos sea legible.
        configurationBuilder.Properties<GoalType>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<ExperienceLevel>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<TrainingType>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<MachineStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<MaintenanceStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<AssessmentStatus>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<PhotoType>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<PlanStatus>().HaveConversion<string>().HaveMaxLength(40);
    }

    public override int SaveChanges()
    {
        RenewVersions();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        RenewVersions();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>SQLite no tiene rowversion: renovamos manualmente el token de concurrencia.</summary>
    private void RenewVersions()
    {
        foreach (var entry in ChangeTracker.Entries<IVersioned>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            entry.Entity.Version = Guid.NewGuid();
        }
    }
}
