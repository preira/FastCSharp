namespace FastCSharp.Subscriber;

public delegate Boolean OnMessageCallback<in T>(T? message);
public delegate T? Handler<T>(T? message);

/// <summary>
/// Independent interface to register callbacks for processing messages from a previously given
/// origin. Handlers can also be registered to form a chain of handlers. 
/// </summary>
/// <typeparam name="T">The type of the message object transmited.</typeparam>
public interface ISubscriber<T>
{
    /// <summary>
    /// Registers the callback function as a message listenner.
    /// Returns self to chain the addition of message handlers
    /// </summary>
    /// <param name="callback">the function to register as message listenner.</param>
    /// <returns></returns>
    public ISubscriber<T> Register(OnMessageCallback<T> callback);

    /// <summary>
    /// Registers handler function to handle message before deelivering to the callback
    /// Returns self to chain the addition of message handlers
    /// </summary>
    /// <param name="handler"></param>
    /// <returns></returns>
    public ISubscriber<T> AddMsgHandler(Handler<T> handler);
}

public interface IWorker<in T>
{
    public Boolean OnMessageCallback(T? message);
}

/// <summary>
/// Serves as independent interface for publishing messages. Usually, implementation uses a
/// Message Queue. In that case, subscriber will connect to the message queue broker and each 
/// Subscriber will connect to a specific queue.
/// See the Subscriber implementation for further configuration options.
/// </summary>
public interface ISubscriberFactory
{
    /// <summary>
    /// Returns a new subscriber that will be listenning to the message origin for the 
    /// current implementation and according to its configuration.
    /// After obtaining the new ISubscriber, you should register the callback to process the message along 
    /// with any Handlers you wish to add.
    /// </summary>
    /// <param name="messageOrigin">The message origin from which messges will be retrieved. For a 
    /// Message Queue system this is tipically the queue name.</param>
    /// <typeparam name="T">The message object type.</typeparam>
    /// <returns>Returns a ISubscriber independent interface.</returns>
    abstract public ISubscriber<T> NewSubscriber<T>(string messageOrigin);
}