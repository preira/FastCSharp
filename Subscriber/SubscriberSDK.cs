using FastCSharp.Subscriber;

namespace FastCSharp.SDK.Subscriber;

public abstract class AbstractSubscriber<T>: ISubscriber<T>
{
    readonly List<Handler<T>> handlers;

    protected AbstractSubscriber()
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
