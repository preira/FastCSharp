using Xunit;
using FastCSharp.Publisher;

namespace Publisher.Tests;

class RemoteControl<T>
{
    public bool HasBeenDisposed { get; set; }
    public bool IsNopCalled { get; set; }
    public bool HasCalledPublish { get; set; }
    public T? PublishResult { get; set; }
    public bool PublishFunction(T? value)
    {
        HasCalledPublish = true;
        // PublishResult = Encoding.UTF8.GetString(arr);
        PublishResult = value;
        return true;
    }
}
class TestPublisher : IDisposable
{
    List<Handler<string>>? Handlers { get; set; }
    readonly RemoteControl<string> rc;
    public TestPublisher(RemoteControl<string> remote) : base()
    {
        rc = remote;
    }

    protected void Dispose(bool disposing)
    {
        rc.HasBeenDisposed = true;
    }

    public async Task<bool> Publish(string? message)
    {
        if(Handlers != null)
        {
            foreach (var handler in Handlers)
            {
                message = await handler(message);
            }
        }
        return rc.PublishFunction(message);
    }

    public void AddMsgHandler(Handler<string> handler)
    {
        if(Handlers == null)
        {
            Handlers = new List<Handler<string>>();
        }
        Handlers.Add(handler);
    }

    public void NOp()
    {
        rc.IsNopCalled = true;
    }

    public void Dispose()
    {
        Dispose(true);
    }
}

public class Publisher_UnitTest
{
    [Fact]
    public async void AddedMessageHandlerShouldBeInvoked()
    {
        var rc = new RemoteControl<string>
        {
            // IsHealthy = true
        };
        var countCalls = 0;
        using(var publisher = new TestPublisher(rc))
        {
            publisher.AddMsgHandler(m => Task.FromResult<string?>(m + (++countCalls)));
            await publisher.Publish("The Earth is orbiting the Sun.");
        }
        Assert.Equal(1, countCalls);
    }

    [Fact]
    public void TestPublisher_increaseCoverage()
    {
        var rc = new RemoteControl<string>
        {
            // IsHealthy = true
        };
        using(var publisher = new TestPublisher(rc))
        {
            publisher.NOp();
        }
    }

    [Fact]
    public async void AddedMessageHandlersShouldBeInvoked()
    {
        var rc = new RemoteControl<string>
        {
            // IsHealthy = true
        };
        var countCalls = 0;
        var handlersCount = 0;
        using(var publisher = new TestPublisher(rc))
        {
            for (; handlersCount < 10;++handlersCount)
            {
                publisher.AddMsgHandler(m => Task.FromResult<string?>(m + (++countCalls)));
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
            // IsHealthy = true
        };
        using(var publisher = new TestPublisher(rc))
        {
            publisher.AddMsgHandler(m => Task.FromResult(m?.Replace('e', '_')));
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
            // IsHealthy = true
        };
        using(var publisher = new TestPublisher(rc))
        {
            publisher.AddMsgHandler(m => Task.FromResult(m?.Replace('e', '_')));
            result = result.Replace('e', '_');
            publisher.AddMsgHandler(m => Task.FromResult(m?.Replace('a', '*')));
            result = result.Replace('a', '*');
            publisher.AddMsgHandler(m => Task.FromResult(m?.Replace('a', '?')));
            result = result.Replace('a', '?');
            publisher.AddMsgHandler(m => Task.FromResult(m?.Replace('_', '-')));
            result = result.Replace('_', '-');
            await publisher.Publish(token);
        }
        Assert.Equal(result, rc.PublishResult);
    }

}