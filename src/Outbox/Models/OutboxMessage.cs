using EventStorage.Models;

namespace EventStorage.Outbox.Models;

internal class OutboxMessage : BaseMessageBox, IOutboxMessage;