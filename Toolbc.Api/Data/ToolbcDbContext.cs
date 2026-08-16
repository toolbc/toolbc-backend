using Microsoft.EntityFrameworkCore;
using Toolbc.Api.Domain;

namespace Toolbc.Api.Data;

public sealed class ToolbcDbContext(DbContextOptions<ToolbcDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<PatientProfile> PatientProfiles => Set<PatientProfile>();
    public DbSet<DoctorProfile> DoctorProfiles => Set<DoctorProfile>();
    public DbSet<TreatmentPlan> TreatmentPlans => Set<TreatmentPlan>();
    public DbSet<MedicationDoseLog> MedicationDoseLogs => Set<MedicationDoseLog>();
    public DbSet<SymptomLog> SymptomLogs => Set<SymptomLog>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<AppNotification> Notifications => Set<AppNotification>();
    public DbSet<WeightLog> WeightLogs => Set<WeightLog>();
    public DbSet<LabResult> LabResults => Set<LabResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(user => user.Email).IsUnique();
            entity.Property(user => user.Email).HasMaxLength(160);
            entity.Property(user => user.FullName).HasMaxLength(120);
            entity.Property(user => user.Role).HasConversion<string>();
        });

        modelBuilder.Entity<PatientProfile>()
            .HasOne(profile => profile.AssignedDoctor)
            .WithMany(doctor => doctor.Patients)
            .HasForeignKey(profile => profile.AssignedDoctorId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<PatientProfile>()
            .HasOne(profile => profile.TreatmentPlan)
            .WithOne(plan => plan.PatientProfile)
            .HasForeignKey<TreatmentPlan>(plan => plan.PatientProfileId);

        modelBuilder.Entity<TreatmentPlan>()
            .Property(plan => plan.Status)
            .HasConversion<string>();

        modelBuilder.Entity<MedicationDoseLog>()
            .Property(log => log.Status)
            .HasConversion<string>();

        modelBuilder.Entity<SymptomLog>()
            .Property(log => log.RiskLevel)
            .HasConversion<string>();

        modelBuilder.Entity<Reminder>()
            .Property(reminder => reminder.Status)
            .HasConversion<string>();

        modelBuilder.Entity<AppNotification>()
            .Property(notification => notification.Type)
            .HasConversion<string>();

        modelBuilder.Entity<TreatmentPlan>()
            .Property(plan => plan.Phase)
            .HasConversion<string>();
            
        modelBuilder.Entity<LabResult>()
            .Property(result => result.TestType)
            .HasConversion<string>();

        modelBuilder.Entity<LabResult>()
            .Property(result => result.Result)
            .HasConversion<string>();
    }
}
