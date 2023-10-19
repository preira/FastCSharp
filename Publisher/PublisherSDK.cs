using FastCSharp.Publisher;

namespace FastCSharp.SDK.Publisher;
/// <summary>
/// This is the class to be extend in order to imlpement a Publisher for a concrete engine.
/// Objects are serialized in JSon format and converted to byte array before calling the 
/// implementation AsyncPublish.
/// </summary>
/// <typeparam name="T">The type of object to be published</typeparam>
[Obsolete("Use the new implementation of RabbitPublisherFactory")]    
public abstract class AbstractPublisherHandler<T> : IHandler<T>
{
    protected bool disposed = false;
    protected readonly List<Handler<T>> handlers;
    protected AbstractPublisherHandler()
    {
        handlers = new List<Handler<T>>();
    }

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
    public IHandler<T> AddMsgHandler(Handler<T> handler)
    {
        if(disposed) throw new ObjectDisposedException(nameof(AbstractPublisherHandler<T>));
        handlers.Add(handler);
        return this;
    }
}
