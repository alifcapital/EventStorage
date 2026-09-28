using System.Reflection;
using EventStorage.Configurations;

namespace EventStorage.Extensions;

internal static class ExceptionExtensions
{
    private const string InnerExceptionSeparator = " ---> ";

    /// <summary>
    /// Builds the failure reason of the event from the exception. The reflection/task wrappers are skipped and the chain
    /// of exceptions is formatted as "Type: Message". The stack trace is added only if it is enabled in the settings.
    /// </summary>
    /// <param name="exception">The exception thrown while processing the event.</param>
    /// <param name="settings">The inbox or outbox settings.</param>
    /// <returns>The failure reason truncated to the maximum length of the settings.</returns>
    internal static string ToFailureReason(this Exception exception, InboxOrOutboxStructure settings)
    {
        var rootException = UnwrapException(exception);
        string failureReason;
        if (settings.StoreFailureStackTrace)
        {
            failureReason = rootException.ToString();
        }
        else
        {
            var allExceptions = GetExceptionChain(rootException)
                .Select(e => $"{e.GetType().FullName}: {e.Message}");
            failureReason = string.Join(InnerExceptionSeparator, allExceptions);
        }

        return failureReason.TruncateFailureReason(settings.MaxFailureReasonLength);
    }

    #region Helper methods

    /// <summary>
    /// Truncates the failure reason to the maximum length of the settings.
    /// </summary>
    private static string TruncateFailureReason(this string failureReason, int maxFailureReasonLength)
    {
        if (maxFailureReasonLength <= 0 || failureReason is null || failureReason.Length <= maxFailureReasonLength)
            return failureReason;

        return failureReason[..maxFailureReasonLength];
    }

    /// <summary>
    /// Skips the wrapper exceptions which do not give any information about the failure.
    /// </summary>
    private static Exception UnwrapException(Exception exception)
    {
        while (true)
        {
            switch (exception)
            {
                case TargetInvocationException { InnerException: not null }:
                    exception = exception.InnerException;
                    continue;
                case AggregateException { InnerExceptions.Count: 1 } aggregateException:
                    exception = aggregateException.InnerExceptions[0];
                    continue;
                default:
                    return exception;
            }
        }
    }

    /// <summary>
    /// Get exception including all inner exceptions until the first one if exists.
    /// </summary>
    private static IEnumerable<Exception> GetExceptionChain(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
            yield return current;
    }

    #endregion
}