using System.Net.Http.Json;
using ClinicFlowAi.AgentGateway;
using ClinicFlowAi.Domain;

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
		new { method = "POST", path = "/agents/booking/ask", description = "Accepts a natural-language scheduling prompt and returns matching slot options." },
		new { method = "POST", path = "/agents/booking/create-hold", description = "Creates a temporary slot hold by forwarding to the booking API POST /slot-holds." },
		new { method = "POST", path = "/agents/booking/confirm", description = "Confirms a booking by forwarding to the booking API POST /bookings." },
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
app.MapPost("/agents/booking/create-hold", async (AgentCreateHoldRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
	if (string.IsNullOrWhiteSpace(request.ClinicId) || string.IsNullOrWhiteSpace(request.ClinicianId))
	{
		return Results.BadRequest(new { error = "ClinicId and ClinicianId are required." });
	}

	if (request.HoldDurationMinutes < 1 || request.HoldDurationMinutes > 60)
	{
		return Results.BadRequest(new { error = "HoldDurationMinutes must be between 1 and 60." });
	}

	var client = httpClientFactory.CreateClient("ClinicFlowApi");
	using var upstreamResponse = await client.PostAsJsonAsync("/slot-holds", new
	{
		request.ClinicId,
		request.ClinicianId,
		request.StartsAtUtc,
		request.HoldDurationMinutes
	}, cancellationToken);

	if (!upstreamResponse.IsSuccessStatusCode)
	{
		var error = await upstreamResponse.Content.ReadAsStringAsync(cancellationToken);
		return Results.Text(error, upstreamResponse.Content.Headers.ContentType?.ToString() ?? "application/json", statusCode: (int)upstreamResponse.StatusCode);
	}

	var hold = await upstreamResponse.Content.ReadFromJsonAsync<AgentCreateHoldResponse>(cancellationToken: cancellationToken);
	return hold is null
		? Results.Problem("Unexpected empty response from API.")
		: Results.Ok(hold);
});
app.MapPost("/agents/booking/confirm", async (AgentConfirmBookingRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
	if (string.IsNullOrWhiteSpace(request.ClinicId) || string.IsNullOrWhiteSpace(request.ClinicianId) || string.IsNullOrWhiteSpace(request.PatientReferenceId))
	{
		// PatientReferenceId intentionally excluded from logs
		return Results.BadRequest(new { error = "ClinicId, ClinicianId, and PatientReferenceId are required." });
	}

	if (request.EndsAtUtc <= request.StartsAtUtc)
	{
		return Results.BadRequest(new { error = "EndsAtUtc must be after StartsAtUtc." });
	}

	var client = httpClientFactory.CreateClient("ClinicFlowApi");
	using var upstreamResponse = await client.PostAsJsonAsync("/bookings", new
	{
		request.ClinicId,
		request.ClinicianId,
		request.PatientReferenceId,
		request.StartsAtUtc,
		request.EndsAtUtc
	}, cancellationToken);

	if (!upstreamResponse.IsSuccessStatusCode)
	{
		var error = await upstreamResponse.Content.ReadAsStringAsync(cancellationToken);
		return Results.Text(error, upstreamResponse.Content.Headers.ContentType?.ToString() ?? "application/json", statusCode: (int)upstreamResponse.StatusCode);
	}

	var booking = await upstreamResponse.Content.ReadFromJsonAsync<AgentConfirmBookingResponse>(cancellationToken: cancellationToken);
	return booking is null
		? Results.Problem("Unexpected empty response from API.")
		: Results.Ok(booking);
});
app.MapPost("/agents/booking/ask", async (AgentNlSchedulingRequest request, IConfiguration configuration, CancellationToken cancellationToken) =>
{
    // --- Input validation (gateway independently validates — allowlisted surface) ---
    if (string.IsNullOrWhiteSpace(request.Prompt))
    {
        app.Logger.LogWarning("NlScheduling request rejected. Reason={Reason}", "PromptRequired");
        return Results.BadRequest(new { error = "Prompt is required." });
    }

    if (request.Prompt.Length > 500)
    {
        app.Logger.LogWarning("NlScheduling request rejected. Reason={Reason}", "PromptTooLong");
        return Results.BadRequest(new { error = "Prompt must be 500 characters or fewer." });
    }

    // Normalise MaxResults: 0 → 1, >10 → 10 (silent, no rejection)
    var count = request.MaxResults < 1 ? 1 : Math.Clamp(request.MaxResults, 1, 10);
    var inferredClinicianName = request.ClinicianName ?? PromptSchedulingInference.InferClinicianName(request.Prompt);
    var inferredClinicianRole = request.ClinicianRole ?? PromptSchedulingInference.InferClinicianRole(request.Prompt);
    var inferredPreferredTimeOfDay = PromptSchedulingInference.InferPreferredTimeOfDay(request.Prompt, request.PreferredTimeOfDay);

    // Audit-safe: log role and count only — NOT prompt text, NOT clinician name
    app.Logger.LogInformation("NlScheduling request received. Role={Role} MaxResults={MaxResults}",
        inferredClinicianRole, count);

    var response = await InvokeFoundryOrFallbackAsync(
        request with
        {
            ClinicianName = inferredClinicianName,
            ClinicianRole = inferredClinicianRole,
            PreferredTimeOfDay = inferredPreferredTimeOfDay,
            MaxResults = count
        },
        configuration,
        cancellationToken);
    return Results.Ok(response);
});
app.MapPost("/agents/faq/answer", (AgentFaqQuery query) => Results.Ok(new { answer = "Use approved knowledge base only.", query.Question }));

app.Run();

static async Task<AgentNlSchedulingResponse> InvokeFoundryOrFallbackAsync(
	AgentNlSchedulingRequest request,
	IConfiguration configuration,
	CancellationToken cancellationToken)
{
	var projectEndpoint = configuration["Foundry:ProjectEndpoint"];
	var modelDeployment = configuration["Foundry:ModelDeploymentName"];

	// When Foundry config is present, slot in the real Azure AI Foundry call here.
	// For now, always use the deterministic fallback.
	if (!string.IsNullOrWhiteSpace(projectEndpoint) && !string.IsNullOrWhiteSpace(modelDeployment))
	{
		// TODO: replace with Azure.AI.Projects SDK call once the package is added.
		// var client = new AgentsClient(projectEndpoint, new DefaultAzureCredential());
		// var instructions = configuration["Foundry:AgentInstructions"] ?? string.Empty;
		// ...
	}

	return await Task.FromResult(GenerateFallbackResponse(request));
}

static AgentNlSchedulingResponse GenerateFallbackResponse(AgentNlSchedulingRequest request)
{
	var clinicianName = request.ClinicianName ?? PromptSchedulingInference.InferClinicianName(request.Prompt) ?? "Dr Default";
	var clinicianRole = request.ClinicianRole ?? PromptSchedulingInference.InferClinicianRole(request.Prompt) ?? "General Practitioner";
	var clinicianId = clinicianName.Replace(" ", "").ToLowerInvariant();
	var earliestStartTime = PromptSchedulingInference.InferEarliestStartTime(request.Prompt, request.PreferredTimeOfDay) ?? new TimeOnly(9, 0);

	var slots = new List<AgentNlSlotOption>(request.MaxResults);
	var baseDate = DateTimeOffset.UtcNow.Date.AddDays(1);
	var cursor = new DateTimeOffset(baseDate, TimeSpan.Zero).Add(earliestStartTime.ToTimeSpan());

	for (int i = 0; i < request.MaxResults; i++)
	{
		var start = cursor.AddMinutes(30 * i);
		var end = start.AddMinutes(30);
		var label = $"{start:dddd d MMM, HH:mm} \u2013 {end:HH:mm}";
		slots.Add(new AgentNlSlotOption(
			SlotId: Guid.NewGuid().ToString("N"),
			ClinicianId: clinicianId,
			ClinicianName: clinicianName,
			ClinicianRole: clinicianRole,
			StartsAtUtc: start,
			EndsAtUtc: end,
			DisplayLabel: label));
	}

	return new AgentNlSchedulingResponse(
		InterpretedIntent: $"Schedule with {clinicianRole}{(request.PreferredTimeOfDay is null ? "" : $" in the {request.PreferredTimeOfDay}")}",
		Slots: slots,
		Message: slots.Count == 0 ? "No slots found matching your criteria" : null);
}

public partial class Program
{
}
