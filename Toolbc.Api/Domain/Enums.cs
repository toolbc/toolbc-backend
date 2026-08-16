namespace Toolbc.Api.Domain;

public enum UserRole
{
    Patient = 1,
    Doctor = 2,
    Admin = 3
}

public enum TreatmentStatus
{
    Active = 1,
    Completed = 2,
    Paused = 3
}

public enum DoseStatus
{
    Pending = 1,
    Taken = 2,
    Missed = 3,
    Snoozed = 4
}

public enum RiskLevel
{
    Low = 1,
    Moderate = 2,
    High = 3
}

public enum ReminderStatus
{
    Pending = 1,
    Sent = 2,
    Confirmed = 3,
    Escalated = 4,
    Resolved = 5
}

public enum NotificationType
{
    Reminder = 1,
    Alert = 2,
    Account = 3
}

public enum TreatmentPhase
{
    Intensive = 1,
    Continuation = 2,
    Completed = 3,
    Extended = 4
}

public enum LabTestType
{
    TCM_GeneXpert = 1,
    BTA_Smear = 2,
    ChestXRay = 3,
    Other = 4
}

public enum LabResultValue
{
    Positive = 1,
    Negative = 2,
    Indeterminate = 3
}
