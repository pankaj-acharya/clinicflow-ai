namespace ClinicFlowAi.Domain;

public sealed class BookingEngine
{
    public IReadOnlyList<AvailabilitySlot> GetAvailability(
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        IEnumerable<ScheduleRule> rules,
        IEnumerable<Closure> closures,
        IEnumerable<Appointment> appointments,
        AppointmentType appointmentType)
    {
        var result = new List<AvailabilitySlot>();
        var ruleList = rules.ToList();
        var closureDates = closures.Select(c => c.Date).ToHashSet();
        var appointmentList = appointments.Where(a => a.State is AppointmentState.Held or AppointmentState.Confirmed).ToList();

        for (var current = windowStartUtc; current < windowEndUtc; current = current.AddDays(1))
        {
            if (closureDates.Contains(DateOnly.FromDateTime(current.UtcDateTime)))
            {
                continue;
            }

            foreach (var rule in ruleList.Where(r => r.DayOfWeek == current.UtcDateTime.DayOfWeek))
            {
                var start = current.DateTime.Date.Add(rule.Start.ToTimeSpan());
                var end = current.DateTime.Date.Add(rule.End.ToTimeSpan());
                for (var slotStart = start; slotStart.Add(appointmentType.Duration) <= end; slotStart = slotStart.Add(appointmentType.Duration))
                {
                    var slotEnd = slotStart.Add(appointmentType.Duration);
                    var conflicts = appointmentList.Any(a => slotStart < a.EndsAtUtc.UtcDateTime && slotEnd > a.StartsAtUtc.UtcDateTime);
                    if (!conflicts)
                    {
                        result.Add(new AvailabilitySlot(new DateTimeOffset(slotStart, TimeSpan.Zero), new DateTimeOffset(slotEnd, TimeSpan.Zero)));
                    }
                }
            }
        }

        return result;
    }

    public static bool CanHoldSlot(DateTimeOffset nowUtc, DateTimeOffset holdExpiresAtUtc) => nowUtc < holdExpiresAtUtc;

    public static SlotHold CreateHold(
        string holdId,
        string clinicId,
        string clinicianId,
        DateTimeOffset startsAtUtc,
        TimeSpan holdDuration,
        DateTimeOffset nowUtc)
    {
        var expiresAtUtc = nowUtc.Add(holdDuration);
        if (!CanHoldSlot(nowUtc, expiresAtUtc))
        {
            throw new InvalidOperationException("Hold expiration must be in the future.");
        }

        return new SlotHold(holdId, clinicId, clinicianId, startsAtUtc, expiresAtUtc);
    }

    public static BookingResult ConfirmBooking(BookingRequest request, string appointmentId, bool alreadyConfirmed)
    {
        if (request.EndsAtUtc <= request.StartsAtUtc)
        {
            throw new InvalidOperationException("Appointment end time must be after the start time.");
        }

        return new BookingResult(appointmentId, alreadyConfirmed ? AppointmentState.Confirmed : AppointmentState.Confirmed);
    }
}
