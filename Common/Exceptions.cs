using System.Runtime.Serialization;

namespace FastCSharp;

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
    protected CircuitException(SerializationInfo info, StreamingContext context) 
    : base(info, context)
    {
        // intentionally empty
    }
    public CircuitException(string? message, Exception? inner) : base(message, inner)
    {
        // intentionally empty
    }
}


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
    protected OpenCircuitException(SerializationInfo info, StreamingContext context) 
    : base(info, context)
    {
        // intentionally empty
    }
    public OpenCircuitException(string? message, Exception? inner) : base(message, inner)
    {
        // intentionally empty
    }
}

[Serializable]
public class IncorrectInitializationException : Exception
{
    public IncorrectInitializationException() : base()
    {
        // intentionally empty
    }
    public IncorrectInitializationException(string? message) : base(message)
    {
        // intentionally empty
    }
    public IncorrectInitializationException(string? message, Exception? inner) : base(message, inner)
    {
        // intentionally empty
    }
    protected IncorrectInitializationException(SerializationInfo info, StreamingContext context) 
    : base(info, context)
    {
        // intentionally empty
    }
}

