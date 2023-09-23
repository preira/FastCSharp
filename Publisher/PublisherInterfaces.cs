namespace FastCSharp.Publisher;

public delegate T? Handler<T>(T? message);

public interface IHandler<T> : IDisposable
{
    /// <summary>
    /// Registers handler function to handle message before deelivering to the callback
    /// Returns self to chain the addition of message handlers
    /// </summary>
    /// <param name="handler"></param>
    /// <returns>The same IPublisher object.</returns>
    public IHandler<T> AddMsgHandler(Handler<T> handler);
}

/// <summary>
/// The interface to use to publish objects.
/// </summary>
/// <typeparam name="T">The type of the objects to be published.</typeparam>
public interface IPublisher<T>: IHandler<T>
{ 
    /// <summary>
    /// Publishes the object passed as argument.
    /// It is very important to verify the return value of this method. If it returns false, it means that the
    /// message was not published and it should be handled accordingly.
    /// </summary>
    /// <param name="message">The object to publish.</param>
    /// <returns>A Boolean future indicating if the message was published or not.</returns>
    Task<bool> Publish(T? message);
}

/// <summary>
/// The interface to use to publish a list of objects.
/// </summary>
/// <typeparam name="T">The type of the objects to be published.</typeparam>
public interface IBatchPublisher<T>: IHandler<T>
{ 
    /// <summary>
    /// Publishes the object's list passed as argument.
    /// It is very important to verify the return value of this method. If it returns false, it means that the
    /// message was not published and it should be handled accordingly.
    /// There is no guarantee of which messages are refused. 
    /// </summary>
    /// <param name="message">The object to publish.</param>
    /// <returns>A Boolean future indicating if the message was published or not.</returns>
    Task<bool> BatchPublish(IEnumerable<T> messages);
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
    public IPublisher<T> NewPublisher<T>(string destination, string? routingKey = null);

}

/// <summary>
/// Factory to create new Publisher.  
/// </summary>
public interface IBatchPublisherFactory
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
    public IBatchPublisher<T> NewPublisher<T>(string destination, string? routingKey = null);

}