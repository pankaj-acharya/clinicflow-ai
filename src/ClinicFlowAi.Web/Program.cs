var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { app = "ClinicFlow AI", surface = "patient web shell" }));

app.Run();
