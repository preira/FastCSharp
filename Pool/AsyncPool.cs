using System.Collections.Concurrent;
using FastCSharp.Observability;

namespace FastCSharp.Pool;

public delegate Task<T> CreateAsync<T>();

public class AsyncPool<T, K>  : IReturnable<K>, IAsyncPool<T>, IDisposable
where T : Individual<K>, IAsyncDisposable
where K : class, IDisposable
{
    private CreateAsync<T> IndividualFactoryAsync { get; set; }
    readonly ConcurrentQueue<Individual<K>> available;
    readonly ConcurrentDictionary<int, WeakReference<Individual<K>>> inUse;
    public int MinSize { get; private set;}
    public int MaxSize { get; private set;}
    public double DefaultTimeout { get; private set;}
    private int count;
    public int Count { get => count; }
    private int idx = 0;
    private bool disposed;
    private readonly PoolStats? stats;
    public IPoolStats? Stats { get => stats?.AllTimeStats; }

    private int Index {
        get => Interlocked.Exchange(ref idx, idx = (idx) % Int32.MaxValue + 1);
    }
    
    public readonly SemaphoreSlim _lock = new (1, 1);
    public readonly SemaphoreSlim _signal = new (0);

    // TODO: change to accept a PoolConfig object
    public AsyncPool(
        CreateAsync<T> createIndividualAsync, 
        int minSize, 
        int maxSize, 
        bool initialize = false, 
        bool registerStats = true, 
        double defaultTimeout = 1000)
    {
        IndividualFactoryAsync = createIndividualAsync;

        MinSize = minSize;
        MaxSize = maxSize;
        DefaultTimeout = defaultTimeout;

        available = new ();
        inUse = new ();

        if (initialize)
        {
            _ = DefferedInitializationAsync(minSize);
        }
        if (registerStats) stats = new PoolStats();
        stats?.UpdateSize(Count);
    }

    private async Task DefferedInitializationAsync(int minSize)
    {
        for (int i = 0; i < minSize; i++)
        {
            var isSuccess = await AddIndividualAsync();
            if (!isSuccess) break;
        }
    }

    private async Task<bool> AddIndividualAsync()
    {
        try
        {
            // Individuals can be created in parallel and put into used only after.
            // This is to avoid the need to lock the pool when creating new individuals.
            // If minimal size of the pool has been reached we can dispose the individual.
            // It is inneficient but it only happens at pool start up and it allows the pool to be used earlier.
            var individual = await CreateIndividualAsync();

            await _lock.WaitAsync();

            if (Count >= MinSize)
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
// TODO : need to log this            
            // logger.LogCritical(ex, "Error creating individual.");
            Console.WriteLine(ex);
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<T> BorrowAsync(object caller, double timeout = -1)
    {
        if (disposed) throw new ObjectDisposedException(GetType().FullName);

        // -1 signals to use default
        timeout = timeout > -1 ? timeout : DefaultTimeout;
        try
        {
            var timeLimit = DateTime.Now.AddMilliseconds(timeout);
            await _lock.WaitAsync();

            while (available.IsEmpty && Count >= MaxSize)
            {
                var remaining = timeLimit - DateTime.Now;
                await CheckPoolTimeoutAsync(timeout, remaining <= TimeSpan.Zero);

                stats?.PoolWait();
                bool timedout = await _lock.WaitAsync(remaining);

                if (!available.IsEmpty || Count < MaxSize) break;

                await CheckPoolTimeoutAsync(timeout, timedout);
            }

            Individual<K>? individual = await GetIndividualAsync(caller);

            // Monitor.Pulse(_lock);
            return (T)individual;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<Individual<K>> GetIndividualAsync(object caller)
    {
        Individual<K>? individual;

        var isHit = available.TryDequeue(out individual);

        if (!isHit && Count < MaxSize)
        {
            Interlocked.Increment(ref count);
            individual = await CreateIndividualAsync();
        }

        if (individual != null)
        {
            stats?.PoolRequest(isHit, Count);

            PutInUse(caller, individual);
        }
        else
        {
            stats?.PoolError();
            throw new Exception("If you are reading this, something is wrong with the pool implementation.");
        }

        return individual;
    }

    private async Task CheckPoolTimeoutAsync(double timeout, bool timedout)
    {
        if (timedout)
        {
            stats?.PoolTimeout();
            if (stats?.TimeoutRatio > 0.5) await PurgeInUse();
            throw new TimeoutException($"Could not get a {typeof(T)} from the pool within the {timeout} ms timeout.");
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
            if (!removed || individual.IsStalled || available.Count > (MinSize * 0.8) && Count > MinSize)
            {
                if (removed)
                {
                    Interlocked.Decrement(ref count);
                }
                // Else it is not in the inUse list, it is not a valid connection and should be terminated without updating counters.
                individual.DisposeValue(true);
 
                stats?.PoolDisposed();
                // Monitor.Pulse(_lock);
                return false;
            }

            available.Enqueue(individual);
            stats?.PoolReturn(Count);

            // Monitor.Pulse(_lock);
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
            inUse
                .Where(e => !e.Value.TryGetTarget(out var target))
                .ToList()
                .ForEach(e => inUse.TryRemove(e.Key, out _));
            Interlocked.Exchange(ref count, inUse.Count + available.Count);
            stats?.PoolPurge(Count);
        }
        finally
        {
            _lock.Release();
        }
    }

    private void PutInUse(object caller, Individual<K> individual)
    {
        individual.Owner = caller;
        inUse[individual.Id] = new WeakReference<Individual<K>>(individual);
    }

    private async Task<T> CreateIndividualAsync()
    {
        // Keep individual Id for those in the pool
        var individual = await IndividualFactoryAsync();
        individual.Id = Index;
        individual.ReturnAddress = this;
        return individual;
    }

    protected virtual void Dispose(bool disposing)
    {
        try
        {
            _lock.Wait(5000);

            if (!disposed)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                    disposed = true;
                    foreach (var individual in available)
                    {
                        individual.DisposeValue(true);
                    }
                    for (int i = 0; i < Count; i++)
                    {
                        inUse.TryRemove(i, out _);
                    }
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