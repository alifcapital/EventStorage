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
        var result = await _service.GetEventByIdAsync(Guid.NewGuid());

        Assert.That(result, Is.Null);
    }

    #endregion

    #region ExecuteAsync

    [Test]
    public async Task ExecuteAsync_EventExists_ShouldProcessItWithTheRequest()
    {
        var message = CreateMessage();
        _repository.GetEventByIdAsync(message.Id).Returns(message);
        _processor.ProcessSingleEventAsync(message, Request, Arg.Any<CancellationToken>())
            .Returns(EventActionResult.Success());

        var result = await _service.ExecuteAsync(message.Id, Request);

        Assert.That(result.IsSuccess, Is.True);
        await _processor.Received(1).ProcessSingleEventAsync(message, Request, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_EventDoesNotExist_ShouldReturnNotFound()
    {
        var result = await _service.ExecuteAsync(Guid.NewGuid(), Request);

        Assert.That(result.Status, Is.EqualTo(EventActionResultStatus.NotFound));
        await _processor.DidNotReceiveWithAnyArgs().ProcessSingleEventAsync(default, default, default);
    }

    #endregion

    #region RejectAsync

    [Test]
    public async Task RejectAsync_PendingEvent_ShouldRejectAndStoreIt()
    {
        var message = CreateMessage();
        _repository.GetEventByIdAsync(message.Id).Returns(message);

        var result = await _service.RejectAsync(message.Id, Request);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(message.Status, Is.EqualTo(EventStatus.Rejected));
        Assert.That(message.UpdatedBy, Is.EqualTo(Request.PerformedBy));
        Assert.That(message.StatusComment, Is.EqualTo(Request.Comment));
        await _repository.Received(1).UpdateEventAsync(message);
    }

    [Test]
    public async Task RejectAsync_ProcessedEvent_ShouldReturnInvalidStateWithoutUpdating()
    {
        var message = CreateMessage();
        message.Processed();
        _repository.GetEventByIdAsync(message.Id).Returns(message);

        var result = await _service.RejectAsync(message.Id, Request);

        Assert.That(result.Status, Is.EqualTo(EventActionResultStatus.InvalidState));
        await _repository.DidNotReceive().UpdateEventAsync(Arg.Any<InboxMessage>());
    }

    [Test]
    public async Task RejectAsync_EventIsLocked_ShouldReturnLockedWithoutReadingIt()
    {
        _distributedLock.TryAcquireAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns((IDistributedSynchronizationHandle)null);
        var eventId = Guid.NewGuid();

        var result = await _service.RejectAsync(eventId, Request);

        Assert.That(result.Status, Is.EqualTo(EventActionResultStatus.AlreadyProcessing));
        await _repository.DidNotReceive().GetEventByIdAsync(Arg.Any<Guid>());
        await _repository.DidNotReceive().UpdateEventAsync(Arg.Any<InboxMessage>());
    }

    [Test]
    public async Task RejectAsync_EventDoesNotExist_ShouldReturnNotFound()
    {
        var result = await _service.RejectAsync(Guid.NewGuid(), Request);

        Assert.That(result.Status, Is.EqualTo(EventActionResultStatus.NotFound));
    }

    #endregion

    #region RescheduleAsync

    [Test]
    public async Task RescheduleAsync_RejectedEvent_ShouldMakeItPendingWithNewTryTime()
    {
        var message = CreateMessage();
        message.Rejected();
        _repository.GetEventByIdAsync(message.Id).Returns(message);
        var tryAfterAt = DateTime.Now.AddHours(1);

        var result = await _service.RescheduleAsync(message.Id, tryAfterAt, Request);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(message.Status, Is.EqualTo(EventStatus.Pending));
        Assert.That(message.TryAfterAt, Is.EqualTo(tryAfterAt));
        await _repository.Received(1).UpdateEventAsync(message);
    }

    [Test]
    public async Task RescheduleAsync_FailedEvent_ShouldReturnInvalidState()
    {
        var message = CreateMessage();
        message.Failed(maxTryCount: 10, tryAfterMinutes: 5, failureReason: "Test failure");
        _repository.GetEventByIdAsync(message.Id).Returns(message);

        var result = await _service.RescheduleAsync(message.Id, DateTime.Now, Request);

        Assert.That(result.Status, Is.EqualTo(EventActionResultStatus.InvalidState));
        await _repository.DidNotReceive().UpdateEventAsync(Arg.Any<InboxMessage>());
    }

    #endregion

    #region MarkAsProcessedAsync

    [Test]
    public async Task MarkAsProcessedAsync_RejectedEvent_ShouldMarkItAsProcessed()
    {
        var message = CreateMessage();
        message.Rejected();
        _repository.GetEventByIdAsync(message.Id).Returns(message);

        var result = await _service.MarkAsProcessedAsync(message.Id, Request);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(message.Status, Is.EqualTo(EventStatus.Processed));
        Assert.That(message.UpdatedBy, Is.EqualTo(Request.PerformedBy));
        await _repository.Received(1).UpdateEventAsync(message);
    }

    [Test]
    public async Task MarkAsProcessedAsync_ProcessedEvent_ShouldReturnInvalidState()
    {
        var message = CreateMessage();
        message.Processed();
        _repository.GetEventByIdAsync(message.Id).Returns(message);

        var result = await _service.MarkAsProcessedAsync(message.Id, Request);

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
