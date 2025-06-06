namespace FastCSharp.Pool;

/// <summary>
/// Provides safe access to the value ot type T. Guarantees that the value will be returned to the queue when disposed by the caller.
/// When extending this class you must never give control of the value to the caller.
/// In this way we can ensure that after disposing the value the caller will not be able to use it.
/// This is important because the value is returned to the pool and may be used by another caller.
/// If the caller is able to use the value after disposing it, it may cause unexpected behavior.
/// <br/>
/// The recommended approach is to use the <c>using</c> statement to ensure that the individual is returned to the pool:
/// <example><code>using var item = owner.Borrow(this);</code></example>
/// If you don't use the <c>using</c> statement you must call <c>Dispose()</c> on the individual to return it to the pool.
/// <br/>
/// You should implement your own logic to determine if the individual is stalled and set this flag accordingly.
/// </summary>
/// <typeparam name="T"></typeparam> <summary>
/// 
/// </summary>
/// <typeparam name="T"></typeparam>
public class Individual<T> : IAsyncDisposable
where T : class, IDisposable
{

    private WeakReference? owner;
    protected bool disposed;

    /// <summary>
    /// A Stalled individual is not working properly and will not be reclaimed to the pool.
    /// It will be disposed instead.
    /// You should implement your own logic to determine if the individual is stalled and set this flag accordingly. 
    /// If you implement your own logic to determine if the individual is stalled, you should reduce this method visibility.
    /// </summary>
    /// <value></value>
    public bool IsStalled { get; set;}
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
    // public void Dispose()
    // {
    //     bool? isPoolExists = false; 
    //     Task.Run(async () => isPoolExists = ReturnAddress != null ? await ReturnAddress.Return(this) : false).RunSynchronously();

    //     if (isPoolExists == false)
    //     {
    //         // There is no more pool holding this individual.
    //         DisposeValue(true);
    //     }
    // }

    public async ValueTask DisposeAsync()
    {
        bool isPoolExists = ReturnAddress != null && await ReturnAddress.ReturnAsync(this);

        if (isPoolExists == false)
        {
            // There is no more pool holding this individual.
            DisposeValue(true);
        }
    }
}
