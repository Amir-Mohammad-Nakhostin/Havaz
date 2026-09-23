namespace Havaz.Application;

public sealed record RegisterRequest(string DisplayName, string Email, string Password, string PasswordConfirmation);
public sealed record LoginRequest(string Email, string Password);
public sealed record UserDto(Guid Id, string DisplayName, string Email, string Role, DateTime CreatedAt);
public sealed record HistoryItemDto(string RequestId, string Activity, string Intensity, int DurationMinutes, string Date, int RiskScore, string RiskLevel, string DecisionMode, bool MlAvailable, string? ModelVersion, DateTime CreatedAt);
public sealed record HistoryPageDto(IReadOnlyList<HistoryItemDto> Items, int Total, int Page, int PageSize);
public sealed record SavePlanRequest(string Activity, double Latitude, double Longitude, string SelectedStart, string SelectedEnd, int RiskScore, object? EnvironmentalSummary);
public sealed record SavedPlanDto(Guid Id, string Activity, double Latitude, double Longitude, string SelectedStart, string SelectedEnd, int RiskScore, string EnvironmentalSummaryJson, DateTime CreatedAt);
public interface IAuthService
{
    Task<UserDto?> Register(RegisterRequest request);
    Task<UserDto?> Login(string email, string password);
    Task<UserDto?> GetById(Guid id);
}
