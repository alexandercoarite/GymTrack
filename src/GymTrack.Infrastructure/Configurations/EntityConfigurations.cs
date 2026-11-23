using GymTrack.Domain.Entities;
using GymTrack.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymTrack.Infrastructure.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
        b.Property(x => x.Apellido).HasMaxLength(80).IsRequired();
        b.Ignore(x => x.NombreCompleto);
    }
}

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> b)
    {
        b.Property(x => x.UserId).IsRequired();
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasOne<ApplicationUser>().WithOne().HasForeignKey<UserProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class QuestionnaireResponseConfiguration : IEntityTypeConfiguration<QuestionnaireResponse>
{
    public void Configure(EntityTypeBuilder<QuestionnaireResponse> b)
    {
        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.GruposMuscularesIds).HasMaxLength(200);
        b.Property(x => x.EjerciciosAEvitar).HasMaxLength(500);
        b.Property(x => x.Limitaciones).HasMaxLength(1000);
        b.HasIndex(x => new { x.UserId, x.Fecha });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class BodyAssessmentConfiguration : IEntityTypeConfiguration<BodyAssessment>
{
    public void Configure(EntityTypeBuilder<BodyAssessment> b)
    {
        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.ResultadoAnalisis).HasMaxLength(4000);
        b.Property(x => x.Indicadores).HasMaxLength(4000);
        b.Property(x => x.Observaciones).HasMaxLength(1000);
        b.HasIndex(x => new { x.UserId, x.Fecha });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Fotos).WithOne(p => p.BodyAssessment).HasForeignKey(p => p.BodyAssessmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class BodyPhotoConfiguration : IEntityTypeConfiguration<BodyPhoto>
{
    public void Configure(EntityTypeBuilder<BodyPhoto> b)
    {
        b.Property(x => x.RutaPrivada).HasMaxLength(300).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(60).IsRequired();
    }
}

public class MachineCategoryConfiguration : IEntityTypeConfiguration<MachineCategory>
{
    public void Configure(EntityTypeBuilder<MachineCategory> b)
    {
        b.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(300);
        b.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class MuscleGroupConfiguration : IEntityTypeConfiguration<MuscleGroup>
{
    public void Configure(EntityTypeBuilder<MuscleGroup> b)
    {
        b.Property(x => x.Nombre).HasMaxLength(80).IsRequired();
        b.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class MachineConfiguration : IEntityTypeConfiguration<Machine>
{
    public void Configure(EntityTypeBuilder<Machine> b)
    {
        b.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Instrucciones).HasMaxLength(2000);
        b.Property(x => x.ImagenRuta).HasMaxLength(300);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.Estado);
        b.HasIndex(x => x.Nombre);
        b.HasOne(x => x.Category).WithMany(c => c.Machines).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.MuscleGroup).WithMany(g => g.Machines).HasForeignKey(x => x.MuscleGroupId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MaintenanceRecordConfiguration : IEntityTypeConfiguration<MaintenanceRecord>
{
    public void Configure(EntityTypeBuilder<MaintenanceRecord> b)
    {
        b.Property(x => x.Descripcion).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Observaciones).HasMaxLength(1000);
        b.HasOne(x => x.Machine).WithMany(m => m.MaintenanceRecords).HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class OpeningHourConfiguration : IEntityTypeConfiguration<OpeningHour>
{
    public void Configure(EntityTypeBuilder<OpeningHour> b) => b.HasIndex(x => x.Dia).IsUnique();
}

public class SpecialOpeningHourConfiguration : IEntityTypeConfiguration<SpecialOpeningHour>
{
    public void Configure(EntityTypeBuilder<SpecialOpeningHour> b)
    {
        b.Property(x => x.Descripcion).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Fecha).IsUnique();
    }
}

public class TrainingPlanConfiguration : IEntityTypeConfiguration<TrainingPlan>
{
    public void Configure(EntityTypeBuilder<TrainingPlan> b)
    {
        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
        b.Property(x => x.GruposMusculares).HasMaxLength(300);
        b.Property(x => x.Recomendaciones).HasMaxLength(3000);
        b.HasIndex(x => new { x.UserId, x.Estado });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> b)
    {
        b.Property(x => x.Nombre).HasMaxLength(120).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(500);
        b.Property(x => x.MaquinaReferencia).HasMaxLength(100);
        b.HasOne(x => x.MuscleGroup).WithMany(g => g.Exercises).HasForeignKey(x => x.MuscleGroupId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PlanExerciseConfiguration : IEntityTypeConfiguration<PlanExercise>
{
    public void Configure(EntityTypeBuilder<PlanExercise> b)
    {
        b.Property(x => x.Repeticiones).HasMaxLength(30).IsRequired();
        b.HasOne(x => x.TrainingPlan).WithMany(p => p.Ejercicios).HasForeignKey(x => x.TrainingPlanId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Exercise).WithMany().HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Machine).WithMany().HasForeignKey(x => x.MachineId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ProgressRecordConfiguration : IEntityTypeConfiguration<ProgressRecord>
{
    public void Configure(EntityTypeBuilder<ProgressRecord> b)
    {
        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.ObjetivoAlcanzado).HasMaxLength(300);
        b.Property(x => x.Observaciones).HasMaxLength(500);
        b.HasIndex(x => new { x.UserId, x.Fecha });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> b)
    {
        b.Property(x => x.AdminUserId).HasMaxLength(100).IsRequired();
        b.Property(x => x.Accion).HasMaxLength(100).IsRequired();
        b.Property(x => x.Entidad).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntidadId).HasMaxLength(100);
        b.Property(x => x.Detalle).HasMaxLength(1000);
        b.HasIndex(x => x.FechaUtc);
    }
}
