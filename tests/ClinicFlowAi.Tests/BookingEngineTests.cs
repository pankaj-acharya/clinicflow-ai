using ClinicFlowAi.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClinicFlowAi.Tests;

[TestClass]
public sealed class BookingEngineTests
{
    [TestMethod]
    public void GetAvailability_excludes_conflicting_appointments_and_closures()
    {
        var engine = new BookingEngine();
        var appointmentType = new AppointmentType("exam", "Dental examination", TimeSpan.FromMinutes(30));
        var rules = new[]
        {
            new ScheduleRule("clinic-1", "clinician-1", DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0))
        };
        var closures = new[] { new Closure("clinic-1", new DateOnly(2026, 8, 10), "Bank holiday") };
        var appointments = Array.Empty<Appointment>();

        var slots = engine.GetAvailability(
            new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero),
            rules,
            closures,
            appointments,
            appointmentType);

        Assert.AreEqual(0, slots.Count);
    }

    [TestMethod]
    public void GetAvailability_returns_open_slots_when_not_conflicted()
    {
        var engine = new BookingEngine();
        var appointmentType = new AppointmentType("exam", "Dental examination", TimeSpan.FromMinutes(30));
        var rules = new[]
        {
            new ScheduleRule("clinic-1", "clinician-1", DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(10, 0))
        };

        var slots = engine.GetAvailability(
            new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.Zero),
            rules,
            Array.Empty<Closure>(),
            Array.Empty<Appointment>(),
            appointmentType);

        Assert.AreEqual(2, slots.Count);
    }

    [TestMethod]
    public void CreateHold_sets_future_expiry()
    {
        var hold = BookingEngine.CreateHold(
            "hold-1",
            "clinic-1",
            "clinician-1",
            new DateTimeOffset(2026, 8, 11, 9, 0, 0, TimeSpan.Zero),
            TimeSpan.FromMinutes(10),
            new DateTimeOffset(2026, 8, 11, 8, 0, 0, TimeSpan.Zero));

        Assert.AreEqual(new DateTimeOffset(2026, 8, 11, 8, 10, 0, TimeSpan.Zero), hold.ExpiresAtUtc);
    }

    [TestMethod]
    public void ConfirmBooking_rejects_invalid_time_range()
    {
        Assert.ThrowsException<InvalidOperationException>(() =>
            BookingEngine.ConfirmBooking(
                new BookingRequest(
                    "clinic-1",
                    "clinician-1",
                    "patient-1",
                    new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 8, 11, 9, 0, 0, TimeSpan.Zero)),
                "appt-1",
                false));
    }
}
