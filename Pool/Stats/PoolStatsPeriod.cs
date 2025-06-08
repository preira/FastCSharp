using System.Collections.Concurrent;
using System.Text.Json;

namespace FastCSharp.Pool;

/// <summary>
/// Registers events with granularity by the minute
/// </summary>
public class PoolStatsPeriod : IPoolStats
{
    // max size
    // size change and timestamp (last N size change events)
    // pool hit ratio per period (last N periods)
    // return ratio per period (last N periods)
    // TODO: Add PoolStats history to allow transitioning from a larger stats period and mantain the history
    // TODO: Consider having history for the last minutes in seconds ganularity, last hour in minutes, 
    // last day in hours, month in days, years in months
    // total size for one year: 
    // (12 + 31 + 24 + 60 + 60) x (12 x ( 8 bytes (DateTime) + 4 bytes (int)) 
    //      = 187 x 12 x 12 = 2244 objects x 12 bytes = 26.928 bytes

    public readonly object _lock = new ();
    private readonly ConcurrentDictionary<DateTime, long> poolHitCount;
    private readonly ConcurrentDictionary<DateTime, long> poolNewCount;
    private readonly ConcurrentDictionary<DateTime, long> poolErrorCount;
    private readonly ConcurrentDictionary<DateTime, long> poolRequestCount;
    private readonly ConcurrentDictionary<DateTime, long> poolReturnCount;
    private readonly ConcurrentDictionary<DateTime, long> poolDisposedCount;
    private readonly ConcurrentDictionary<DateTime, long> poolSizeChangeEvents;
    private readonly ConcurrentDictionary<DateTime, long> poolMinSize;
    private readonly ConcurrentDictionary<DateTime, long> poolMaxSize;
    private readonly ConcurrentDictionary<DateTime, long> poolPurgeCount;
    private readonly ConcurrentDictionary<DateTime, long> poolWaitCount;
    private readonly ConcurrentDictionary<DateTime, long> poolTimeoutCount;
    int lastSize;
    public DateTime PeriodStart { get; private set;}

    public PoolStatsPeriod(DateTime period, int size = 0)
    {
        poolHitCount = new ();
        poolNewCount = new ();
        poolErrorCount = new ();
        poolRequestCount = new ();
        poolReturnCount = new ();
        poolDisposedCount = new ();
        poolSizeChangeEvents = new ();
        poolMinSize = new ();
        poolMaxSize = new ();
        poolPurgeCount = new ();
        poolWaitCount = new ();
        poolTimeoutCount = new ();
        lastSize = -1; // force update

        PeriodStart = period;

        UpdateSize(size, PeriodStart);
    }

    public bool UpdateSize(int size, DateTime key)
    {
        if (lastSize == size) return false;
 
        poolSizeChangeEvents.AddOrUpdate(key, 1, (k, v) => v + 1);
        poolMinSize.AddOrUpdate(key, size, (k, v) => v < size ? v : size);
        poolMaxSize.AddOrUpdate(key, size, (k, v) => v > size ? v : size);
        lastSize = size;

        return true;
    }

    private void IncrementWithLock(ConcurrentDictionary<DateTime, long> dictionary, DateTime key, int size = -1, Action? postAction = null)
    {
        lock (_lock)
        {
            dictionary.AddOrUpdate(key, 1, (k, v) => v + 1);

            if(size > -1) UpdateSize(size, key);

            if (postAction!=null) postAction();
        }
    }
    public void PoolRequest(bool isHit, int size, DateTime key)
    {
        lock (_lock)
        {
            poolRequestCount.AddOrUpdate(key, 1, (k, v) => v + 1);
            if (isHit)
            {
                poolHitCount.AddOrUpdate(key, 1, (k, v) => v + 1);
            }
            else
            {
                poolNewCount.AddOrUpdate(key, 1, (k, v) => v + 1);
            }
            UpdateSize(size, key);
        }
    }

    public void PoolReturn(int size, DateTime key) => IncrementWithLock(poolReturnCount, key, size);

    public void PoolPurge(int size, DateTime key) => IncrementWithLock(poolPurgeCount, key, size);

    public void PoolWait(DateTime key) => IncrementWithLock(poolWaitCount, key);

    public void PoolTimeout(DateTime key) => IncrementWithLock(poolTimeoutCount, key);

    public void PoolError(DateTime key) => IncrementWithLock(poolErrorCount, key);

    public void PoolDisposed(DateTime key) => IncrementWithLock(poolDisposedCount, key);

    public long RequestCount
    {
        get
        {
            lock (_lock)
            {
                return poolRequestCount.Sum(e => e.Value);
            }
        }
    }

    public double HitRatio
    {
        get
        {
            lock (_lock)
            {
                double hitCount = poolHitCount.Sum(e => e.Value);
                double requestCount = poolRequestCount.Sum(e => e.Value);
                requestCount = requestCount == 0 ? 1 : requestCount;
                return hitCount / requestCount;
            }
        }
    }

    public double ReturnRatio
    {
        get
        {
            lock (_lock)
            {
                double returnCount = poolReturnCount.Sum(e => e.Value);
                double requestCount = poolRequestCount.Sum(e => e.Value);
                requestCount = requestCount == 0 ? 1 : requestCount;
                return returnCount / requestCount;
            }
        }
    }

    public double ErrorRatio
    {
        get
        {
            lock (_lock)
            {
                double errorCount = poolErrorCount.Sum(e => e.Value);
                double requestCount = poolRequestCount.Sum(e => e.Value);
                requestCount = requestCount == 0 ? 1 : requestCount;
                return errorCount / requestCount;
            }
        }
    }

    public double PurgeRatio
    {
        get
        {
            lock (_lock)
            {
                double purgeCount = poolPurgeCount.Sum(e => e.Value);
                double requestCount = poolRequestCount.Sum(e => e.Value);
                requestCount = requestCount == 0 ? 1 : requestCount;
                return purgeCount / requestCount;
            }
        }
    }

    public double WaitRatio
    {
        get
        {
            lock (_lock)
            {
                double waitCount = poolWaitCount.Sum(e => e.Value);
                double requestCount = poolRequestCount.Sum(e => e.Value);
                requestCount = requestCount == 0 ? 1 : requestCount;
                return waitCount / requestCount;
            }
        }
    }

    public double TimeoutRatio
    {
        get
        {
            lock (_lock)
            {
                double timeoutCount = poolTimeoutCount.Sum(e => e.Value);
                double requestCount = poolRequestCount.Sum(e => e.Value);
                requestCount = requestCount == 0 ? 1 : requestCount;
                return timeoutCount / requestCount;
            }
        }
    }

    public double DisposedRatio
    {
        get
        {
            lock (_lock)
            {
                double disposedCount = poolDisposedCount.Sum(e => e.Value);
                double requestCount = poolRequestCount.Sum(e => e.Value);
                requestCount = requestCount == 0 ? 1 : requestCount;
                return disposedCount / requestCount;
            }
        }
    }

    public double SizeChangeEventsRatio
    {
        get
        {
            lock (_lock)
            {
                double sizeChangeEventsCount = poolSizeChangeEvents.Sum(e => e.Value);
                double requestCount = poolRequestCount.Sum(e => e.Value);
                requestCount = requestCount == 0 ? 1 : requestCount;
                return sizeChangeEventsCount / requestCount;
            }
        }
    }

    public double SizeChangeEventsPerMinute
    {
        get
        {
            lock (_lock)
            {
                double sizeChangeEventsCount = poolSizeChangeEvents.Sum(e => e.Value);
                var period = poolSizeChangeEvents.Max(e => e.Key) - poolSizeChangeEvents.Min(e => e.Key);
                double totalMinutes = period.TotalMinutes;
                totalMinutes = totalMinutes == 0 ? 1 : totalMinutes;
                return sizeChangeEventsCount / totalMinutes;
            }
        }
    }

    public double MaxSize
    {
        get
        {
            lock (_lock)
            {
                return poolMaxSize.Max(e => e.Value);
            }
        }
    }

    public double MinSize
    {
        get
        {
            lock (_lock)
            {
                return poolMinSize.Min(e => e.Value);
            }
        }
    }

    public JsonDocument ToJson()
    {
       Dictionary<string, object> obj = new()
       {
           { "poolHitCount", poolHitCount },
           { "poolNewCount", poolNewCount },
           { "poolErrorCount", poolErrorCount },
           { "poolRequestCount", poolRequestCount },
           { "poolReturnCount", poolReturnCount },
           { "poolDisposedCount", poolDisposedCount },
           { "poolSizeChangeEvents", poolSizeChangeEvents },
           { "poolMinSize", poolMinSize },
           { "poolMaxSize", poolMaxSize },
           { "poolPurgeCount", poolPurgeCount },
           { "poolWaitCount", poolWaitCount },
           { "poolTimeoutCount", poolTimeoutCount }
       };

        lock (_lock)
        {
            return JsonSerializer.SerializeToDocument(obj);
        }
    }
}