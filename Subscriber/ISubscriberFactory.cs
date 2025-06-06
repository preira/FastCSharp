namespace FastCSharp.Subscriber;

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