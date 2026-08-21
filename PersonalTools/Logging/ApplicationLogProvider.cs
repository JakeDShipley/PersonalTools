using Microsoft.Extensions.Logging;
using PersonalTools.Data.Monitoring;

namespace PersonalTools.Logging;

public sealed class ApplicationLogProvider : ILoggerProvider
{
    private readonly IApplicationLogStore _store;

    public ApplicationLogProvider(IApplicationLogStore store)
    {
        _store = store;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new ApplicationLogLogger(categoryName, _store);
    }

    public void Dispose()
    {
    }

    private sealed class ApplicationLogLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly IApplicationLogStore _store;

        public ApplicationLogLogger(string categoryName, IApplicationLogStore store)
        {
            _categoryName = categoryName;
            _store = store;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string message = formatter(state, exception);

            if (string.IsNullOrWhiteSpace(message) && exception is null)
            {
                return;
            }

            _store.Write(
                logLevel,
                eventId,
                _categoryName,
                message,
                exception?.ToString());
        }
    }
}