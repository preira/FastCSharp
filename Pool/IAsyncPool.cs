using FastCSharp.Observability;

namespace FastCSharp.Pool;

public interface IAsyncPool<T> : IHealthReporter
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
    /// <exception cref="Exception"></exception> <summary>
    /// 
    /// </summary>
    /// <param name="caller"></param>
    /// <param name="timeout">A timeout of -1 signals to wait for the default timeout (this is the default value)</param>
    /// <returns></returns>
    Task<T> BorrowAsync(object caller, double timeout = -1);

}
