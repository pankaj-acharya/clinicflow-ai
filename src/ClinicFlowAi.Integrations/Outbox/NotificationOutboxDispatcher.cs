using ClinicFlowAi.Integrations.Connectors;

namespace ClinicFlowAi.Integrations.Outbox;

public sealed class NotificationOutboxDispatcher(IMockConnector connector)
{
    public async Task<NotificationOutboxItem> DispatchAsync(NotificationOutboxItem item, CancellationToken cancellationToken)
    {
        await connector.SendAsync(item, cancellationToken);
        return item with { DispatchedAtUtc = DateTimeOffset.UtcNow, Attempts = item.Attempts + 1 };
    }
}
