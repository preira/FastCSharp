using Microsoft.Extensions.Logging;
using Xunit;

namespace FastCSharp.Logging.Tests;

public class LogUtils_UnitTest
{
    [Theory]
    [InlineData(LogLevel.Critical)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Debug)]
    public void Log_ShouldCallCorrectLoggerMethod_WhenLevelIsEnabled(LogLevel level)
    {
        // Arrange
        //TODO: should create an instance of logger and capture output stream to verify the message
        TestLogger logger = new TestLogger();

        // Act
        var logDelegate = logger.Log(level);
        logDelegate("Test message {0}", 42);

        LogEntry? logEntry = logger.Stack.Pop();
        // Assert
        Assert.Equal(level, logEntry.LogLevel);
        Assert.Equal("Test message 42", logEntry.Message);
    }

    [Fact]
    public void Log_ShouldBeNoOp_WhenLevelIsNotEnabled()
    {
        // Arrange
        TestLogger logger = new TestLogger(false);

        // Act
        var logDelegate = logger.Log(LogLevel.Information);
        Exception? exception = Record.Exception(() => logDelegate("Should not log", 123));

        // Assert
        Assert.Null(exception);
        Assert.Empty(logger.Stack); // No log entries should be added
    }

    [Theory]
    [InlineData(LogLevel.Critical)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Debug)]
    public void LogException_ShouldCallCorrectLoggerMethod_WhenLevelIsEnabled(LogLevel level)
    {
        // Arrange
        //TODO: should create an instance of logger and capture output stream to verify the message
        TestLogger logger = new TestLogger();

        // Act
        var logDelegate = logger.LogException(level);
        logDelegate(new Exception(), "Test message {0}", 42);

        LogEntry? logEntry = logger.Stack.Pop();
        // Assert
        Assert.Equal(level, logEntry.LogLevel);
        Assert.Equal("Test message 42", logEntry.Message);
    }

    [Fact]
    public void LogException_ShouldBeNoOp_WhenLevelIsNotEnabled()
    {
        // Arrange
        TestLogger logger = new TestLogger(false);

        // Act
        var logDelegate = logger.LogException(LogLevel.Information);
        Exception? exception = Record.Exception(() => logDelegate(new Exception(), "Should not log", 123));

        // Assert
        Assert.Null(exception);
        Assert.Empty(logger.Stack); // No log entries should be added
    }
}

internal class TestLogger : ILogger
{
    public Stack<LogEntry> Stack { get; }
    public bool _isEnabled;
    public TestLogger(bool isEnabled = true)
    {
        _isEnabled = isEnabled;
        Stack = new Stack<LogEntry>();
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        throw new NotImplementedException();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _isEnabled;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Stack.Push(new LogEntry(logLevel, formatter(state, exception), exception));
    }
}

internal class LogEntry
{
    public LogLevel LogLevel { get; set; }
    public string Message { get; set; }
    public Exception? Exception { get; set; }
    public object[] Args { get; set; }

    public LogEntry(LogLevel logLevel, string message, Exception? exception, params object[] args)
    {
        LogLevel = logLevel;
        Message = message;
        Exception = exception;
        Args = args;
    }
}