using Xunit;
using FastCSharp.Criptography;
using FastCSharp.Exception;

namespace Common.Tests;

public class Rnd_UnitTest
{
    [Fact]
    public void Test_GetRandomDouble()
    {
        int limitCount = 0;
        int count = 0;
        var limit = Math.Log10(Int32.MaxValue);
        for (int precision = 0; precision < limit; ++precision)
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

    [Fact]
    public void Test_GetRandomDouble_Invalid()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Rnd.GetRandomDouble(-1));
    }

    [Fact]
    public void Test_GetRandomDouble_Invalid2()
    {
        var oneBeyondMax = ((int)Math.Log10(Int32.MaxValue)) + 1;
        Assert.Throws<ArgumentOutOfRangeException>(() => Rnd.GetRandomDouble(oneBeyondMax));
    }

    [Fact]
    public void Test_ZeroShouldGiveAboutFiftyFiftyZerosAndOnes()
    {
        int zeroCount = 0;
        int oneCount = 0;
        for (int i = 0; i < 1000000; ++i)
        {
            if (Rnd.GetRandomDouble(0) == 0)
            {
                ++zeroCount;
            }
            else
            {
                ++oneCount;
            }
        }
        Assert.InRange<int>(zeroCount, 400000, 600000);
        Assert.InRange<int>(oneCount, 400000, 600000);
    }
}

public class Exception_Test
{
    [Fact]
    public void Test_IncorrectInitializationException()
    {
        var ex = new IncorrectInitializationException();
        Assert.NotNull(ex);
    }

    [Fact]
    public void Test_IncorrectInitializationException2()
    {
        var ex = new IncorrectInitializationException("message");
        Assert.NotNull(ex);
    }

    [Fact]
    public void Test_IncorrectInitializationException_withInnerException()
    {
        var innerException = new Exception("Inner");
        var exception = new IncorrectInitializationException("Test", innerException);
        Assert.Equal("Test", exception.Message);
        Assert.Equal("Inner", exception.InnerException.Message);
    }
}