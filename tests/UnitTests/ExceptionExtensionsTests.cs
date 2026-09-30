using System.Reflection;
using EventStorage.Configurations;
using EventStorage.Extensions;

namespace EventStorage.Tests.UnitTests;

internal class ExceptionExtensionsTests
{
    #region ToFailureReason

    [Test]
    public void ToFailureReason_ExceptionWithInnerException_ShouldFormatChainWithoutStackTrace()
    {
        var exception = new InvalidOperationException("Outer", new ArgumentException("Inner"));

        var failureReason = exception.ToFailureReason(new InboxOrOutboxStructure());

        Assert.That(failureReason, Is.EqualTo(
            "System.InvalidOperationException: Outer ---> System.ArgumentException: Inner"));
    }

    [Test]
    public void ToFailureReason_TargetInvocationException_ShouldSkipWrapper()
    {
        var exception = new TargetInvocationException(new InvalidOperationException("Handler failed"));

        var failureReason = exception.ToFailureReason(new InboxOrOutboxStructure());

        Assert.That(failureReason, Is.EqualTo("System.InvalidOperationException: Handler failed"));
    }

    [Test]
    public void ToFailureReason_StoreFailureStackTraceIsEnabled_ShouldContainStackTrace()
    {
        Exception exception;
        try
        {
            throw new InvalidOperationException("Handler failed");
        }
        catch (Exception e)
        {
            exception = e;
        }

        var failureReason = exception.ToFailureReason(new InboxOrOutboxStructure { StoreFailureStackTrace = true });

        Assert.That(failureReason, Does.StartWith("System.InvalidOperationException: Handler failed"));
        Assert.That(failureReason, Does.Contain(nameof(ToFailureReason_StoreFailureStackTraceIsEnabled_ShouldContainStackTrace)));
    }

    [Test]
    public void ToFailureReason_ReasonIsLongerThanMaxLength_ShouldTruncateIt()
    {
        var exception = new InvalidOperationException(new string('a', 100));

        var failureReason = exception.ToFailureReason(new InboxOrOutboxStructure { MaxFailureReasonLength = 50 });

        Assert.That(failureReason, Has.Length.EqualTo(50));
    }

    [Test]
    public void ToFailureReason_MaxLengthIsZero_ShouldNotTruncate()
    {
        var exception = new InvalidOperationException(new string('a', 5000));

        var failureReason = exception.ToFailureReason(new InboxOrOutboxStructure { MaxFailureReasonLength = 0 });

        Assert.That(failureReason, Has.Length.GreaterThan(5000));
    }

    #endregion
}
