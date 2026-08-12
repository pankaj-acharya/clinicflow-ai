using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

var apiBaseUrl = builder.Configuration["ClinicFlowApi:BaseUrl"] ?? "http://localhost:5071";
builder.Services.AddHttpClient("ClinicFlowApi", client =>
{
	client.BaseAddress = new Uri(apiBaseUrl);
	client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/availability", async (HttpContext context, IHttpClientFactory httpClientFactory) =>
{
	var client = httpClientFactory.CreateClient("ClinicFlowApi");
	using var response = await client.GetAsync($"/availability{context.Request.QueryString}", context.RequestAborted);
	var payload = await response.Content.ReadAsStringAsync(context.RequestAborted);
	var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";

	return Results.Content(payload, contentType, null, (int)response.StatusCode);
});

app.MapPost("/ask", async (HttpContext context, IHttpClientFactory httpClientFactory) =>
{
	var client = httpClientFactory.CreateClient("ClinicFlowApi");
	using var requestBody = new StreamContent(context.Request.Body);
	requestBody.Headers.ContentType = new MediaTypeHeaderValue("application/json");
	using var response = await client.PostAsync("/ask", requestBody, context.RequestAborted);
	var payload = await response.Content.ReadAsStringAsync(context.RequestAborted);
	var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";

	return Results.Content(payload, contentType, null, (int)response.StatusCode);
});

app.MapPost("/book", async (HttpContext context, IHttpClientFactory httpClientFactory) =>
{
	var client = httpClientFactory.CreateClient("ClinicFlowApi");
	using var requestBody = new StreamContent(context.Request.Body);
	requestBody.Headers.ContentType = new MediaTypeHeaderValue("application/json");
	using var response = await client.PostAsync("/bookings", requestBody, context.RequestAborted);
	var payload = await response.Content.ReadAsStringAsync(context.RequestAborted);
	var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";

	return Results.Content(payload, contentType, null, (int)response.StatusCode);
});

app.Run();

public partial class Program
{
}
