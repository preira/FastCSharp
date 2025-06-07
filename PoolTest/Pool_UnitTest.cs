using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FastCSharp.Pool.Tests;

public class Pool_UnitTest
{
    ILoggerFactory LoggerFactory => new Mock<ILoggerFactory>().Object;

    [Fact]
    public async Task CreateNewPool()
    {
        int count = 0;
        var pool = new AsyncPool<Item, Int>(
            async () => {
                await Task.Yield();
                return new Item(
                    new Int
                    {
                        Value = count++
                    }
                );
            },
            LoggerFactory,
            1, 10
            );

        var item = await pool.BorrowAsync(this);
        Assert.Equal(0, item.Value(this));
    }

    [Fact]
    public async Task CallPoolFromOneOwnerAndUseByDifferentOwnerFails()
    {
        int count = 0;
        var pool = new AsyncPool<Item, Int>(
            async () => {
                await Task.Yield();
                return new Item(
                    new Int
                    {
                        Value = count++
                    }
                );
            },
            LoggerFactory,
            1, 10
            );

        var item = await new Owner().BorrowAsync(pool);
        Assert.Throws<InvalidOperationException>(() => new Owner().Use(item));
    }

    [Fact]
    public async Task CallPoolAndDeclareStalledItem()
    {

        int count = 0;
        var pool = new AsyncPool<Item, Int>(
            async () => {
                await Task.Yield();
                return new Item(
                    new Int
                    {
                        Value = count++
                    }
                );
            },
            LoggerFactory,
            1, 10
            );

        var item = await new Owner().BorrowAsync(pool);
        item.SetStalled();

        await item.DisposeAsync();

        var owner = new Owner();
        item = await owner.BorrowAsync(pool);
        Assert.Equal(count-1, item.Value(owner));
    }

    [Fact]
    public async Task DisposeItemAndTryToUse()
    {
        int count = 0;
        var pool = new AsyncPool<Item, Int>(
            async () => {
                await Task.Yield();
                return new Item(
                    new Int
                    {
                        Value = count++
                    }
                );
            },
            LoggerFactory,
            1, 10
            );

        var owner = new Owner();
        var item = await owner.BorrowAsync(pool);
        item.DisposeValue();
        Assert.Throws<ObjectDisposedException>(() => owner.Use(item));
    }

    [Fact]
    public async Task ReturnsItemAndTryToUseAndRetrieveSameFromPool()
    {
        int count = 0;
        var pool = new AsyncPool<Item, Int>(
            async () => {
                await Task.Yield();
                return new Item(
                    new Int
                    {
                        Value = count++
                    }
                );
            },
            LoggerFactory,
            7, 10
            );

        var owner = new Owner();
        // not using using here, so we can explicitly dispose it
        var item = await owner.BorrowAsync(pool);
        int value = item.Value(owner); // it is locked to this owner
        await item.DisposeAsync(); // returns to pool

        await using var item2 = await owner.BorrowAsync(pool);

        Assert.Equal(value, item2.Value(owner));
    }

    [Fact]
    public async Task DisposeItemAndTryToUseAndRetrieveSameFromPool()
    {
        int count = 0;
        var pool = new AsyncPool<Item, Int>(
            async () => {
                await Task.Yield();
                return new Item(
                    new Int
                    {
                        Value = count++
                    }
                );
            },
            LoggerFactory,
            7, 10
            );

        var owner = new Owner();
        // not using using here, so we can explicitly dispose it
        var item = await owner.BorrowAsync(pool);
        int value = item.Value(owner); // it is locked to this owner
        item.DisposeValue(); // Disposes the value without returning to pool
        await using var item2 = await owner.BorrowAsync(pool);
        Assert.NotEqual(value, item2.Value(owner));
    }

    [Fact]
    public async Task CallPoolMultipleTimes()
    {
        int count = 0;
        var pool = new AsyncPool<Item, Int>(
            async () => {
                await Task.Yield();
                return new Item(
                    new Int
                    {
                        Value = count++
                    }
                );
            }, 
            LoggerFactory,
            7, 10
            );

        for(int i = 0; i < 100; i++)
        {
            await using var item = await pool.BorrowAsync(this, 10000);
            Assert.InRange(item.Value(this), 0, 10);
        }
        Assert.InRange(pool.Count, 1, 10);
    }
    
    [Fact]
    public void CallPoolMultipleTimesMultipleThreadsAndOwners()
    {
        int count = 0;
        ConcurrentStack<Exception> exceptions = new ();
        var pool = new AsyncPool<Item, Int>(
            async () => {
                await Task.Yield();
                return new Item(
                    new Int
                    {
                        Value = Interlocked.Increment(ref count)
                    }
                );
            }, 
            LoggerFactory,
            7, 10
            );
        Thread[] threads = new Thread[10];
        for(int i = 0; i < threads.Length; i++)
        {
            threads[i] = new Thread(async () => {
                try
                {
                    var owner = new Owner();
                    for(int j = 0; j < 1000; j++)
                    {
                        await owner.BorrowAndUseAsync(pool);
                        Thread.SpinWait(1000*Random.Shared.Next(1, 10));
                    }
                }
                catch(Exception ex)
                {
                    exceptions.Push(ex);
                }
            });
        }
        foreach(var thread in threads)
        {
            thread.Start();
        }
        foreach(var thread in threads)
        {
            thread.Join();
        }
        Assert.InRange(pool.Count, 1, 10);
        Assert.Empty(exceptions);
        if (exceptions.Count > 0)
        {
            throw new AggregateException(exceptions);
        }
    }
    
    [Fact]
    public void CallPoolWithMoreThreadsThanIndividuals()
    {
        int count = 0;
        int minPoolSize = 7;
        int MaxPoolSize = 10;
        int threadCount = 12;
        ConcurrentStack<Exception> exceptions = new ();
        var pool = new AsyncPool<Item, Int>(
            async () => {
                await Task.Yield();
                return new Item(
                    new Int
                    {
                        Value = Interlocked.Increment(ref count)
                    }
                );
            }, 
            LoggerFactory,
            minPoolSize, MaxPoolSize
            );
        DateTime start = DateTime.Now;
        Thread[] threads = new Thread[threadCount];
        for(int i = 0; i < threads.Length; i++)
        {
            threads[i] = new Thread(async () => {
                try
                {
                    var owner = new Owner();
                    for(int j = 0; j < 100; j++)
                    {
                        await owner.BorrowAndUseWithRandomSpinWaitAsync(pool);
                        Thread.SpinWait(100*Random.Shared.Next(1, 10));
                    }
                }
                catch(Exception ex)
                {
                    exceptions.Push(ex);
                }
            });
        }
        foreach(var thread in threads)
        {
            thread.Start();
        }
        foreach(var thread in threads)
        {
            thread.Join();
        }

        Assert.NotNull(pool.Stats);
        IPoolStats stats = pool.Stats;

        // Assert.Fail(JsonSerializer.Serialize(stats.ToJson()));
        Assert.InRange(stats.HitRatio, 0, 1);
        Assert.InRange(stats.ReturnRatio, 0, 1);
        Assert.Equal(0, stats.ErrorRatio);
        Assert.InRange(stats.PurgeRatio, 0, 1);
        Assert.InRange(stats.WaitRatio, 0, 1);
        Assert.Equal(0, stats.TimeoutRatio);
        Assert.InRange(stats.DisposedRatio, 0, 1);
        Assert.Equal(0, stats.MinSize);
        Assert.InRange(stats.MaxSize, 1, MaxPoolSize);
        Assert.True(stats.PeriodStart < start, $"PeriodStart: {stats.PeriodStart} (should be before {start})");

        Assert.InRange(pool.Count, 1, 10);



        Assert.Empty(exceptions);
        if (exceptions.Count > 0)
        {
            throw new AggregateException(exceptions);
        }
    }
    
    [Fact]
    public async Task MakeUseOfPoolSize()
    {
        int count = 0;
        var pool = new AsyncPool<Item, Int>(
            async () => {
                await Task.Yield();
                return new Item(
                    new Int
                    {
                        Value = count++
                    }
                );
            }, 
            LoggerFactory,
            7, 10
            );

        var items = new Queue<Item>();
        // Count = 5
        for(int i = 0; i < 5; i++)
        {
            var item = await pool.BorrowAsync(this, 10);
            items.Enqueue(item);
            Assert.InRange(item.Value(this), 0, 5);
        }
        Assert.Equal(5, pool.Count);

        // Count = Count + 5
        for(int i = 0; i < 5; i++)
        {
            var item = await pool.BorrowAsync(this, 10);
            items.Enqueue(item);
            Assert.InRange(item.Value(this), 5, 10);
        }
        Assert.Equal(10, pool.Count);

        // Count = Count(10) [available = 5; inUse = 5]
        for(int i = 0; i < 5; i++)
        {
            await items.Dequeue().DisposeAsync();
        }
        Assert.Equal(10, pool.Count);

        // Count = Count - 3(7) [available = 6 (80%); inUse = 1]
        for(int i = 0; i < 4; i++)
        {
            await items.Dequeue().DisposeAsync();
        }
        Assert.Equal(7, pool.Count);

        // Count = Count + 1(8) [available = 0; inUse = 8]
        for(int i = 0; i < 7; i++)
        {
            var item = await pool.BorrowAsync(this, 10);
            items.Enqueue(item);
        }
        Assert.Equal(8, pool.Count);
        
        // Count = Count + 2(10) [available = 0; inUse = 10]
        for(int i = 0; i < 2; i++)
        {
            var item = await pool.BorrowAsync(this, 1);
            items.Enqueue(item);
        }
        Assert.Equal(10, pool.Count);
        Assert.Equal(10, items.Count);

        await Assert.ThrowsAsync<TimeoutException>(async () => await pool.BorrowAsync(this, 1));

        // Count = 7 [available = 7; inUse = 0]
        foreach(var item in items)
        {
            await item.DisposeAsync();
        }
        Assert.Equal(7, pool.Count);
    }
    
    [Fact]
    public async Task CallPoolMultipleTimesAndOwners()
    {
        int count = 0;
        var pool = new AsyncPool<Item, Int>(
            async () => {
                await Task.Yield();
                return new Item(
                    new Int
                    {
                        Value = count++
                    }
                );
            },
            LoggerFactory,
            1, 10
            );

        for(int i = 0; i < 100; i++)
        {
            await new Owner().BorrowAndUseAsync(pool);
        }
        Assert.InRange(pool.Count, 1, 10);
    }

}

class Item : Individual<Int>
{
    public Item(Int value) : base(value)
    {
    }

    public int Value(object owner)
    {
        return GetValue(owner).Value;
    }

    public void SetStalled()
    {
        IsStalled = true;
    }

    public void DisposeValue()
    {
        DisposeValue(true);
    }
}
class Int : IDisposable
{
    public int Value { get; set; }

    public void Dispose()
    {
        // throw new NotImplementedException();
    }
}

class Owner
{
    public async Task BorrowAndUseAsync(IAsyncPool<Item> pool)
    {
        await using var item = await pool.BorrowAsync(this);
        item.Value(this);
    }
    public async Task BorrowAndUseWithRandomSpinWaitAsync(IAsyncPool<Item> pool)
    {
        await using var item = await pool.BorrowAsync(this);
        Thread.SpinWait(10*Random.Shared.Next(1, 10));
        item.Value(this);
    }
    public async Task<Item> BorrowAsync(IAsyncPool<Item> pool)
    {
        var item = await pool.BorrowAsync(this);
        return item;
    }
    public void Use(Item item)
    {
        item.Value(this);
    }   
}