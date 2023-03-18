namespace FastCSharp.Publisher;

public delegate T? Handler<T>(T? message);

/// <summary>
/// The interface to use to publish objects.
/// </summary>
/// <typeparam name="T">The type of the objects to be published.</typeparam>
public interface IPublisher<T>: IDisposable
{ 
    Task<Boolean> Publish(T? message);

    /// <summary>
    /// Registers handler function to handle message before deelivering to the callback
    /// Returns self to chain the addition of message handlers
    /// </summary>
    /// <param name="handler"></param>
    /// <returns>The same IPublisher object.</returns>
    public IPublisher<T> AddMsgHandler(Handler<T> handler);
}

/// <summary>
/// Factory to create new Publisher.  
/// </summary>
public interface IPublisherFactory
{
    /// <summary>
    /// Creates a new Publisher for a specific destination. In case the destination has a direct output
    /// it also takes that reference. This may be the case for a Direct Exchange that delivers directly 
    /// to a Queue.
    /// </summary>
    /// <param name="destination">The name of the destination. An Exchange for example.</param>
    /// <param name="direct">The name of the direct delivery for the destination. A Queue for example.</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public IPublisher<T> NewPublisher<T>(string destination, string routingKey = "");

}