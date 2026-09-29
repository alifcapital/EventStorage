using EventStorage.Configurations;
using EventStorage.Constants;
using EventStorage.Exceptions;
using EventStorage.Inbox;
using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;
using EventStorage.Management;
using EventStorage.Management.Models;
using EventStorage.Models;
using Medallion.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventStorage.Tests.UnitTests.Management;

/// <summary>
/// Tests of the <see cref="BaseEventsManagementService{TRepository,TProcessor,TMessage}"/> via the inbox events service.
/// </summary>
internal class InboxEventsServiceTests
{
    private IKeyedServiceProvider _serviceProvider;
    private IInboxRepository _repository;
    private IInboxEventsProcessor _processor;
    private IDistributedLock _distributedLock;
    private InboxEventsService _service;

    private static readonly EventActionRequest Request = new() { PerformedBy = "operator", Comment = "Test comment" };

    #region SetUp

    [SetUp]
    public void SetUp()
    {
        _serviceProvider = Substitute.For<IKeyedServiceProvider>();
        _repository = Substitute.For<IInboxRepository>();
        _serviceProvider.GetService(typeof(IInboxRepository)).Returns(_repository);
        _processor = Substitute.For<IInboxEventsProcessor>();
        _serviceProvider.GetService(typeof(IInboxEventsProcessor)).Returns(_processor);
        MockDistributedLockProvider();

        _service = CreateService(isEnabled: true);
    }

    #endregion

    #region GetEventAsync

    [Test]
    public async Task GetEventAsync_EventDoesNotExist_ShouldReturnNull()
    {
        var result = await _service.GetEventByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetEventAsync_EventExists_ShouldPassCancellationTokenToRepository()
    {
        var message = CreateMessage();
        using var cancellationTokenSource = new CancellationTokenSource();
        _repository.GetEventByIdAsync(message.Id, cancellationTokenSource.Token).Returns(message);

        var result = await _service.GetEventByIdAsync(message.Id, cancellationTokenSource.Token);

        Assert.That(result.Id, Is.EqualTo(message.Id));
        await _repository.Received(1).GetEventByIdAsync(message.Id, cancellationTokenSource.Token);
    }

    [Test]
    public async Task GetEventAsync_CancellationRequested_ShouldThrowWithoutReadingEvent()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() =>
            _service.GetEventByIdAsync(Guid.NewGuid(), cancellationTokenSource.Token));
        await _repository.DidNotReceive().GetEventByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region ExecuteAsync

    [Test]
    public async Task ExecuteAsync_EventExists_ShouldProcessItWithTheRequest()
    {
        var message = CreateMessage();
        _repository.GetEventByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        _processor.ProcessSingleEventAsync(message, Request, Arg.Any<CancellationToken>())
            .Returns(EventActionResult.Success());

        var result = await _service.ExecuteAsync(message.Id, Request, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        await _processor.Received(1).ProcessSingleEventAsync(message, Request, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_EventDoesNotExist_ShouldReturnNotFound()
    {
        var result = await _service.ExecuteAsync(Guid.NewGuid(), Request, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EventActionResultStatus.NotFound));
        await _processor.DidNotReceiveWithAnyArgs().ProcessSingleEventAsync(default, default, default);
    }

    [Test]
    public async Task ExecuteAsync_EventExists_ShouldPassCancellationTokenToRepositoryAndProcessor()
    {
        var message = CreateMessage();
        using var cancellationTokenSource = new CancellationTokenSource();
        _repository.GetEventByIdAsync(message.Id, cancellationTokenSource.Token).Returns(message);
        _processor.ProcessSingleEventAsync(message, Request, cancellationTokenSource.Token)
            .Returns(EventActionResult.Success());

        await _service.ExecuteAsync(message.Id, Request, cancellationTokenSource.Token);

        await _repository.Received(1).GetEventByIdAsync(message.Id, cancellationTokenSource.Token);
        await _processor.Received(1).ProcessSingleEventAsync(message, Request, cancellationTokenSource.Token);
    }

    #endregion

    #region RejectAsync

    [Test]
    public async Task RejectAsync_PendingEvent_ShouldRejectAndStoreIt()
    {
        var message = CreateMessage();
        _repository.GetEventByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);

        var result = await _service.RejectAsync(message.Id, Request, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(message.Status, Is.EqualTo(EventStatus.Rejected));
        Assert.That(message.UpdatedBy, Is.EqualTo(Request.PerformedBy));
        Assert.That(message.StatusComment, Is.EqualTo(Request.Comment));
        await _repository.Received(1).UpdateEventAsync(message, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RejectAsync_ProcessedEvent_ShouldReturnInvalidStateWithoutUpdating()
    {
        var message = CreateMessage();
        message.Processed();
        _repository.GetEventByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);

        var result = await _service.RejectAsync(message.Id, Request, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EventActionResultStatus.InvalidState));
        await _repository.DidNotReceive().UpdateEventAsync(Arg.Any<InboxMessage>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RejectAsync_EventIsLocked_ShouldReturnLockedWithoutReadingIt()
    {
        _distributedLock.TryAcquireAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns((IDistributedSynchronizationHandle)null);
        var eventId = Guid.NewGuid();

        var result = await _service.RejectAsync(eventId, Request, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EventActionResultStatus.AlreadyProcessing));
        await _repository.DidNotReceive().GetEventByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().UpdateEventAsync(Arg.Any<InboxMessage>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RejectAsync_EventDoesNotExist_ShouldReturnNotFound()
    {
        var result = await _service.RejectAsync(Guid.NewGuid(), Request, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EventActionResultStatus.NotFound));
    }

    [Test]
    public async Task RejectAsync_PendingEvent_ShouldPassCancellationTokenToLockAndRepository()
    {
        var message = CreateMessage();
        using var cancellationTokenSource = new CancellationTokenSource();
        _repository.GetEventByIdAsync(message.Id, cancellationTokenSource.Token).Returns(message);

        var result = await _service.RejectAsync(message.Id, Request, cancellationTokenSource.Token);

        Assert.That(result.IsSuccess, Is.True);
        await _distributedLock.Received(1).TryAcquireAsync(Arg.Any<TimeSpan>(), cancellationTokenSource.Token);
        await _repository.Received(1).GetEventByIdAsync(message.Id, cancellationTokenSource.Token);
        await _repository.Received(1).UpdateEventAsync(message, cancellationTokenSource.Token);
    }

    #endregion

    #region RescheduleAsync

    [Test]
    public async Task RescheduleAsync_RejectedEvent_ShouldMakeItPendingWithNewTryTime()
    {
        var message = CreateMessage();
        message.Rejected();
        _repository.GetEventByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);
        var tryAfterAt = DateTime.Now.AddHours(1);

        var result = await _service.RescheduleAsync(message.Id, tryAfterAt, Request, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(message.Status, Is.EqualTo(EventStatus.Pending));
        Assert.That(message.TryAfterAt, Is.EqualTo(tryAfterAt));
        await _repository.Received(1).UpdateEventAsync(message, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RescheduleAsync_FailedEvent_ShouldReturnInvalidState()
    {
        var message = CreateMessage();
        message.Failed(maxTryCount: 10, tryAfterMinutes: 5, failureReason: "Test failure");
        _repository.GetEventByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);

        var result = await _service.RescheduleAsync(message.Id, DateTime.Now, Request, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EventActionResultStatus.InvalidState));
        await _repository.DidNotReceive().UpdateEventAsync(Arg.Any<InboxMessage>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region MarkAsProcessedAsync

    [Test]
    public async Task MarkAsProcessedAsync_RejectedEvent_ShouldMarkItAsProcessed()
    {
        var message = CreateMessage();
        message.Rejected();
        _repository.GetEventByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);

        var result = await _service.MarkAsProcessedAsync(message.Id, Request, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(message.Status, Is.EqualTo(EventStatus.Processed));
        Assert.That(message.UpdatedBy, Is.EqualTo(Request.PerformedBy));
        await _repository.Received(1).UpdateEventAsync(message, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MarkAsProcessedAsync_ProcessedEvent_ShouldReturnInvalidState()
    {
        var message = CreateMessage();
        message.Processed();
        _repository.GetEventByIdAsync(message.Id, Arg.Any<CancellationToken>()).Returns(message);

        var result = await _service.MarkAsProcessedAsync(message.Id, Request, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(EventActionResultStatus.InvalidState));
    }

    #endregion

    #region Helper methods

    private InboxEventsService CreateService(bool isEnabled)
    {
        var settings = new InboxAndOutboxSettings
        {
            Inbox = new InboxOrOutboxStructure { IsEnabled = isEnabled, TableName = FunctionalityNames.Inbox }
        };

        return new InboxEventsService(_serviceProvider, settings, Substitute.For<ILogger<InboxEventsService>>());
    }

    private void MockDistributedLockProvider()
    {
        var distributedLockProvider = Substitute.For<IDistributedLockProvider>();
        _serviceProvider.GetRequiredKeyedService(typeof(IDistributedLockProvider), FunctionalityNames.Inbox)
            .Returns(distributedLockProvider);

        _distributedLock = Substitute.For<IDistributedLock>();
        distributedLockProvider.CreateLock(Arg.Any<string>()).Returns(_distributedLock);

        var distributedSynchronizationHandle = Substitute.For<IDistributedSynchronizationHandle>();
        _distributedLock.TryAcquireAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(distributedSynchronizationHandle);
    }

    private static InboxMessage CreateMessage()
    {
        return new InboxMessage
        {
            Id = Guid.NewGuid(),
            Provider = nameof(EventProviderType.MessageBroker),
            EventName = "TestEvent",
            EventPath = "Test.Path",
            Payload = "{}",
            TryAfterAt = DateTime.Now
        };
    }

    #endregion
}
