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
