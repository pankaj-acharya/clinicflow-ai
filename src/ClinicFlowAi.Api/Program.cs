using System.Net.Http.Json;
using ClinicFlowAi.Domain;
using ClinicFlowAi.Api;
using ClinicFlowAi.Infrastructure.Postgres;
using ClinicFlowAi.Infrastructure.Postgres.Persistence;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

var gatewayBaseUrl = builder.Configuration["ClinicFlowGateway:BaseUrl"] ?? "http://localhost:5073";
builder.Services.AddHttpClient("AgentGateway", client =>
{
    client.BaseAddress = new Uri(gatewayBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Wire Postgres persistence when a connection string is present; otherwise the
// API runs in in-memory mode (development default with no DB configured).
var pgConnection = builder.Configuration.GetConnectionString("ClinicFlowDb");
var postgresEnabled = !string.IsNullOrWhiteSpace(pgConnection);
if (postgresEnabled)
    builder.Services.AddPostgresInfrastructure(pgConnection!);

var app = builder.Build();

// Migrate schema and seed reference data whenever Postgres is enabled.
// The seeder is idempotent — it skips if data already exists.
if (postgresEnabled)
    await app.MigrateAndSeedAsync();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/availability", async ([AsParameters] AvailabilityQuery query, [FromServices] IAppointmentRepository? repo) =>
{
    // If Postgres is wired, use real data; otherwise fall back to in-memory stub
    if (repo is not null)
    {
        try
        {
            var slots = await repo.GetAvailableSlotsAsync(
                query.ClinicId,
                query.ClinicianId,
                null, // no role filtering at this layer
                query.WindowStartUtc,
                query.WindowEndUtc);
            
            return Results.Ok(slots.Select(s => new
            {
                s.StartsAtUtc,
                s.EndsAtUtc
            }));
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Postgres availability query failed, falling back to stub");
        }
    }

    // Fallback to in-memory stub
    var engine = new BookingEngine();
    var domainSlots = engine.GetAvailability(
        query.WindowStartUtc,
        query.WindowEndUtc,
        [new ScheduleRule(query.ClinicId, query.ClinicianId, query.WindowStartUtc.UtcDateTime.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(10, 0))],
        [],
        [],
        new AppointmentType(query.AppointmentTypeCode, query.AppointmentTypeCode, TimeSpan.FromMinutes(30)));

    return Results.Ok(domainSlots);
});

app.MapPost("/slot-holds", (SlotHoldRequest request) =>
{
    var hold = BookingEngine.CreateHold(
        Guid.NewGuid().ToString("N"),
        request.ClinicId,
        request.ClinicianId,
        request.StartsAtUtc,
        TimeSpan.FromMinutes(request.HoldDurationMinutes),
        DateTimeOffset.UtcNow);

    return Results.Ok(hold);
});

app.MapPost("/bookings", async (BookingRequestDto request, [FromServices] IAppointmentRepository? repo) =>
{
    // If Postgres is wired, create booking with persistent slot marking
    if (repo is not null)
    {
        try
        {
            var booking = await repo.CreateBookingAsync(
                GenerateSlotId(request.ClinicianId, request.StartsAtUtc),
                request.PatientReferenceId);
            
            return Results.Ok(new
            {
                booking.Id,
                Status = booking.Status,
                booking.Slot.StartsAtUtc,
                booking.Slot.EndsAtUtc
            });
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Postgres booking creation failed, falling back to stub");
        }
    }

    // Fallback to in-memory stub
    var result = BookingEngine.ConfirmBooking(
        new BookingRequest(
            request.ClinicId,
            request.ClinicianId,
            request.PatientReferenceId,
            request.StartsAtUtc,
            request.EndsAtUtc),
        Guid.NewGuid().ToString("N"),
        alreadyConfirmed: false);

    return Results.Ok(result);
});

app.MapPost("/ask", async (NlSchedulingRequest request, [FromServices] IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    // --- Input validation ---
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

    // Allowed roles per spec (case-insensitive)
    string[] allowedRoles = ["dentist", "hygienist", "therapist", "nurse"];
    if (request.ClinicianRole is not null &&
        !allowedRoles.Contains(request.ClinicianRole.Trim().ToLowerInvariant()))
    {
        app.Logger.LogWarning("NlScheduling request rejected. Reason={Reason}", "InvalidClinicianRole");
        return Results.BadRequest(new { error = "ClinicianRole must be one of: dentist, hygienist, therapist, nurse." });
    }

    // Normalise MaxResults: 0 → 1, >10 → 10 (silent, no rejection)
    var count = request.MaxResults < 1 ? 1 : Math.Clamp(request.MaxResults, 1, 10);

    // Silently filter out invalid PreferredDays entries
    string[] validDayNames = ["monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"];
    var filteredDays = request.PreferredDays
        ?.Where(d => !string.IsNullOrWhiteSpace(d) && validDayNames.Contains(d.Trim().ToLowerInvariant()))
        .ToArray();

    // Audit-safe: log role and count only — NOT prompt text, NOT clinician name
    app.Logger.LogInformation("NlScheduling request received. Role={Role} MaxResults={MaxResults}",
        request.ClinicianRole, count);

    var normalised = request with { MaxResults = count, PreferredDays = filteredDays };

    try
    {
        var client = httpClientFactory.CreateClient("AgentGateway");
        using var gatewayResponse = await client.PostAsJsonAsync(
            "/agents/booking/ask",
            new
            {
                normalised.Prompt,
                normalised.ClinicianRole,
                normalised.ClinicianName,
                normalised.PreferredDays,
                normalised.PreferredTimeOfDay,
                MaxResults = count
            },
            cancellationToken);

        if (gatewayResponse.IsSuccessStatusCode)
        {
            var gatewayResult = await gatewayResponse.Content
                .ReadFromJsonAsync<NlSchedulingResponse>(cancellationToken: cancellationToken);
            if (gatewayResult is not null)
                return Results.Ok(gatewayResult);
        }
        else
        {
            app.Logger.LogWarning("Gateway unavailable, using stub fallback");
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Gateway unavailable, using stub fallback");
    }

    try
    {
        var slots = GenerateStubSlots(normalised, count);
        var stubResponse = new NlSchedulingResponse(
            InterpretedIntent: $"Stub: schedule with {normalised.ClinicianRole ?? "any clinician"}",
            Slots: slots,
            Message: slots.Count == 0 ? "No slots found matching your criteria" : null);
        return Results.Ok(stubResponse);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Stub fallback also failed for NlScheduling request.");
        return Results.Ok(NlSchedulingResponse.Empty("Service temporarily unavailable."));
    }
});

app.Run();

// Generate a deterministic slot ID from clinician ID and start time for lookup purposes
static string GenerateSlotId(string clinicianId, DateTimeOffset startsAtUtc)
{
    return $"slot-{clinicianId}-{startsAtUtc:yyyyMMddHHmmss}".Replace(" ", "").ToLowerInvariant();
}

static IReadOnlyList<AvailableSlotOption> GenerateStubSlots(NlSchedulingRequest request, int count)
{
    var clinicianId = request.ClinicianName is not null
        ? request.ClinicianName.Replace(" ", "").ToLowerInvariant()
        : "clinician-1";
    var clinicianName = request.ClinicianName ?? "Dr. Default";
    var clinicianRole = request.ClinicianRole ?? "General Practitioner";

    var slots = new List<AvailableSlotOption>(count);
    // Start from tomorrow at 09:00 UTC
    var baseDate = DateTimeOffset.UtcNow.Date.AddDays(1);
    var cursor = new DateTimeOffset(baseDate, TimeSpan.Zero).AddHours(9);

    for (int i = 0; i < count; i++)
    {
        var start = cursor.AddMinutes(30 * i);
        var end = start.AddMinutes(30);
        var label = $"{start:dddd d MMM, HH:mm} \u2013 {end:HH:mm}";
        slots.Add(new AvailableSlotOption(
            SlotId: Guid.NewGuid().ToString("N"),
            ClinicianId: clinicianId,
            ClinicianName: clinicianName,
            ClinicianRole: clinicianRole,
            StartsAtUtc: start,
            EndsAtUtc: end,
            DisplayLabel: label));
    }

    return slots;
}

public partial class Program
{
}
