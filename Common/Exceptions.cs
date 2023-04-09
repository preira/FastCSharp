using System.Runtime.Serialization;

namespace FastCSharp.Exception;

public class IncorrectInitializationException : System.Exception
{
    public IncorrectInitializationException() : base()
    {
        // intentionally empty
    }
    public IncorrectInitializationException(string? message) : base(message)
    {
        // intentionally empty
    }
    public IncorrectInitializationException(string? message, System.Exception? inner) : base(message, inner)
    {
        // intentionally empty
    }
}

