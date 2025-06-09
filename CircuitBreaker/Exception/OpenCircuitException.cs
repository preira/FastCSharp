namespace FastCSharp.CircuitBreaker;

[Serializable]
public class OpenCircuitException: Exception
{
    public OpenCircuitException() : base()
    {
        // intentionally empty
    }
    public OpenCircuitException(string? message) : base(message)
    {
        // intentionally empty
    }
    public OpenCircuitException(string? message, Exception? inner) : base(message, inner)
    {
        // intentionally empty
    }
}
