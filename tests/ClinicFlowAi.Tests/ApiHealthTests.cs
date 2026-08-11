extern alias ApiHost;
extern alias GatewayHost;
extern alias WebHost;

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClinicFlowAi.Tests;

[TestClass]
public sealed class HostSmokeTests
{
    [TestMethod]
    public async Task Api_health_returns_ok()
    {
        await using var factory = new WebApplicationFactory<ApiHost::Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(content, "ok");
    }

    [TestMethod]
    public async Task Api_availability_returns_slots()
    {
        await using var factory = new WebApplicationFactory<ApiHost::Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/availability?ClinicId=clinic-1&ClinicianId=clinician-1&WindowStartUtc=2026-08-11T00:00:00Z&WindowEndUtc=2026-08-12T00:00:00Z&AppointmentTypeCode=exam");
        var content = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(content, "startsAtUtc");
    }

    [TestMethod]
    public async Task Web_health_returns_ok()
    {
        await using var factory = new WebApplicationFactory<WebHost::Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(content, "ok");
    }

    [TestMethod]
    public async Task Web_availability_proxies_api_response()
    {
        const string payload = "[{\"startsAtUtc\":\"2026-08-11T09:00:00Z\",\"endsAtUtc\":\"2026-08-11T09:30:00Z\"}]";

        await using var factory = new WebApplicationFactory<WebHost::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddHttpClient("ClinicFlowApi")
                        .ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler(payload));
                });
            });

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/availability?ClinicId=clinic-1&ClinicianId=clinician-1&WindowStartUtc=2026-08-11T00:00:00Z&WindowEndUtc=2026-08-12T00:00:00Z&AppointmentTypeCode=exam");
        var content = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(content, "2026-08-11T09:00:00Z");
    }

    [TestMethod]
    public async Task Agent_gateway_health_returns_ok()
    {
        await using var factory = new WebApplicationFactory<GatewayHost::Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/agents/health");
        var content = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(content, "ok");
    }

    [TestMethod]
    public async Task Agent_gateway_root_describes_routes()
    {
        await using var factory = new WebApplicationFactory<GatewayHost::Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(content, "ClinicFlow AI Agent Gateway");
        StringAssert.Contains(content, "/agents/health");
        StringAssert.Contains(content, "/agents/booking/check-availability");
        StringAssert.Contains(content, "/agents/booking/create-hold");
        StringAssert.Contains(content, "/agents/booking/confirm");
        StringAssert.Contains(content, "/agents/faq/answer");
    }

    [TestMethod]
    public async Task Agent_gateway_check_availability_calls_api_and_returns_slots()
    {
        var handler = new CapturingHttpMessageHandler("[{\"startsAtUtc\":\"2026-08-11T09:00:00+00:00\",\"endsAtUtc\":\"2026-08-11T09:30:00+00:00\"}]");

        await using var factory = new WebApplicationFactory<GatewayHost::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddHttpClient("ClinicFlowApi")
                        .ConfigurePrimaryHttpMessageHandler(() => handler);
                });
            });

        using var client = factory.CreateClient();
        var request = new
        {
            ClinicId = "clinic-1",
            ClinicianId = "clinician-1",
            WindowStartUtc = "2026-08-11T00:00:00Z",
            WindowEndUtc = "2026-08-12T00:00:00Z",
            AppointmentTypeCode = "exam"
        };

        using var response = await client.PostAsync("/agents/booking/check-availability", new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"));
        var content = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(content, "\"slots\"");
        StringAssert.Contains(content, "2026-08-11T09:00:00");
        Assert.IsNotNull(handler.LastRequestUri);
        Assert.AreEqual(HttpMethod.Get, handler.LastMethod);
        StringAssert.Contains(handler.LastRequestUri!.ToString(), "/availability?");
        StringAssert.Contains(handler.LastRequestUri!.ToString(), "AppointmentTypeCode=exam");
    }

    [TestMethod]
    public async Task Agent_gateway_check_availability_rejects_invalid_window()
    {
        await using var factory = new WebApplicationFactory<GatewayHost::Program>();
        using var client = factory.CreateClient();

        var request = new
        {
            ClinicId = "clinic-1",
            ClinicianId = "clinician-1",
            WindowStartUtc = "2026-08-12T00:00:00Z",
            WindowEndUtc = "2026-08-11T00:00:00Z",
            AppointmentTypeCode = "exam"
        };

        using var response = await client.PostAsync("/agents/booking/check-availability", new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class StubHttpMessageHandler(string payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }

    private sealed class CapturingHttpMessageHandler(string payload) : HttpMessageHandler
    {
        public HttpMethod? LastMethod { get; private set; }
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastRequestUri = request.RequestUri;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}

