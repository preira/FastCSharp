namespace FastCSharp.Subscriber;

public delegate Task<Boolean> OnMessageCallback<in T>(T? message);
public delegate T? Handler<T>(T? message);

/// <summary>
/// Serves as independent interface for publishing messages. Usually, implementation uses a
/// Message Queue. In that case, subscriber will connect to the message queue broker and each
/// Subscriber will connect to a specific queue.
/// See the Subscriber implementation for further configuration options.
/// </summary>
/// <typeparam name="T">The type of the message object transmited.</typeparam>
/// <remarks>
/// The Subscriber is a generic class that can be used to subscribe to a message queue or any other
/// message origin. The message origin is defined by the implementation of the ISubscriberFactory
/// interface. The Subscriber is a generic class that can be used to subscribe to a message queue or any other
/// message origin. The message origin is defined by the implementation of the ISubscriberFactory
/// interface.
/// </remarks>
/// <example>
/// 
/// <code>
/// // Create a new SubscriberFactory
/// ISubscriberFactory factory = new SubscriberFactory();
/// 
/// // Create a new Subscriber for the message origin "MyQueue"
/// ISubscriber<MyMessage> subscriber = factory.NewSubscriber<MyMessage>("MyQueue");
/// 
/// // Register a callback function to process the message
/// subscriber.Register((MyMessage? message) => {
///    // Do something with the message
///   return true;
/// });
/// 
/// // Start the subscriber
/// subscriber.Start();
/// </code>
/// 
/// </example>
public interface ISubscriber<T> : IDisposable
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

public interface IEventSubscriber<T> : ISubscriber<T>
{
    public void Unregister();

    public void AttemptRecovery();
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
    /// Message Queue system this is tipically the queue indentifier that correlates to the queue config in the Subscriber configuration.</param>
    /// <typeparam name="T">The message object type.</typeparam>
    /// <returns>Returns a ISubscriber independent interface.</returns>
    abstract public ISubscriber<T> NewSubscriber<T>(string messageOrigin);
}