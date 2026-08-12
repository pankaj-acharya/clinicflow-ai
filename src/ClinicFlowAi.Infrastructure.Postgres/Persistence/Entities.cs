namespace ClinicFlowAi.Infrastructure.Postgres.Persistence;

public sealed class ClinicianEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string ClinicId { get; set; } = string.Empty;

    public ICollection<ScheduleRuleEntity> ScheduleRules { get; set; } = [];
    public ICollection<AppointmentSlotEntity> AppointmentSlots { get; set; } = [];
}

public sealed class ScheduleRuleEntity
{
    public string Id { get; set; } = string.Empty;
    public string ClinicianId { get; set; } = string.Empty;
    public string ClinicId { get; set; } = string.Empty;
    public int DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public ClinicianEntity Clinician { get; set; } = null!;
}

public sealed class AppointmentSlotEntity
{
    public string Id { get; set; } = string.Empty;
    public string ClinicianId { get; set; } = string.Empty;
    public string ClinicId { get; set; } = string.Empty;
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset EndsAtUtc { get; set; }
    public bool IsBooked { get; set; }
    public string AppointmentTypeCode { get; set; } = string.Empty;

    public ClinicianEntity Clinician { get; set; } = null!;
    public BookingEntity? Booking { get; set; }
}

public sealed class BookingEntity
{
    public string Id { get; set; } = string.Empty;
    public string SlotId { get; set; } = string.Empty;
    public string PatientReferenceId { get; set; } = string.Empty;
    public DateTimeOffset ConfirmedAtUtc { get; set; }
    public string Status { get; set; } = "confirmed";

    public AppointmentSlotEntity Slot { get; set; } = null!;
}

public sealed class AuditEventEntity
{
    public string Id { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string Payload { get; set; } = string.Empty;
}
