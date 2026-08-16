using Microsoft.EntityFrameworkCore;
using Toolbc.Api.Domain;
using Toolbc.Api.Services;

namespace Toolbc.Api.Data;

public static class ToolbcSeedData
{
    public static async Task SeedAsync(ToolbcDbContext db, IPasswordHasher passwordHasher)
    {
        if (await db.Users.AnyAsync())
        {
            return;
        }

        var admin = new AppUser
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FullName = "Admin ToolBC",
            Email = "admin@admin.com",
            PasswordHash = passwordHasher.Hash("Admin123!"),
            Role = UserRole.Admin,
            CreatedBy = "seed"
        };

        var doctorUser = new AppUser
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            FullName = "Dr. Arya Pratama",
            Email = "arya@dokter.com",
            PasswordHash = passwordHasher.Hash("Dokter123!"),
            Role = UserRole.Doctor,
            CreatedBy = "seed"
        };

        var patientUser = new AppUser
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            FullName = "Davina Karambol",
            Email = "davina@pasien.com",
            PasswordHash = passwordHasher.Hash("Pasien123!"),
            Role = UserRole.Patient,
            CreatedBy = "seed"
        };

        var doctor = new DoctorProfile
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            UserId = doctorUser.Id,
            Specialty = "Pulmonology"
        };

        var patient = new PatientProfile
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            UserId = patientUser.Id,
            MedicalRecordNumber = "HE-9201",
            AssignedDoctorId = doctor.Id,
            TreatmentStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-23)),
            Weight = 55m,
            WeightRecordedAt = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-23)).ToDateTime(TimeOnly.MinValue),
            Comorbidities = null
        };

        var treatmentPlan = new TreatmentPlan
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
            PatientProfileId = patient.Id,
            Phase = TreatmentPhase.Intensive,
            MedicineSummary = "Isoniazid (H) + Rifampicin (R) + Pyrazinamide (Z) + Ethambutol (E)",
            TotalDays = 180,
            Status = TreatmentStatus.Active
        };

        var now = DateTimeOffset.UtcNow;
        var doseLogs = new[]
        {
            new MedicationDoseLog
            {
                TreatmentPlanId = treatmentPlan.Id,
                ScheduledAt = now.Date.AddHours(1),
                ConfirmedAt = now.Date.AddHours(1).AddMinutes(4),
                Status = DoseStatus.Taken,
                Notes = "Morning medicine completed"
            },
            new MedicationDoseLog
            {
                TreatmentPlanId = treatmentPlan.Id,
                ScheduledAt = now.Date.AddHours(13),
                Status = DoseStatus.Pending,
                Notes = "Evening dose reminder"
            },
            new MedicationDoseLog
            {
                TreatmentPlanId = treatmentPlan.Id,
                ScheduledAt = now.Date.AddDays(-1).AddHours(1),
                ConfirmedAt = now.Date.AddDays(-1).AddHours(1).AddMinutes(7),
                Status = DoseStatus.Taken,
                Notes = "Confirmed after reminder"
            }
        };

        var symptoms = new SymptomLog
        {
            PatientProfileId = patient.Id,
            LoggedAt = now.AddDays(-1),
            PersistentCough = true,
            FeverOrChills = false,
            NightSweats = false,
            WeightLossOrLowAppetite = false,
            RiskLevel = RiskLevel.Low,
            Feedback = "Gejala tercatat risiko rendah. Tetap minum obat sesuai jadwal."
        };

        var reminders = new[]
        {
            new Reminder
            {
                PatientProfileId = patient.Id,
                ScheduledAt = now.Date.AddHours(13),
                Status = ReminderStatus.Sent,
                Message = "Evening medicine pending"
            },
            new Reminder
            {
                PatientProfileId = patient.Id,
                ScheduledAt = now.AddDays(-2),
                Status = ReminderStatus.Resolved,
                Message = "Reminder resolved by patient"
            }
        };

        var notifications = new[]
        {
            new AppNotification
            {
                UserId = patientUser.Id,
                Type = NotificationType.Reminder,
                Title = "Morning dose completed",
                Message = "Today - 08:04 WIB",
                IsRead = true
            },
            new AppNotification
            {
                UserId = patientUser.Id,
                Type = NotificationType.Reminder,
                Title = "Evening dose reminder",
                Message = "Today - 19:55 WIB",
                IsRead = false
            },
            new AppNotification
            {
                UserId = patientUser.Id,
                Type = NotificationType.Alert,
                Title = "Symptom log submitted",
                Message = "Yesterday - low risk feedback",
                IsRead = true
            }
        };

        var weightLog = new WeightLog
        {
            PatientProfileId = patient.Id,
            Weight = 55m,
            RecordedAt = patient.TreatmentStartDate.ToDateTime(TimeOnly.MinValue),
            Notes = "Initial weight"
        };

        var labResult = new LabResult
        {
            PatientProfileId = patient.Id,
            TestType = LabTestType.TCM_GeneXpert,
            Result = LabResultValue.Positive,
            TestedAt = patient.TreatmentStartDate.ToDateTime(TimeOnly.MinValue),
            Notes = "Initial test",
            RecordedBy = doctorUser.FullName
        };

        db.Users.AddRange(admin, doctorUser, patientUser);
        db.DoctorProfiles.Add(doctor);
        db.PatientProfiles.Add(patient);
        db.TreatmentPlans.Add(treatmentPlan);
        db.MedicationDoseLogs.AddRange(doseLogs);
        db.SymptomLogs.Add(symptoms);
        db.Reminders.AddRange(reminders);
        db.Notifications.AddRange(notifications);
        db.WeightLogs.Add(weightLog);
        db.LabResults.Add(labResult);

        await db.SaveChangesAsync();
    }
}
