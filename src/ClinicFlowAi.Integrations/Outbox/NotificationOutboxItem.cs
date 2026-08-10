namespace ClinicFlowAi.Integrations.Outbox;

public sealed record NotificationOutboxItem(string Id, string Kind, DateTimeOffset CreatedAtUtc);
