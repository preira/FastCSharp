using Xunit;
using FastCSharp.SDK.Subscriber;

namespace Subscriber.Tests;

class TestSubscriber : AbstractSubscriber<string>
{
    protected override void _Register(OnMessageCallback<string> callback) => callback("ok");
}

public class UnitTest1
{
    [Fact]
    public void ShouldCallCallback()
    {
        bool success = false;
        var testImplementation = new TestSubscriber();

        testImplementation.Register(s => success = true);
        Assert.True(success, "Should have executed callback.");
    }

    [Fact]
    public void ShouldAddHandler()
    {
        bool success = false;
        bool isHandlerOk = false;
        var testImplementation = new TestSubscriber();
        testImplementation.AddMsgHandler(s => { isHandlerOk = true; return s; });
        Assert.False(isHandlerOk, "Shouldn't have executed handler yet.");

        testImplementation.Register(s => success = true);
        Assert.True(success, "Should have executed callback.");
        Assert.True(isHandlerOk, "Should have executed handler.");
    }

    [Fact]
    public void ShouldAddHandlers()
    {
        bool success = false;
        bool isHandler1Ok = false;
        bool isHandler2Ok = false;
        var testImplementation = new TestSubscriber();
        testImplementation.AddMsgHandler(s => { isHandler1Ok = true; return s; });
        testImplementation.AddMsgHandler(s => { isHandler2Ok = true; return s; });
        Assert.False(isHandler1Ok, "Shouldn't have executed handler 1 yet.");
        Assert.False(isHandler2Ok, "Shouldn't have executed handler 2 yet.");

        testImplementation.Register(s => success = true);
        Assert.True(success, "Should have executed callback.");
        Assert.True(isHandler1Ok, "Should have executed handler 1.");
        Assert.True(isHandler2Ok, "Should have executed handler 2.");
    }
}