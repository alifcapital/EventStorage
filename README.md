## EventStorage

EventStorage is a library designed to simplify the implementation of the [Inbox and outbox patterns](https://en.wikipedia.org/wiki/Inbox_and_outbox_pattern) for handling multiple types of events in your application. It allows you to persist all incoming and outgoing event messages in the database. Currently, it supports storing event data only in a PostgreSQL database.

### Setting up the library

Make sure you have installed and run [PostgreSQL](https://www.postgresql.org/download/) in your machine.

To use this package from GitHub Packages in your projects, you need to authenticate using a **Personal Access Token (PAT)**.

#### Step 1: Create a Personal Access Token (PAT)

You need a GitHub [**Personal Access Token (PAT)**](https://docs.github.com/en/github/authenticating-to-github/creating-a-personal-access-token) to authenticate and pull packages from GitHub Packages. To create one:

1. Go to your GitHub account.
2. Navigate to **Settings > Developer settings > Personal access tokens > Tokens (classic)**.
3. Click on **Generate new token**.
4. Select the following scope: `read:packages` (for reading packages)
5. Generate the token and copy it. You'll need this token for authentication.


#### Step 2: Add GitHub Packages as a NuGet Source

You can choose one of two methods to add GitHub Packages as a source: either by adding the source dynamically via the `dotnet` CLI or using `NuGet.config`.

**Option 1:** Adding Source via `dotnet` CLI

Add the GitHub Package source with the token dynamically using the environment variable:

```bash
dotnet nuget add source https://nuget.pkg.github.com/alifcapital/index.json --name github --username GITHUB_USERNAME --password YOUR_PERSONAL_ACCESS_TOKEN --store-password-in-clear-text
```
* Replace GITHUB_USERNAME with your GitHub username or any non-empty string if you are using the Personal Access Token (PAT).
* Replace YOUR_PERSONAL_ACCESS_TOKEN with the generated PAT.

**Option 2**: Using `NuGet.config`
Add or update the `NuGet.config` file in your project root with the following content:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="github" value="https://nuget.pkg.github.com/alifcapital/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github>
      <add key="Username" value="GITHUB_USERNAME" />
      <add key="ClearTextPassword" value="YOUR_PERSONAL_ACCESS_TOKEN" />
    </github>
  </packageSourceCredentials>
</configuration>
```
* Replace GITHUB_USERNAME with your GitHub username or any non-empty string if you are using the Personal Access Token (PAT).
* Replace YOUR_PERSONAL_ACCESS_TOKEN with the generated PAT.

#### Step 3: Add the Package to Your Project
Once you deal with the nuget source, install the package by:

**Via CLI:**

```bash
dotnet add package AlifCapital.EventStorage --version <VERSION>
```

Or add it to your .csproj file:

```xml
<PackageReference Include="AlifCapital.EventStorage" Version="<VERSION>" />
```
Make sure to replace <VERSION> with the correct version of the package you want to install.

### How to use the library
Register the nuget package's necessary services to the services of DI in the Program.cs and pass the assemblies to find and load the events, publishers and receivers automatically:

```
builder.Services.AddEventStore(builder.Configuration,
    assemblies: [typeof(Program).Assembly]
    , options =>
    {
        options.Inbox.IsEnabled = true;
        options.Inbox.ConnectionString = "Connection string of the SQL database";
        //Other settings of the Inbox
        
        options.Outbox.IsEnabled = true;
        options.Outbox.ConnectionString = "Connection string of the SQL database";
        //Other settings of the Outbox
    });
```

Based on the configuration the tables will be automatically created while starting the server, if not exists.

### Using the Outbox pattern while publishing event
**Scenario 1:** _When user is deleted I need to notice the another service using the WebHook._<br/>

Start creating a structure of event to send. Your record must implement the `ISendEvent` interface. Example:

```
public record UserDeleted : ISendEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    
    public Guid UserId { get; init; }
    
    public string UserName { get; init; }
}
```
The `EventId` property is required, the other property can be added based on your business logic.<br/>

Since the library doesn't know about the actual sending of events, we need to create an event publisher specific to the type of event we want to publish. Add an event publisher by inheriting `IWebHookEventPublisher` and your `UserDeleted` event to manage a publishing event using the WebHook.

```
public class DeletedUserPublisher : IWebHookEventPublisher<UserDeleted>
{
    // private readonly IWebHookProvider _webHookProvider;
    //
    // public DeletedUserPublisher(IWebHookProvider webHookProvider)
    // {
    //     _webHookProvider = webHookProvider;
    // }

    public async Task Publish(UserDeleted @event, string eventPath)
    {
        //Add your logic
        return Task.CompletedTask;
    }
}
```
The event provider support a few types: `MessageBroker`-for RabbitMQ message or any other message broker, `Sms`-for SMS message, `Http`-for Http requests, `WebHook`- for WebHook call, `Email` for sending email, `Unknown` for other unknown type messages.
Depend on the event provider, the event subscriber must implement the necessary publisher interface: `IMessageBrokerEventPublisher`, `ISmsEventPublisher`, `IHttpEventPublisher`, `IWebHookEventPublisher`, `IEmailEventPublisher` and `IUnknownEventPublisher`- for `Unknown` provider type.

Now you can inject the `IEventSenderManager` interface from anywhere in your application, and use the `Send` method to publish your event.

```
public class UserController : ControllerBase
{
    private readonly IEventSenderManager _eventSenderManager;

    public UserController(IEventSenderManager eventSenderManager)
    {
        _eventSenderManager = eventSenderManager;
    }
    
    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        if (!Items.TryGetValue(id, out User item))
            return NotFound();

        var userDeleted = new UserDeleted { UserId = item.Id, UserName = item.Name };
        var webHookUrl = "https:example.com/api/users";
        var succussfullySent = _eventSenderManager.Send(userDeleted, EventProviderType.WebHook, webHookUrl);
        
        Items.Remove(id);
        return Ok(item);
    }
}
```

When we use the `Send` method of the `IEventSenderManager` to send an event, the event is first stored in the database. Based on our configuration (_by default, after one second_), the event will then be automatically execute the `Publish` method of created the `DeletedUserPublisher` event publisher.

If an event fails for any reason, the server will automatically retry publishing it, with delays based on the configuration you set in the [Outbox section](#options-of-inbox-and-outbox-sections).

**Scenario 2:** _When user is created I need to notice the another service using the RabbitMQ._<br/>

Start creating a structure of event to send. Your record must implement the `ISendEvent` interface. Example:

```
public record UserCreated : ISendEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    
    public Guid UserId { get; init; }
    
    public string UserName { get; init; }
    
    public int Age { get; init; }
}
```

Next, add an event publisher to manage a publishing event with the MessageBroker provider. Since the event storage functionality is designed as a separate library, it doesn't know about the actual sending of events. Therefore, we need to create single an event publisher to the specific provider, in our use case is for a MessageBroker.

```
public class MessageBrokerEventPublisher : IMessageBrokerEventPublisher
{
    // private readonly IEventPublisherManager _eventPublisher;
    
    // public MessageBrokerEventPublisher(IEventPublisherManager eventPublisher)
    // {
    //     _eventPublisher = eventPublisher;
    // }
    
    public async Task Publish(ISendEvent @event, string eventPath)
    {
        // _eventPublisher.Publish((IPublishEvent)@event);
        return Task.CompletedTask;
    }
}
```

The MessageBrokerEventPublisher is serve for all kinds of events, those are sending to the MessageBroker provider. But if you want to create event publisher for the event type for being able to use properties of event without casting, you need to just create event publisher by using generic interface of necessary publisher. In our use case is IMessageBrokerEventPublisher<UserCreated>.

```
public class CreatedUserMessageBrokerEventPublisher : IMessageBrokerEventPublisher<UserCreated>
{
    // private readonly IEventPublisherManager _eventPublisher;
    //
    // public CreatedUserMessageBrokerEventPublisher(IEventPublisherManager eventPublisher)
    // {
    //     _eventPublisher = eventPublisher;
    // }
    
    public async Task Publish(UserCreated @event, string eventPath)
    {
        // _eventPublisher.Publish(@event);
        //Add you logic to publish an event to the RabbitMQ
        
        return Task.CompletedTask;
    }
}
```

Since we want to publish our an event to the RabbitMQ, the event subscriber must implement the `IMessageBrokerEventPublisher` by passing the type of event (`UserCreated`), we want to publish.
Your application is now ready to use this publisher. Inject the `IEventSenderManager` interface from anywhere in your application, and use the `Send` method to publish your `UserCreated` event.

```
public class UserController : ControllerBase
{
    private readonly IEventSenderManager _eventSenderManager;

    public UserController(IEventSenderManager eventSenderManager)
    {
        _eventSenderManager = eventSenderManager;
    }
    
    [HttpPost]
    public IActionResult Create([FromBody] User item)
    {
        Items.Add(item.Id, item);

        var userCreated = new UserCreated { UserId = item.Id, UserName = item.Name };
        var routingKey = "usser.created";
        var succussfullySent = _eventSenderManager.Send(userCreated, EventProviderType.MessageBroker, routingKey);
        
        return Ok(item);
    }
}
```

##### Is there any way to add some additional data to the event while sending and use that while publishing event?

Yes, there is a way to do that. For that, we need to just implement `IHasAdditionalData` interface to the event structure of our sending event:

```
public record UserCreated : ISendEvent, IHasAdditionalData
{
    public Guid Id { get; }= Guid.NewGuid();
    
    public Guid UserId { get; init; } 
    
    public string UserName { get; init; }
    
    public Dictionary<string, string> AdditionalData { get; set; }
}
```

When we implement the implement `IHasAdditionalData` interface, it requires us to add collection property named `AdditionalData`. Now it is ready to use that:

```
var userCreated = new UserCreated { UserId = item.Id, UserName = item.Name };
userCreated.AdditionalData = new();
userCreated.AdditionalData.Add("login", "admin");
userCreated.AdditionalData.Add("password", "123");
var succussfullySent = _eventSenderManager.Send(userCreated, EventProviderType.MessageBroker, eventPath);
```

While publishing event, now you are able to read and use the added property from your event:

```
public class CreatedUserMessageBrokerEventPublisher : IMessageBrokerEventPublisher<UserCreated>
{
    //Your logic
    
    public async Task Publish(UserCreated @event, string eventPath)
    {
        var login = @event.AdditionalData["login"];
        var password = @event.AdditionalData["password"];
        //Your logic
        _eventPublisher.Publish(@event);
        
        return Task.CompletedTask;
    }
}
```

### Using the Inbox pattern while receiving event

Start creating a structure of event to receive. Your record must implement the `IReceiveEvent` interface. Example:

```
public record UserCreated : IReceiveEvent
{
    public Guid Id { get; init; }
    
    public Guid UserId { get; init; }
    
    public string UserName { get; init; }
    
    public int Age { get; init; }
}
```

Next, add an event receiver to manage a publishing RabbitMQ event.

```
public class UserCreatedReceiver : IRabbitMqEventReceiver<UserCreated>
{
    private readonly ILogger<UserCreatedReceiver> _logger;

    public UserCreatedReceiver(ILogger<UserCreatedReceiver> logger)
    {
        _logger = logger;
    }

    public async Task Receive(UserCreated @event)
    {
        _logger.LogInformation("EventId ({EventId}): {UserName} user is created with the {UserId} id", @event.EventId,
            @event.UserName, @event.UserId);
        //Add your logic in here
        
        return Task.CompletedTask;
    }
}
```

Now the `UserCreatedReceiver` receiver is ready to receive the event. To make it work, from your logic which you receive the event from the RabbitMQ, you need to inject the `IEventReceiverManager` interface and puss the received event to the `Received` method.

```
UserCreated receivedEvent = new UserCreated
{
    //Get created you data from the Consumer of RabbitMQ.
};
try
{
    IEventReceiverManager eventReceiverManager = scope.ServiceProvider.GetService<IEventReceiverManager>();
    if (eventReceiverManager is not null)
    {
        var succussfullyReceived = eventReceiverManager.Received(receivedEvent, eventArgs.RoutingKey, EventProviderType.RabbitMq);
        if(succussfullyReceived){
            //If the event received twice, it will return false. You need to add your logic to manage this use case.
        }
    }else{
        //the IEventReceiverManager will not be injected if the Inbox pattern is not enabled. You need to add your logic to manage this use case.
    }
}
catch (Exception ex)
{
    //You need to add logic to handle some unexpected use cases.
}
```

That's all. As we mentioned in above, the event provider support a few types: `MessageBroker`-for RabbitMQ message or any other message broker, `Http`-for receiving http requests, `Sms`-for SMS message, `WebHook`- for WebHook call, `Email` for sending email, `Unknown` for other unknown type messages.
Depend on the event provider, the event receiver must implement the necessary receiver interface: `IMessageBrokerEventReceiver`, `ISmsEventReceiver`, `IWebHookEventReceiver`, `IHttpEventReceiver`, `IEmailEventReceiver` and `IUnknownEventReceiver`- for `Unknown` provider type.

### Options of Inbox and Outbox sections

The `InboxAndOutbox` is the main section for setting of the Outbox and Inbox functionalities. The `Outbox` and `Inbox` subsections offer numerous options.

```
"InboxAndOutbox": {
    "Inbox": {
      //Your inbox settings
    },
    "Outbox": {
      "IsEnabled": false,
      "TableName": "Outbox",
      "MaxConcurrency": 10,
      "TryCount": 5,
      "TryAfterMinutes": 20,
      "TryAfterMinutesIfEventNotFound": 60,
      "SecondsToDelayProcessEvents": 2,
      "DaysToCleanUpEvents": 30,
      "HoursToDelayCleanUpEvents": 2,
      "ConnectionString": "Connection string of the SQL database"
    }
  }
```
**Description of options:**

`IsEnabled` - Enables or disables the use of Inbox/Outbox for storing received/sent events. Default value is false. <br/>
`TableName` - Specifies the table name used for storing received/sent events. Default value is "Inbox" for Inbox, "Outbox" for Outbox.<br/>
`MaxConcurrency` - Sets the maximum number of concurrent tasks for executing received/publishing events. Default value is 10.<br/>
`TryCount` - Defines the number of attempts before increasing the delay for the next retry. Default value is 10.<br/>
`TryAfterMinutes` - Specifies the number of minutes to wait before retrying if an event fails. Default value is 5.<br/>
`TryAfterMinutesIfEventNotFound` - For increasing the TryAfterAt to amount of minutes if the event not found to publish or receive. Default value is 60.<br/>
`SecondsToDelayProcessEvents` - The delay in seconds before processing events. Default value is 1.<br/>
`DaysToCleanUpEvents` - Number of days after which processed events are cleaned up. Cleanup only occurs if this value is 1 or higher. Default value is 0.<br/>
`HoursToDelayCleanUpEvents` - Specifies the delay in hours before cleaning up processed events. Default value is 1.<br/>
`ConnectionString` - The connection string for the PostgreSQL database used to store or read received/sent events.<br/>

All options of the Inbox and Outbox are optional, if we don't pass the value of them, it will use the default value of the option.
