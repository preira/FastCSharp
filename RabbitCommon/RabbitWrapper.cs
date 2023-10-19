using FastCSharp.Pool;
using RabbitMQ.Client;

namespace FastCSharp.RabbitCommon;
public interface IRabbitChannel : IDisposable
{
    public void BasicPublish(
        object owner,
        IBasicProperties? basicProperties, 
        byte[] body);

    /// <summary>
    /// Waits until all messages published since the last call have been either ack'd or nack'd by the broker.
    /// You need to pass the this reference as the owner of the channel.
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="confirmTimeout"></param>
    public void WaitForConfirmsOrDie(object owner, TimeSpan confirmTimeout);

    /// <summary>
    /// Declare an exchange passively; that is, check if the named exchange exists.
    /// You need to pass the this reference as the owner of the channel.
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="exchangeName">The name of the targeted Exchange</param>
    // public void ExchangeDeclarePassive(object owner, string exchangeName);
    
    /// <summary>
    /// Declare a queue passively; that is, check if the named queue exists.
    /// You need to pass the this reference as the owner of the channel.
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="routingKey">The name of the targeted Queue</param>
    // public void QueueDeclarePassive(object owner, string routingKey);

    public void ConfirmSelect(object owner);

    public ulong? NextPublishSeqNo(object owner);

    public bool IsStalled { get; set;  }
}
public class RabbitChannel : Individual<IModel>, IRabbitChannel
{
    private readonly string exchangeName;
    private readonly string? routingKey;
    private readonly string? queue;

    public RabbitChannel(
        IModel channel, 
        string exchangeName, 
        string? queue = null, 
        string? routingKey = null) 
    : base(channel)
    {
        this.exchangeName = exchangeName;
        this.routingKey = routingKey;
        this.queue = queue;

        channel.ExchangeDeclarePassive(exchangeName);
        
        if (!string.IsNullOrWhiteSpace(queue)) channel.QueueDeclarePassive(queue);
    }

    /// <summary>
    /// Publishes a message to the exchange.
    /// You need to pass the this reference as the owner of the channel. 
    /// the remaining parameters are the same as the ones in the RabbitMQ.Client.IModel.BasicPublish method.
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="basicProperties"></param>
    /// <param name="body"></param>
    public void BasicPublish(
        object owner,
        IBasicProperties? basicProperties, 
        byte[] body) 
        => GetValue(owner).BasicPublish(
            exchangeName, queue ?? routingKey ?? "", basicProperties, body);

    /// <summary>
    /// Waits until all messages published since the last call have been either ack'd or nack'd by the broker.
    /// You need to pass the this reference as the owner of the channel.
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="confirmTimeout"></param>
    public void WaitForConfirmsOrDie(object owner, TimeSpan confirmTimeout) 
        => GetValue(owner)?.WaitForConfirmsOrDie(confirmTimeout);

    /// <summary>
    /// Declare an exchange passively; that is, check if the named exchange exists.
    /// You need to pass the this reference as the owner of the channel.
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="exchangeName">The name of the targeted Exchange</param>
    // public void ExchangeDeclarePassive(object owner, string exchangeName) 
    //     => GetValue(owner)?.ExchangeDeclarePassive(exchangeName);
    
    /// <summary>
    /// Declare a queue passively; that is, check if the named queue exists.
    /// You need to pass the this reference as the owner of the channel.
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="queue">The name of the targeted Queue</param>
    // public void QueueDeclarePassive(object owner, string queue)
    //     => GetValue(owner)?.QueueDeclarePassive(queue);

    public void ConfirmSelect(object owner) => GetValue(owner)?.ConfirmSelect();

    public ulong? NextPublishSeqNo(object owner) => GetValue(owner)?.NextPublishSeqNo;

}
