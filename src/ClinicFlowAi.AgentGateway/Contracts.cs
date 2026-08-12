namespace ClinicFlowAi.AgentGateway;

public sealed record AgentBookingAction(
	string ClinicId,
	string ClinicianId,
	DateTimeOffset StartsAtUtc,
	DateTimeOffset EndsAtUtc);

public sealed record AgentCheckAvailabilityRequest(
	string ClinicId,
	string ClinicianId,
	DateTimeOffset WindowStartUtc,
	DateTimeOffset WindowEndUtc,
	string AppointmentTypeCode);

public sealed record AgentAvailabilitySlot(DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc);

public sealed record AgentCheckAvailabilityResponse(IReadOnlyList<AgentAvailabilitySlot> Slots);

public sealed record AgentFaqQuery(string Question);

public sealed record AgentNlSchedulingRequest(
    string Prompt,
    string? ClinicianRole,
    string? ClinicianName,
    string[]? PreferredDays,
    string? PreferredTimeOfDay,
    int MaxResults);

public sealed record AgentNlSlotOption(
    string SlotId,
    string ClinicianId,
    string ClinicianName,
    string ClinicianRole,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string DisplayLabel);

public sealed record AgentNlSchedulingResponse(
    string InterpretedIntent,
    IReadOnlyList<AgentNlSlotOption> Slots,
    string? Message);

public sealed record AgentCreateHoldRequest(
	string ClinicId,
	string ClinicianId,
	DateTimeOffset StartsAtUtc,
	int HoldDurationMinutes);

public sealed record AgentCreateHoldResponse(
	string HoldId,
	string ClinicId,
	string ClinicianId,
	DateTimeOffset StartsAtUtc,
	DateTimeOffset ExpiresAtUtc);

public sealed record AgentConfirmBookingRequest(
	string ClinicId,
	string ClinicianId,
	string PatientReferenceId,
	DateTimeOffset StartsAtUtc,
	DateTimeOffset EndsAtUtc);

public sealed record AgentConfirmBookingResponse(
	string BookingId,
	string Status,
	DateTimeOffset StartsAtUtc,
	DateTimeOffset EndsAtUtc);
