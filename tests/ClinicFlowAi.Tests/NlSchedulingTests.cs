extern alias ApiHost;
extern alias GatewayHost;

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClinicFlowAi.Tests;

// ---------------------------------------------------------------------------
// Shared helpers
// ---------------------------------------------------------------------------

file sealed class StubHttpMessageHandler(string payload, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}

file sealed class FailingHttpMessageHandler(HttpStatusCode statusCode) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(statusCode));
}

// ---------------------------------------------------------------------------
// API /ask tests
// ---------------------------------------------------------------------------

[TestClass]
public sealed class ApiNlSchedulingTests
{
    private const string GatewayStubPayload =
        """{"interpretedIntent":"stub","slots":[{"slotId":"s1","clinicianId":"c1","clinicianName":"Dr Test","clinicianRole":"dentist","startsAtUtc":"2026-08-13T09:00:00Z","endsAtUtc":"2026-08-13T09:30:00Z","displayLabel":"Wednesday 13 Aug, 09:00 \u2013 09:30"}],"message":null}""";

    [TestMethod]
    public async Task Api_ask_returns_nl_scheduling_response()
    {
        await using var factory = new WebApplicationFactory<ApiHost::Program>()
            .WithWebHostBuilder(b => b.ConfigureServices(services =>
            {
                services.AddHttpClient("AgentGateway")
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler(GatewayStubPayload));
            }));

        using var client = factory.CreateClient();
        var body = new StringContent(
            JsonSerializer.Serialize(new { prompt = "next dentist", maxResults = 1 }),
            Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/ask", body);
        var content = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(content, "\"slots\"");
        StringAssert.Contains(content, "s1");
    }

    [TestMethod]
    public async Task Api_ask_falls_back_to_stub_when_gateway_fails()
    {
        await using var factory = new WebApplicationFactory<ApiHost::Program>()
            .WithWebHostBuilder(b => b.ConfigureServices(services =>
            {
                services.AddHttpClient("AgentGateway")
                    .ConfigurePrimaryHttpMessageHandler(() => new FailingHttpMessageHandler(HttpStatusCode.ServiceUnavailable));
            }));

        using var client = factory.CreateClient();
        var body = new StringContent(
            JsonSerializer.Serialize(new { prompt = "next dentist", maxResults = 1 }),
            Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/ask", body);
        var content = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(content, "\"slots\"");
    }

    [TestMethod]
    public async Task Api_ask_rejects_empty_prompt()
    {
        await using var factory = new WebApplicationFactory<ApiHost::Program>();
        using var client = factory.CreateClient();
        var body = new StringContent(
            JsonSerializer.Serialize(new { prompt = "", maxResults = 1 }),
            Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/ask", body);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Api_ask_rejects_prompt_over_500_chars()
    {
        await using var factory = new WebApplicationFactory<ApiHost::Program>();
        using var client = factory.CreateClient();
        var prompt = new string('x', 501);
        var body = new StringContent(
            JsonSerializer.Serialize(new { prompt, maxResults = 1 }),
            Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/ask", body);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Api_ask_rejects_invalid_clinician_role()
    {
        await using var factory = new WebApplicationFactory<ApiHost::Program>();
        using var client = factory.CreateClient();
        var body = new StringContent(
            JsonSerializer.Serialize(new { prompt = "test", clinicianRole = "wizard", maxResults = 1 }),
            Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/ask", body);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

// ---------------------------------------------------------------------------
// Gateway /agents/booking/ask + hold + confirm tests
// ---------------------------------------------------------------------------

[TestClass]
public sealed class GatewayNlAskTests
{
    [TestMethod]
    public async Task Gateway_ask_returns_fallback_slots()
    {
        await using var factory = new WebApplicationFactory<GatewayHost::Program>();
        using var client = factory.CreateClient();
        var body = new StringContent(
            JsonSerializer.Serialize(new { prompt = "next hygienist", maxResults = 2 }),
            Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/agents/booking/ask", body);
        var content = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(content, "\"slots\"");
        StringAssert.Contains(content, "interpretedIntent");
    }

    [TestMethod]
    public async Task Gateway_ask_rejects_empty_prompt()
    {
        await using var factory = new WebApplicationFactory<GatewayHost::Program>();
        using var client = factory.CreateClient();
        var body = new StringContent(
            JsonSerializer.Serialize(new { prompt = "", maxResults = 1 }),
            Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/agents/booking/ask", body);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Gateway_create_hold_forwards_to_api()
    {
        const string holdPayload = """{"holdId":"h1","clinicId":"clinic-1","clinicianId":"c1","startsAtUtc":"2026-08-13T09:00:00Z","expiresAtUtc":"2026-08-13T09:10:00Z"}""";

        await using var factory = new WebApplicationFactory<GatewayHost::Program>()
            .WithWebHostBuilder(b => b.ConfigureServices(services =>
            {
                services.AddHttpClient("ClinicFlowApi")
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler(holdPayload));
            }));

        using var client = factory.CreateClient();
        var body = new StringContent(
            JsonSerializer.Serialize(new
            {
                clinicId = "clinic-1",
                clinicianId = "c1",
                startsAtUtc = "2026-08-13T09:00:00Z",
                holdDurationMinutes = 10
            }),
            Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/agents/booking/create-hold", body);
        var content = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(content, "h1");
    }

    [TestMethod]
    public async Task Gateway_confirm_booking_forwards_to_api()
    {
        const string bookingPayload = """{"bookingId":"b1","status":"confirmed","startsAtUtc":"2026-08-13T09:00:00Z","endsAtUtc":"2026-08-13T09:30:00Z"}""";

        await using var factory = new WebApplicationFactory<GatewayHost::Program>()
            .WithWebHostBuilder(b => b.ConfigureServices(services =>
            {
                services.AddHttpClient("ClinicFlowApi")
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler(bookingPayload));
            }));

        using var client = factory.CreateClient();
        var body = new StringContent(
            JsonSerializer.Serialize(new
            {
                clinicId = "clinic-1",
                clinicianId = "c1",
                patientReferenceId = "patient-1",
                startsAtUtc = "2026-08-13T09:00:00Z",
                endsAtUtc = "2026-08-13T09:30:00Z"
            }),
            Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/agents/booking/confirm", body);
        var content = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(content, "b1");
        StringAssert.Contains(content, "confirmed");
    }

    [TestMethod]
    public async Task Gateway_confirm_booking_rejects_invalid_window()
    {
        await using var factory = new WebApplicationFactory<GatewayHost::Program>();
        using var client = factory.CreateClient();
        var body = new StringContent(
            JsonSerializer.Serialize(new
            {
                clinicId = "clinic-1",
                clinicianId = "c1",
                patientReferenceId = "patient-1",
                startsAtUtc = "2026-08-13T09:30:00Z",
                endsAtUtc = "2026-08-13T09:00:00Z"  // ends before starts
            }),
            Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/agents/booking/confirm", body);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

// ---------------------------------------------------------------------------
// Full ask → book integration flow
// ---------------------------------------------------------------------------

[TestClass]
public sealed class BookingFlowIntegrationTests
{
    [TestMethod]
    public async Task Full_ask_then_book_flow_returns_consistent_slot_id()
    {
        const string slotId = "slot-42";
        var gatewayPayload =
            $$$"""{"interpretedIntent":"test","slots":[{"slotId":"{{{slotId}}}","clinicianId":"c1","clinicianName":"Dr Test","clinicianRole":"dentist","startsAtUtc":"2026-08-13T09:00:00Z","endsAtUtc":"2026-08-13T09:30:00Z","displayLabel":"Wednesday 13 Aug, 09:00 \u2013 09:30"}],"message":null}""";

        await using var factory = new WebApplicationFactory<ApiHost::Program>()
            .WithWebHostBuilder(b => b.ConfigureServices(services =>
            {
                services.AddHttpClient("AgentGateway")
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler(gatewayPayload));
            }));

        using var client = factory.CreateClient();

        // Step 1: ask
        var askBody = new StringContent(
            JsonSerializer.Serialize(new { prompt = "next dentist", maxResults = 1 }),
            Encoding.UTF8, "application/json");
        using var askResponse = await client.PostAsync("/ask", askBody);
        var askContent = await askResponse.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, askResponse.StatusCode);
        StringAssert.Contains(askContent, slotId);

        // Capture slotId from response
        using var doc = JsonDocument.Parse(askContent);
        var capturedSlotId = doc.RootElement
            .GetProperty("slots")[0]
            .GetProperty("slotId")
            .GetString();
        Assert.AreEqual(slotId, capturedSlotId);

        // Step 2: book using fields from the slot
        var bookBody = new StringContent(
            JsonSerializer.Serialize(new
            {
                clinicId = "clinic-1",
                clinicianId = "c1",
                patientReferenceId = "patient-1",
                startsAtUtc = "2026-08-13T09:00:00Z",
                endsAtUtc = "2026-08-13T09:30:00Z"
            }),
            Encoding.UTF8, "application/json");
        using var bookResponse = await client.PostAsync("/bookings", bookBody);
        var bookContent = await bookResponse.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, bookResponse.StatusCode);
        StringAssert.Contains(bookContent, "appointmentId");
    }
}
