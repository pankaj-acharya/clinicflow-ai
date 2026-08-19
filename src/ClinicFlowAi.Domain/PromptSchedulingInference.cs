using System.Globalization;
using System.Text.RegularExpressions;

namespace ClinicFlowAi.Domain;

public sealed record PromptSchedulingFilters(
    string? ClinicianName,
    string? ClinicianRole,
    string? PreferredTimeOfDay,
    TimeOnly? EarliestStartTime,
    IReadOnlySet<DayOfWeek> PreferredWeekdays,
    IReadOnlySet<DateOnly> PreferredDates);
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

    private static readonly Regex IsoDatePattern = new(
        @"\b(?<year>\d{4})-(?<month>\d{1,2})-(?<day>\d{1,2})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MonthDayPattern = new(
        @"\b(?<month>jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:tember)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\s+(?<day>\d{1,2})(?:,?\s*(?<year>\d{4}))?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DayMonthPattern = new(
        @"\b(?<day>\d{1,2})\s+(?<month>jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:tember)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)(?:,?\s*(?<year>\d{4}))?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NextWeekdayPattern = new(
        @"\b(?:next|this|coming)\s+(?<day>monday|tuesday|wednesday|thursday|friday|saturday|sunday)\b",
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

    public static PromptSchedulingFilters InferFilters(
        string prompt,
        string? clinicianName = null,
        string? clinicianRole = null,
        string? preferredTimeOfDay = null,
        IEnumerable<string>? preferredDays = null,
        DateTimeOffset? referenceUtc = null)
    {
        var inferredPreferredDays = InferPreferredWeekdays(prompt, preferredDays);
        var inferredPreferredDates = InferSpecificDates(prompt, referenceUtc ?? DateTimeOffset.UtcNow);

        return new PromptSchedulingFilters(
            ClinicianName: clinicianName ?? InferClinicianName(prompt),
            ClinicianRole: clinicianRole ?? InferClinicianRole(prompt),
            PreferredTimeOfDay: InferPreferredTimeOfDay(prompt, preferredTimeOfDay),
            EarliestStartTime: InferEarliestStartTime(prompt, preferredTimeOfDay),
            PreferredWeekdays: inferredPreferredDays,
            PreferredDates: inferredPreferredDates);
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

    private static HashSet<DayOfWeek> InferPreferredWeekdays(string prompt, IEnumerable<string>? preferredDays)
    {
        var weekdays = new HashSet<DayOfWeek>();

        if (preferredDays is not null)
        {
            foreach (var day in preferredDays)
            {
                if (TryParseDayOfWeek(day, out var parsedDay))
                {
                    weekdays.Add(parsedDay);
                }
            }
        }

        foreach (var candidate in new[] { "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday" })
        {
            if (ContainsWord(prompt, candidate))
            {
                weekdays.Add(ParseDayOfWeek(candidate));
            }
        }
        return weekdays;
    }

    private static HashSet<DateOnly> InferSpecificDates(string prompt, DateTimeOffset referenceUtc)
    {
        var dates = new HashSet<DateOnly>();

        foreach (Match match in IsoDatePattern.Matches(prompt))
        {
            if (TryParseDate(match.Groups["year"].Value, match.Groups["month"].Value, match.Groups["day"].Value, out var date))
            {
                dates.Add(date);
            }
        }

        foreach (Match match in MonthDayPattern.Matches(prompt))
        {
            if (TryParseDate(match.Groups["year"].Success ? match.Groups["year"].Value : referenceUtc.Year.ToString(CultureInfo.InvariantCulture), match.Groups["month"].Value, match.Groups["day"].Value, out var date))
            {
                if (!match.Groups["year"].Success && date < DateOnly.FromDateTime(referenceUtc.UtcDateTime.Date))
                {
                    date = date.AddYears(1);
                }

                dates.Add(date);
            }
        }

        foreach (Match match in DayMonthPattern.Matches(prompt))
        {
            if (TryParseDate(match.Groups["year"].Success ? match.Groups["year"].Value : referenceUtc.Year.ToString(CultureInfo.InvariantCulture), match.Groups["month"].Value, match.Groups["day"].Value, out var date))
            {
                if (!match.Groups["year"].Success && date < DateOnly.FromDateTime(referenceUtc.UtcDateTime.Date))
                {
                    date = date.AddYears(1);
                }

                dates.Add(date);
            }
        }

        foreach (Match match in NextWeekdayPattern.Matches(prompt))
        {
            if (TryParseDayOfWeek(match.Groups["day"].Value, out var dayOfWeek))
            {
                dates.Add(GetNextWeekday(DateOnly.FromDateTime(referenceUtc.UtcDateTime.Date), dayOfWeek));
            }
        }

        if (ContainsWord(prompt, "today"))
        {
            dates.Add(DateOnly.FromDateTime(referenceUtc.UtcDateTime.Date));
        }

        if (ContainsWord(prompt, "tomorrow"))
        {
            dates.Add(DateOnly.FromDateTime(referenceUtc.UtcDateTime.Date.AddDays(1)));
        }

        return dates;
    }

    private static bool TryParseDayOfWeek(string value, out DayOfWeek dayOfWeek)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "monday":
                dayOfWeek = DayOfWeek.Monday;
                return true;
            case "tuesday":
                dayOfWeek = DayOfWeek.Tuesday;
                return true;
            case "wednesday":
                dayOfWeek = DayOfWeek.Wednesday;
                return true;
            case "thursday":
                dayOfWeek = DayOfWeek.Thursday;
                return true;
            case "friday":
                dayOfWeek = DayOfWeek.Friday;
                return true;
            case "saturday":
                dayOfWeek = DayOfWeek.Saturday;
                return true;
            case "sunday":
                dayOfWeek = DayOfWeek.Sunday;
                return true;
            default:
                dayOfWeek = default;
                return false;
        }
    }

    private static DayOfWeek ParseDayOfWeek(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "monday" => DayOfWeek.Monday,
            "tuesday" => DayOfWeek.Tuesday,
            "wednesday" => DayOfWeek.Wednesday,
            "thursday" => DayOfWeek.Thursday,
            "friday" => DayOfWeek.Friday,
            "saturday" => DayOfWeek.Saturday,
            "sunday" => DayOfWeek.Sunday,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported day of week")
        };
    }

    private static bool TryParseDate(string yearText, string monthText, string dayText, out DateOnly date)
    {
        if (!int.TryParse(yearText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) ||
            !int.TryParse(dayText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var day))
        {
            date = default;
            return false;
        }

        if (!TryParseMonth(monthText, out var month))
        {
            date = default;
            return false;
        }

        try
        {
            date = new DateOnly(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            date = default;
            return false;
        }
    }

    private static bool TryParseMonth(string value, out int month)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "1":
            case "01":
            case "january":
            case "jan":
                month = 1;
                return true;
            case "2":
            case "02":
            case "february":
            case "feb":
                month = 2;
                return true;
            case "3":
            case "03":
            case "march":
            case "mar":
                month = 3;
                return true;
            case "4":
            case "04":
            case "april":
            case "apr":
                month = 4;
                return true;
            case "5":
            case "05":
            case "may":
                month = 5;
                return true;
            case "6":
            case "06":
            case "june":
            case "jun":
                month = 6;
                return true;
            case "7":
            case "07":
            case "july":
            case "jul":
                month = 7;
                return true;
            case "8":
            case "08":
            case "august":
            case "aug":
                month = 8;
                return true;
            case "9":
            case "09":
            case "september":
            case "sep":
                month = 9;
                return true;
            case "10":
            case "october":
            case "oct":
                month = 10;
                return true;
            case "11":
            case "november":
            case "nov":
                month = 11;
                return true;
            case "12":
            case "december":
            case "dec":
                month = 12;
                return true;
            default:
                month = default;
                return false;
        }
    }

    private static DateOnly GetNextWeekday(DateOnly referenceDate, DayOfWeek targetDay)
    {
        var daysUntilTarget = ((int)targetDay - (int)referenceDate.DayOfWeek + 7) % 7;
        if (daysUntilTarget == 0)
        {
            daysUntilTarget = 7;
        }

        return referenceDate.AddDays(daysUntilTarget);
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
