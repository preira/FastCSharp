namespace FastCSharp.CircuitBreaker;

[Serializable]
public class CircuitException : Exception
{
    public CircuitException() : base()
    {
        // intentionally empty
    }
    public CircuitException(string? message) : base(message)
    {
        // intentionally empty
    }
    public CircuitException(string? message, Exception? inner) : base(message, inner)
    {
        // intentionally empty
    }
}
