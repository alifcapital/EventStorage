using EventStorage.Inbox.Models;
using EventStorage.Inbox.Repositories;
using EventStorage.Models;
using EventStorage.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventStorage.Tests.UnitTests.Inbox
{
    /// <summary>
    /// The class inherits the base class, which contains all the tests
    /// written for the generic type.
    /// </summary>
    [TestFixture]
    internal class InboxRepositoryTests() : BaseEventRepositoryTests<InboxMessage>(
        baseEventRepository: new InboxRepository(NullLogger<InboxRepository>.Instance, InboxAndOutboxSettings),
        dataContext: new DataContext<InboxMessage>(
            InboxAndOutboxSettings.Inbox.ConnectionString,
            InboxAndOutboxSettings.Inbox.TableName
        )
    )
    {
        #region InsertEvent

        [Test]
        public void InsertEvent_PassingNamingPolicyType_StoresEventWithCorrectNamingPolicyType()
        {
            const string eventNamingPolicy = NamingPolicyTypeNames.SnakeCaseLower;
            var inboxMessage = new InboxMessage
            {
                Id = Guid.NewGuid(),
                Provider = "TestProvider",
                EventName = "TestEvent1",
                EventPath = "/test/path",
                Payload = "TestPayload",
                Headers = "TestHeaders",
                AdditionalData = "TestAdditionalData",
                TryCount = 0,
                TryAfterAt = DateTime.Now.AddMinutes(5),
                NamingPolicyType = eventNamingPolicy,
            };

            var result = Repository.InsertEvent(inboxMessage);

            Assert.That(result, Is.True);
            var firstEventFromDb = DataContext.GetById(inboxMessage.Id);
            Assert.That(firstEventFromDb.Id, Is.EqualTo(firstEventFromDb!.Id));
            Assert.That(firstEventFromDb.EventName, Is.EqualTo(firstEventFromDb!.EventName));
            Assert.That(firstEventFromDb.NamingPolicyType, Is.EqualTo(eventNamingPolicy));
        }

        #endregion
    }
}