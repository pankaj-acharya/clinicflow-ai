var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/agents/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/agents/booking/check-availability", () => Results.Ok(new { action = "booking.checkAvailability" }));
app.MapPost("/agents/booking/create-hold", () => Results.Ok(new { action = "booking.createHold" }));
app.MapPost("/agents/booking/confirm", () => Results.Ok(new { action = "booking.confirm" }));
app.MapPost("/agents/faq/answer", (AgentFaqQuery query) => Results.Ok(new { answer = "Use approved knowledge base only.", query.Question }));

app.Run();
