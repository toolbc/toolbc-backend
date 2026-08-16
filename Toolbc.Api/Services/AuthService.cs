using Microsoft.EntityFrameworkCore;
using Toolbc.Api.Contracts;
using Toolbc.Api.Data;
using Toolbc.Api.Domain;

namespace Toolbc.Api.Services;

public interface IAuthService
{
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<(UserResponse? User, string? Error)> CreateUserAsync(CreateUserRequest request, string createdBy, CancellationToken cancellationToken);
}

public sealed class AuthService(
    ToolbcDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService) : IAuthService
{
    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
            item => item.Email == email && item.IsActive,
            cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return null;
        }

        var token = tokenService.CreateToken(user);
        return new AuthResponse(token.Token, token.ExpiresAt, ToUserResponse(user));
    }

    public async Task<(UserResponse? User, string? Error)> CreateUserAsync(
        CreateUserRequest request,
        string createdBy,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var roleError = ValidateRoleEmail(email, request.Role);
        if (roleError is not null)
        {
            return (null, roleError);
        }

        if (request.Password.Length < 6)
        {
            return (null, "Password minimal 6 karakter.");
        }

        if (await db.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            return (null, "Email sudah terdaftar.");
        }

        var user = new AppUser
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = request.Role,
            CreatedBy = createdBy
        };

        db.Users.Add(user);

        if (request.Role == UserRole.Patient)
        {
            var patientProfile = new PatientProfile
            {
                User = user,
                MedicalRecordNumber = CreateMedicalRecordNumber(),
                AssignedDoctorId = request.AssignedDoctorId,
                TreatmentStartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Comorbidities = request.Comorbidities
            };

            db.PatientProfiles.Add(patientProfile);
            db.TreatmentPlans.Add(new TreatmentPlan
            {
                PatientProfile = patientProfile,
                Phase = TreatmentPhase.Intensive,
                MedicineSummary = "Isoniazid (H) + Rifampicin (R) + Pyrazinamide (Z) + Ethambutol (E)",
                TotalDays = 180,
                Status = TreatmentStatus.Active
            });
        }

        if (request.Role == UserRole.Doctor)
        {
            db.DoctorProfiles.Add(new DoctorProfile
            {
                User = user,
                Specialty = string.IsNullOrWhiteSpace(request.Note) ? "TB Care" : request.Note.Trim()
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return (ToUserResponse(user), null);
    }

    private static string? ValidateRoleEmail(string email, UserRole role)
    {
        return role switch
        {
            UserRole.Admin when !email.EndsWith("@admin.com") => "Email admin harus berakhiran @admin.com.",
            UserRole.Doctor when !email.EndsWith("@dokter.com") => "Email dokter harus berakhiran @dokter.com.",
            UserRole.Patient when !email.EndsWith("@pasien.com") => "Email pasien harus berakhiran @pasien.com.",
            _ => null
        };
    }

    public static UserResponse ToUserResponse(AppUser user)
    {
        return new UserResponse(user.Id, user.FullName, user.Email, user.Role, user.IsActive);
    }

    private static string CreateMedicalRecordNumber()
    {
        return $"HE-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(100, 999)}";
    }
}
