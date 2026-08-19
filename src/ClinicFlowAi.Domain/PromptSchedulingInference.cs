using System.Globalization;
using System.Text.RegularExpressions;

namespace ClinicFlowAi.Domain;

public static class PromptSchedulingInference
{
    private static readonly HashSet<string> ClinicianNameStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "in",
        "on",
        "at",
        "after",
        "before",
        "preferably",
        "today",
        "tomorrow",
        "this",
        "next"
    };

    private static readonly Regex WithClinicianNamePattern = new(
        @"\b(?:with|for)\s+(?<title>dr|doctor|mr|mrs|ms|miss)\.?\s+(?<name>[a-z][\p{L}'-]*(?:\s+[a-z][\p{L}'-]*){0,3})(?=\s+(?:in|on|at|after|before|preferably|today|tomorrow|this|next)\b|[.,!?]|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BareClinicianNamePattern = new(
        @"\b(?<title>dr|doctor|mr|mrs|ms|miss)\.?\s+(?<name>[a-z][\p{L}'-]*(?:\s+[a-z][\p{L}'-]*){0,3})(?=\s+(?:in|on|at|after|before|preferably|today|tomorrow|this|next)\b|[.,!?]|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AfterTimePattern = new(
        @"\bafter\s+(?<hour>\d{1,2})(?::(?<minute>\d{2}))?\s*(?<meridiem>am|pm)?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string? InferClinicianName(string prompt)
    {
        var match = WithClinicianNamePattern.Match(prompt);
        if (match.Success)
        {
            return CanonicalizeClinicianName(match);
        }

        match = BareClinicianNamePattern.Match(prompt);
        if (match.Success)
        {
            return CanonicalizeClinicianName(match);
        }

        return null;
    }

    public static string? InferClinicianRole(string prompt)
    {
        if (ContainsWord(prompt, "dentist"))
        {
            return "dentist";
        }

        if (ContainsWord(prompt, "hygienist"))
        {
            return "hygienist";
        }

        if (ContainsWord(prompt, "therapist"))
        {
            return "therapist";
        }

        if (ContainsWord(prompt, "nurse"))
        {
            return "nurse";
        }

        return null;
    }

    public static string? InferPreferredTimeOfDay(string prompt, string? existingPreferredTimeOfDay = null)
    {
        var normalizedPreferredTimeOfDay = NormalizeTimeOfDay(existingPreferredTimeOfDay);
        if (normalizedPreferredTimeOfDay is not null)
        {
            return normalizedPreferredTimeOfDay;
        }

        if (TryParseAfterTime(prompt, out var explicitAfterTime))
        {
            return TimeOfDayFor(explicitAfterTime);
        }

        return InferPreferredTimeOfDayFromPrompt(prompt);
    }

    public static TimeOnly? InferEarliestStartTime(string prompt, string? existingPreferredTimeOfDay = null)
    {
        if (TryParseAfterTime(prompt, out var explicitAfterTime))
        {
            return explicitAfterTime;
        }

        var preferredTimeOfDay = NormalizeTimeOfDay(existingPreferredTimeOfDay) ?? InferPreferredTimeOfDayFromPrompt(prompt);
        return preferredTimeOfDay switch
        {
            "morning" => new TimeOnly(9, 0),
            "afternoon" => new TimeOnly(14, 0),
            "evening" => new TimeOnly(17, 0),
            _ => null
        };
    }

    private static string CanonicalizeClinicianName(Match match)
    {
        var title = match.Groups["title"].Value.Equals("dr", StringComparison.OrdinalIgnoreCase)
            ? "Dr"
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(match.Groups["title"].Value.ToLowerInvariant());
        var nameParts = match.Groups["name"].Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .TakeWhile(part => !ClinicianNameStopWords.Contains(part))
            .ToArray();
        var name = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(string.Join(' ', nameParts).ToLowerInvariant());
        return $"{title} {name}";
    }

    private static bool ContainsWord(string prompt, string word) =>
        prompt.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;

    private static string? NormalizeTimeOfDay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "morning" or "afternoon" or "evening"
            ? normalized
            : null;
    }

    private static string? InferPreferredTimeOfDayFromPrompt(string prompt)
    {
        if (ContainsWord(prompt, "morning"))
        {
            return "morning";
        }

        if (ContainsWord(prompt, "afternoon"))
        {
            return "afternoon";
        }

        if (ContainsWord(prompt, "evening"))
        {
            return "evening";
        }

        return null;
    }

    private static string TimeOfDayFor(TimeOnly time)
    {
        if (time < new TimeOnly(12, 0))
        {
            return "morning";
        }

        if (time < new TimeOnly(17, 0))
        {
            return "afternoon";
        }

        return "evening";
    }

    private static bool TryParseAfterTime(string prompt, out TimeOnly time)
    {
        var match = AfterTimePattern.Match(prompt);
        if (!match.Success)
        {
            time = default;
            return false;
        }

        var hour = int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture);
        var minute = match.Groups["minute"].Success
            ? int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture)
            : 0;

        var meridiem = match.Groups["meridiem"].Value.ToLowerInvariant();
        if (meridiem == "pm" && hour < 12)
        {
            hour += 12;
        }
        else if (meridiem == "am" && hour == 12)
        {
            hour = 0;
        }

        if (hour is < 0 or > 23 || minute is < 0 or > 59)
        {
            time = default;
            return false;
        }

        time = new TimeOnly(hour, minute);
        return true;
    }
}
