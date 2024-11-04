using EventStorage.Configurations;
using EventStorage.Inbox.Repositories;
using EventStorage.Outbox.Repositories;
using EventStorage.Tests.Infrastructure;
using EventStorage.Tests.Infrastructure.Config;
using Microsoft.Extensions.Configuration;

namespace EventStorage.Tests;

[SetUpFixture]
public class TestInit
{
    /// <summary>
    /// The connection string of the database to connect the Postgres
    /// </summary>
    internal static string DatabaseConnectionString { get; set; }
    
    /// <summary>
    /// Object to lock the creation of the database to avoid concurrency problems
    /// </summary>
    private static readonly object LockCreateDatabase = new();
    
    [OneTimeSetUp]
    public void RunBeforeAllTests()
    {
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
        var configuration = ConfigurationHelper.LoadConfiguration();
        
        if (string.IsNullOrEmpty(connectionString))
        {
            connectionString = configuration.GetValue<string>("DatabaseOptions:ConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception("Connection string not found");
            }
        }
        
        DatabaseConnectionString = connectionString;
        
        var inboxAndOutboxSettings = GetDefaultSettings(configuration: configuration);

        lock (LockCreateDatabase)
        {
            using var context = new EventStorageContext();
            context.Database.EnsureCreated();
            
            var inboxRepository = new InboxRepository(inboxAndOutboxSettings.Inbox);
            inboxRepository.CreateTableIfNotExists();
            
            var outboxRepository = new OutboxRepository(inboxAndOutboxSettings.Outbox);
            outboxRepository.CreateTableIfNotExists();
        }

        BaseTestEntity.InboxAndOutboxSettings = inboxAndOutboxSettings;
    }
    
    [OneTimeTearDown]
    public static void RunAfterAllTests()
    {
        using var context = new EventStorageContext();
        context.Database.EnsureDeleted();
    }

    private InboxAndOutboxSettings GetDefaultSettings(IConfiguration configuration)
    {
        var defaultSettings = configuration.GetSection("InboxAndOutbox").Get<InboxAndOutboxSettings>() ?? new();
        defaultSettings.Inbox.ConnectionString = DatabaseConnectionString;
        defaultSettings.Outbox.ConnectionString = DatabaseConnectionString;
        if (defaultSettings.Inbox is null)
        {
            defaultSettings.Inbox = new();
            defaultSettings.Inbox.TableName = nameof(defaultSettings.Inbox);
        }

        if (defaultSettings.Outbox is null)
        {
            defaultSettings.Outbox = new();
            defaultSettings.Outbox.TableName = nameof(defaultSettings.Outbox);
        }
        
        if (defaultSettings.Inbox.IsEnabled && string.IsNullOrEmpty(defaultSettings.Inbox.TableName))
            throw new Exception("If the inbox is enabled, the table name cannot be empty");

        if (defaultSettings.Outbox.IsEnabled && string.IsNullOrEmpty(defaultSettings.Outbox.TableName))
            throw new Exception("If the outbox is enabled, the table name cannot be empty");

        return defaultSettings;
    }
}