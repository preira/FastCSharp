
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

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
    private static readonly TimeSpan CACHE_TIMEOUT = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CACHE_EXPIRATION_SCAN_FREQUENCY = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Returns the ration of requests that timed out.
    /// </summary>
    public double TimeoutRatio { get => _lastMinuteStat.TimeoutRatio; }

    /// <summary>
    /// Returns the all time stats for the pool.
    /// </summary>
    public PoolStatsPeriod AllTimeStats { get => alltimeStats; }
    private readonly MemoryCache _requestCache;
    private readonly MemoryCache _waitCache;

    public PoolStats()
    {
        var options = new MemoryCacheOptions
        {
            ExpirationScanFrequency = CACHE_EXPIRATION_SCAN_FREQUENCY,
        };
        _requestCache = new MemoryCache(options);
        _waitCache = new MemoryCache(options);

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

        PoolStatsPeriod? minute = GetAndRotate(minuteStats, timeFrames.Minute);
        minute.UpdateSize(size, timeFrames.Minute);
        _lastMinuteStat = minute;

        PoolStatsPeriod? hour = GetAndRotate(hourStats, timeFrames.Hour);
        hour.UpdateSize(size, timeFrames.Hour);

        PoolStatsPeriod? day = GetAndRotate(dayStats, timeFrames.Day);
        day.UpdateSize(size, timeFrames.Day);

        PoolStatsPeriod? month = GetAndRotate(monthStats, timeFrames.Month);
        month.UpdateSize(size, timeFrames.Month);

        PoolStatsPeriod? year = Get(yearStats, timeFrames.Year);
        year.UpdateSize(size, timeFrames.Year);

        alltimeStats.UpdateSize(size, timeFrames.FullDateTime);

        lastSize = size;

        return true;
    }

    private PoolStatsPeriod GetAndRotate(ConcurrentDictionary<DateTime, PoolStatsPeriod> statsPeriods, DateTime key)
    {
        lock (_lock)
        {
            if (statsPeriods.TryGetValue(key, out var period))
            {
                return period;
            }
            else
            {
                period = new PoolStatsPeriod(DateTime.Now, -1);
                statsPeriods.Clear();
                statsPeriods[key] = period;
                return period;
            }

        }
    }

    private PoolStatsPeriod Get(ConcurrentDictionary<DateTime, PoolStatsPeriod> statsPeriods, DateTime key)
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

    private static void WrapCache(MemoryCache cache, Guid requestGuid, Action execute)
    {
        if (cache.TryGetValue(requestGuid, out bool _)) return;
        cache.Set(requestGuid, true, CACHE_TIMEOUT);

        execute();
    }

    public void PoolRequestForRequestGuid(bool isHit, int size, Guid requestGuid)
    {
        WrapCache(_requestCache, requestGuid, () => PoolRequest(isHit, size));
    }

    public void PoolRequest(bool isHit, int size)
    {
        var timeFrames = new CurrentTimeFrames();

        PoolStatsPeriod? minute = GetAndRotate(minuteStats, timeFrames.Minute);
        minute.PoolRequest(isHit, size, timeFrames.Minute);
        _lastMinuteStat = minute;

        PoolStatsPeriod? hour = GetAndRotate(hourStats, timeFrames.Hour);
        hour.PoolRequest(isHit, size, timeFrames.Hour);

        PoolStatsPeriod? day = GetAndRotate(dayStats, timeFrames.Day);
        day.PoolRequest(isHit, size, timeFrames.Day);

        PoolStatsPeriod? month = GetAndRotate(monthStats, timeFrames.Month);
        month.PoolRequest(isHit, size, timeFrames.Month);

        PoolStatsPeriod? year = Get(yearStats, timeFrames.Year);
        year.PoolRequest(isHit, size, timeFrames.Year);

        alltimeStats.PoolRequest(isHit, size, timeFrames.FullDateTime);
    }

    public void PoolReturn(int size)
    {
        var timeFrames = new CurrentTimeFrames();

        PoolStatsPeriod? minute = GetAndRotate(minuteStats, timeFrames.Minute);
        minute.PoolReturn(size, timeFrames.Minute);
        _lastMinuteStat = minute;

        PoolStatsPeriod? hour = GetAndRotate(hourStats, timeFrames.Hour);
        hour.PoolReturn(size, timeFrames.Hour);

        PoolStatsPeriod? day = GetAndRotate(dayStats, timeFrames.Day);
        day.PoolReturn(size, timeFrames.Day);

        PoolStatsPeriod? month = GetAndRotate(monthStats, timeFrames.Month);
        month.PoolReturn(size, timeFrames.Month);

        PoolStatsPeriod? year = Get(yearStats, timeFrames.Year);
        year.PoolReturn(size, timeFrames.Year);

        alltimeStats.PoolReturn(size, timeFrames.FullDateTime);
    } 

    public void PoolPurge(int size)
    {
        var timeFrames = new CurrentTimeFrames();

        PoolStatsPeriod? minute = GetAndRotate(minuteStats, timeFrames.Minute);
        minute.PoolPurge(size, timeFrames.Minute);
        _lastMinuteStat = minute;

        PoolStatsPeriod? hour = GetAndRotate(hourStats, timeFrames.Hour);
        hour.PoolPurge(size, timeFrames.Hour);

        PoolStatsPeriod? day = GetAndRotate(dayStats, timeFrames.Day);
        day.PoolPurge(size, timeFrames.Day);

        PoolStatsPeriod? month = GetAndRotate(monthStats, timeFrames.Month);
        month.PoolPurge(size, timeFrames.Month);

        PoolStatsPeriod? year = Get(yearStats, timeFrames.Year);
        year.PoolPurge(size, timeFrames.Year);

        alltimeStats.PoolPurge(size, timeFrames.FullDateTime);
    }

    public void PoolWaitForRequest(Guid requestGuid)
    {
        WrapCache(_waitCache, requestGuid, () => PoolWait());
    }

    public void PoolWait()
    {
        var timeFrames = new CurrentTimeFrames();

        PoolStatsPeriod? minute = GetAndRotate(minuteStats, timeFrames.Minute);
        minute.PoolWait(timeFrames.Minute);
        _lastMinuteStat = minute;

        PoolStatsPeriod? hour = GetAndRotate(hourStats, timeFrames.Hour);
        hour.PoolWait(timeFrames.Hour);

        PoolStatsPeriod? day = GetAndRotate(dayStats, timeFrames.Day);
        day.PoolWait(timeFrames.Day);

        PoolStatsPeriod? month = GetAndRotate(monthStats, timeFrames.Month);
        month.PoolWait(timeFrames.Month);

        PoolStatsPeriod? year = Get(yearStats, timeFrames.Year);
        year.PoolWait(timeFrames.Year);

        alltimeStats.PoolWait(timeFrames.FullDateTime);
    } 

    public void PoolTimeout()
    {
        var timeFrames = new CurrentTimeFrames();

        PoolStatsPeriod? minute = GetAndRotate(minuteStats, timeFrames.Minute);
        minute.PoolTimeout(timeFrames.Minute);
        _lastMinuteStat = minute;

        PoolStatsPeriod? hour = GetAndRotate(hourStats, timeFrames.Hour);
        hour.PoolTimeout(timeFrames.Hour);

        PoolStatsPeriod? day = GetAndRotate(dayStats, timeFrames.Day);
        day.PoolTimeout(timeFrames.Day);

        PoolStatsPeriod? month = GetAndRotate(monthStats, timeFrames.Month);
        month.PoolTimeout(timeFrames.Month);

        PoolStatsPeriod? year = Get(yearStats, timeFrames.Year);
        year.PoolTimeout(timeFrames.Year);

        alltimeStats.PoolTimeout(timeFrames.FullDateTime);
    } 


    public void PoolError()
    {
        var timeFrames = new CurrentTimeFrames();

        PoolStatsPeriod? minute = GetAndRotate(minuteStats, timeFrames.Minute);
        minute.PoolError(timeFrames.Minute);
        _lastMinuteStat = minute;

        PoolStatsPeriod? hour = GetAndRotate(hourStats, timeFrames.Hour);
        hour.PoolError(timeFrames.Hour);

        PoolStatsPeriod? day = GetAndRotate(dayStats, timeFrames.Day);
        day.PoolError(timeFrames.Day);

        PoolStatsPeriod? month = GetAndRotate(monthStats, timeFrames.Month);
        month.PoolError(timeFrames.Month);

        PoolStatsPeriod? year = Get(yearStats, timeFrames.Year);
        year.PoolError(timeFrames.Year);

        alltimeStats.PoolError(timeFrames.FullDateTime);
    }

    public void PoolDisposed()
    {
        var timeFrames = new CurrentTimeFrames();

        PoolStatsPeriod? minute = GetAndRotate(minuteStats, timeFrames.Minute);
        minute.PoolDisposed(timeFrames.Minute);
        _lastMinuteStat = minute;

        PoolStatsPeriod? hour = GetAndRotate(hourStats, timeFrames.Hour);
        hour.PoolDisposed(timeFrames.Hour);

        PoolStatsPeriod? day = GetAndRotate(dayStats, timeFrames.Day);
        day.PoolDisposed(timeFrames.Day);

        PoolStatsPeriod? month = GetAndRotate(monthStats, timeFrames.Month);
        month.PoolDisposed(timeFrames.Month);

        PoolStatsPeriod? year = Get(yearStats, timeFrames.Year);
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