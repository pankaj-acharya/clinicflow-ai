extern alias ApiHost;
extern alias GatewayHost;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClinicFlowAi.Domain;
using ClinicFlowAi.Infrastructure.Postgres.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task Api_ask_uses_sql_slots_and_honours_weekday_filters()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var friday = NextWeekday(today, DayOfWeek.Friday);
        var monday = NextWeekday(today, DayOfWeek.Monday);

        await using var factory = new WebApplicationFactory<ApiHost::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<IAppointmentRepository>(new FakeAppointmentRepository(CreateSeedSlots(friday, monday)));
                });
            });
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            "/ask",
            new StringContent(
                JsonSerializer.Serialize(new
                {
                    prompt = "Show me next 5 appointments with Dr James Harper on Friday after 2 PM",
                    maxResults = 5
                }),
                Encoding.UTF8,
                "application/json"));

        var content = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var slots = content.RootElement.GetProperty("slots").EnumerateArray().ToArray();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(2, slots.Length);
        Assert.IsTrue(slots.All(slot => slot.GetProperty("clinicianName").GetString() == "Dr. James Harper"));
        Assert.IsTrue(slots.All(slot => DateTimeOffset.Parse(slot.GetProperty("startsAtUtc").GetString()!).DayOfWeek == DayOfWeek.Friday));
        Assert.IsTrue(slots.All(slot => DateTimeOffset.Parse(slot.GetProperty("startsAtUtc").GetString()!).TimeOfDay >= TimeSpan.FromHours(14)));
    }

    [TestMethod]
    public async Task Api_ask_uses_sql_slots_and_honours_explicit_date_filters()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var friday = NextWeekday(today, DayOfWeek.Friday);
        var monday = NextWeekday(today, DayOfWeek.Monday);

        await using var factory = new WebApplicationFactory<ApiHost::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<IAppointmentRepository>(new FakeAppointmentRepository(CreateSeedSlots(friday, monday)));
                });
            });
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            "/ask",
            new StringContent(
                JsonSerializer.Serialize(new
                {
                    prompt = $"Show me next 5 appointments with Dr James Harper on {monday:yyyy-MM-dd}",
                    maxResults = 5
                }),
                Encoding.UTF8,
                "application/json"));

        var content = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var slots = content.RootElement.GetProperty("slots").EnumerateArray().ToArray();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(2, slots.Length);
        Assert.IsTrue(slots.All(slot => DateTimeOffset.Parse(slot.GetProperty("startsAtUtc").GetString()!).Date == monday.ToDateTime(TimeOnly.MinValue)));
        Assert.IsTrue(slots.All(slot => slot.GetProperty("clinicianName").GetString() == "Dr. James Harper"));
    }

    private static List<AppointmentSlotEntity> CreateSeedSlots(DateOnly friday, DateOnly monday)
    {
        return
        [
            CreateSlot("slot-friday-1", friday, new TimeOnly(14, 0)),
            CreateSlot("slot-friday-2", friday, new TimeOnly(15, 0)),
            CreateSlot("slot-monday-1", monday, new TimeOnly(14, 0)),
            CreateSlot("slot-monday-2", monday, new TimeOnly(15, 0)),
            CreateSlot("slot-tuesday-1", monday.AddDays(1), new TimeOnly(14, 0))
        ];
    }

    private static DateOnly NextWeekday(DateOnly start, DayOfWeek day)
    {
        var daysUntil = ((int)day - (int)start.DayOfWeek + 7) % 7;
        if (daysUntil == 0)
        {
            daysUntil = 7;
        }

        return start.AddDays(daysUntil);
    }

    private static AppointmentSlotEntity CreateSlot(string id, DateOnly date, TimeOnly time)
    {
        var starts = new DateTimeOffset(date.Year, date.Month, date.Day, time.Hour, time.Minute, 0, TimeSpan.Zero);
        return new AppointmentSlotEntity
        {
            Id = id,
            ClinicId = "clinic-1",
            ClinicianId = "clinician-dentist-1",
            StartsAtUtc = starts,
            EndsAtUtc = starts.AddMinutes(30),
            IsBooked = false,
            AppointmentTypeCode = "exam",
            Clinician = new ClinicianEntity
            {
                Id = "clinician-dentist-1",
                ClinicId = "clinic-1",
                Name = "Dr. James Harper",
                Role = "dentist"
            }
        };
    }

    private sealed class FakeAppointmentRepository(IReadOnlyList<AppointmentSlotEntity> slots) : IAppointmentRepository
    {
        public Task<IReadOnlyList<AppointmentSlotEntity>> GetAvailableSlotsAsync(string clinicId, string? clinicianId, string? clinicianRole, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
        {
            var filtered = slots
                .Where(slot => slot.ClinicId == clinicId)
                .Where(slot => !slot.IsBooked)
                .Where(slot => clinicianId is null || slot.ClinicianId == clinicianId)
                .Where(slot => clinicianRole is null || slot.Clinician.Role == clinicianRole)
                .Where(slot => slot.StartsAtUtc >= from && slot.EndsAtUtc <= to)
                .ToList();

            return Task.FromResult<IReadOnlyList<AppointmentSlotEntity>>(filtered);
        }

        public Task<AppointmentSlotEntity?> GetSlotByIdAsync(string slotId, CancellationToken ct = default)
            => Task.FromResult(slots.FirstOrDefault(slot => slot.Id == slotId));

        public Task<BookingEntity> CreateBookingAsync(string slotId, string patientReferenceId, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
