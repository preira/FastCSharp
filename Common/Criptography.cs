using System.Security.Cryptography;

namespace FastCSharp.Criptography;

/// <summary>
/// Static class for cryptographic operations
/// </summary>
static public class Rnd
{
    private static readonly int maxPrecision = (int)Math.Log10(Int32.MaxValue);

    /// <summary>
    /// Random double generator. It generates a random double between 0 (inclusive) and 1 (inclusive) with the given precision.
    /// e.g. GetRandomDouble(3) will generate a random double between 0 and 1 with 3 decimal places.
    /// </summary>
    public static double GetRandomDouble(int precision)
    {
        if (precision < 0 || precision > maxPrecision)
        {
            throw new ArgumentOutOfRangeException(nameof(precision));
        }
        int upperBound = (int)Math.Pow(10, precision) + 1;
        return ((double)RandomNumberGenerator.GetInt32(upperBound)) / upperBound;
    }

}
