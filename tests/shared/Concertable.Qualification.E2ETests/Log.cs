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

    [LoggerMessage(Level = LogLevel.Information, Message = "[Mount] {Resource} {Source} -> {Target} sourceExists={Exists}")]
    internal static partial void QualificationContainerMount(
        this ILogger logger,
        string resource,
        string source,
        string target,
        bool exists);

    [LoggerMessage(Level = LogLevel.Information, Message = "[Annotations] {Resource}: {Annotations}")]
    internal static partial void QualificationResourceAnnotations(
        this ILogger logger,
        string resource,
        string annotations);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[Probe] stripped {Annotation} from {Resource}")]
    internal static partial void QualificationAnnotationStripped(
        this ILogger logger,
        string resource,
        string annotation);
}
