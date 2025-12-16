using FastCSharp.Pool;
using RabbitMQ.Client;

namespace FastCSharp.RabbitCommon;
public interface IRabbitChannel : IAsyncDisposable
{
    /// <summary>
    /// Publishes a message to the exchange.
    /// You need to pass the this reference as the owner of the channel. 
    /// the remaining parameters are the same as the ones in the RabbitMQ.Client.IChannel.BasicPublish method.
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="body"></param>
    /// <param name="cancellationToken"></param>
    public Task BasicPublishAsync(
        object owner,
        byte[] body,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the next publish sequence number for the given owner.
    /// </summary>
    /// <param name="owner"></param>
    /// <returns></returns>
    public Task<ulong?> NextPublishSeqNoAsync(object owner);

    public bool IsStalled { get; set;  }
}

/// <inheritdoc/>
public class RabbitChannel : Individual<IChannel>, IRabbitChannel
{
    private readonly string exchangeName;
    private readonly string? routingKey;
    private readonly string? queue;

    public RabbitChannel(
        IChannel channel, 
        string exchangeName, 
        string? queue = null, 
        string? routingKey = null) 
    : base(channel)
    {
        this.exchangeName = exchangeName;
        this.routingKey = routingKey;
        this.queue = queue;

        var verifyTask = VerifyChannelHealth(channel, exchangeName, queue);
        verifyTask.Wait();
        if (verifyTask.IsFaulted)
        {
            throw verifyTask.Exception!;
        }
    }

    private static async Task VerifyChannelHealth(IChannel channel, string exchangeName, string? queue)
    {
        await channel.ExchangeDeclarePassiveAsync(exchangeName);
        if (!string.IsNullOrWhiteSpace(queue))
        {
            await channel.QueueDeclarePassiveAsync(queue);
        }
    }

    /// <inheritdoc/>
    public async Task BasicPublishAsync(
        object owner,
        byte[] body,
        CancellationToken cancellationToken = default) 
        => await GetValue(owner).BasicPublishAsync(exchangeName, queue ?? routingKey ?? "",  body, cancellationToken);

    /// <inheritdoc/>
    public async Task<ulong?> NextPublishSeqNoAsync(object owner) => await GetValue(owner)!.GetNextPublishSequenceNumberAsync();

}
