namespace FastCSharp.RabbitCommon;

public interface IRabbitConnection : IAsyncDisposable
{
    bool IsOpen { get; }
    // bool DisposeChannel_Depracated(RabbitChannel channel);
    Task<IRabbitChannel> GetChannelAsync(object owner, string exchangeName, string? queue, string? routingKey);
    // bool ResetConnection(bool dispose = true);
    Task CloseAsync();
}
