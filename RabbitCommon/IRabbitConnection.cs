namespace FastCSharp.RabbitCommon;

public interface IRabbitConnection : IAsyncDisposable
{
    bool IsOpen { get; }
    Task<IRabbitChannel> GetChannelAsync(object owner, string exchangeName, string? queue, string? routingKey);
    Task CloseAsync();
}
