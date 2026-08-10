var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/agents/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/agents/faq", () => Results.Ok(new { answer = "Escalate to approved knowledge base." }));

app.Run();
