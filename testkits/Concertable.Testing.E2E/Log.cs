using Microsoft.Extensions.Logging;
using System.Net;

namespace Concertable.Testing.E2E;

internal static partial class Log
{
    #region AppFixture

    [LoggerMessage(Level = LogLevel.Information, Message = "Initializing E2E test fixture")]
    internal static partial void InitializingE2ETestFixture(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "E2E test fixture ready")]
    internal static partial void E2ETestFixtureReady(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resetting test state")]
    internal static partial void ResettingTestState(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reseed endpoint returned {StatusCode}")]
    internal static partial void ReseedEndpointFailed(this ILogger logger, int statusCode);

    #endregion

    #region HealthWaiter

    [LoggerMessage(Level = LogLevel.Information, Message = "Waiting for app to become healthy at {Url}/health")]
    internal static partial void WaitingForAppToBeHealthy(this ILogger logger, string url);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Health check: {StatusCode}")]
    internal static partial void HealthCheck(this ILogger logger, HttpStatusCode statusCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "App is healthy")]
    internal static partial void AppIsHealthy(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Waiting for SPAs to serve at {Urls}")]
    internal static partial void WaitingForSpasToServe(this ILogger logger, string urls);

    [LoggerMessage(Level = LogLevel.Information, Message = "SPAs are serving")]
    internal static partial void SpasAreServing(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "[Health] {Url} → {Error}")]
    internal static partial void HealthCheckError(this ILogger logger, string url, string error);

    #endregion

    #region PollingService

    [LoggerMessage(Level = LogLevel.Warning, Message = "Condition check failed")]
    internal static partial void ConditionCheckFailed(this ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Polling action failed")]
    internal static partial void PollingActionFailed(this ILogger logger, Exception ex);

    #endregion

    #region AspireResourceLogger

    [LoggerMessage(Level = LogLevel.Information, Message = "[Aspire] {Resource}: {State}")]
    internal static partial void AspireResourceStateChanged(this ILogger logger, string resource, string state);

    [LoggerMessage(Level = LogLevel.Information, Message = "[Aspire] {Resource} | {Line}")]
    internal static partial void AspireResourceLog(this ILogger logger, string resource, string line);

    #endregion
}
