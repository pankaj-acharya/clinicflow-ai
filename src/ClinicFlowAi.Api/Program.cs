using ClinicFlowAi.Domain;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/availability/sample", () =>
{
    var engine = new BookingEngine();
    var slots = engine.GetAvailability(
        new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.Zero),
        [new ScheduleRule("clinic-1", "clinician-1", DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(10, 0))],
        [],
        [],
        new AppointmentType("exam", "Dental examination", TimeSpan.FromMinutes(30)));

    return Results.Ok(slots);
});

app.Run();
