namespace ClinicFlowAi.Api;

public sealed record AvailabilityQuery(
    string ClinicId,
    string ClinicianId,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    string AppointmentTypeCode);

public sealed record SlotHoldRequest(
    string ClinicId,
    string ClinicianId,
    DateTimeOffset StartsAtUtc,
    int HoldDurationMinutes);

public sealed record BookingRequestDto(
    string ClinicId,
    string ClinicianId,
    string PatientReferenceId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc);

// Natural-language scheduling request from web/agent
public sealed record NlSchedulingRequest(
    string Prompt,                          // raw user prompt — do NOT log
    string? ClinicianRole = null,           // e.g. "dentist", "hygienist" — null = any
    string? ClinicianName = null,           // e.g. "Mrs Smith" — null = any; do NOT log
    string[]? PreferredDays = null,         // e.g. ["Monday","Tuesday","Friday"] — null = any
    string? PreferredTimeOfDay = null,      // "morning", "afternoon", "any" — null = any
    int MaxResults = 1);                    // how many slots to return (default 1, max 10)

// One slot option returned in the NL response
public sealed record AvailableSlotOption(
    string SlotId,
    string ClinicianId,
    string ClinicianName,
    string ClinicianRole,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string DisplayLabel);                   // human-friendly e.g. "Monday 17 Aug, 09:00 – 09:30"

// NL scheduling response envelope
public sealed record NlSchedulingResponse(
    string InterpretedIntent,               // what the system understood from the prompt
    IReadOnlyList<AvailableSlotOption> Slots,
    string? Message)                        // e.g. "No slots found matching your criteria"
{
    public static NlSchedulingResponse Empty(string message) =>
        new(InterpretedIntent: "unknown", Slots: [], Message: message);
}
