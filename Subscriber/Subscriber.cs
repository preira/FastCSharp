namespace FastCSharp.SDK.Subscriber;

public delegate Boolean OnMessageCallback<T>(T? message);
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

public interface IWorker<T>
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

public abstract class AbstractSubscriber<T>: ISubscriber<T>
{
    List<Handler<T>> handlers;

    public AbstractSubscriber()
    {
        handlers = new List<Handler<T>>();
    }

    /// <summary>
    /// Adds a callback to handle message upon arrival and before being processed by subscribed callback.
    /// This results ion a chain of responsibility where the order in which the handlers were added will 
    /// be respected. 
    /// These handlers can handle different responsibilities such as message validation or criptography.
    /// </summary>
    /// <param name="handler">The callback to handle the message.</param>
    /// <returns></returns>
    public ISubscriber<T> AddMsgHandler(Handler<T> handler)
    {
        handlers.Add(handler);
        return this;
    }

    /// <summary>
    /// Registers the callback to process the message. In praticality this is guaranteed to be the last 
    /// callback to handle the message.
    /// </summary>
    /// <param name="callback">The callback to process the message.</param>
    /// <returns></returns>
    public ISubscriber<T> Register(OnMessageCallback<T> callback)
    {
        _Register( (message)=> 
            {

                foreach (var handler in handlers)
                {
                    message = handler(message);
                }
                // TODO: Async call to trigger event
                // MessageReceived(message);
                return callback(message);
            });
        return this;
    }

    /// <summary>
    /// Subscriber implementations should implement this method, handling all connection management
    /// at this point. The message callback should be invoked once the message has been deserialized to 
    /// the message Type.
    /// </summary>
    /// <param name="callback">The callback to process the message.</param>    
    protected abstract void _Register(OnMessageCallback<T> callback);
}    
