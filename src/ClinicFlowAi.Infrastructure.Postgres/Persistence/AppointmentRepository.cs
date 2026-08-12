using Microsoft.EntityFrameworkCore;

namespace ClinicFlowAi.Infrastructure.Postgres.Persistence;

public sealed class AppointmentRepository(ClinicFlowDbContext db) : IAppointmentRepository
{
    public async Task<IReadOnlyList<AppointmentSlotEntity>> GetAvailableSlotsAsync(
        string clinicId, string? clinicianId, string? clinicianRole,
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var query = db.AppointmentSlots
            .Include(s => s.Clinician)
            .Where(s => !s.IsBooked
                && s.ClinicId == clinicId
                && s.StartsAtUtc >= from
                && s.EndsAtUtc <= to);

        if (clinicianId is not null)
            query = query.Where(s => s.ClinicianId == clinicianId);

        if (clinicianRole is not null)
            query = query.Where(s => s.Clinician.Role == clinicianRole);

        return await query.AsNoTracking().ToListAsync(ct);
    }

    public async Task<AppointmentSlotEntity?> GetSlotByIdAsync(string slotId, CancellationToken ct = default)
        => await db.AppointmentSlots
            .Include(s => s.Clinician)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == slotId, ct);

    public async Task<BookingEntity> CreateBookingAsync(
        string slotId, string patientReferenceId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var slot = await db.AppointmentSlots
            .FirstOrDefaultAsync(s => s.Id == slotId, ct)
            ?? throw new InvalidOperationException($"Slot '{slotId}' not found.");

        if (slot.IsBooked)
            throw new InvalidOperationException($"Slot '{slotId}' is already booked.");

        slot.IsBooked = true;

        var booking = new BookingEntity
        {
            Id = Guid.NewGuid().ToString(),
            SlotId = slotId,
            PatientReferenceId = patientReferenceId,
            ConfirmedAtUtc = DateTimeOffset.UtcNow,
            Status = "confirmed"
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return booking;
    }
}
