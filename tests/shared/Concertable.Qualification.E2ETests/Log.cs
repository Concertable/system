using Microsoft.Extensions.Logging;

namespace Concertable.Qualification.E2ETests;

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "[Probe] {Service} {Url} → {Error}")]
    internal static partial void QualificationProbeFailed(
        this ILogger logger,
        string service,
        string url,
        string error);
}
