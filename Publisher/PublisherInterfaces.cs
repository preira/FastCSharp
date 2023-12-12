using FastCSharp.Observability;

namespace FastCSharp.Publisher;

public delegate  Task<M?> Handler<M>(M? message);

/// <summary>
/// The interface to use to publish objects.
/// </summary>
/// <typeparam name="T">The type of the objects to be published.</typeparam>
public interface IPublisher<T>: IDisposable, IHealthReporter
{ 
    /// <summary>
    /// Publishes the object passed as argument.
    /// It is very important to verify the return value of this method. If it returns false, it means that the
    /// message was not published and it should be handled accordingly.
    /// </summary>
    /// <param name="message">The object to publish.</param>
    /// <returns>A Boolean future indicating if the message was published or not.</returns>
    Task<bool> Publish(T? message);

    /// <summary>
    /// Publishes the object's list passed as argument.
    /// It is very important to verify the return value of this method. If it returns false, it means that the
    /// message was not published and it should be handled accordingly.
    /// There is no guarantee of which messages are refused. 
    /// </summary>
    /// <param name="message">The object to publish.</param>
    /// <returns>A Boolean future indicating if the message was published or not.</returns>
    Task<bool> Publish(IEnumerable<T> messages);

    public IPublisher<T> ForExchange(string exchange);

    public IPublisher<T> ForQueue(string queue);

    public IPublisher<T> ForRouting(string key);

}


