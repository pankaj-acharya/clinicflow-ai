using System.Net.Http.Json;
using ClinicFlowAi.AgentGateway;

var builder = WebApplication.CreateBuilder(args);

var apiBaseUrl = builder.Configuration["ClinicFlowApi:BaseUrl"] ?? "http://localhost:5071";
builder.Services.AddHttpClient("ClinicFlowApi", client =>
{
	client.BaseAddress = new Uri(apiBaseUrl);
	client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
	service = "ClinicFlow AI Agent Gateway",
	description = "Allowlisted agent-facing endpoints for health, booking actions, and FAQ responses.",
	routes = new[]
	{
		new { method = "GET", path = "/agents/health", description = "Returns the gateway health status." },
		new { method = "POST", path = "/agents/booking/check-availability", description = "Returns availability slots for a booking window by calling the booking API." },
		new { method = "POST", path = "/agents/booking/create-hold", description = "Accepts a booking-related request from an agent and represents creating a temporary slot hold." },
		new { method = "POST", path = "/agents/booking/confirm", description = "Accepts a booking-related request from an agent and represents confirming a booking." },
		new { method = "POST", path = "/agents/faq/answer", description = "Accepts an FAQ question and returns an allowlisted guidance response." }
	}
}));
app.MapGet("/agents/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/agents/booking/check-availability", async (AgentCheckAvailabilityRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
	if (string.IsNullOrWhiteSpace(request.ClinicId) || string.IsNullOrWhiteSpace(request.ClinicianId) || string.IsNullOrWhiteSpace(request.AppointmentTypeCode))
	{
		return Results.BadRequest(new { error = "ClinicId, ClinicianId, WindowStartUtc, WindowEndUtc, and AppointmentTypeCode are required." });
	}

	if (request.WindowEndUtc <= request.WindowStartUtc)
	{
		return Results.BadRequest(new { error = "WindowEndUtc must be after WindowStartUtc." });
	}

	var queryString = string.Join("&", new[]
	{
		$"ClinicId={Uri.EscapeDataString(request.ClinicId)}",
		$"ClinicianId={Uri.EscapeDataString(request.ClinicianId)}",
		$"WindowStartUtc={Uri.EscapeDataString(request.WindowStartUtc.ToString("O"))}",
		$"WindowEndUtc={Uri.EscapeDataString(request.WindowEndUtc.ToString("O"))}",
		$"AppointmentTypeCode={Uri.EscapeDataString(request.AppointmentTypeCode)}"
	});

	var client = httpClientFactory.CreateClient("ClinicFlowApi");
	using var upstreamResponse = await client.GetAsync($"/availability?{queryString}", cancellationToken);
	if (!upstreamResponse.IsSuccessStatusCode)
	{
		var error = await upstreamResponse.Content.ReadAsStringAsync(cancellationToken);
		return Results.Text(error, upstreamResponse.Content.Headers.ContentType?.ToString() ?? "application/json", statusCode: (int)upstreamResponse.StatusCode);
	}

	var slots = await upstreamResponse.Content.ReadFromJsonAsync<List<AgentAvailabilitySlot>>(cancellationToken: cancellationToken) ?? [];
	return Results.Ok(new AgentCheckAvailabilityResponse(slots));
});
app.MapPost("/agents/booking/create-hold", () => Results.Ok(new { action = "booking.createHold" }));
app.MapPost("/agents/booking/confirm", () => Results.Ok(new { action = "booking.confirm" }));
app.MapPost("/agents/faq/answer", (AgentFaqQuery query) => Results.Ok(new { answer = "Use approved knowledge base only.", query.Question }));

app.Run();

public partial class Program
{
}
