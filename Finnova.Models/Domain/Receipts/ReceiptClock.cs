namespace Finnova.Models.Domain.Receipts;

/// <summary>"Today" in the business time zone Asia/Kolkata (Assumption A19).</summary>
public static class ReceiptClock
{
    private static readonly TimeZoneInfo Ist = Resolve();

    private static TimeZoneInfo Resolve()
    {
        foreach (var id in new[] { "Asia/Kolkata", "India Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromHours(5.5), "IST", "IST");
    }

    public static DateOnly TodayIst() => TodayIst(DateTime.UtcNow);

    public static DateOnly TodayIst(DateTime utcNow)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), Ist));
}