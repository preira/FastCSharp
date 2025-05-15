using System.Collections.Concurrent;
using Xunit;

namespace FastCSharp.Pool.Tests;

public class Pool_UnitTest
{

    [Fact]
    public void CreateNewPool()
    {
        int count = 0;
        var pool = new Pool<Item, Int>(
            () => {
                return new Item(
                    new Int
                    {
                        Value = count++
                    }
                );
            },
            1, 10
            );

        var item = pool.Borrow(this);
        Assert.Equal(0, item.Value(this));
    }

    [Fact]
    public void CallPoolFromOneOwnerAndUseByDifferentOwnerFails()
    {
        int count = 0;
        var pool = new Pool<Item, Int>(
            () => {
                return new Item(
                    new Int
                    {
                        Value = count++
                    }
                );
            },
            1, 10
            );

        var item = new Owner().Borrow(pool);
        Assert.Throws<InvalidOperationException>(() => new Owner().Use(item));
    }

    [Fact]
    public void CallPoolAndDeclareStalledItem()
    {

        int count = 0;
        var pool = new Pool<Item, Int>(
            () => {
                return new Item(
                    new Int
                    {
                        Value = count++
                    }
                );
            },
            1, 10
            );

        var item = new Owner().Borrow(pool);
        item.SetStalled();
        item.Dispose();
        var owner = new Owner();
        item = owner.Borrow(pool);
        Assert.Equal(count-1, item.Value(owner));
    }

    [Fact]
    public void DisposeItemAndTryToUse()
    {
        int count = 0;
        var pool = new Pool<Item, Int>(
            () => {
                return new Item(
                    new Int
                    {
                        Value = count++
                    }
                );
            },
            1, 10
            );

        var owner = new Owner();
        var item = owner.Borrow(pool);
        item.DisposeValue();
        Assert.Throws<ObjectDisposedException>(() => owner.Use(item));
    }

    [Fact]
    public void ReturnsItemAndTryToUseAndRetrieveSameFromPool()
    {
        int count = 0;
        var pool = new Pool<Item, Int>(
            () => {
                return new Item(
                    new Int
                    {
                        Value = count++
                    }
                );
            },
            7, 10
            );

        var owner = new Owner();
        // not using using here, so we can explicitly dispose it
        var item = owner.Borrow(pool);
        int value = item.Value(owner); // it is locked to this owner
        item.Dispose(); // returns to pool
        using var item2 = owner.Borrow(pool);
        Assert.Equal(value, item2.Value(owner));
    }

    [Fact]
    public void DisposeItemAndTryToUseAndRetrieveSameFromPool()
    {
        int count = 0;
        var pool = new Pool<Item, Int>(
            () => {
                return new Item(
                    new Int
                    {
                        Value = count++
                    }
                );
            },
            7, 10
            );

        var owner = new Owner();
        // not using using here, so we can explicitly dispose it
        var item = owner.Borrow(pool);
        int value = item.Value(owner); // it is locked to this owner
        item.DisposeValue(); // Disposes the value without returning to pool
        using var item2 = owner.Borrow(pool);
        Assert.NotEqual(value, item2.Value(owner));
    }

    [Fact]
    public void CallPoolMultipleTimes()
    {
        int count = 0;
        var pool = new Pool<Item, Int>(
            () => {
                return new Item(
                    new Int
                    {
                        Value = count++
                    }
                );
            }, 
            7, 10
            );

        for(int i = 0; i < 100; i++)
        {
            using var item = pool.Borrow(this, 10000);
            Assert.InRange(item.Value(this), 0, 10);
        }
        Assert.InRange(pool.Count, 1, 10);
    }
    
    [Fact]
    public void CallPoolMultipleTimesMultipleThreadsAndOwners()
    {
        int count = 0;
        ConcurrentStack<Exception> exceptions = new ();
        var pool = new Pool<Item, Int>(
            () => {
                return new Item(
                    new Int
                    {
                        Value = Interlocked.Increment(ref count)
                    }
                );
            }, 
            7, 10
            );
        Thread[] threads = new Thread[10];
        for(int i = 0; i < threads.Length; i++)
        {
            threads[i] = new Thread(() => {
                try
                {
                    var owner = new Owner();
                    for(int j = 0; j < 1000; j++)
                    {
                        owner.BorrowAndUse(pool);
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
        var pool = new Pool<Item, Int>(
            () => {
                return new Item(
                    new Int
                    {
                        Value = Interlocked.Increment(ref count)
                    }
                );
            }, 
            minPoolSize, MaxPoolSize
            );
        DateTime start = DateTime.Now;
        Thread[] threads = new Thread[threadCount];
        for(int i = 0; i < threads.Length; i++)
        {
            threads[i] = new Thread(() => {
                try
                {
                    var owner = new Owner();
                    for(int j = 0; j < 100; j++)
                    {
                        owner.BorrowAndUseWithRandomSpinWait(pool);
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
    public void MakeUseOfPoolSize()
    {
        int count = 0;
        var pool = new Pool<Item, Int>(
            () => {
                return new Item(
                    new Int
                    {
                        Value = count++
                    }
                );
            }, 
            7, 10
            );

        var items = new Queue<Item>();
        // Count = 5
        for(int i = 0; i < 5; i++)
        {
            var item = pool.Borrow(this, 10);
            items.Enqueue(item);
            Assert.InRange(item.Value(this), 0, 5);
        }
        Assert.Equal(5, pool.Count);

        // Count = Count + 5
        for(int i = 0; i < 5; i++)
        {
            var item = pool.Borrow(this, 10);
            items.Enqueue(item);
            Assert.InRange(item.Value(this), 5, 10);
        }
        Assert.Equal(10, pool.Count);

        // Count = Count(10) [available = 5; inUse = 5]
        for(int i = 0; i < 5; i++)
        {
            items.Dequeue().Dispose();
        }
        Assert.Equal(10, pool.Count);

        // Count = Count - 3(7) [available = 6 (80%); inUse = 1]
        for(int i = 0; i < 4; i++)
        {
            items.Dequeue().Dispose();
        }
        Assert.Equal(7, pool.Count);

        // Count = Count + 1(8) [available = 0; inUse = 8]
        for(int i = 0; i < 7; i++)
        {
            var item = pool.Borrow(this, 10);
            items.Enqueue(item);
        }
        Assert.Equal(8, pool.Count);
        
        // Count = Count + 2(10) [available = 0; inUse = 10]
        for(int i = 0; i < 2; i++)
        {
            var item = pool.Borrow(this, 1);
            items.Enqueue(item);
        }
        Assert.Equal(10, pool.Count);
        Assert.Equal(10, items.Count);

        Assert.Throws<TimeoutException>(() => pool.Borrow(this, 1));

        // Count = 7 [available = 7; inUse = 0]
        foreach(var item in items)
        {
            item.Dispose();
        }
        Assert.Equal(7, pool.Count);
    }
    
    [Fact]
    public void CallPoolMultipleTimesAndOwners()
    {
        int count = 0;
        var pool = new Pool<Item, Int>(
            () => {
                return new Item(
                    new Int
                    {
                        Value = count++
                    }
                );
            },
            1, 10
            );

        for(int i = 0; i < 100; i++)
        {
            new Owner().BorrowAndUse(pool);
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
    public void BorrowAndUse(IPool<Item> pool)
    {
        using var item = pool.Borrow(this);
        item.Value(this);
    }
    public void BorrowAndUseWithRandomSpinWait(IPool<Item> pool)
    {
        using var item = pool.Borrow(this);
        Thread.SpinWait(10*Random.Shared.Next(1, 10));
        item.Value(this);
    }
    public Item Borrow(IPool<Item> pool)
    {
        var item = pool.Borrow(this);
        return item;
    }
    public void Use(Item item)
    {
        item.Value(this);
    }   
}