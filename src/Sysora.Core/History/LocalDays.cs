namespace Sysora.Core.History;

/// <summary>
/// Calendar days of a time zone ("today", "yesterday"), as instants. Each midnight carries its own UTC offset: a day that
/// contains a daylight saving time change lasts 23 or 25 hours, and the day before it starts at its real midnight, not an
/// hour early or late.
/// </summary>
public static class LocalDays
{
    /// <summary>
    /// Midnight that starts the day of <paramref name="time"/> in <paramref name="zone"/>, or the day
    /// <paramref name="daysBefore"/> days earlier.
    /// </summary>
    public static DateTimeOffset Midnight(DateTimeOffset time, TimeZoneInfo zone, int daysBefore = 0)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var date = TimeZoneInfo.ConvertTime(time, zone).Date.AddDays(-daysBefore);
        return new DateTimeOffset(date, zone.GetUtcOffset(date));
    }
}
