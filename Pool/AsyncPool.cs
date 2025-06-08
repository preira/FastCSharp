using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using FastCSharp.Observability;
using Microsoft.Extensions.Logging;

namespace FastCSharp.Pool;

public delegate Task<T> CreateAsync<T>();

public class AsyncPool<T, K>  : IReturnable<K>, IAsyncPool<T>, IDisposable
where T : Individual<K>, IAsyncDisposable
where K : class, IDisposable
{
    public TimeSpan DefaultTimeout { get; private set;}
    public IPoolStats? Stats { get => stats?.AllTimeStats; }
    public int MinSize { get; private set;}
    public int MaxSize { get; private set;}

    private int count;
    public int Count { get => Volatile.Read(ref count); }

    private CreateAsync<T> IndividualFactoryAsync { get; set; }
    private readonly ConcurrentQueue<Individual<K>> available;
    private readonly ConcurrentDictionary<int, WeakReference<Individual<K>>> inUse;
    private readonly ILogger logger;

    private bool disposed;
    private readonly PoolStats? stats;
    private readonly SemaphoreSlim _lock = new (1, 1);

    /// <summary>
    /// Flag to indicate if an individual is being added to the pool.
    /// This is used to prevent multiple threads from trying to add individuals at the same time.
    /// It is set to 1 when an individual is being added and reset to 0 when the addition is complete.
    /// Using int instead of bool allows to use Interlocked operations for thread safety, simplifying the implementation.
    /// </summary>
    private int isAddingIndividual = 0;

    private object _idxLock = new();
    private int idx = 0;
    private int Index
    {
        get
        {
            lock (_idxLock)
            {
                idx = ++idx % int.MaxValue;
                return idx;
            }
        }
    }
    
    // TODO: change to:
    // - [ ] accept a PoolConfig object
    // - [ ] accept a CancelationToken 
    public AsyncPool(
        CreateAsync<T> createIndividualAsync,
        ILoggerFactory loggerFactory,
        int minSize,
        int maxSize,
        bool initialize = false,
        bool registerStats = true,
        double defaultTimeout = 1000)
    {
        logger = loggerFactory.CreateLogger<AsyncPool<T, K>>();

        ArgumentNullException.ThrowIfNull(createIndividualAsync);
        IndividualFactoryAsync = createIndividualAsync;

        // avoiding problematic configurations
        MinSize = minSize > 0 ? minSize : 0;
        MaxSize = minSize < maxSize ? maxSize : minSize;
        if (MaxSize < 1) throw new ArgumentException("MaxSize must be greater than 0.");

        DefaultTimeout = TimeSpan.FromMilliseconds(defaultTimeout);

        available = new();
        inUse = new();

        if (initialize)
        {
            _ = DefferedInitializationAsync(minSize);
        }
        if (registerStats) stats = new PoolStats();
        stats?.UpdateSize(Count);
    }

    private async Task DefferedInitializationAsync(int minSize)
    {
        List<ConfiguredTaskAwaitable> tasks = new List<ConfiguredTaskAwaitable>(minSize);
        for (int i = 0; i < minSize; i++)
        {
            tasks.Add(
                Task.Run(async () =>
                {
                    await AddIndividualAsync().ConfigureAwait(false);
                }).ConfigureAwait(false)
            );
        }
        foreach (var task in tasks)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Error during pool initialization.");
            }            
        }
    }

    private async Task<bool> AddIndividualAsync()
    {
        try
        {
            var individual = await CreateIndividualAsync().ConfigureAwait(false);

            await _lock.WaitAsync();

            if (count >= MaxSize)
            {
                individual.DisposeValue(true);
                return false;
            }

            available.Enqueue(individual);
            Interlocked.Increment(ref count);

            stats?.UpdateSize(Count);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error creating individual.");
            return false;
        }
        finally
        {
            Interlocked.Exchange(ref isAddingIndividual, 0);
            _lock.Release();
        }
    }

    public async Task<T> BorrowAsync(object caller, double timeoutInMilliseconds = -1)
    {
        if (disposed) throw new ObjectDisposedException(GetType().FullName);

        // -1 signals to use default
        var timeoutSpan = timeoutInMilliseconds > -1 ? TimeSpan.FromMilliseconds(timeoutInMilliseconds) : DefaultTimeout;
        var timeLimit = DateTime.Now.Add(timeoutSpan);
        
        Individual<K>? individual = null;

        while (individual == null)
        {
            var remaining = GetRemainingTime(timeLimit);

            // Should release the lock if timeout is reached
            bool isLockAcquired = false;
            try
            {
                isLockAcquired = await _lock.WaitAsync(remaining);
                if (!isLockAcquired)
                {
                    // Throws TimeoutException if timed out
                    CheckPoolTimeoutAsync(timeoutSpan, true);
                }

                while (available.IsEmpty && Count >= MaxSize)
                {
                    stats?.PoolWait();

                    _lock.Release();

                    // Yield to allow other threads to give opportunity to others
                    await Task.Yield();

                    remaining = GetRemainingTime(timeLimit);

                    isLockAcquired = await _lock.WaitAsync(remaining);
                    if (!isLockAcquired)
                    {
                        // Throws TimeoutException if timed out
                        CheckPoolTimeoutAsync(timeoutSpan, true);
                    }

                    if (!available.IsEmpty || Count < MaxSize) break;

                }

                individual = GetIndividualAsync(caller);

                if (individual == null && Interlocked.CompareExchange(ref isAddingIndividual, 1, 0) == 0 && count < MaxSize)
                {
                    // trigger add a new Individual and go back to waiting for an individual.
                    _ = Task.Run(async () =>
                    {
                        await AddIndividualAsync().ConfigureAwait(false);
                    }).ConfigureAwait(false);
                }
            }
            finally
            {
                if (isLockAcquired)
                {
                    _lock.Release();
                }
            }
        }

        return (T)individual;
    }

    /// <summary>
    /// Calculates the remaining time until the time limit is reached.
    /// If the remaining time is negative, it returns TimeSpan.Zero.
    /// </summary>
    /// <param name="timeLimit"></param>
    /// <returns></returns>
    private static TimeSpan GetRemainingTime(DateTime timeLimit)
    {
        TimeSpan remaining = timeLimit - DateTime.Now;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private Individual<K>? GetIndividualAsync(object caller)
    {
        Individual<K>? individual;

        var isHit = available.TryDequeue(out individual);

        if (individual != null)
        {
            stats?.PoolRequest(isHit, Count);

            PutInUse(caller, individual);
        }

        return individual;
    }

    private void CheckPoolTimeoutAsync(TimeSpan timeout, bool timedout)
    {
        if (timedout)
        {
            stats?.PoolTimeout();
            if (stats?.TimeoutRatio > 0.5) _PurgeInUse();
            throw new TimeoutException($"Could not get a {typeof(T)} from the pool within the {timeout.TotalMilliseconds} ms timeout.");
        }
    }

    public async Task<bool> ReturnAsync(Individual<K> individual)
    {
        if (disposed) return false;
        try
        {
            await _lock.WaitAsync();

            var removed = inUse.Remove(individual.Id, out _);
            // If the available count is greater than 80% of the minimum size, we can dispose this individual.
            if (!removed || individual.IsStalled || available.Count > (MinSize * 0.8) && Count > MinSize || Count > MaxSize)
            {
                if (removed)
                {
                    Interlocked.Decrement(ref count);
                }
                // Else it is not in the inUse list, it is not a valid connection and should be terminated without updating counters.
                individual.DisposeValue(true);
 
                stats?.PoolDisposed();

                return false;
            }

            available.Enqueue(individual);
            stats?.PoolReturn(Count);

            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task PurgeInUse()
    {
        if (disposed) return;
        try
        {
            await _lock.WaitAsync();
            _PurgeInUse();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Removes all individuals that are not in use from the inUse list.
    /// Calls to this method <b>MUST</b> come with a lock on the pool.
    /// </summary>
    private void _PurgeInUse()
    {
        inUse
            .Where(e => !e.Value.TryGetTarget(out var target))
            .ToList()
            .ForEach(e => inUse.TryRemove(e.Key, out _));
        Interlocked.Exchange(ref count, inUse.Count + available.Count);
        stats?.PoolPurge(Count);
    }

    private void PutInUse(object caller, Individual<K> individual)
    {
        individual.Owner = caller;
        // Allow the individual to be disposed and not returned to the pool.
        inUse[individual.Id] = new WeakReference<Individual<K>>(individual);
    }

    private async Task<T> CreateIndividualAsync()
    {
        try
        {
            var individual = await IndividualFactoryAsync().ConfigureAwait(false);
            individual.Id = Index;
            individual.ReturnAddress = this;
            return individual;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "The AsyncPool got an Error from IndividualFactoryAsync while attempting to create an individual.");
            throw;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed) return;
        try
        {
            _lock.Wait(5000);

            if (disposing)
            {
                // dispose managed state (managed objects)
                disposed = true;
                foreach (var individual in available)
                {
                    individual.DisposeValue(true);
                }
                foreach (var key in inUse.Keys)
                {
                    inUse.TryRemove(key, out _);
                }
            }

        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public Task<IHealthReport> ReportHealthStatusAsync()
    {
        return Task.Run(
            () => {

                int currentlyAvailable = available.Count();
                int currentlyInUse = inUse.Count();
                string name = GetType().Name;
                HealthStatus status = HealthStatus.Healthy;

                if (currentlyInUse >= MaxSize)
                {
                    status = HealthStatus.Saturated;
                }
                return (IHealthReport) new HealthReport(name, status, $"Pool size is {Count} and has {currentlyAvailable} available individuals and can grow by {MaxSize - currentlyInUse} individuals.");
            });
    }
}