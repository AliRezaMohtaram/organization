using System.Globalization;

using OrgChart.Razor.Resources;

namespace OrgChart.Razor.Admin;

/// <summary>
/// Jalali dates for the admin UI in a given time zone. Periods are entered as whole days: "from" is the start of that
/// day; "to" is inclusive and stored as the start of the next day (exclusive), as in the domain model.
/// </summary>
public sealed class OrgDates(TimeZoneInfo timeZone)
{
    private static readonly PersianCalendar s_calendar = new();

    /// <summary>"۱۴۰۵/۰۷/۱۶" (and " ۱۴:۳۰" with time), or empty for null.</summary>
    public string Format(DateTime? utc, bool withTime = false) =>
        utc is { } value ? OrgFormat.Digits(FormatLocal(ToLocal(value), withTime)) : "";

    /// <summary>An exclusive end at midnight as the last included day; other times as they are.</summary>
    public string FormatEnd(DateTime? utcExclusive)
    {
        if (utcExclusive is not { } value)
        {
            return "";
        }

        DateTime local = ToLocal(value);
        return OrgFormat.Digits(local.TimeOfDay == TimeSpan.Zero
            ? FormatLocal(local.AddDays(-1), withTime: false)
            : FormatLocal(local, withTime: true));
    }

    /// <summary>Value for a date input: Latin digits, "1405/07/16"; the end is shown inclusive.</summary>
    public string Input(DateTime? utc, bool isEnd = false)
    {
        if (utc is not { } value)
        {
            return "";
        }

        DateTime local = ToLocal(value);
        if (isEnd && local.TimeOfDay == TimeSpan.Zero)
        {
            local = local.AddDays(-1);
        }

        return FormatLocal(local, withTime: false);
    }

    /// <summary>"از … تا …", "از …", "تا …" or "نامحدود".</summary>
    public string Period(DateTime? from, DateTime? to) => (from, to) switch
    {
        (null, null) => OrgText.Get("Period_Always"),
        (not null, null) => OrgText.Format("Period_From", Format(from)),
        (null, not null) => OrgText.Format("Period_Until", FormatEnd(to)),
        _ => OrgText.Format("Period_Between", Format(from), FormatEnd(to)),
    };

    /// <summary>
    /// Parses "1405/7/16" or "1405-07-16" (Persian or Latin digits). Empty means no bound. <paramref name="inclusiveEnd"/>
    /// turns the day into the start of the following day.
    /// </summary>
    public bool TryParse(string? text, bool inclusiveEnd, out DateTime? utc)
    {
        utc = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        string[] parts = OrgFormat.LatinDigits(text.Trim()).Split('/', '-');
        if (parts.Length != 3
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int year)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int month)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int day)
            || year < 1300 || year > 1500 || month < 1 || month > 12
            || day < 1 || day > s_calendar.GetDaysInMonth(year, month))
        {
            return false;
        }

        DateTime local = s_calendar.ToDateTime(year, month, day, 0, 0, 0, 0);
        if (inclusiveEnd)
        {
            local = local.AddDays(1);
        }

        utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), timeZone);
        return true;
    }

    /// <summary>Start of the given local day, as UTC.</summary>
    public DateTime StartOfDay(DateTime utc)
    {
        DateTime local = ToLocal(utc).Date;
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), timeZone);
    }

    private DateTime ToLocal(DateTime value) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value, DateTimeKind.Utc), timeZone);

    private static string FormatLocal(DateTime local, bool withTime)
    {
        string date = $"{s_calendar.GetYear(local):0000}/{s_calendar.GetMonth(local):00}/{s_calendar.GetDayOfMonth(local):00}";
        return withTime ? $"{date} {local:HH:mm}" : date;
    }
}
