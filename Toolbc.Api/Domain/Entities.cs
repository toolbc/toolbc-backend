namespace Toolbc.Api.Domain;

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public PatientProfile? PatientProfile { get; set; }
    public DoctorProfile? DoctorProfile { get; set; }
}

public sealed class PatientProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string MedicalRecordNumber { get; set; } = string.Empty;
    public Guid? AssignedDoctorId { get; set; }
    public DoctorProfile? AssignedDoctor { get; set; }
    public DateOnly TreatmentStartDate { get; set; }
    public decimal? Weight { get; set; }
    public DateTimeOffset? WeightRecordedAt { get; set; }
    public string? Comorbidities { get; set; }
    public TreatmentPlan? TreatmentPlan { get; set; }
}

public sealed class DoctorProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string Specialty { get; set; } = "TB Care";
    public ICollection<PatientProfile> Patients { get; set; } = [];
}

public sealed class TreatmentPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientProfileId { get; set; }
    public PatientProfile PatientProfile { get; set; } = null!;
    public TreatmentPhase Phase { get; set; } = TreatmentPhase.Intensive;
    public string MedicineSummary { get; set; } = "Isoniazid (H) + Rifampicin (R) + Pyrazinamide (Z) + Ethambutol (E)";
    public int TotalDays { get; set; } = 180;
    public TreatmentStatus Status { get; set; } = TreatmentStatus.Active;
    public ICollection<MedicationDoseLog> DoseLogs { get; set; } = [];
}

public sealed class MedicationDoseLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TreatmentPlanId { get; set; }
    public TreatmentPlan TreatmentPlan { get; set; } = null!;
    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DoseStatus Status { get; set; } = DoseStatus.Pending;
    public string? Notes { get; set; }
}

public sealed class SymptomLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientProfileId { get; set; }
    public PatientProfile PatientProfile { get; set; } = null!;
    public DateTimeOffset LoggedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool PersistentCough { get; set; }
    public bool FeverOrChills { get; set; }
    public bool NightSweats { get; set; }
    public bool WeightLossOrLowAppetite { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public string Feedback { get; set; } = string.Empty;
}

public sealed class Reminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientProfileId { get; set; }
    public PatientProfile PatientProfile { get; set; } = null!;
    public DateTimeOffset ScheduledAt { get; set; }
    public ReminderStatus Status { get; set; } = ReminderStatus.Pending;
    public string Message { get; set; } = string.Empty;
}

public sealed class AppNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WeightLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientProfileId { get; set; }
    public PatientProfile PatientProfile { get; set; } = null!;
    public decimal Weight { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; set; }
}

public sealed class LabResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientProfileId { get; set; }
    public PatientProfile PatientProfile { get; set; } = null!;
    public LabTestType TestType { get; set; }
    public LabResultValue Result { get; set; }
    public DateTimeOffset TestedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; set; }
    public string? RecordedBy { get; set; }
}
