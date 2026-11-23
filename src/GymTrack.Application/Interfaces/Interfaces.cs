using GymTrack.Application.DTOs;
using GymTrack.Domain.Enums;

namespace GymTrack.Application.Interfaces;

public interface ICurrentUser
{
    Task<string?> GetUserIdAsync();
    Task<string> RequireUserIdAsync();
    Task<bool> IsAdminAsync();
    /// <summary>Lanza ForbiddenException si el usuario actual no es administrador.</summary>
    Task EnsureAdminAsync();
}

public interface IGymClock
{
    /// <summary>Hora local del gimnasio.</summary>
    DateTime Now { get; }
}

public interface IAuditService
{
    Task LogAsync(string accion, string entidad, string? entidadId = null, string? detalle = null);
}

public interface IEmailService
{
    Task SendAsync(string to, string subject, string body);
}

public interface IMachineImageStorage
{
    Task<string> SaveAsync(byte[] content);
    void Delete(string? path);
}

public interface IPhotoStorage
{
    Task<string> SaveAsync(string userId, byte[] content, string extension);
    Stream OpenRead(string key);
    void Delete(string key);
}

public record BodyPhotoInput(PhotoType Tipo, byte[] Contenido, string ContentType);

public record BodyAnalysisResult(bool Success, string? Resumen, string? IndicadoresJson, string? Error);

public interface IBodyAnalysisService
{
    /// <summary>Indica si hay un proveedor de visión artificial configurado.</summary>
    bool IsConfigured { get; }
    Task<BodyAnalysisResult> AnalyzeAsync(IReadOnlyList<BodyPhotoInput> photos, CancellationToken cancellationToken = default);
}

public interface IMachineService
{
    Task<IReadOnlyList<MachineDto>> SearchAsync(MachineFilter? filter = null);
    Task<MachineDto?> GetAsync(int id);
    Task<int> CreateAsync(MachineEditModel model);
    Task UpdateAsync(MachineEditModel model);
    Task DeleteAsync(int id);
    Task ChangeStatusAsync(int id, MachineStatus status);
}

public interface ILookupService
{
    Task<IReadOnlyList<LookupDto>> GetCategoriesAsync();
    Task<int> SaveCategoryAsync(LookupEditModel model);
    Task DeleteCategoryAsync(int id);
    Task<IReadOnlyList<LookupDto>> GetMuscleGroupsAsync();
    Task<int> SaveMuscleGroupAsync(LookupEditModel model);
    Task DeleteMuscleGroupAsync(int id);
}

public interface IMaintenanceService
{
    Task<IReadOnlyList<MaintenanceDto>> ListAsync(bool soloAbiertos);
    Task<int> RegisterAsync(MaintenanceCreateModel model);
    Task ResolveAsync(int recordId, string? observaciones);
}

public interface IOpeningHourService
{
    Task<IReadOnlyList<OpeningHourModel>> GetWeekAsync();
    Task SaveWeekAsync(IReadOnlyList<OpeningHourModel> week);
    Task<IReadOnlyList<SpecialOpeningHourModel>> GetSpecialAsync(bool soloProximos);
    Task<int> SaveSpecialAsync(SpecialOpeningHourModel model);
    Task DeleteSpecialAsync(int id);
    Task<GymStatusDto> GetTodayStatusAsync();
}

public interface IUserAdminService
{
    Task<IReadOnlyList<UserListItem>> SearchAsync(string? texto);
    Task<UserDetailDto?> GetAsync(string id);
    Task SetActiveAsync(string id, bool activo);
    Task SetRoleAsync(string id, string rol);
    Task<IReadOnlyList<AssessmentDto>> GetAssessmentsAsync(string userId);
}

public interface IProfileService
{
    Task<ProfileModel> GetAsync();
    Task UpdateAsync(ProfileModel model);
    Task ChangePasswordAsync(string currentPassword, string newPassword);
}

public interface IQuestionnaireService
{
    Task<QuestionnaireModel?> GetLatestAsync();
    Task SaveAsync(QuestionnaireModel model);
}

public interface IRoutineService
{
    Task<RoutineDto?> GetActiveAsync();
    Task<IReadOnlyList<RoutineDto>> GetHistoryAsync();
    Task<RoutineDto> GenerateAsync();
}

public interface IBodyAssessmentService
{
    bool AnalysisAvailable { get; }
    Task<IReadOnlyList<AssessmentDto>> ListAsync();
    Task<int> CreateAsync(AssessmentCreateModel model);
    Task DeleteAsync(int assessmentId);
    Task DeleteAllAsync();
    /// <summary>Solo el propietario puede abrir una fotografía.</summary>
    Task<PhotoContent?> OpenPhotoAsync(int photoId, string requestingUserId);
}

public interface IProgressService
{
    Task<IReadOnlyList<ProgressDto>> ListAsync();
    Task<int> AddAsync(ProgressModel model);
    Task DeleteAsync(int id);
    Task DeleteAllAsync();
}

public interface IDashboardService
{
    Task<AdminDashboardDto> GetAdminAsync();
    Task<ClientDashboardDto> GetClientAsync();
}
