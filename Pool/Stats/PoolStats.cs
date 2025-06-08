
using System.Collections.Concurrent;
using System.Text.Json;

namespace FastCSharp.Pool;

/// <summary>
/// Registers events with granularity by the minute
/// </summary>
public class PoolStats 
{
    public readonly object _lock = new ();
    private readonly PoolStatsPeriod alltimeStats;
    private readonly ConcurrentDictionary<DateTime, PoolStatsPeriod> yearStats;
    private readonly ConcurrentDictionary<DateTime, PoolStatsPeriod> monthStats;
    private readonly ConcurrentDictionary<DateTime, PoolStatsPeriod> dayStats;
    private readonly ConcurrentDictionary<DateTime, PoolStatsPeriod> hourStats;
    private readonly ConcurrentDictionary<DateTime, PoolStatsPeriod> minuteStats;
    private volatile PoolStatsPeriod _lastMinuteStat;
    private int lastSize;

    /// <summary>
    /// Returns the ration of requests that timed out.
    /// </summary>
    public double TimeoutRatio { get => _lastMinuteStat.TimeoutRatio; }

    /// <summary>
    /// Returns the all time stats for the pool.
    /// </summary>
    public PoolStatsPeriod AllTimeStats { get => alltimeStats; }

    public PoolStats()
    {
        _lastMinuteStat = new PoolStatsPeriod(DateTime.Now, -1);
        alltimeStats = new PoolStatsPeriod(DateTime.Now, -1);
        yearStats = new();
        monthStats = new();
        dayStats = new();
        hourStats = new();
        minuteStats = new();

        lastSize = -1; // force update
        
        UpdateSize(0);
    }

    public bool UpdateSize(int size)
    {
        if (lastSize == size) return false;

        var timeFrames = new CurrentTimeFrames();

        PoolStatsPeriod? minute = GetAndRotate(minuteStats, timeFrames.Minute, CurrentTimeFrames.TruncateMinute);
        minute.UpdateSize(size, timeFrames.Minute);
        _lastMinuteStat = minute;

        PoolStatsPeriod? hour = GetAndRotate(hourStats, timeFrames.Hour, CurrentTimeFrames.TruncateHour);
        hour.UpdateSize(size, timeFrames.Hour);

        PoolStatsPeriod? day = GetAndRotate(dayStats, timeFrames.Day, CurrentTimeFrames.TruncateDay);
        day.UpdateSize(size, timeFrames.Day);

        PoolStatsPeriod? month = GetAndRotate(monthStats, timeFrames.Month, CurrentTimeFrames.TruncateMonth);
        month.UpdateSize(size, timeFrames.Month);

        PoolStatsPeriod? year = Get(yearStats, timeFrames.Year, CurrentTimeFrames.TruncateYear);
        year.UpdateSize(size, timeFrames.Year);

        alltimeStats.UpdateSize(size, timeFrames.FullDateTime);

        lastSize = size;

        return true;
    }

    private PoolStatsPeriod GetAndRotate(ConcurrentDictionary<DateTime, PoolStatsPeriod> statsPeriods, DateTime key, Func<DateTime, DateTime> Truncate)
    {
        lock (_lock)
        {
            if (statsPeriods.TryGetValue(key, out var period))
            {
                return period;
            }
            else
            {
                period = new PoolStatsPeriod(key, -1);
                statsPeriods.Clear();
                statsPeriods[key] = period;
                return period;
            }

        }
    }

    private PoolStatsPeriod Get(ConcurrentDictionary<DateTime, PoolStatsPeriod> statsPeriods, DateTime key, Func<DateTime, DateTime> Truncate)
    {
        lock (_lock)
        {
            if (statsPeriods.TryGetValue(key, out var period))
            {
                return period;
            }
            else
            {
                period = new PoolStatsPeriod(key, -1);
                statsPeriods[key] = period;
                return period;
            }

        }
    }

    public void PoolRequest(bool isHit, int size)
    {
        var timeFrames = new CurrentTimeFrames();

        PoolStatsPeriod? minute = GetAndRotate(minuteStats, timeFrames.Minute, CurrentTimeFrames.TruncateMinute);
        minute.PoolRequest(isHit, size, timeFrames.Minute);
        _lastMinuteStat = minute;

        PoolStatsPeriod? hour = GetAndRotate(hourStats, timeFrames.Hour, CurrentTimeFrames.TruncateHour);
        hour.PoolRequest(isHit, size, timeFrames.Hour);

        PoolStatsPeriod? day = GetAndRotate(dayStats, timeFrames.Day, CurrentTimeFrames.TruncateDay);
        day.PoolRequest(isHit, size, timeFrames.Day);

        PoolStatsPeriod? month = GetAndRotate(monthStats, timeFrames.Month, CurrentTimeFrames.TruncateMonth);
        month.PoolRequest(isHit, size, timeFrames.Month);

        PoolStatsPeriod? year = Get(yearStats, timeFrames.Year, CurrentTimeFrames.TruncateYear);
        year.PoolRequest(isHit, size, timeFrames.Year);

        alltimeStats.PoolRequest(isHit, size, timeFrames.FullDateTime);
    }

    public void PoolReturn(int size)
    {
        var timeFrames = new CurrentTimeFrames();

        PoolStatsPeriod? minute = GetAndRotate(minuteStats, timeFrames.Minute, CurrentTimeFrames.TruncateMinute);
        minute.PoolReturn(size, timeFrames.Minute);
        _lastMinuteStat = minute;

        PoolStatsPeriod? hour = GetAndRotate(hourStats, timeFrames.Hour, CurrentTimeFrames.TruncateHour);
        hour.PoolReturn(size, timeFrames.Hour);

        PoolStatsPeriod? day = GetAndRotate(dayStats, timeFrames.Day, CurrentTimeFrames.TruncateDay);
        day.PoolReturn(size, timeFrames.Day);

        PoolStatsPeriod? month = GetAndRotate(monthStats, timeFrames.Month, CurrentTimeFrames.TruncateMonth);
        month.PoolReturn(size, timeFrames.Month);

        PoolStatsPeriod? year = Get(yearStats, timeFrames.Year, CurrentTimeFrames.TruncateYear);
        year.PoolReturn(size, timeFrames.Year);

        alltimeStats.PoolReturn(size, timeFrames.FullDateTime);
    } 

    public void PoolPurge(int size)
    {
        var timeFrames = new CurrentTimeFrames();

        PoolStatsPeriod? minute = GetAndRotate(minuteStats, timeFrames.Minute, CurrentTimeFrames.TruncateMinute);
        minute.PoolPurge(size, timeFrames.Minute);
        _lastMinuteStat = minute;

        PoolStatsPeriod? hour = GetAndRotate(hourStats, timeFrames.Hour, CurrentTimeFrames.TruncateHour);
        hour.PoolPurge(size, timeFrames.Hour);

        PoolStatsPeriod? day = GetAndRotate(dayStats, timeFrames.Day, CurrentTimeFrames.TruncateDay);
        day.PoolPurge(size, timeFrames.Day);

        PoolStatsPeriod? month = GetAndRotate(monthStats, timeFrames.Month, CurrentTimeFrames.TruncateMonth);
        month.PoolPurge(size, timeFrames.Month);

        PoolStatsPeriod? year = Get(yearStats, timeFrames.Year, CurrentTimeFrames.TruncateYear);
        year.PoolPurge(size, timeFrames.Year);

        alltimeStats.PoolPurge(size, timeFrames.FullDateTime);
    } 

    public void PoolWait()
    {
        var timeFrames = new CurrentTimeFrames();

        PoolStatsPeriod? minute = GetAndRotate(minuteStats, timeFrames.Minute, CurrentTimeFrames.TruncateMinute);
        minute.PoolWait(timeFrames.Minute);
        _lastMinuteStat = minute;

        PoolStatsPeriod? hour = GetAndRotate(hourStats, timeFrames.Hour, CurrentTimeFrames.TruncateHour);
        hour.PoolWait(timeFrames.Hour);

        PoolStatsPeriod? day = GetAndRotate(dayStats, timeFrames.Day, CurrentTimeFrames.TruncateDay);
        day.PoolWait(timeFrames.Day);

        PoolStatsPeriod? month = GetAndRotate(monthStats, timeFrames.Month, CurrentTimeFrames.TruncateMonth);
        month.PoolWait(timeFrames.Month);

        PoolStatsPeriod? year = Get(yearStats, timeFrames.Year, CurrentTimeFrames.TruncateYear);
        year.PoolWait(timeFrames.Year);

        alltimeStats.PoolWait(timeFrames.FullDateTime);
    } 

    public void PoolTimeout()
    {
        var timeFrames = new CurrentTimeFrames();

        PoolStatsPeriod? minute = GetAndRotate(minuteStats, timeFrames.Minute, CurrentTimeFrames.TruncateMinute);
        minute.PoolTimeout(timeFrames.Minute);
        _lastMinuteStat = minute;

        PoolStatsPeriod? hour = GetAndRotate(hourStats, timeFrames.Hour, CurrentTimeFrames.TruncateHour);
        hour.PoolTimeout(timeFrames.Hour);

        PoolStatsPeriod? day = GetAndRotate(dayStats, timeFrames.Day, CurrentTimeFrames.TruncateDay);
        day.PoolTimeout(timeFrames.Day);

        PoolStatsPeriod? month = GetAndRotate(monthStats, timeFrames.Month, CurrentTimeFrames.TruncateMonth);
        month.PoolTimeout(timeFrames.Month);

        PoolStatsPeriod? year = Get(yearStats, timeFrames.Year, CurrentTimeFrames.TruncateYear);
        year.PoolTimeout(timeFrames.Year);

        alltimeStats.PoolTimeout(timeFrames.FullDateTime);
    } 


    public void PoolError()
    {
        var timeFrames = new CurrentTimeFrames();

        PoolStatsPeriod? minute = GetAndRotate(minuteStats, timeFrames.Minute, CurrentTimeFrames.TruncateMinute);
        minute.PoolError(timeFrames.Minute);
        _lastMinuteStat = minute;

        PoolStatsPeriod? hour = GetAndRotate(hourStats, timeFrames.Hour, CurrentTimeFrames.TruncateHour);
        hour.PoolError(timeFrames.Hour);

        PoolStatsPeriod? day = GetAndRotate(dayStats, timeFrames.Day, CurrentTimeFrames.TruncateDay);
        day.PoolError(timeFrames.Day);

        PoolStatsPeriod? month = GetAndRotate(monthStats, timeFrames.Month, CurrentTimeFrames.TruncateMonth);
        month.PoolError(timeFrames.Month);

        PoolStatsPeriod? year = Get(yearStats, timeFrames.Year, CurrentTimeFrames.TruncateYear);
        year.PoolError(timeFrames.Year);

        alltimeStats.PoolError(timeFrames.FullDateTime);
    }

    public void PoolDisposed()
    {
        var timeFrames = new CurrentTimeFrames();

        PoolStatsPeriod? minute = GetAndRotate(minuteStats, timeFrames.Minute, CurrentTimeFrames.TruncateMinute);
        minute.PoolDisposed(timeFrames.Minute);
        _lastMinuteStat = minute;

        PoolStatsPeriod? hour = GetAndRotate(hourStats, timeFrames.Hour, CurrentTimeFrames.TruncateHour);
        hour.PoolDisposed(timeFrames.Hour);

        PoolStatsPeriod? day = GetAndRotate(dayStats, timeFrames.Day, CurrentTimeFrames.TruncateDay);
        day.PoolDisposed(timeFrames.Day);

        PoolStatsPeriod? month = GetAndRotate(monthStats, timeFrames.Month, CurrentTimeFrames.TruncateMonth);
        month.PoolDisposed(timeFrames.Month);

        PoolStatsPeriod? year = Get(yearStats, timeFrames.Year, CurrentTimeFrames.TruncateYear);
        year.PoolDisposed(timeFrames.Year);

        alltimeStats.PoolDisposed(timeFrames.FullDateTime);
    }



    public JsonDocument ToJson()
    {
       Dictionary<string, object> obj = new()
       {
            { "minute", minuteStats },
            { "hour", hourStats },
            { "day", dayStats },
            { "month", monthStats },
            { "year", yearStats },
            { "lastSize", lastSize },
            { "alltime", alltimeStats }
       };

        lock (_lock)
        {
            return JsonSerializer.SerializeToDocument(obj);
        }
    }
}