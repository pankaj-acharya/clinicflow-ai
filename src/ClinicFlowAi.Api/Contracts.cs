namespace ClinicFlowAi.Api;

public sealed record AvailabilityQuery(
    string ClinicId,
    string ClinicianId,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    string AppointmentTypeCode);

public sealed record SlotHoldRequest(
    string ClinicId,
    string ClinicianId,
    DateTimeOffset StartsAtUtc,
    int HoldDurationMinutes);

public sealed record BookingRequestDto(
    string ClinicId,
    string ClinicianId,
    string PatientReferenceId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc);
