using EventStorage.Configurations;
using EventStorage.Inbox.Repositories;
using EventStorage.Outbox.Repositories;
using EventStorage.Services;
using NSubstitute;

namespace EventStorage.Tests.UnitTests;

public class EventStoreTablesCreatorTests : BaseTestEntity
{
    private InboxAndOutboxSettings _settings;

    #region Setup

    [SetUp]
    public void Setup()
    {
        _settings = new InboxAndOutboxSettings();
    }

    #endregion

    #region CreateTablesIfNotExists

    [Test]
    public async Task CreateTablesIfNotExists_BothRepositoriesAreNull_ShouldNotThrowException()
    {
        var tablesCreator = new EventStoreTablesCreator(_settings);

        await Assert.DoesNotThrowAsync(() => tablesCreator.CreateTablesIfNotExistsAsync(CancellationToken.None));
    }

    [Test]
    public async Task
        CreateTablesIfNotExists_InboxRepositoryIsNotNullButOutboxRepositoryIsNull_ShouldNotThrowExceptionAndCallInboxRepository()
    {
        var inboxRepository = Substitute.For<IInboxRepository>();
        var tablesCreator = new EventStoreTablesCreator(_settings, inboxRepository);

        await Assert.DoesNotThrowAsync(() => tablesCreator.CreateTablesIfNotExistsAsync(CancellationToken.None));

        inboxRepository.Received(1).CreateTableIfNotExists();
    }

    [Test]
    public async Task
        CreateTablesIfNotExists_OutboxRepositoryIsNotNullButInboxRepositoryIsNull_ShouldNotThrowExceptionAndCallOutboxRepository()
    {
        var outboxRepository = Substitute.For<IOutboxRepository>();
        var tablesCreator = new EventStoreTablesCreator(_settings, null, outboxRepository);

        await Assert.DoesNotThrowAsync(() => tablesCreator.CreateTablesIfNotExistsAsync(CancellationToken.None));

        outboxRepository.Received(1).CreateTableIfNotExists();
    }

    [Test]
    public async Task CreateTablesIfNotExists_BothRepositoriesAreNotNull_ShouldNotThrowExceptionAndCallBothRepositories()
    {
        var inboxRepository = Substitute.For<IInboxRepository>();
        var outboxRepository = Substitute.For<IOutboxRepository>();
        var tablesCreator = new EventStoreTablesCreator(_settings, inboxRepository, outboxRepository);

        await Assert.DoesNotThrowAsync(() => tablesCreator.CreateTablesIfNotExistsAsync(CancellationToken.None));

        outboxRepository.Received(1).CreateTableIfNotExists();
        outboxRepository.Received(1).CreateTableIfNotExists();
    }

    #endregion
}