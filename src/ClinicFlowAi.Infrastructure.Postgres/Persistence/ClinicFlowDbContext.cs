using Microsoft.EntityFrameworkCore;

namespace ClinicFlowAi.Infrastructure.Postgres.Persistence;

public sealed class ClinicFlowDbContext(DbContextOptions<ClinicFlowDbContext> options) : DbContext(options)
{
    public DbSet<ClinicianEntity> Clinicians => Set<ClinicianEntity>();
    public DbSet<ScheduleRuleEntity> ScheduleRules => Set<ScheduleRuleEntity>();
    public DbSet<AppointmentSlotEntity> AppointmentSlots => Set<AppointmentSlotEntity>();
    public DbSet<BookingEntity> Bookings => Set<BookingEntity>();
    public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClinicianEntity>(e =>
        {
            e.ToTable("clinicians");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).IsRequired();
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.Role).IsRequired();
            e.Property(x => x.ClinicId).IsRequired();
        });

        modelBuilder.Entity<ScheduleRuleEntity>(e =>
        {
            e.ToTable("schedule_rules");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).IsRequired();
            e.Property(x => x.ClinicianId).IsRequired();
            e.Property(x => x.ClinicId).IsRequired();
            e.HasOne(x => x.Clinician)
                .WithMany(c => c.ScheduleRules)
                .HasForeignKey(x => x.ClinicianId);
        });

        modelBuilder.Entity<AppointmentSlotEntity>(e =>
        {
            e.ToTable("appointment_slots");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).IsRequired();
            e.Property(x => x.ClinicianId).IsRequired();
            e.Property(x => x.ClinicId).IsRequired();
            e.Property(x => x.AppointmentTypeCode).IsRequired();
            e.HasOne(x => x.Clinician)
                .WithMany(c => c.AppointmentSlots)
                .HasForeignKey(x => x.ClinicianId);
            e.HasIndex(x => new { x.ClinicianId, x.StartsAtUtc });
        });

        modelBuilder.Entity<BookingEntity>(e =>
        {
            e.ToTable("bookings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).IsRequired();
            e.Property(x => x.SlotId).IsRequired();
            e.Property(x => x.PatientReferenceId).IsRequired();
            e.Property(x => x.Status).IsRequired();
            e.HasOne(x => x.Slot)
                .WithOne(s => s.Booking)
                .HasForeignKey<BookingEntity>(x => x.SlotId);
            e.HasIndex(x => x.SlotId);
        });

        modelBuilder.Entity<AuditEventEntity>(e =>
        {
            e.ToTable("audit_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).IsRequired();
            e.Property(x => x.EntityType).IsRequired();
            e.Property(x => x.EntityId).IsRequired();
            e.Property(x => x.EventType).IsRequired();
            e.Property(x => x.Payload).IsRequired();
        });
    }
}
