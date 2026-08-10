using ClinicFlowAi.Integrations.Outbox;

namespace ClinicFlowAi.Integrations.Connectors;

public interface IMockConnector
{
    Task SendAsync(NotificationOutboxItem item, CancellationToken cancellationToken);
}
