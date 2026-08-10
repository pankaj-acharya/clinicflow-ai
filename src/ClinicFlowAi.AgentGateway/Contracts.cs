namespace ClinicFlowAi.AgentGateway;

public sealed record AgentBookingAction(string ClinicId, string ClinicianId, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc);

public sealed record AgentFaqQuery(string Question);
