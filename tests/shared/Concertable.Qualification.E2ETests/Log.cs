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

    [LoggerMessage(Level = LogLevel.Information, Message = "[Env] {Resource} resolved {Count} variables")]
    internal static partial void QualificationEnvironmentResolved(
        this ILogger logger,
        string resource,
        int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "[Env] {Resource} could not resolve its environment")]
    internal static partial void QualificationEnvironmentUnresolved(
        this ILogger logger,
        string resource,
        Exception exception);
}
