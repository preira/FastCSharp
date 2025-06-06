
namespace FastCSharp.Date;

/// <summary>
/// Static class for cryptographic operations
/// </summary>
static public class DateTimeExtensions
    {
    /// <summary>
    /// Truncates date to the specified resolution.
    /// 
    /// dateTime = dateTime.Truncate(TimeSpan.FromMilliseconds(1)); // Truncate to whole ms
    /// dateTime = dateTime.Truncate(TimeSpan.FromSeconds(1)); // Truncate to whole second
    /// dateTime = dateTime.Truncate(TimeSpan.FromMinutes(1)); // Truncate to whole minute
    /// 
    /// Based on https://stackoverflow.com/questions/1004698/how-to-truncate-milliseconds-off-of-a-net-datetime/1005222#1005222
    /// </summary>
    /// <param name="original"></param>
    /// <param name="resolution">Order of magnitude from micrseconds.</param>
    /// <returns></returns>
    public static DateTime Truncate(this DateTime original, TimeSpan resolution)
    {
        if (resolution == TimeSpan.Zero) return original;
        if (original == DateTime.MinValue) return original; // Truncating DateTime.MinValue could further decrease the value, which is not what we want.

        var ticks = original.Ticks;
        return new DateTime(
            ticks - (ticks % resolution.Ticks), 
            original.Kind
            );
    }


}
