using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClinicFlowAi.Infrastructure.Postgres.Persistence;

/// <summary>
/// Seeds reference clinicians, schedule rules, and upcoming appointment slots
/// for development and testing. Only runs when the database is empty.
/// </summary>
public static class DbSeeder
{
    private const string ClinicId = "clinic-1";

    private static readonly (string Id, string Name, string Role)[] Clinicians =
    [
        ("clinician-dentist-1",   "Dr. James Harper",   "dentist"),
        ("clinician-dentist-2",   "Dr. Sarah Okafor",   "dentist"),
        ("clinician-hygienist-1", "Mrs. Lisa Smith",    "hygienist"),
        ("clinician-hygienist-2", "Mr. David Chen",     "hygienist"),
    ];

    // Mon=1, Tue=2, Wed=3, Thu=4, Fri=5 (DayOfWeek enum: Sun=0)
    private static readonly int[] WorkingDays = [1, 2, 3, 4, 5];

    public static async Task SeedAsync(ClinicFlowDbContext db, ILogger logger, CancellationToken ct = default)
    {
        if (await db.Clinicians.AnyAsync(ct))
        {
            logger.LogInformation("Database already seeded — skipping.");
            return;
        }

        logger.LogInformation("Seeding development data...");

        // ── Clinicians ────────────────────────────────────────────────────────
        foreach (var (id, name, role) in Clinicians)
        {
            db.Clinicians.Add(new ClinicianEntity
            {
                Id       = id,
                Name     = name,
                Role     = role,
                ClinicId = ClinicId,
            });
        }

        // ── Schedule rules (Mon–Fri 09:00–17:00 per clinician) ───────────────
        var ruleIndex = 0;
        foreach (var (cid, _, _) in Clinicians)
        {
            foreach (var day in WorkingDays)
            {
                db.ScheduleRules.Add(new ScheduleRuleEntity
                {
                    Id          = $"rule-{++ruleIndex}",
                    ClinicianId = cid,
                    ClinicId    = ClinicId,
                    DayOfWeek   = day,
                    StartTime   = new TimeOnly(9, 0),
                    EndTime     = new TimeOnly(17, 0),
                });
            }
        }

        // ── Appointment slots (30-min slots, next 14 days, working days only) ─
        var slotIndex = 0;
        var today = DateTimeOffset.UtcNow.Date;

        foreach (var (cid, _, role) in Clinicians)
        {
            var typeCode = role == "dentist" ? "exam" : "hygiene";

            for (var dayOffset = 1; dayOffset <= 14; dayOffset++)
            {
                var date = today.AddDays(dayOffset);
                if (!WorkingDays.Contains((int)date.DayOfWeek))
                    continue;

                // Morning slots: 09:00 – 12:00 (6 × 30 min)
                for (var h = 9; h < 12; h++)
                {
                    foreach (var m in new[] { 0, 30 })
                    {
                        var start = new DateTimeOffset(date.Year, date.Month, date.Day, h, m, 0, TimeSpan.Zero);
                        db.AppointmentSlots.Add(new AppointmentSlotEntity
                        {
                            Id                  = $"slot-{++slotIndex}",
                            ClinicianId         = cid,
                            ClinicId            = ClinicId,
                            StartsAtUtc         = start,
                            EndsAtUtc           = start.AddMinutes(30),
                            IsBooked            = false,
                            AppointmentTypeCode = typeCode,
                        });
                    }
                }

                // Afternoon slots: 13:00 – 17:00 (8 × 30 min)
                for (var h = 13; h < 17; h++)
                {
                    foreach (var m in new[] { 0, 30 })
                    {
                        var start = new DateTimeOffset(date.Year, date.Month, date.Day, h, m, 0, TimeSpan.Zero);
                        db.AppointmentSlots.Add(new AppointmentSlotEntity
                        {
                            Id                  = $"slot-{++slotIndex}",
                            ClinicianId         = cid,
                            ClinicId            = ClinicId,
                            StartsAtUtc         = start,
                            EndsAtUtc           = start.AddMinutes(30),
                            IsBooked            = false,
                            AppointmentTypeCode = typeCode,
                        });
                    }
                }
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Seeded {Clinicians} clinicians, {Rules} schedule rules, {Slots} appointment slots.",
            Clinicians.Length, ruleIndex, slotIndex);
    }
}
