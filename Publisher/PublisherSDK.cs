using System.Text.Json;
using FastCSharp.Publisher;

namespace FastCSharp.SDK.Publisher;
/// <summary>
/// This is the class to be extend in order to imlpement a Publisher for a concrete engine.
/// Objects are serialized in JSon format and converted to byte array before calling the 
/// implementation AsyncPublish.
/// </summary>
/// <typeparam name="T">The type of object to be published</typeparam>
public abstract class AbstractPublisher<T> : IPublisher<T>
{
    protected bool disposed = false;
    readonly List<Handler<T>> handlers;
    protected AbstractPublisher()
    {
        handlers = new List<Handler<T>>();
    }

    /// <summary>
    /// Will publish the object passed as argument in JSon formst, according to
    /// the underlaying implementation.
    /// </summary>
    /// <param name="message">The object to publish.</param>
    /// <returns>A Boolean future.</returns>
    public async Task<bool> Publish(T? message)
    {
        foreach (var handler in handlers)
        {
            message = handler(message);
        } 
        byte[] jsonUtf8Bytes = JsonSerializer.SerializeToUtf8Bytes<T?>(message);
        return await Publish(jsonUtf8Bytes);   
    }

    private async Task<bool> Publish(byte[] message)
    {
        if (IsHealthyOrTryRecovery())
        {
            Task<bool> task = new ( () => AsyncPublish(message) );
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

    private bool IsHealthyOrTryRecovery()
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
    protected abstract bool IsHealthy();

    /// <summary>
    /// Should contain logic to recover a faulty connection status given by IsHealthy().
    /// </summary>
    /// <param name="dispose"></param>
    /// <returns></returns>
    protected abstract bool ResetConnection(bool dispose = true);

    /// <summary>
    /// Should dispose of any managed or unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Should dispose of any managed and unmanaged resources.
    /// </summary>
    protected abstract void Dispose(bool disposing);

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
