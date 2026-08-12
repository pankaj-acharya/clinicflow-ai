namespace ClinicFlowAi.Infrastructure.Postgres.Persistence;

public interface IAppointmentRepository
{
    Task<IReadOnlyList<AppointmentSlotEntity>> GetAvailableSlotsAsync(
        string clinicId, string? clinicianId, string? clinicianRole,
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);

    Task<AppointmentSlotEntity?> GetSlotByIdAsync(string slotId, CancellationToken ct = default);

    Task<BookingEntity> CreateBookingAsync(string slotId, string patientReferenceId, CancellationToken ct = default);
}
