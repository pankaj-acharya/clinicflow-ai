extern alias ApiHost;
extern alias GatewayHost;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClinicFlowAi.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClinicFlowAi.Tests;

[TestClass]
public sealed class SchedulingPromptInferenceTests
{
    [TestMethod]
    public void Infer_prompt_extracts_clinician_and_afternoon_time()
    {
        const string prompt = "Show me next 5 appointments with Dr James Harper in afternoon after 2 PM";

        Assert.AreEqual("Dr James Harper", PromptSchedulingInference.InferClinicianName(prompt));
        Assert.AreEqual("afternoon", PromptSchedulingInference.InferPreferredTimeOfDay(prompt));
        Assert.AreEqual(new TimeOnly(14, 0), PromptSchedulingInference.InferEarliestStartTime(prompt));
    }

    [TestMethod]
    public async Task Agent_gateway_ask_returns_afternoon_slots_for_follow_up_prompt()
    {
        await using var factory = new WebApplicationFactory<GatewayHost::Program>();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            "/agents/booking/ask",
            JsonContent.Create(new
            {
                Prompt = "Show me next 5 appointments with Dr James Harper in afternoon after 2 PM",
                MaxResults = 5
            }));

        var content = await response.Content.ReadFromJsonAsync<GatewayHost::ClinicFlowAi.AgentGateway.AgentNlSchedulingResponse>();
        Assert.IsNotNull(content);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(5, content.Slots.Count);
        Assert.AreEqual("Dr James Harper", content.Slots[0].ClinicianName);
        Assert.IsTrue(content.Slots.All(slot => slot.StartsAtUtc.TimeOfDay >= TimeSpan.FromHours(14)));
    }

    [TestMethod]
    public async Task Api_ask_fallback_returns_afternoon_slots_for_follow_up_prompt()
    {
        await using var factory = new WebApplicationFactory<ApiHost::Program>()
            .WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ClinicFlowGateway:BaseUrl"] = "http://127.0.0.1:1"
                });
            });
        });
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            "/ask",
            new StringContent(
                JsonSerializer.Serialize(new
                {
                    prompt = "Show me next 5 appointments with Dr James Harper in afternoon after 2 PM",
                    maxResults = 5
                }),
                Encoding.UTF8,
                "application/json"));

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var slots = document.RootElement.GetProperty("slots").EnumerateArray().ToArray();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(5, slots.Length);
        Assert.IsTrue(slots.All(slot => DateTimeOffset.Parse(slot.GetProperty("startsAtUtc").GetString()!).TimeOfDay >= TimeSpan.FromHours(14)));
    }
}
