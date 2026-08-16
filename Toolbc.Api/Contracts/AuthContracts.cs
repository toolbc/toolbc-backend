using Toolbc.Api.Domain;

namespace Toolbc.Api.Contracts;

public sealed record LoginRequest(string Email, string Password);

public sealed record CreateUserRequest(
    string FullName,
    string Email,
    string Password,
    UserRole Role,
    string? Note = null,
    Guid? AssignedDoctorId = null,
    string? Comorbidities = null);

public sealed record UserResponse(
    Guid Id,
    string FullName,
    string Email,
    UserRole Role,
    bool IsActive);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    UserResponse User);
