namespace ClinicFlowAi.Integrations.Outbox;

public sealed record NotificationOutboxItem(
    string Id,
    string Kind,
    string PayloadJson,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DispatchedAtUtc,
    int Attempts);
