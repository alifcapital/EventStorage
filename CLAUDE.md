# CLAUDE.md — AlifCapital Event Infrastructure

## Library Ecosystem Overview

This project consists of **two tightly coupled libraries**. Changes to `EventStorage` can break `EventBus.RabbitMQ` — always consider both when modifying shared contracts.

```
┌─────────────────────────────────────────┐
│         AlifCapital.EventBus.RabbitMQ   │  v10.0.14
│   (RabbitMQ transport + wiring layer)   │
│                                         │
│  depends on ──────────────────────────► │
└─────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────┐
│         AlifCapital.EventStorage        │  v10.0.12
│   (Inbox/Outbox persistence + retry)    │
└─────────────────────────────────────────┘
```

| | EventStorage | EventBus.RabbitMQ |
|---|---|---|
| **Package ID** | `AlifCapital.EventStorage` | `AlifCapital.EventBus.RabbitMQ` |
| **Version** | `10.0.12` | `10.0.14` |
| **Repo** | `github.com/alifcapital/EventStorage` | `github.com/alifcapital/EventBus.RabbitMQ` |
| **Framework** | `.NET 10.0` | `.NET 10.0` |

---

## ⚠️ Cross-Library Change Policy

When modifying `EventStorage`, always check for breaking impact on `EventBus.RabbitMQ`:

- **Interfaces** — `IOutboxEvent`, `IInboxEvent`, `IHasHeaders`, `IHasAdditionalData`, `IEvent`, `IMessageBrokerEventPublisher`, `IMessageBrokerEventHandler` are directly implemented or consumed by `EventBus.RabbitMQ`
- **Extension method signatures** — `AddEventStore(...)` is called internally by `AddRabbitMqEventBus(...)`
- **`InboxAndOutboxOptions` / `InboxOrOutboxStructure`** — passed through `eventStoreOptions` in the RabbitMQ registration
- **`EventProviderType` enum** — `MessageBroker` is used by the RabbitMQ consumer to store inbox events
- **`IInboxEventManager` / `IOutboxEventManager`** — injected directly in `EventConsumerService` and user controllers in the RabbitMQ layer
- **`EventHandlerArgs` / `InboxEventArgs`** — event hook types wired through both libraries' registrations

---

## Library 1: AlifCapital.EventStorage

### Purpose
Provides the **Transactional Outbox and Inbox pattern** implementation. Ensures reliable at-least-once delivery and idempotent processing of inter-service events, with automatic retries and persistence to PostgreSQL or SQL Server.

### Core Concepts

**Outbox Pattern** — guarantees reliable event *publishing*. Events are persisted to the DB before dispatch, then sent asynchronously by a background processor.

- Entry point: `IOutboxEventManager`
- Event contract: `IOutboxEvent` (requires `Guid EventId`)
- `StoreAsync(event)` — persists immediately
- `Collect(event)` — buffers in memory, flushes at end of scope; `CleanCollectedEvents()` to discard

**Inbox Pattern** — guarantees *idempotent receiving*. Incoming events are stored before handling to block duplicate execution.

- Entry point: `IInboxEventManager`
- Event contract: `IInboxEvent` (requires `Guid EventId`)
- `StoreAsync(event, providerType)` returns `false` if the event was already received

### Architecture

```
┌─────────────────────────────────────────────────────────┐
│                   Application Layer                     │
│    IOutboxEventManager          IInboxEventManager      │
└────────────┬────────────────────────┬───────────────────┘
             │                        │
     StoreAsync / Collect         StoreAsync
             │                        │
┌────────────▼────────────────────────▼───────────────────┐
│          PostgreSQL / SQL Server Database                │
│       [ outbox table ]        [ inbox table ]           │
└────────────┬────────────────────────┬───────────────────┘
             │                        │
┌────────────▼────────────────────────▼───────────────────┐
│              Background Processor Jobs                   │
│   OutboxEventsProcessorJob    InboxEventsProcessorJob    │
│         (BaseEventsProcessorJob)                         │
└────────────┬────────────────────────┬───────────────────┘
             │                        │
     IEventPublisher<T>         IEventHandler<T>
     (per provider type)        (per provider type)
```

**Background Processing:**
- Auto-creates DB tables on startup
- Runs a polling loop with configurable `SecondsToDelayProcessEvents` delay
- **PostgreSQL distributed locks** (`DistributedLock.Postgres`) prevent duplicate processing across replicas
- `SemaphoreSlim` enforces `MaxConcurrency` within a single instance
- Failed events are retried with back-off based on `TryCount` / `TryAfterMinutes`

### Supported Event Providers

| Provider | Publisher Interface | Handler Interface |
|---|---|---|
| `MessageBroker` | `IMessageBrokerEventPublisher<T>` | `IMessageBrokerEventHandler<T>` |
| `Http` | `IHttpEventPublisher<T>` | `IHttpEventHandler<T>` |
| `WebHook` | `IWebHookEventPublisher<T>` | `IWebHookEventHandler<T>` |
| `Sms` | `ISmsEventPublisher<T>` | `ISmsEventHandler<T>` |
| `Email` | `IEmailEventPublisher<T>` | `IEmailEventHandler<T>` |
| `gRPC` | `IGrpcEventPublisher<T>` | — |
| `Unknown` | `IUnknownEventPublisher<T>` | `IUnknownEventHandler<T>` |

**Rules:**
- One publisher per event type — exception thrown on duplicate
- Multiple handlers per inbox event type are allowed
- Global publishers apply to all events of a given provider type

### Optional Event Interfaces

| Interface | Purpose |
|---|---|
| `IHasHeaders` | Attach `Dictionary<string, string> Headers` — used by RabbitMQ layer for header propagation |
| `IHasAdditionalData` | Attach `Dictionary<string, string> AdditionalData` for contextual metadata |

### Registration

```csharp
builder.Services.AddEventStore(
    builder.Configuration,
    assemblies: [typeof(Program).Assembly],
    options =>
    {
        options.Inbox.IsEnabled = true;
        options.Inbox.ConnectionString = "...";
        options.Outbox.IsEnabled = true;
        options.Outbox.ConnectionString = "...";
    });
```

### Configuration Reference

```json
"InboxAndOutbox": {
  "SecondsToDelayBeforeCreatingEventStoreTables": 0,
  "Inbox": {
    "IsEnabled": true,
    "TableName": "inbox",
    "MaxConcurrency": 10,
    "MaxEventsToFetch": 100,
    "TryCount": 10,
    "TryAfterMinutes": 5,
    "TryAfterMinutesIfEventNotFound": 60,
    "SecondsToDelayProcessEvents": 1,
    "DaysToCleanUpEvents": 0,
    "HoursToDelayCleanUpEvents": 1,
    "ConnectionString": "..."
  },
  "Outbox": { /* same options as Inbox */ }
}
```

**Key constraints:**
- `IsEnabled` defaults to `false` — must be explicitly set
- Inbox and Outbox **cannot share the same `TableName`**
- `DaysToCleanUpEvents` must be ≥ 1 to activate cleanup

### Project Structure

```
src/
├── Configurations/         # InboxOrOutboxStructure, InboxAndOutboxOptions
├── Models/                 # IOutboxEvent, IInboxEvent, IHasHeaders, IHasAdditionalData
├── Outbox/
│   ├── Managers/           # OutboxEventManager : IOutboxEventManager
│   ├── OutboxEventsProcessor.cs
│   └── Repositories/       # IOutboxRepository
├── Inbox/
│   ├── Managers/           # InboxEventManager : IInboxEventManager
│   ├── InboxEventsProcessor.cs
│   └── Repositories/       # IInboxRepository
├── BackgroundServices/     # BaseEventsProcessorJob, EventStorageNotifier
├── Extensions/             # EventStoreExtensions (AddEventStore)
├── Repositories/           # BaseEventRepository (Dapper + Npgsql)
└── Instrumentation/        # OpenTelemetry, EventStorageTraceInstrumentation
```

---

## Library 2: AlifCapital.EventBus.RabbitMQ

### Purpose
Extends `EventStorage` to provide **RabbitMQ transport** for event publishing and subscribing. Handles connection management, channel lifecycle, exchange/queue topology, and wires received messages into the EventStorage Inbox pipeline.

### Key Dependencies

| Package | Role |
|---|---|
| `AlifCapital.EventStorage` `10.0.12` | Inbox/Outbox persistence and retry |
| `RabbitMQ.Client` `7.2.0` | AMQP transport |
| `Polly` `8.6.5` | Resilient connection retry on startup |

### How It Extends EventStorage

`EventBus.RabbitMQ` plugs into `EventStorage` at two points:

1. **Outbox → RabbitMQ:** Provides a built-in `MessageBrokerEventPublisher` that implements `IMessageBrokerEventPublisher`. The EventStorage background processor calls this to dispatch stored outbox events to RabbitMQ.

2. **RabbitMQ → Inbox:** `EventConsumerService` receives messages from RabbitMQ. If `UseInbox: true`, it calls `IInboxEventManager.Store(...)` to persist the event before invoking handlers. This enables idempotent processing with automatic retry.

### Core Interfaces

| Interface | Implements | Purpose |
|---|---|---|
| `IPublishEvent` | `IBaseEvent`, `IOutboxEvent` | Contract for all outgoing events |
| `ISubscribeEvent` | `IBaseEvent`, `IInboxEvent` | Contract for all incoming events |
| `IBaseEvent` | `IEvent`, `IHasHeaders` | Common base — includes `CreatedAt` and `Headers` |
| `IEventSubscriber<T>` | `IMessageBrokerEventHandler<T>` | Handler contract for received events |

All publish and subscribe events **automatically support headers** via `IBaseEvent → IHasHeaders`.

### Publishing Flow

```
IEventPublisherManager.PublishAsync(event)
        │
        ▼
EventPublisherCollector → resolve EventPublisherOptions (routing key, virtual host, etc.)
        │
        ▼
RabbitMQ Channel → exchange → queue

--- OR with Outbox ---

IOutboxEventManager.StoreAsync(event, EventProviderType.MessageBroker)
        │
        ▼
Outbox DB table
        │  (background processor picks up after SecondsToDelayProcessEvents)
        ▼
MessageBrokerEventPublisher.PublishAsync(event)
        │
        ▼
IEventPublisherManager.PublishAsync(event) → RabbitMQ
```

### Subscribing / Receiving Flow

```
RabbitMQ → EventConsumerService.Consumer_ReceivingEvent()
        │
        ├─ UseInbox=false ──► deserialize → IEventSubscriber<T>.HandleAsync()
        │
        └─ UseInbox=true  ──► IInboxEventManager.Store(eventId, payload, headers, ...)
                                      │
                                      ▼
                              Inbox DB table
                                      │  (background processor)
                                      ▼
                              IEventSubscriber<T>.HandleAsync()
```

### Multi-Virtual-Host Support

The library is designed to work with **multiple RabbitMQ virtual hosts** simultaneously. Each publisher/subscriber can be assigned to a specific virtual host via `VirtualHostKey`.

Settings resolution priority (highest → lowest):
1. Per-event publisher/subscriber options (code or config)
2. Virtual host settings (`VirtualHostSettings[key]`)
3. `DefaultSettings`

### Registration

```csharp
builder.Services.AddRabbitMqEventBus(
    builder.Configuration,
    assemblies: [typeof(Program).Assembly],
    defaultOptions: options => { options.HostName = "localhost"; },
    virtualHostSettingsOptions: settings =>
    {
        settings.Add("payments", new RabbitMqHostSettings
        {
            VirtualHost = "payments", HostName = "localhost",
            UserName = "admin", Password = "admin123"
        });
    },
    eventPublisherManagerOptions: pub =>
    {
        pub.AddPublisher<UserDeleted>(op => op.RoutingKey = "users.deleted");
    },
    eventSubscriberManagerOptions: sub =>
    {
        sub.AddSubscriber<PaymentCreated, PaymentCreatedHandler>(op =>
            op.VirtualHostKey = "payments");
    },
    eventStoreOptions: options =>           // optional — configures EventStorage
    {
        options.Inbox.IsEnabled = true;
        options.Outbox.IsEnabled = true;
    });
```

`AddRabbitMqEventBus` internally calls `AddEventStore`, so **do not call both** in the same application.

### Configuration Reference

```json
"RabbitMQSettings": {
  "DefaultSettings": {
    "IsEnabled": true,
    "HostName": "localhost",
    "HostPort": 5672,
    "VirtualHost": "/",
    "UserName": "guest",
    "Password": "guest",
    "ExchangeName": "DefaultExchange",
    "ExchangeType": "topic",
    "QueueName": "my_queue",
    "RoutingKey": "events.#",
    "RetryConnectionCount": 3,
    "UseInbox": false,
    "EventNamingPolicy": "PascalCase",
    "PropertyNamingPolicy": "PascalCase",
    "UseTls": false,
    "QueueArguments": { "x-queue-type": "quorum" },
    "ExchangeArguments": {}
  },
  "Publishers": {
    "UserDeleted": { "RoutingKey": "users.deleted", "VirtualHostKey": "payments" }
  },
  "Subscribers": {
    "PaymentCreated": { "QueueName": "payments_queue", "RoutingKey": "payments.created" }
  },
  "VirtualHostSettings": {
    "payments": {
      "VirtualHost": "payments", "ExchangeName": "payments_exchange"
    }
  }
}
```

**Naming policies** (apply at `DefaultSettings`, `VirtualHostSettings`, or per event):
`PascalCase` · `CamelCase` · `SnakeCaseLower` · `SnakeCaseUpper` · `KebabCaseLower` · `KebabCaseUpper`

**Key constraints:**
- `IsEnabled: false` disables all RabbitMQ consumers and publishers
- `UseInbox: true` requires `Inbox.IsEnabled: true` in `InboxAndOutbox` config — will throw at runtime if missing
- One publisher per event type — exception on duplicate registration
- Multiple subscribers per event type — all are executed on receive

### Connection Resilience

`RabbitMqConnection` uses **Polly** for initial connection with exponential backoff:
- Retries on `SocketException` and `BrokerUnreachableException`
- Retry count controlled by `RetryConnectionCount` (default: 3)
- Reconnects automatically on `ConnectionShutdown` and `CallbackException`
- Channel is recreated per consumer on exception

### TLS Support

```json
"DefaultSettings": {
  "UseTls": true,
  "SslProtocolVersion": "Tls12",
  "ClientCertPath": "/certs/client.pem",
  "ClientCertKeyPath": "/certs/client.key"
}
```

### Project Structure

```
src/
├── Configurations/         # RabbitMqSettings, RabbitMqOptions, RabbitMqHostSettings
├── Connections/            # RabbitMqConnection, RabbitMqConnectionManager
├── Publishers/
│   ├── Managers/           # EventPublisherManager, EventPublisherCollector
│   ├── Messaging/          # MessageBrokerEventPublisher (IMessageBrokerEventPublisher)
│   └── Models/             # IPublishEvent, PublishEvent
├── Subscribers/
│   ├── Consumers/          # EventConsumerService (RabbitMQ channel + Inbox wiring)
│   ├── Managers/           # EventSubscriberCollector
│   └── Models/             # ISubscribeEvent, IEventSubscriber<T>
├── BackgroundServices/     # StartEventBusServices, EventBusNotifier
├── Extensions/             # RabbitMqExtensions (AddRabbitMqEventBus)
├── Instrumentation/        # OpenTelemetry trace for RabbitMQ events
└── Models/                 # IBaseEvent
tests/
├── EventBus.RabbitMQ.Tests/
└── Services/
    ├── UsersService/        # Integration test microservice
    ├── OrdersService/       # Integration test microservice
    └── AspireHost/          # .NET Aspire test orchestration (net10.0)
```

---

## Shared Tech Stack

| Concern | Technology |
|---|---|
| Runtime | .NET 10.0 |
| Database | PostgreSQL (primary), SQL Server (supported) |
| ORM / Query | Dapper, Npgsql |
| Distributed Lock | `DistributedLock.Postgres` (Medallion) |
| Message Broker | RabbitMQ via `RabbitMQ.Client` 7.2.0 |
| Resilience | Polly 8.6.5 |
| In-Memory Messaging | `AlifCapital.InMemoryMessaging` |
| Testing | NUnit 4, NSubstitute |
| Test Orchestration | .NET Aspire |
| CI/CD | GitHub Actions |

---

## Observability

Both libraries support OpenTelemetry tracing.

**EventStorage:**
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddEventStorageInstrumentation());
```

**EventBus.RabbitMQ** — adds its own source on top of EventStorage instrumentation. Enable both for full end-to-end trace coverage.

**Trace tags:** `messaging.system`, `event.id`, `event.type`, `event.provider`, `event.naming-policy-type`

---

## CI/CD Workflows (Both Libraries)

| Workflow | Trigger |
|---|---|
| `build.yml` | Push / PR |
| `run-tests.yml` | Reusable — NUnit tests with PostgreSQL + RabbitMQ containers |
| `service-tests.yml` | PR to `main` |
| `push-nuget-package.yml` | Manual / release |
| `release.yml` | Release tag |
| `service-versioning.yml` | Reusable — auto-increments patch in `.csproj`, commits to `main` |

Version format: `MAJOR.MINOR.PATCH` — patch auto-rolls to next minor at 100.