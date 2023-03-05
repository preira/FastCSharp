using Xunit;
using FastCSharp.SDK.Publisher;
using System.Text.Json;

namespace Publisher.Tests;

class RemoteControl<T>
{
    public Boolean HasBeenDisposed { get; set; }
    public Boolean IsHealthy { get; set; }
    public Boolean HasCalledHealthy { get; set; }
    public Boolean IsResetConnection { get; set; }
    public Boolean HasResetConnection { get; set; }
    public Boolean HasCalledAsyncPublish { get; set; }
    public T? PublishResult { get; set; }
    public bool PublishFunction(byte[] arr)
    {
        // PublishResult = Encoding.UTF8.GetString(arr);
        PublishResult = JsonSerializer.Deserialize<T>(arr);
        return true;
    }

    public void Reset()
    {
        HasBeenDisposed = false;
        IsHealthy = false;
        HasCalledAsyncPublish = false;
        IsResetConnection = false;
        HasResetConnection = false;
        HasCalledAsyncPublish = false;
    }
}
class TestPublisher : AbstractPublisher<string>
{
    RemoteControl<string> rc;
    public TestPublisher(RemoteControl<string> remote) : base()
    {
        rc = remote;
    }

    public override void Dispose()
    {
        rc.HasBeenDisposed = true;
    }

    protected override bool AsyncPublish(byte[] body)
    {
        rc.HasCalledAsyncPublish = true;
        return rc.PublishFunction(body);
    }

    protected override bool IsHealthy()
    {
        rc.HasCalledHealthy = true;
        return rc.IsHealthy;
    }

    protected override bool ResetConnection(bool dispose = true)
    {
        rc.HasResetConnection = true;
        return rc.IsResetConnection;
    }
}

public class UnitTest1
{
    [Fact]
    public void PublisherShouldInvokeDispose()
    {
        var rc = new RemoteControl<string>();
        using(var publisher = new TestPublisher(rc))
        {
            Assert.False(rc.HasBeenDisposed, "Shouldn't have been disposed yet.");
        }
        Assert.True(rc.HasBeenDisposed, "Should have been disposed.");
    }

    [Fact]
    public async void ShouldCall_IsHealthy()
    {
        var rc = new RemoteControl<string>
        {
            IsHealthy = true
        };
        using(var publisher = new TestPublisher(rc))
        {
            await publisher.Publish("The Earth is round.");
        }
        Assert.True(rc.HasCalledHealthy, "Should have called IsHealthy.");
    }

    [Fact]
    public async void IfIsHealthyThenShouldntCallResetConnection()
    {
        var rc = new RemoteControl<string>
        {
            IsHealthy = true
        };
        using(var publisher = new TestPublisher(rc))
        {
            await publisher.Publish("The Earth is round.");
        }
        Assert.True(rc.HasCalledHealthy, "Should have called IsHealthy.");
        Assert.False(rc.HasResetConnection, "Shouldn't have called ResetConnection.");
    }

    [Fact]
    public async void ShouldCall_ResetConnection()
    {
        var rc = new RemoteControl<string>();
        using(var publisher = new TestPublisher(rc))
        {
            await publisher.Publish("The Moon is round.");
        }
        Assert.True(rc.HasResetConnection, "Should have called ResetConnection.");
    }

    [Fact]
    public async void ShouldCall_AsyncPublish()
    {
        var rc = new RemoteControl<string>
        {
            IsHealthy = true
        };
        using(var publisher = new TestPublisher(rc))
        {
            await publisher.Publish("The Earth is orbiting the Sun.");
        }
        Assert.True(rc.HasCalledAsyncPublish, "Should have called AsyncPublish.");
    }

    [Fact]
    public async void CallToAsyncPublishShouldSendTheSameMsg()
    {
        var msg = "Mercury is the planet closest to the sun.";
        var rc = new RemoteControl<string>
        {
            IsHealthy = true
        };
        using(var publisher = new TestPublisher(rc))
        {
            await publisher.Publish(msg);
        }
        Assert.Equal(msg, rc.PublishResult);
    }

    [Fact]
    public async void AddedMessageHandlerShouldBeInvoked()
    {
        var rc = new RemoteControl<string>
        {
            IsHealthy = true
        };
        var countCalls = 0;
        using(var publisher = new TestPublisher(rc))
        {
            publisher.AddMsgHandler(m => m + (++countCalls));
            await publisher.Publish("The Earth is orbiting the Sun.");
        }
        Assert.Equal(1, countCalls);
    }

    [Fact]
    public async void AddedMessageHandlersShouldBeInvoked()
    {
        var rc = new RemoteControl<string>
        {
            IsHealthy = true
        };
        var countCalls = 0;
        var handlersCount = 0;
        using(var publisher = new TestPublisher(rc))
        {
            for (; handlersCount < 10;++handlersCount)
            {
                publisher.AddMsgHandler(m => m + (++countCalls));
            }
            await publisher.Publish("The Earth is orbiting the Sun.");
        }
        Assert.Equal(handlersCount, countCalls);
    }

    [Fact]
    public async void AddedMessageHandlerShouldAffectTheMessage()
    {
        var token = "There are other planets orbiting the Sun.";
        var result = token;
        var rc = new RemoteControl<string>
        {
            IsHealthy = true
        };
        using(var publisher = new TestPublisher(rc))
        {
            publisher.AddMsgHandler(m => m?.Replace('e', '_'));
            result = result.Replace('e', '_');
            await publisher.Publish(token);
        }
        Assert.Equal(result, rc.PublishResult);
    }

    [Fact]
    public async void AddedMessageHandlersShouldAffectTheMessageByAddedOrder()
    {
        var token = "The Sun is in a Galaxy.";
        var result = token;
        var rc = new RemoteControl<string>
        {
            IsHealthy = true
        };
        using(var publisher = new TestPublisher(rc))
        {
            publisher.AddMsgHandler(m => m?.Replace('e', '_'));
            result = result.Replace('e', '_');
            publisher.AddMsgHandler(m => m?.Replace('a', '*'));
            result = result.Replace('a', '*');
            publisher.AddMsgHandler(m => m?.Replace('a', '?'));
            result = result.Replace('a', '?');
            publisher.AddMsgHandler(m => m?.Replace('_', '-'));
            result = result.Replace('_', '-');
            await publisher.Publish(token);
        }
        Assert.Equal(result, rc.PublishResult);
    }

}