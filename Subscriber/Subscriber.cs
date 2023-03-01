namespace FastCSharp.SDK.Subscriber;

delegate Boolean OnMessageCallback<T>(T? message);
delegate T? Handler<T>(T? message);

interface ISubscriber<T>
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

interface IWorker<T>
{
    public Boolean OnMessageCallback(T? message);
}

abstract class AbstreactSubscriberFactory
{
    abstract public ISubscriber<T> NewSubscriber<T>();
}
