using Microsoft.Extensions.Logging;

namespace Concertable.Testing.E2E;

// Writes log entries to a file so CI can upload them as an artifact; the in-fixture
// console logger isn't surfaced by `dotnet test`.
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter writer;
    private readonly object gate = new();

    public FileLoggerProvider(string path) =>
        writer = new StreamWriter(path, append: false) { AutoFlush = true };

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, writer, gate);

    public void Dispose() => writer.Dispose();

    private sealed class FileLogger(string category, StreamWriter writer, object gate) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (gate)
            {
                writer.WriteLine($"{logLevel,-11} {category}: {formatter(state, exception)}");
                if (exception is not null)
                    writer.WriteLine(exception);
            }
        }
    }
}
