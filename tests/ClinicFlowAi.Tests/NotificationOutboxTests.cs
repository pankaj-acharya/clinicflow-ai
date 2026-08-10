using ClinicFlowAi.Integrations.Connectors;
using ClinicFlowAi.Integrations.Outbox;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClinicFlowAi.Tests;

[TestClass]
public sealed class NotificationOutboxTests
{
    [TestMethod]
    public async Task DispatchAsync_marks_item_dispatched()
    {
        var dispatcher = new NotificationOutboxDispatcher(new StubConnector());
        var item = new NotificationOutboxItem("1", "booking.confirmed", "{}", DateTimeOffset.UtcNow, null, 0);

        var dispatched = await dispatcher.DispatchAsync(item, CancellationToken.None);

        Assert.IsNotNull(dispatched.DispatchedAtUtc);
        Assert.AreEqual(1, dispatched.Attempts);
    }

    private sealed class StubConnector : IMockConnector
    {
        public Task SendAsync(NotificationOutboxItem item, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
