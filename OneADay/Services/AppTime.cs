namespace OneADay.Services;

/// <summary>
/// The app's clock. Days roll over at midnight Pacific Time, regardless of
/// where the server runs, so the countdown and the teaser schedule agree.
/// </summary>
public static class AppTime
{
    private static readonly TimeZoneInfo Pacific = ResolvePacific();

    private static TimeZoneInfo ResolvePacific()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
        }
        catch (TimeZoneNotFoundException)
        {
            // Windows id fallback
            return TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        }
    }

    public static DateTime NowPacific => TimeZoneInfo.ConvertTime(DateTime.UtcNow, Pacific);

    public static DateOnly Today => DateOnly.FromDateTime(NowPacific);

    /// <summary>Time remaining until the next challenge (midnight Pacific).</summary>
    public static TimeSpan UntilNextChallenge => NowPacific.Date.AddDays(1) - NowPacific;
}
