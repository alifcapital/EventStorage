using EventStorage.Inbox.Models;
using EventStorage.Models;

namespace EventStorage.Tests.UnitTests;

internal class BaseMessageBoxTests : BaseTestEntity
{
    #region Status changing methods

    [Test]
    public void NewEvent_ShouldBePendingWithoutUpdatedAt()
    {
        var message = new InboxMessage();

        Assert.That(message.Status, Is.EqualTo(EventStatus.Pending));
        Assert.That(message.StatusName, Is.EqualTo(nameof(EventStatus.Pending)));
        Assert.That(message.UpdatedAt, Is.Null);
    }

    [Test]
    public void Processed_ShouldSetProcessedStatusAndUpdatedAt()
    {
        var message = new InboxMessage();

        message.Processed();

        Assert.That(message.Status, Is.EqualTo(EventStatus.Processed));
        Assert.That(message.UpdatedAt, Is.EqualTo(DateTime.Now).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public void Failed_ShouldSetFailedStatusUpdatedAtAndIncreaseTryCount()
    {
        var message = new InboxMessage();

        message.Failed(maxTryCount: 10, tryAfterMinutes: 5);

        Assert.That(message.Status, Is.EqualTo(EventStatus.Failed));
        Assert.That(message.TryCount, Is.EqualTo(1));
        Assert.That(message.UpdatedAt, Is.EqualTo(DateTime.Now).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public void Rejected_ShouldSetRejectedStatusAndUpdatedAt()
    {
        var message = new InboxMessage();

        message.Rejected();

        Assert.That(message.Status, Is.EqualTo(EventStatus.Rejected));
        Assert.That(message.StatusName, Is.EqualTo(nameof(EventStatus.Rejected)));
        Assert.That(message.UpdatedAt, Is.EqualTo(DateTime.Now).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public void Rescheduled_RejectedEvent_ShouldSetPendingStatusAndTryAfterAt()
    {
        var message = new InboxMessage();
        message.Rejected();
        var tryAfterAt = DateTime.Now.AddMinutes(30);

        message.Rescheduled(tryAfterAt);

        Assert.That(message.Status, Is.EqualTo(EventStatus.Pending));
        Assert.That(message.TryAfterAt, Is.EqualTo(tryAfterAt));
        Assert.That(message.UpdatedAt, Is.EqualTo(DateTime.Now).Within(TimeSpan.FromSeconds(1)));
    }

    #endregion
}
