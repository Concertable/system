using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Concertable.Testing.E2E;

public sealed class MessageSinkLoggerProvider(IMessageSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new MessageSinkLogger(sink, categoryName);

    public void Dispose() { }

    private sealed class MessageSinkLogger(IMessageSink sink, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = $"[{logLevel}] {categoryName}: {formatter(state, exception)}";
            if (exception is not null)
                message += $"{Environment.NewLine}{exception}";

            sink.OnMessage(new Xunit.Sdk.DiagnosticMessage(message));
        }
    }
}
