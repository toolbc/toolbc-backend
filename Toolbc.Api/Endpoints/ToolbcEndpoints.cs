using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Toolbc.Api.Contracts;
using Toolbc.Api.Data;
using Toolbc.Api.Domain;
using Toolbc.Api.Services;

namespace Toolbc.Api.Endpoints;

public static class ToolbcEndpoints
{
    public static IEndpointRouteBuilder MapToolbcEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            service = "ToolBC Backend",
            time = DateTimeOffset.UtcNow
        }));

        api.MapPost("/auth/login", async (
            LoginRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var response = await authService.LoginAsync(request, cancellationToken);
            return response is null
                ? Results.Unauthorized()
                : Results.Ok(response);
        });

        api.MapPost("/bootstrap/admin", async (
            CreateUserRequest request,
            ToolbcDbContext db,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            if (await db.Users.AnyAsync(cancellationToken))
            {
                return Results.Conflict(new { error = "Bootstrap ditutup karena user sudah ada." });
            }

            if (request.Role != UserRole.Admin)
            {
                return Results.BadRequest(new { error = "Bootstrap pertama hanya boleh membuat admin." });
            }

            var result = await authService.CreateUserAsync(request, "bootstrap", cancellationToken);
            return result.User is null
                ? Results.BadRequest(new { error = result.Error })
                : Results.Created($"/api/admin/users/{result.User.Id}", result.User);
        });

        api.MapPost("/admin/users", async (
            CreateUserRequest request,
            ClaimsPrincipal principal,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var createdBy = principal.FindFirstValue(ClaimTypes.Email) ?? "admin";
            var result = await authService.CreateUserAsync(request, createdBy, cancellationToken);
            return result.User is null
                ? Results.BadRequest(new { error = result.Error })
                : Results.Created($"/api/admin/users/{result.User.Id}", result.User);
        }).RequireAuthorization("AdminOnly");

        api.MapGet("/admin/users", async (
            ToolbcDbContext db,
            UserRole? role,
            CancellationToken cancellationToken) =>
        {
            var query = db.Users.AsNoTracking();
            if (role.HasValue)
            {
                query = query.Where(user => user.Role == role.Value);
            }

            var users = await query
                .OrderBy(user => user.FullName)
                .Select(user => AuthService.ToUserResponse(user))
                .ToListAsync(cancellationToken);

            return Results.Ok(users);
        }).RequireAuthorization("AdminOnly");

        api.MapGet("/admin/doctors", async (
            ToolbcDbContext db,
            CancellationToken cancellationToken) =>
        {
            var doctors = await db.DoctorProfiles
                .AsNoTracking()
                .Include(profile => profile.User)
                .OrderBy(profile => profile.User.FullName)
                .Select(profile => new
                {
                    profile.Id,
                    profile.User.FullName,
                    profile.User.Email,
                    profile.Specialty
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(doctors);
        }).RequireAuthorization("AdminOnly");

        api.MapGet("/patients/me/dashboard", GetPatientDashboardAsync)
            .RequireAuthorization("PatientOnly");

        api.MapPost("/patients/me/medication-logs", ConfirmMedicationLogAsync)
            .RequireAuthorization("PatientOnly");

        api.MapPost("/patients/me/symptom-logs", CreateSymptomLogAsync)
            .RequireAuthorization("PatientOnly");

        api.MapGet("/patients/me/history", GetPatientHistoryAsync)
            .RequireAuthorization("PatientOnly");

        api.MapGet("/notifications", GetNotificationsAsync)
            .RequireAuthorization();

        api.MapPost("/chat/reply", async (
            ChatReplyRequest request,
            IGeminiChatService chatService,
            CancellationToken cancellationToken) =>
        {
            var response = await chatService.GenerateReplyAsync(request, cancellationToken);
            return Results.Ok(response);
        }).RequireAuthorization();

        api.MapGet("/doctors/me/dashboard", GetDoctorDashboardAsync)
            .RequireAuthorization("DoctorOnly");

        api.MapGet("/doctors/me/patients", GetDoctorPatientsAsync)
            .RequireAuthorization("DoctorOnly");

        api.MapGet("/doctors/me/adherence", GetDoctorAdherenceAsync)
            .RequireAuthorization("DoctorOnly");

        api.MapGet("/doctors/me/reminders", GetDoctorRemindersAsync)
            .RequireAuthorization("DoctorOnly");

        api.MapPatch("/reminders/{id:guid}/status", UpdateReminderStatusAsync)
            .RequireAuthorization("DoctorOnly");

        api.MapPost("/doctor/patients/{patientProfileId:guid}/transition-phase", TransitionPhaseAsync)
            .RequireAuthorization("DoctorOnly");

        api.MapPost("/patient/weight", RecordWeightAsync)
            .RequireAuthorization("PatientOnly");

        api.MapGet("/doctor/patients/{patientProfileId:guid}/weight-history", GetWeightHistoryAsync)
            .RequireAuthorization("DoctorOnly");

        api.MapPost("/doctor/patients/{patientProfileId:guid}/lab-results", AddLabResultAsync)
            .RequireAuthorization("DoctorOnly");

        api.MapGet("/doctor/patients/{patientProfileId:guid}/lab-results", GetLabResultsAsync)
            .RequireAuthorization("DoctorOnly");

        return app;
    }

    private static async Task<IResult> GetPatientDashboardAsync(
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var profile = await LoadPatientProfile(db, principal.GetUserId(), cancellationToken);
        if (profile is null || profile.TreatmentPlan is null)
        {
            return Results.NotFound(new { error = "Profil pasien belum tersedia." });
        }

        var now = DateTimeOffset.UtcNow;
        var overdueDoses = profile.TreatmentPlan.DoseLogs
            .Where(log => log.Status == DoseStatus.Pending && log.ScheduledAt <= now.AddHours(-12))
            .ToList();
            
        if (overdueDoses.Count > 0)
        {
            foreach (var dose in overdueDoses)
            {
                dose.Status = DoseStatus.Missed;
            }
            await db.SaveChangesAsync(cancellationToken);
        }


        var notifications = (await db.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == profile.UserId)
            .Select(notification => new NotificationDto(
                notification.Id,
                notification.Type,
                notification.Title,
                notification.Message,
                notification.IsRead,
                notification.CreatedAt))
            .ToListAsync(cancellationToken))
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(5)
            .ToList();

        return Results.Ok(new PatientDashboardResponse(
            AuthService.ToUserResponse(profile.User),
            profile.MedicalRecordNumber,
            profile.AssignedDoctor?.User.FullName,
            BuildTreatmentSummary(profile),
            notifications));
    }

    private static async Task<IResult> ConfirmMedicationLogAsync(
        MedicationLogRequest request,
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        MedicationDoseLog? doseLog = null;

        if (string.Equals(request.DoseLogId, "today", StringComparison.OrdinalIgnoreCase))
        {
            doseLog = (await db.MedicationDoseLogs
                .AsTracking()
                .Include(log => log.TreatmentPlan)
                .ThenInclude(plan => plan.PatientProfile)
                .Where(log => log.TreatmentPlan.PatientProfile.UserId == userId && log.Status == DoseStatus.Pending)
                .ToListAsync(cancellationToken))
                .OrderBy(log => log.ScheduledAt)
                .FirstOrDefault();
        }
        else if (Guid.TryParse(request.DoseLogId, out var parsedGuid))
        {
            doseLog = await db.MedicationDoseLogs
                .AsTracking()
                .Include(log => log.TreatmentPlan)
                .ThenInclude(plan => plan.PatientProfile)
                .FirstOrDefaultAsync(
                    log => log.Id == parsedGuid && log.TreatmentPlan.PatientProfile.UserId == userId,
                    cancellationToken);
        }

        if (doseLog is null)
        {
            return Results.NotFound(new { error = "Log obat tidak ditemukan." });
        }

        doseLog.Status = request.Status;
        doseLog.Notes = request.Notes ?? doseLog.Notes;
        doseLog.ConfirmedAt = request.Status == DoseStatus.Taken ? DateTimeOffset.UtcNow : doseLog.ConfirmedAt;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new
        {
            doseLog.Id,
            doseLog.Status,
            doseLog.ConfirmedAt
        });
    }

    private static async Task<IResult> CreateSymptomLogAsync(
        SymptomLogRequest request,
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var profile = await db.PatientProfiles.FirstOrDefaultAsync(
            item => item.UserId == principal.GetUserId(),
            cancellationToken);
        if (profile is null)
        {
            return Results.NotFound(new { error = "Profil pasien belum tersedia." });
        }

        var risk = AnalyzeRisk(request);
        var feedback = risk switch
        {
            RiskLevel.High => "Risiko tinggi. Hubungi dokter atau fasilitas kesehatan segera, terutama bila sesak, batuk darah, atau demam tinggi menetap.",
            RiskLevel.Moderate => "Risiko sedang. Tetap minum obat dan laporkan gejala yang menetap ke dokter.",
            _ => "Risiko rendah. Gejala tercatat, lanjutkan pengobatan sesuai jadwal."
        };

        var symptomLog = new SymptomLog
        {
            PatientProfileId = profile.Id,
            PersistentCough = request.PersistentCough,
            FeverOrChills = request.FeverOrChills,
            NightSweats = request.NightSweats,
            WeightLossOrLowAppetite = request.WeightLossOrLowAppetite,
            RiskLevel = risk,
            Feedback = feedback
        };

        db.SymptomLogs.Add(symptomLog);
        db.Notifications.Add(new AppNotification
        {
            UserId = profile.UserId,
            Type = NotificationType.Alert,
            Title = "Symptom log submitted",
            Message = $"{risk} risk feedback",
            IsRead = false
        });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created(
            $"/api/patients/me/symptom-logs/{symptomLog.Id}",
            new SymptomLogResponse(symptomLog.Id, symptomLog.RiskLevel, symptomLog.Feedback, symptomLog.LoggedAt));
    }

    private static async Task<IResult> GetPatientHistoryAsync(
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var profile = await db.PatientProfiles
            .AsNoTracking()
            .Include(item => item.TreatmentPlan)
            .FirstOrDefaultAsync(item => item.UserId == principal.GetUserId(), cancellationToken);
        if (profile?.TreatmentPlan is null)
        {
            return Results.NotFound(new { error = "Profil pasien belum tersedia." });
        }

        var doseItems = (await db.MedicationDoseLogs
            .AsNoTracking()
            .Where(log => log.TreatmentPlanId == profile.TreatmentPlan.Id)
            .ToListAsync(cancellationToken))
            .OrderByDescending(log => log.ScheduledAt)
            .Take(20)
            .Select(log => new HistoryItemDto(
                log.Status == DoseStatus.Taken ? "Medicine completed" : "Medicine reminder",
                log.Notes ?? log.ScheduledAt.ToString("u"),
                "medication",
                log.ConfirmedAt ?? log.ScheduledAt))
            .ToList();

        var symptomItems = (await db.SymptomLogs
            .AsNoTracking()
            .Where(log => log.PatientProfileId == profile.Id)
            .ToListAsync(cancellationToken))
            .OrderByDescending(log => log.LoggedAt)
            .Take(20)
            .Select(log => new HistoryItemDto(
                "Symptom log submitted",
                $"{log.RiskLevel} risk feedback",
                "symptom",
                log.LoggedAt))
            .ToList();

        return Results.Ok(doseItems.Concat(symptomItems).OrderByDescending(item => item.CreatedAt).ToList());
    }

    private static async Task<IResult> GetNotificationsAsync(
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        [FromQuery] NotificationType? type,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var query = db.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId);
        if (type.HasValue)
        {
            query = query.Where(notification => notification.Type == type.Value);
        }

        var response = (await query
            .Select(notification => new NotificationDto(
                notification.Id,
                notification.Type,
                notification.Title,
                notification.Message,
                notification.IsRead,
                notification.CreatedAt))
            .ToListAsync(cancellationToken))
            .OrderByDescending(notification => notification.CreatedAt)
            .ToList();

        return Results.Ok(response);
    }

    private static async Task<IResult> GetDoctorDashboardAsync(
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var doctor = await LoadDoctorProfile(db, principal.GetUserId(), cancellationToken);
        if (doctor is null)
        {
            return Results.NotFound(new { error = "Profil dokter belum tersedia." });
        }

        var patientIds = doctor.Patients.Select(patient => patient.Id).ToArray();
        var urgentAlerts = await db.SymptomLogs.CountAsync(
            log => patientIds.Contains(log.PatientProfileId) && log.RiskLevel == RiskLevel.High,
            cancellationToken);
        var pendingFollowUp = await db.Reminders.CountAsync(
            reminder => patientIds.Contains(reminder.PatientProfileId) &&
                        (reminder.Status == ReminderStatus.Pending || reminder.Status == ReminderStatus.Escalated),
            cancellationToken);

        var todayUtc = DateTime.UtcNow.Date;
        var todayReviews = await db.SymptomLogs.CountAsync(
            log => patientIds.Contains(log.PatientProfileId) && log.LoggedAt >= todayUtc,
            cancellationToken);

        return Results.Ok(new DoctorDashboardResponse(
            AuthService.ToUserResponse(doctor.User),
            doctor.Patients.Count,
            urgentAlerts,
            todayReviews,
            pendingFollowUp));
    }

    private static async Task<IResult> GetDoctorPatientsAsync(
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var doctor = await LoadDoctorProfile(db, principal.GetUserId(), cancellationToken);
        if (doctor is null)
        {
            return Results.NotFound(new { error = "Profil dokter belum tersedia." });
        }

        var patientIds = doctor.Patients.Select(patient => patient.Id).ToArray();
        var allSymptomLogs = await db.SymptomLogs
            .Where(log => patientIds.Contains(log.PatientProfileId))
            .ToListAsync(cancellationToken);
        var latestRisks = allSymptomLogs
            .GroupBy(log => log.PatientProfileId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(log => log.LoggedAt).First().RiskLevel);

        var response = doctor.Patients.Select(patient =>
        {
            var summary = BuildTreatmentSummary(patient);
            var risk = latestRisks.TryGetValue(patient.Id, out var r) ? r : RiskLevel.Low;
            var phaseTransitionDue = summary.TreatmentDay >= 56 && patient.TreatmentPlan?.Phase == TreatmentPhase.Intensive;
            return new DoctorPatientDto(
                patient.Id,
                patient.User.FullName,
                patient.MedicalRecordNumber,
                summary.TreatmentDay,
                summary.AdherencePercent,
                risk,
                summary.AdherencePercent < 80 ? "Needs review" : "Stable",
                phaseTransitionDue,
                patient.Weight);
        });

        return Results.Ok(response.ToList());
    }

    private static async Task<IResult> GetDoctorAdherenceAsync(
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var doctor = await LoadDoctorProfile(db, principal.GetUserId(), cancellationToken);
        if (doctor is null)
        {
            return Results.NotFound(new { error = "Profil dokter belum tersedia." });
        }

        var summaries = doctor.Patients.Select(BuildTreatmentSummary).ToArray();
        var buckets = new[]
        {
            new AdherenceBucketDto("High Risk", summaries.Count(item => item.AdherencePercent < 75)),
            new AdherenceBucketDto("Moderate Risk", summaries.Count(item => item.AdherencePercent is >= 75 and < 90)),
            new AdherenceBucketDto("Stable", summaries.Count(item => item.AdherencePercent >= 90))
        };

        return Results.Ok(buckets);
    }

    private static async Task<IResult> GetDoctorRemindersAsync(
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var doctor = await LoadDoctorProfile(db, principal.GetUserId(), cancellationToken);
        if (doctor is null)
        {
            return Results.NotFound(new { error = "Profil dokter belum tersedia." });
        }

        var patientIds = doctor.Patients.Select(patient => patient.Id).ToArray();
        var reminders = (await db.Reminders
            .AsNoTracking()
            .Include(reminder => reminder.PatientProfile)
            .ThenInclude(patient => patient.User)
            .Where(reminder => patientIds.Contains(reminder.PatientProfileId))
            .ToListAsync(cancellationToken))
            .OrderByDescending(reminder => reminder.ScheduledAt)
            .Select(reminder => new ReminderDto(
                reminder.Id,
                reminder.PatientProfile.User.FullName,
                reminder.Message,
                reminder.Status,
                reminder.ScheduledAt))
            .ToList();

        return Results.Ok(reminders);
    }

    private static async Task<IResult> UpdateReminderStatusAsync(
        Guid id,
        ReminderStatus status,
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var doctor = await LoadDoctorProfile(db, principal.GetUserId(), cancellationToken);
        if (doctor is null)
        {
            return Results.NotFound(new { error = "Profil dokter belum tersedia." });
        }

        var patientIds = doctor.Patients.Select(patient => patient.Id).ToHashSet();

        var reminder = await db.Reminders.FindAsync([id], cancellationToken);
        if (reminder is null || !patientIds.Contains(reminder.PatientProfileId))
        {
            return Results.NotFound(new { error = "Reminder tidak ditemukan." });
        }

        reminder.Status = status;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { reminder.Id, reminder.Status });
    }

    private static async Task<PatientProfile?> LoadPatientProfile(
        ToolbcDbContext db,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await db.PatientProfiles
            .AsNoTracking()
            .AsSplitQuery()
            .Include(profile => profile.User)
            .Include(profile => profile.AssignedDoctor)
            .ThenInclude(doctor => doctor!.User)
            .Include(profile => profile.TreatmentPlan)
            .ThenInclude(plan => plan!.DoseLogs)
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);
    }

    private static async Task<DoctorProfile?> LoadDoctorProfile(
        ToolbcDbContext db,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await db.DoctorProfiles
            .AsNoTracking()
            .AsSplitQuery()
            .Include(profile => profile.User)
            .Include(profile => profile.Patients)
            .ThenInclude(patient => patient.User)
            .Include(profile => profile.Patients)
            .ThenInclude(patient => patient.TreatmentPlan)
            .ThenInclude(plan => plan!.DoseLogs)
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);
    }

    private static TreatmentSummaryDto BuildTreatmentSummary(PatientProfile profile)
    {
        var plan = profile.TreatmentPlan!;
        var startDate = profile.TreatmentStartDate.ToDateTime(TimeOnly.MinValue);
        var treatmentDay = Math.Max(1, (DateTime.UtcNow.Date - startDate.Date).Days + 1);
        var completion = Math.Min(100, treatmentDay * 100 / plan.TotalDays);
        var consideredLogs = plan.DoseLogs
            .Where(log => log.Status is DoseStatus.Taken or DoseStatus.Missed)
            .ToArray();
        var adherence = consideredLogs.Length == 0
            ? 100
            : (int)Math.Round(consideredLogs.Count(log => log.Status == DoseStatus.Taken) * 100d / consideredLogs.Length);
        var streak = plan.DoseLogs
            .Where(log => log.Status == DoseStatus.Taken)
            .Select(log => DateOnly.FromDateTime((log.ConfirmedAt ?? log.ScheduledAt).UtcDateTime))
            .Distinct()
            .OrderDescending()
            .TakeWhileConsecutiveDays();
        var nextDose = plan.DoseLogs
            .Where(log => log.Status == DoseStatus.Pending)
            .OrderBy(log => log.ScheduledAt)
            .FirstOrDefault();

        return new TreatmentSummaryDto(
            treatmentDay,
            plan.TotalDays,
            completion,
            adherence,
            streak,
            plan.MedicineSummary,
            nextDose is null ? "No pending dose" : nextDose.ScheduledAt.ToLocalTime().ToString("ddd HH:mm"),
            plan.Phase);
    }

    private static RiskLevel AnalyzeRisk(SymptomLogRequest request)
    {
        var count = new[]
        {
            request.PersistentCough,
            request.FeverOrChills,
            request.NightSweats,
            request.WeightLossOrLowAppetite
        }.Count(value => value);

        if (count >= 3 || (request.FeverOrChills && (request.NightSweats || request.WeightLossOrLowAppetite)))
        {
            return RiskLevel.High;
        }

        return count >= 2 ? RiskLevel.Moderate : RiskLevel.Low;
    }

    private static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("Token tidak memiliki user id valid.");
    }

    private static int TakeWhileConsecutiveDays(this IEnumerable<DateOnly> dates)
    {
        var expected = DateOnly.FromDateTime(DateTime.UtcNow);
        var streak = 0;
        foreach (var date in dates)
        {
            if (date != expected)
            {
                if (streak == 0 && date == expected.AddDays(-1))
                {
                    expected = date;
                }
                else
                {
                    break;
                }
            }

            streak++;
            expected = expected.AddDays(-1);
        }

        return streak;
    }

    private static async Task<IResult> TransitionPhaseAsync(
        Guid patientProfileId,
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var doctorId = principal.GetUserId();
        var patient = await db.PatientProfiles
            .Include(p => p.TreatmentPlan)
            .Include(p => p.User)
            .Include(p => p.AssignedDoctor)
            .FirstOrDefaultAsync(p => p.Id == patientProfileId && p.AssignedDoctor!.UserId == doctorId, cancellationToken);

        if (patient is null || patient.TreatmentPlan is null)
        {
            return Results.NotFound(new { error = "Pasien tidak ditemukan atau bukan pasien Anda." });
        }

        var plan = patient.TreatmentPlan;
        if (plan.Phase == TreatmentPhase.Intensive)
        {
            plan.Phase = TreatmentPhase.Continuation;
            plan.MedicineSummary = "Isoniazid (H) + Rifampicin (R)";
        }
        else if (plan.Phase == TreatmentPhase.Continuation)
        {
            plan.Phase = TreatmentPhase.Completed;
            plan.Status = TreatmentStatus.Completed;
        }

        db.Notifications.Add(new AppNotification
        {
            UserId = patient.UserId,
            Type = NotificationType.Alert,
            Title = "Fase Pengobatan Diperbarui",
            Message = $"Fase pengobatan Anda sekarang adalah {plan.Phase}.",
            IsRead = false
        });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { plan.Phase, plan.MedicineSummary });
    }

    private static async Task<IResult> RecordWeightAsync(
        WeightRequest request,
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var patient = await db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        
        if (patient is null)
        {
            return Results.NotFound(new { error = "Profil pasien tidak ditemukan." });
        }

        var now = DateTimeOffset.UtcNow;
        patient.Weight = request.Weight;
        patient.WeightRecordedAt = now;

        var weightLog = new WeightLog
        {
            PatientProfileId = patient.Id,
            Weight = request.Weight,
            RecordedAt = now,
            Notes = request.Notes
        };

        db.WeightLogs.Add(weightLog);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new WeightLogDto(weightLog.Id, weightLog.Weight, weightLog.RecordedAt, weightLog.Notes));
    }

    private static async Task<IResult> GetWeightHistoryAsync(
        Guid patientProfileId,
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var doctorId = principal.GetUserId();
        var isDoctorPatient = await db.PatientProfiles
            .Include(p => p.AssignedDoctor)
            .AnyAsync(p => p.Id == patientProfileId && p.AssignedDoctor!.UserId == doctorId, cancellationToken);
        
        if (!isDoctorPatient)
        {
            return Results.NotFound(new { error = "Pasien tidak ditemukan atau bukan pasien Anda." });
        }

        var logs = (await db.WeightLogs
            .AsNoTracking()
            .Where(w => w.PatientProfileId == patientProfileId)
            .ToListAsync(cancellationToken))
            .OrderByDescending(w => w.RecordedAt)
            .Select(w => new WeightLogDto(w.Id, w.Weight, w.RecordedAt, w.Notes))
            .ToList();

        return Results.Ok(logs);
    }

    private static async Task<IResult> AddLabResultAsync(
        Guid patientProfileId,
        LabResultRequest request,
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var doctorId = principal.GetUserId();
        var doctor = await db.DoctorProfiles.Include(d => d.User).FirstOrDefaultAsync(d => d.UserId == doctorId, cancellationToken);
        var isDoctorPatient = await db.PatientProfiles
            .Include(p => p.AssignedDoctor)
            .AnyAsync(p => p.Id == patientProfileId && p.AssignedDoctor!.UserId == doctorId, cancellationToken);
        
        if (doctor is null || !isDoctorPatient)
        {
            return Results.NotFound(new { error = "Pasien tidak ditemukan atau bukan pasien Anda." });
        }

        var labResult = new LabResult
        {
            PatientProfileId = patientProfileId,
            TestType = request.TestType,
            Result = request.Result,
            TestedAt = DateTimeOffset.UtcNow,
            Notes = request.Notes,
            RecordedBy = doctor.User.FullName
        };

        db.LabResults.Add(labResult);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new LabResultDto(labResult.Id, labResult.TestType, labResult.Result, labResult.TestedAt, labResult.Notes, labResult.RecordedBy));
    }

    private static async Task<IResult> GetLabResultsAsync(
        Guid patientProfileId,
        ClaimsPrincipal principal,
        ToolbcDbContext db,
        CancellationToken cancellationToken)
    {
        var doctorId = principal.GetUserId();
        var isDoctorPatient = await db.PatientProfiles
            .Include(p => p.AssignedDoctor)
            .AnyAsync(p => p.Id == patientProfileId && p.AssignedDoctor!.UserId == doctorId, cancellationToken);
        
        if (!isDoctorPatient)
        {
            return Results.NotFound(new { error = "Pasien tidak ditemukan atau bukan pasien Anda." });
        }

        var results = (await db.LabResults
            .AsNoTracking()
            .Where(l => l.PatientProfileId == patientProfileId)
            .ToListAsync(cancellationToken))
            .OrderByDescending(l => l.TestedAt)
            .Select(l => new LabResultDto(l.Id, l.TestType, l.Result, l.TestedAt, l.Notes, l.RecordedBy))
            .ToList();

        return Results.Ok(results);
    }
}
