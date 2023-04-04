﻿using FastCSharp.Subscriber;

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


    /// <summary>
    /// Should dispose of any managed and unmanaged resources.
    /// </summary>
    protected abstract void Dispose(bool disposing);

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The subscriber should implement this method to reset the subscriber to its initial state.
    /// This will register a new consumer and, if needed, a new connection and channel.
    /// </summary>
    public abstract void Reset();

    /// <summary>
    /// The subscriber should implement this method to unsubscribe the consumer from the message queue.
    /// This will stop the subscriber from receiving messages.
    /// </summary>
    public abstract void UnSubscribe();
}    
