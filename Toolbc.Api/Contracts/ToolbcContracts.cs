using Toolbc.Api.Domain;

namespace Toolbc.Api.Contracts;



public sealed record TreatmentSummaryDto(
    int TreatmentDay,
    int TotalDays,
    int CompletionPercent,
    int AdherencePercent,
    int StreakDays,
    string MedicineSummary,
    string NextDoseLabel,
    TreatmentPhase Phase);

public sealed record PatientDashboardResponse(
    UserResponse Patient,
    string MedicalRecordNumber,
    string? DoctorName,
    TreatmentSummaryDto Treatment,
    IReadOnlyList<NotificationDto> Notifications);

public sealed record MedicationLogRequest(
    string DoseLogId,
    DoseStatus Status,
    string? Notes = null);

public sealed record SymptomLogRequest(
    bool PersistentCough,
    bool FeverOrChills,
    bool NightSweats,
    bool WeightLossOrLowAppetite);

public sealed record SymptomLogResponse(
    Guid Id,
    RiskLevel RiskLevel,
    string Feedback,
    DateTimeOffset LoggedAt);

public sealed record HistoryItemDto(
    string Title,
    string Subtitle,
    string Type,
    DateTimeOffset CreatedAt);

public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string Message,
    bool IsRead,
    DateTimeOffset CreatedAt);

public sealed record DoctorDashboardResponse(
    UserResponse Doctor,
    int AssignedPatients,
    int UrgentAlerts,
    int TodayReviews,
    int PendingFollowUp);

public sealed record DoctorPatientDto(
    Guid PatientProfileId,
    string FullName,
    string MedicalRecordNumber,
    int TreatmentDay,
    int AdherencePercent,
    RiskLevel CurrentRisk,
    string Badge,
    bool PhaseTransitionDue,
    decimal? Weight);

public sealed record AdherenceBucketDto(string Label, int Count);

public sealed record ReminderDto(
    Guid Id,
    string PatientName,
    string Message,
    ReminderStatus Status,
    DateTimeOffset ScheduledAt);

public sealed record WeightRequest(decimal Weight, string? Notes = null);

public sealed record WeightLogDto(Guid Id, decimal Weight, DateTimeOffset RecordedAt, string? Notes);

public sealed record LabResultRequest(LabTestType TestType, LabResultValue Result, string? Notes = null);

public sealed record LabResultDto(Guid Id, LabTestType TestType, LabResultValue Result, DateTimeOffset TestedAt, string? Notes, string? RecordedBy);
