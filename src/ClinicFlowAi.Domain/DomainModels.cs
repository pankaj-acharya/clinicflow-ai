namespace ClinicFlowAi.Domain;

public enum AppointmentState
{
    Held,
    Confirmed,
    Cancelled,
    Expired,
    Rescheduled
}

public sealed record AppointmentType(string Code, string Name, TimeSpan Duration);

public sealed record Clinic(string Id, string Name, string TimeZoneId);

public sealed record Clinician(string Id, string ClinicId, string DisplayName);

public sealed record ScheduleRule(string ClinicId, string ClinicianId, DayOfWeek DayOfWeek, TimeOnly Start, TimeOnly End);

public sealed record ScheduleVersion(string ClinicId, int Version, DateTimeOffset CreatedAtUtc);

public sealed record Closure(string ClinicId, DateOnly Date, string Reason);

public sealed record PatientReference(string Id, string ExternalReference);

public sealed record SlotHold(string Id, string ClinicId, string ClinicianId, DateTimeOffset StartsAtUtc, DateTimeOffset ExpiresAtUtc);

public sealed record Appointment(
    string Id,
    string ClinicId,
    string ClinicianId,
    string PatientReferenceId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    AppointmentState State);

public sealed record AuditEvent(string Id, string Actor, string Action, DateTimeOffset OccurredAtUtc);

public sealed record AvailabilitySlot(DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc);

