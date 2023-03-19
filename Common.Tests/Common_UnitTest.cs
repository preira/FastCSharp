using Xunit;
using FastCSharp;

namespace Common.Tests;

public class RndTest
{
    [Fact]
    public void GetRandomDouble()
    {
        int limitCount = 0;
        int count = 0;
        for (int precision = 10; precision < int.MaxValue / 10; precision *= 10)
        {
            double random = Rnd.GetRandomDouble(precision);
            Assert.InRange<double>(random, 0, 1);
            ++count;
            if(random == 0 || random == 1)
            {
                ++limitCount;
            }
        }
        Assert.NotEqual<int>(count, limitCount);
    }
}