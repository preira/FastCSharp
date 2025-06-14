using Microsoft.Extensions.Logging;

namespace FastCSharp.Logging;

public static class LogUtils
{

    public delegate void LogExceptionDelegate(Exception? exception, string? message, params object?[] args);
    public static LogExceptionDelegate LogException(this ILogger logger, LogLevel level)
    {
        if (logger.IsEnabled(level))
        {
            switch (level)
            {
                case LogLevel.Critical:
                    return logger.LogCritical;
                case LogLevel.Error:
                    return logger.LogError;
                case LogLevel.Information:
                    return logger.LogInformation;
                case LogLevel.Trace:
                    return logger.LogTrace;
                case LogLevel.Warning:
                    return logger.LogWarning;
                default:
                    return logger.LogDebug;
            }
        }
        return (Exception? exception, string? message, params object?[] args) => {  }; // No-op if logging is not enabled for the level.
    }

    public delegate void LogDelegate(string? message, params object?[] args);
    public static LogDelegate Log(this ILogger logger, LogLevel level)
    {
        if (logger.IsEnabled(level))
        {
            switch (level)
            {
                case LogLevel.Critical:
                    return logger.LogCritical;
                case LogLevel.Error:
                    return logger.LogError;
                case LogLevel.Information:
                    return logger.LogInformation;
                case LogLevel.Trace:
                    return logger.LogTrace;
                case LogLevel.Warning:
                    return logger.LogWarning;
                default:
                    return logger.LogDebug;
            }
        }
        return (string? message, params object?[] args) => {  }; // No-op if logging is not enabled for the level.
    }
}