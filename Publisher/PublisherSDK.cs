namespace FastCSharp.SDK.Publisher;

using System.Text.Json;
using FastCSharp.Publisher;

/// <summary>
/// This is the class to be extend in order to imlpement a Publisher for a concrete engine.
/// Objects are serialized in JSon format and converted to byte array before calling the 
/// implementation AsyncPublish.
/// </summary>
/// <typeparam name="T">The type of object to be published</typeparam>
public abstract class AbstractPublisher<T> : IPublisher<T>, IDisposable
{
    List<Handler<T>> handlers;
    public AbstractPublisher()
    {
        handlers = new List<Handler<T>>();
    }

    /// <summary>
    /// Will publish the object passed as argument in JSon formst, according to
    /// the underlaying implementation.
    /// </summary>
    /// <param name="obj">The object to publish.</param>
    /// <returns>A Boolean future.</returns>
    public async Task<Boolean> Publish(T? obj)
    {
        foreach (var handler in handlers)
        {
            obj = handler(obj);
        } 
        byte[] jsonUtf8Bytes =JsonSerializer.SerializeToUtf8Bytes<T?>(obj);
        return await Publish(jsonUtf8Bytes);   
    }

    private async Task<Boolean> Publish(byte[] message)
    {
        if (IsHealthyOrTryRecovery())
        {
            Task<Boolean> task = new Task<bool>( () => AsyncPublish(message) );
            task.Start();

            return await task;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// This is where it should be implemented the logic to send the object. 
    /// </summary>
    /// <param name="body"></param>
    /// <returns></returns>
    protected abstract bool AsyncPublish(byte[] body);

    private Boolean IsHealthyOrTryRecovery()
        // => IsHealthy() || ResetConnection();
    {
        if(IsHealthy())
        {
            return true;
        }
        // try to recover
        return ResetConnection();
    }

    /// <summary>
    /// Should contain logic to make sure that the publication will succeed, like checking connection
    /// or queues.
    /// </summary>
    /// <returns>a Boolean indicating if the service is healthy</returns>
    protected abstract Boolean IsHealthy();

    /// <summary>
    /// Should contain logic to recover a faulty connection status given by IsHealthy().
    /// </summary>
    /// <param name="dispose"></param>
    /// <returns></returns>
    protected abstract Boolean ResetConnection(bool dispose = true);

    /// <summary>
    /// Should dispose of any managed resources.
    /// </summary>
    public abstract void Dispose();

    /// <summary>
    /// Adds a Message Handler to a chain of observers. The order by which the Handlers are added 
    /// will be respected when processing an objet to be sent.
    /// </summary>
    /// <param name="handler">The function to handle the \<T\> object.</param>
    /// <returns>The publisher it self.</returns>
    public IPublisher<T> AddMsgHandler(Handler<T> handler)
    {
        handlers.Add(handler);
        return this;
    }
}
