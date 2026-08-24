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
        private readonly bool _isApplicationCategory;
        private readonly bool _isUnhandledRequestCategory;

        public ApplicationLogLogger(string categoryName, IApplicationLogStore store)
        {
            _categoryName = categoryName;
            _store = store;
            _isApplicationCategory = categoryName.Equals("PersonalTools", StringComparison.Ordinal) ||categoryName.StartsWith("PersonalTools.", StringComparison.Ordinal);
            _isUnhandledRequestCategory = categoryName.StartsWith("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", StringComparison.Ordinal);
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            // The database viewer is for events produced by Personal Tools code. Framework and
            // hosting diagnostics remain available through console/systemd without filling the
            // application table with request, SignalR and hosting lifecycle noise.
            return (_isApplicationCategory && logLevel >= LogLevel.Information) || (_isUnhandledRequestCategory && logLevel >= LogLevel.Error);
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
