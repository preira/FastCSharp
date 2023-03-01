namespace FastCSharp.SDK.Publisher;

using System.Text;
using System.Text.Json;

delegate T? Handler<T>(T? message);

interface IPublisher<T>: IDisposable
{ 
    Task<Boolean> Publish(string? message);
    Task<Boolean> Publish(T? message);

    /// <summary>
    /// Registers handler function to handle message before deelivering to the callback
    /// Returns self to chain the addition of message handlers
    /// </summary>
    /// <param name="handler"></param>
    /// <returns></returns>
    public IPublisher<T> AddMsgHandler(Handler<T> handler);
}

interface IMessageQueueHandlerFactory<T>
{
    public IPublisher<T> NewPublisher();

}

abstract class AbstractPublisher<T> : IPublisher<T>, IDisposable
{
    List<Handler<T>> handlers;
    public AbstractPublisher()
    {
        handlers = new List<Handler<T>>();
    }

    public async Task<Boolean> Publish(T? obj)
    {
        Console.WriteLine("[Encoded as an object]");
        foreach (var handler in handlers)
        {
            obj = handler(obj);
        } 
        byte[] jsonUtf8Bytes =JsonSerializer.SerializeToUtf8Bytes<T?>(obj);
        return await Publish(jsonUtf8Bytes);   
    }

    public async Task<Boolean> Publish(string? message)
    {
        Console.WriteLine("[Encoded as a string]");
        var body = Encoding.UTF8.GetBytes(message ?? "FAILED");
        return await Publish(body);   
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

    protected abstract bool AsyncPublish(byte[] body);

    private Boolean IsHealthyOrTryRecovery()
    {
        if(IsHealthy())
        {
            return true;
        }
        // try to recover
        return ResetConnection();
    }
    protected abstract Boolean IsHealthy();

    protected abstract Boolean ResetConnection(bool dispose = true);


    public abstract void Dispose();

    public IPublisher<T> AddMsgHandler(Handler<T> handler)
    {
        handlers.Add(handler);
        return this;
    }
}
