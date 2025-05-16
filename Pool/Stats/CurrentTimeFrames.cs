using FastCSharp.Date;

namespace FastCSharp.Pool;

/// <summary>
/// Creates a new instance of the CurrentTimeFrames class and initializes the dateTime property to the current date and time.
/// </summary>
public class CurrentTimeFrames
{
    private readonly DateTime dateTime = DateTime.Now;

    /// <summary>
    /// Returns the date and time set for the CurrentTimeFrames instance.
    /// </summary>
    public DateTime FullDateTime { get => dateTime; }

    /// <summary>
    /// Returns the date and time truncated to the nearest minute representing the minute time frame.
    /// </summary>
    public DateTime Minute { get => TruncateMinute(dateTime); }

    /// <summary>
    /// Returns the date and time truncated to the nearest hour representing the hour time frame.
    /// </summary>
    public DateTime Hour { get => TruncateHour(dateTime); }

    /// <summary>
    /// Returns the date and time truncated to the nearest day representing the day time frame.
    /// </summary>
    public DateTime Day { get => TruncateDay(dateTime); }

    /// <summary>
    /// Returns the date and time truncated to the nearest month representing the month time frame.
    /// </summary>
    public DateTime Month { get => TruncateMonth(dateTime); }

    /// <summary>
    /// Returns the date and time truncated to the nearest year representing the year time frame.
    /// </summary>
    public DateTime Year { get => TruncateYear(dateTime); }

    /// <summary>
    /// Truncates the given DateTime to the minute.
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static DateTime TruncateMinute(DateTime dateTime)
    {
        return dateTime.Truncate(TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Truncates the given DateTime to the hour.
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static DateTime TruncateHour(DateTime dateTime)
    {
        return dateTime.Truncate(TimeSpan.FromHours(1));
    }

    /// <summary>
    /// Truncates the given DateTime to the day.
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static DateTime TruncateDay(DateTime dateTime)
    {
        return dateTime.Truncate(TimeSpan.FromDays(1));
    }

    /// <summary>
    /// Truncates the given DateTime to the month.
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static DateTime TruncateMonth(DateTime dateTime)
    {
        return new(dateTime.Year, dateTime.Month, 1, 0, 0, 0, dateTime.Kind);
    }

    /// <summary>
    /// Truncates the given DateTime to the year.
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static DateTime TruncateYear(DateTime dateTime)
    {
        return new(dateTime.Year, 1, 1, 0, 0, 0, dateTime.Kind);
    }


}