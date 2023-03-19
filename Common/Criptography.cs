using System.Security.Cryptography;

namespace FastCSharp;

static public class Rnd
{
    public static double GetRandomDouble(int precision) 
        => ((double) RandomNumberGenerator.GetInt32(precision)) / precision;

}
