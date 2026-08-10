using ClinicFlowAi.Domain;
using ClinicFlowAi.Api;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/availability", ([AsParameters] AvailabilityQuery query) =>
{
    var engine = new BookingEngine();
    var slots = engine.GetAvailability(
        query.WindowStartUtc,
        query.WindowEndUtc,
        [new ScheduleRule(query.ClinicId, query.ClinicianId, query.WindowStartUtc.UtcDateTime.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(10, 0))],
        [],
        [],
        new AppointmentType(query.AppointmentTypeCode, query.AppointmentTypeCode, TimeSpan.FromMinutes(30)));

    return Results.Ok(slots);
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

app.MapPost("/bookings", (BookingRequestDto request) =>
{
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

app.Run();

public partial class Program
{
}
