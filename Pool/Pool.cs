
using System.Collections.Concurrent;
using System.Text.Json;
using FastCSharp.Date;

namespace FastCSharp.Pool;

internal interface IReturnable<T>
where T : class, IDisposable
{
    bool Return(Individual<T> individual);
}

public interface IPool<T>
{
    /// <summary>
    /// Borrows an individual of type <c>T</c> from the pool. 
    /// If no <c>T</c> individual is available the caller thread will wait until one is available or the timeout is reached. 
    /// The individual object will be locked for the given caller until it is returned to the pool through <c>Dispose()</c>.
    /// <br/>
    /// The recommended approach is to use the <c>using</c> statement to ensure that the individual is returned to the pool:
    /// <example><code>using var item = owner.Borrow(this);</code></example>
    /// If you don't use the <c>using</c> statement you must call <c>Dispose()</c> on the individual to return it to the pool.
    /// </summary>
    /// <param name="caller"></param>
    /// <param name="timeout">Time to wait until an individual is available</param>
    /// <returns></returns>
    /// <exception cref="ObjectDisposedException"></exception>
    /// <exception cref="TimeoutException"></exception>
    /// <exception cref="System.Exception"></exception> <summary>
    /// 
    /// </summary>
    /// <param name="caller"></param>
    /// <param name="timeout"></param>
    /// <returns></returns>
    T Borrow(object caller, int timeout = 1000);
}

public delegate T Create<T>();

public class Pool<T, K>  : IReturnable<K>, IPool<T>, IDisposable
where T : Individual<K>, IDisposable
where K : class, IDisposable
{
    private Create<T> Factory { get; set; }
    readonly ConcurrentQueue<Individual<K>> available;
    readonly ConcurrentDictionary<int, WeakReference<Individual<K>>> inUse;
    public int MinSize { get; private set;}
    public int MaxSize { get; private set;}
    private int count;
    public int Count { get => count; }
    private int idx = 0;
    private bool disposed;
    private PoolStats stats;
    public IPoolStats Stats { get => stats; }

    private int Index {
        get => Interlocked.Exchange(ref idx, idx = (idx) % Int32.MaxValue + 1);
    }
    
    public readonly object _lock = new ();

    public Pool(Create<T> factory, int minSize, int maxSize, bool initialize = false, bool registerStats = true)
    {
        Factory = factory;

        MinSize = minSize;
        MaxSize = maxSize;

        available = new ();
        inUse = new ();


        if (initialize)
        {
            for (int i = 0; i < minSize; i++)
            {
                available.Enqueue(CreateIndividual());
            }
            Interlocked.Exchange(ref count, minSize);
        }
        // TODO: Revise to comply with stats history also TBD
        stats = new PoolStats(TimeSpan.FromHours(1), Count);
    }

    public T Borrow(object caller, int timeout = 1000)
    {
        if (disposed) throw new ObjectDisposedException(GetType().FullName);
        try
        {
            var timeLimit = DateTime.Now.AddMilliseconds(timeout);
            Monitor.Enter(_lock);

            while (available.IsEmpty && Count >= MaxSize)
            {
                var remaining = timeLimit - DateTime.Now;
                if (remaining <= TimeSpan.Zero) 
                {
                    stats.PoolTimeout();
                    if (stats.TimeoutRatio > 0.5) PurgeInUse();
                    throw new TimeoutException($"Could not get a {typeof(T)} from the pool within the {timeout} ms timeout.");
                }
                
                stats.PoolWait();
                bool timedout = Monitor.Wait(_lock, remaining);
                
                if (!available.IsEmpty || Count < MaxSize) break;

                if (timedout)
                {
                    stats.PoolTimeout();
                    if (stats.LastPeriodTimeoutRatio > 0.5) PurgeInUse();
                    throw new TimeoutException($"Could not get a {typeof(T)} from the pool within the {timeout} ms timeout.");
                }
            }

            Individual<K>? individual;

            var isHit = available.TryDequeue(out individual);
            
            if (!isHit && Count < MaxSize)
            {
                Interlocked.Increment(ref count);
                individual = CreateIndividual();
            }

            if (individual != null)
            {
                stats.PoolRequest(isHit, Count);
            
                PutInUse(caller, individual);
            }
            else
            {
                stats.PoolError();
                throw new Exception("If you are reading this, something is wrong with the pool implementation.");
            }


            Monitor.Pulse(_lock);
            return (T)individual;
        }
        finally
        {
            Monitor.Exit(_lock);
        }
    }

    public bool Return(Individual<K> individual)
    {
        if (disposed) return false;
        try
        {
            Monitor.Enter(_lock);

            var removed = inUse.Remove(individual.Id, out _);
            // If the available count is greater than 80% of the minimum size, we can dispose this individual.
            if (!removed || available.Count > (MinSize * 0.8)   && Count > MinSize)
            {
                if (removed)
                {
                    Interlocked.Decrement(ref count);
                }
                // Else it is not in the inUse list, it is not a valid connection and should be terminated without updating counters.
                individual.DisposeValue(true);
 
                stats.PoolDisposed();
                Monitor.Pulse(_lock);
                return false;
            }

            available.Enqueue(individual);
            stats.PoolReturn(Count);

            Monitor.Pulse(_lock);
            return true;
        }
        finally
        {
            Monitor.Exit(_lock);
        }
    }

    public void PurgeInUse()
    {
        if (disposed) return;
        try
        {
            Monitor.Enter(_lock);
            inUse
                .Where(e => !e.Value.TryGetTarget(out var target))
                .ToList()
                .ForEach(e => inUse.TryRemove(e.Key, out _));
            Interlocked.Exchange(ref count, inUse.Count + available.Count);
            stats.PoolPurge(Count);
        }
        finally
        {
            Monitor.Exit(_lock);
        }
    }

    private void PutInUse(object caller, Individual<K> individual)
    {
        individual.Owner = caller;
        inUse[individual.Id] = new WeakReference<Individual<K>>(individual);
    }

    private T CreateIndividual()
    {
        // Keep individual Id for those in the pool
        var individual = Factory();
        individual.Id = Index;
        individual.ReturnAddress = this;
        return individual;
    }

    protected virtual void Dispose(bool disposing)
    {
        try
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                    disposed = true;
                    Monitor.TryEnter(_lock, 5000);
                    foreach (var individual in available)
                    {
                        individual.DisposeValue(true);
                    }
                    for(int i = 0; i < Count; i++)
                    {
                        inUse.TryRemove(i, out _);
                    }
                }

                Monitor.PulseAll(_lock);
            }
        }
        finally
        {
            Monitor.Exit(_lock);
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Provides safe access to the value ot type T. Guarantees that the value will be returned to the queue when disposed by the caller.
/// When extending this class you must never give control of the value to the caller.
/// In this way we can ensure that after disposing the value the caller will not be able to use it.
/// This is important because the value is returned to the pool and may be used by another caller.
/// </summary>
/// <typeparam name="T"></typeparam> <summary>
/// 
/// </summary>
/// <typeparam name="T"></typeparam>
public class Individual<T> : IDisposable
where T : class, IDisposable
{
    private WeakReference? owner;
    protected bool disposed;
    internal object? Owner { 
        get => owner?.Target; 
        set => owner = new WeakReference(value);
    }

    internal int Id { get; set; }
    internal IReturnable<T>? ReturnAddress { get; set; }
    protected T value;

    public Individual(T value)
    {
        this.value = value;
    }

    protected T GetValue(object owner)
    {
        if (Owner != owner) throw new InvalidOperationException("This individual is not owned by the caller.");
        if (disposed) throw new ObjectDisposedException(GetType().FullName);
        return value;
    }

    protected internal void DisposeValue(bool disposing)
    {
        if (disposing)
        {
            disposed = true;
            value?.Dispose();
        }
    }
    public void Dispose()
    {
        var isPoolExists = ReturnAddress?.Return(this);
        if (isPoolExists == false)
        {
            // There is no more pool holding this individual.
            DisposeValue(true);
        }
    }
}

public interface IPoolStats
{
    public long RequestCount { get; }
    public double HitRatio { get; }
    public double ReturnRatio { get; }
    public double ErrorRatio { get; }
    public double PurgeRatio { get; }
    public double WaitRatio { get; }
    public double TimeoutRatio { get; }
    public double DisposedRatio { get; }
    public double SizeChangeEventsRatio { get; }
    public double SizeChangeEventsPerMinute { get; }
    public double MaxSize { get; }
    public double MinSize { get; }
    public DateTime PeriodStart { get; }
    public JsonDocument ToJson();
}

/// <summary>
/// Registers events with granularity by the minute
/// </summary>
public class PoolStats : IPoolStats
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

    ConcurrentDictionary<DateTime, int> poolHitCount;
    ConcurrentDictionary<DateTime, int> poolNewCount;
    ConcurrentDictionary<DateTime, int> poolErrorCount;
    ConcurrentDictionary<DateTime, int> poolRequestCount;
    ConcurrentDictionary<DateTime, int> poolReturnCount;
    ConcurrentDictionary<DateTime, int> poolDisposedCount;
    ConcurrentDictionary<DateTime, int> poolSizeChangeEvents;
    ConcurrentDictionary<DateTime, int> poolMinSize;
    ConcurrentDictionary<DateTime, int> poolMaxSize;
    ConcurrentDictionary<DateTime, int> poolPurgeCount;
    ConcurrentDictionary<DateTime, int> poolWaitCount;
    ConcurrentDictionary<DateTime, int> poolTimeoutCount;
    int lastSize;
    TimeSpan period;
    public DateTime PeriodStart { get; private set;}

    public PoolStats(TimeSpan period, int size = 0)
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
        this.period = period;
        PeriodStart = DateTime.Now.Truncate(period);

        UpdateSize(size, PeriodStart);
    }

    private bool UpdateSize(int size, DateTime key)
    {
        if (lastSize == size) return false;
 
        poolSizeChangeEvents.AddOrUpdate(key, 1, (k, v) => v + 1);
        poolMinSize.AddOrUpdate(key, size, (k, v) => v < size ? v : size);
        poolMaxSize.AddOrUpdate(key, size, (k, v) => v > size ? v : size);
        lastSize = size;

        return true;
    }

    private void IncrementWithLock(ConcurrentDictionary<DateTime, int> dictionary, int size = -1, Action? postAction = null)
    {
        lock (this)
        {
            var key = DateTime.Now.Truncate(period);
            dictionary.AddOrUpdate(key, 1, (k, v) => v + 1);

            if(size > -1) UpdateSize(size, key);

            if (postAction!=null) postAction();
        }
    }

    public void PoolRequest(bool isHit, int size)
    {
        lock (this)
        {
            var key = DateTime.Now.Truncate(period);
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

    public void PoolReturn(int size) => IncrementWithLock(poolReturnCount, size);

    public void PoolPurge(int size) => IncrementWithLock(poolPurgeCount, size);

    public void PoolWait() => IncrementWithLock(poolWaitCount);

    public void PoolTimeout() => IncrementWithLock(poolTimeoutCount);

    public void PoolError() => IncrementWithLock(poolErrorCount);

    public void PoolDisposed() => IncrementWithLock(poolDisposedCount);

    public long RequestCount
    {
        get
        {
            lock (this)
            {
                return poolRequestCount.Sum(e => e.Value);
            }
        }
    }

    public double HitRatio
    {
        get
        {
            lock (this)
            {
                var hitCount = poolHitCount.Sum(e => e.Value);
                var requestCount = poolRequestCount.Sum(e => e.Value);
                return hitCount / requestCount;
            }
        }
    }

    public double ReturnRatio
    {
        get
        {
            lock (this)
            {
                var returnCount = poolReturnCount.Sum(e => e.Value);
                var requestCount = poolRequestCount.Sum(e => e.Value);
                return returnCount / requestCount;
            }
        }
    }

    public double ErrorRatio
    {
        get
        {
            lock (this)
            {
                var errorCount = poolErrorCount.Sum(e => e.Value);
                var requestCount = poolRequestCount.Sum(e => e.Value);
                return errorCount / requestCount;
            }
        }
    }

    public double PurgeRatio
    {
        get
        {
            lock (this)
            {
                var purgeCount = poolPurgeCount.Sum(e => e.Value);
                var requestCount = poolRequestCount.Sum(e => e.Value);
                return purgeCount / requestCount;
            }
        }
    }

    public double WaitRatio
    {
        get
        {
            lock (this)
            {
                var waitCount = poolWaitCount.Sum(e => e.Value);
                var requestCount = poolRequestCount.Sum(e => e.Value);
                return waitCount / requestCount;
            }
        }
    }

    public double TimeoutRatio
    {
        get
        {
            lock (this)
            {
                var timeoutCount = poolTimeoutCount.Sum(e => e.Value);
                var requestCount = poolRequestCount.Sum(e => e.Value);
                return timeoutCount / requestCount;
            }
        }
    }

    public double LastPeriodTimeoutRatio
    {
        get
        {
            lock (this)
            {
                var key = DateTime.Now.Truncate(period);
                var timeoutCount = poolTimeoutCount.Where(e => e.Key == key).Sum(e => e.Value);
                var requestCount = poolRequestCount.Where(e => e.Key == key).Sum(e => e.Value);
                return requestCount == 0 ? 0 : timeoutCount / requestCount;
            }
        }
    }

    public double DisposedRatio
    {
        get
        {
            lock (this)
            {
                var disposedCount = poolDisposedCount.Sum(e => e.Value);
                var requestCount = poolRequestCount.Sum(e => e.Value);
                return disposedCount / requestCount;
            }
        }
    }

    public double SizeChangeEventsRatio
    {
        get
        {
            lock (this)
            {
                var sizeChangeEventsCount = poolSizeChangeEvents.Sum(e => e.Value);
                var requestCount = poolRequestCount.Sum(e => e.Value);
                return sizeChangeEventsCount / requestCount;
            }
        }
    }

    public double SizeChangeEventsPerMinute
    {
        get
        {
            lock (this)
            {
                var sizeChangeEventsCount = poolSizeChangeEvents.Sum(e => e.Value);
                var period = poolSizeChangeEvents.Max(e => e.Key) - poolSizeChangeEvents.Min(e => e.Key);
                return sizeChangeEventsCount / period.TotalMinutes;
            }
        }
    }

    public double MaxSize
    {
        get
        {
            lock (this)
            {
                return poolMaxSize.Max(e => e.Value);
            }
        }
    }

    public double MinSize
    {
        get
        {
            lock (this)
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

        lock (this)
        {
            return JsonSerializer.SerializeToDocument(obj);
        }
    }
}