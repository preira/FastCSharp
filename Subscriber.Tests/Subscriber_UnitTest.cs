using Xunit;
using FastCSharp.SDK.Subscriber;
using FastCSharp.Subscriber;
using Microsoft.Extensions.Configuration;
using FastCSharp.Observability;

namespace Subscriber.Tests;

class TestSubscriber : AbstractSubscriber<string>
{
    protected override void Dispose(bool disposing)
    {
    }

    private OnMessageCallback<string>? _callback;

    public override IConfigurationSection? Options { get; }

    public override bool IsHealthy => throw new NotImplementedException();

    public async Task Call()
    {
        if(_callback != null)
        {
            await _callback("ok");
        }
    }

    protected override Task _RegisterAsync(OnMessageCallback<string> callback)
    {
        _callback = callback;
        return Task.CompletedTask;
    }  

    public override Task ResetAsync()
    {
        return Task.CompletedTask;
    }

    public override Task UnSubscribeAsync()
    {
        return Task.CompletedTask;
    }

    public override Task<IHealthReport> ReportHealthStatusAsync()
    {
        throw new NotImplementedException();
    }
}

public class Subscriber_UnitTest
{
    [Fact]
    public async void ShouldCallCallback()
    {
        bool success = false;
        using var testImplementation = new TestSubscriber();

        await testImplementation.RegisterAsync(
            async (msg) =>
            {
                var task = new Task<bool>(() => success = true);
                task.Start();
                return await task;
            }
        );
        await testImplementation.Call();
        Assert.True(success, "Should have executed callback.");
    }

    [Fact]
    public async void ShouldAddHandler()
    {
        bool success = false;
        bool isHandlerOk = false;
        var testImplementation = new TestSubscriber();
        testImplementation.AddMsgHandler(s => { isHandlerOk = true; return s; });
        Assert.False(isHandlerOk, "Shouldn't have executed handler yet.");

        await testImplementation.RegisterAsync(
            async (msg) =>
            {
                var task = new Task<bool>(() => success = true);
                task.Start();
                return await task;
            }
        );
        await testImplementation.Call();
        Assert.True(success, "Should have executed callback.");
        Assert.True(isHandlerOk, "Should have executed handler.");
    }

    [Fact]
    public async void ShouldAddHandlers()
    {
        bool success = false;
        bool isHandler1Ok = false;
        bool isHandler2Ok = false;
        var testImplementation = new TestSubscriber();
        testImplementation.AddMsgHandler(s => { isHandler1Ok = true; return s; });
        testImplementation.AddMsgHandler(s => { isHandler2Ok = true; return s; });
        // Reset and unsubscribe to do nothing here. Just to test that the handlers are called for coverage.
        await testImplementation.ResetAsync();
        await testImplementation.UnSubscribeAsync();

        await testImplementation.RegisterAsync(
            async (msg) =>
            {
                var task = new Task<bool>(() => success = true);
                task.Start();
                return await task;
            }
        );
        Assert.False(isHandler1Ok, "Shouldn't have executed handler 1 yet.");
        Assert.False(isHandler2Ok, "Shouldn't have executed handler 2 yet.");
        await testImplementation.Call();
        Assert.True(success, "Should have executed callback.");
        Assert.True(isHandler1Ok, "Should have executed handler 1.");
        Assert.True(isHandler2Ok, "Should have executed handler 2.");
    }
}