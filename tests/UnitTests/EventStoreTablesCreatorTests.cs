using EventStorage.Inbox.Repositories;
using EventStorage.Outbox.Repositories;
using EventStorage.Services;
using NSubstitute;

namespace EventStorage.Tests.UnitTests;

public class EventStoreTablesCreatorTests : BaseTestEntity
{
    #region CreateTablesIfNotExists

    [Test]
    public void CreateTablesIfNotExists_BothRepositoriesAreNull_ShouldNotThrowException()
    {
        var tablesCreator = new EventStoreTablesCreator();

        Assert.DoesNotThrow(() => tablesCreator.CreateTablesIfNotExists());
    }
    
    [Test]
    public void CreateTablesIfNotExists_InboxRepositoryIsNotNullButOutboxRepositoryIsNull_ShouldNotThrowExceptionAndCallInboxRepository()
    {
        var inboxRepository = Substitute.For<IInboxRepository>();
        var tablesCreator = new EventStoreTablesCreator(inboxRepository);

        Assert.DoesNotThrow(() => tablesCreator.CreateTablesIfNotExists());
        
        inboxRepository.Received(1).CreateTableIfNotExists();
    }
    
    [Test]
    public void CreateTablesIfNotExists_OutboxRepositoryIsNotNullButInboxRepositoryIsNull_ShouldNotThrowExceptionAndCallOutboxRepository()
    {
        var outboxRepository = Substitute.For<IOutboxRepository>();
        var tablesCreator = new EventStoreTablesCreator(null, outboxRepository);

        Assert.DoesNotThrow(() => tablesCreator.CreateTablesIfNotExists());
        
        outboxRepository.Received(1).CreateTableIfNotExists();
    }
    
    [Test]
    public void CreateTablesIfNotExists_BothRepositoriesAreNotNull_ShouldNotThrowExceptionAndCallBothRepositories()
    {
        var inboxRepository = Substitute.For<IInboxRepository>();
        var outboxRepository = Substitute.For<IOutboxRepository>();
        var tablesCreator = new EventStoreTablesCreator(inboxRepository, outboxRepository);

        Assert.DoesNotThrow(() => tablesCreator.CreateTablesIfNotExists());
        
        outboxRepository.Received(1).CreateTableIfNotExists();
        outboxRepository.Received(1).CreateTableIfNotExists();
    }

    #endregion
}