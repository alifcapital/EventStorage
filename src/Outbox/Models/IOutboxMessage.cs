
using EventStorage.Models;

namespace EventStorage.Outbox.Models;

/// <summary>
/// The outbox message structure to be stored in the outbox.
/// </summary>
internal interface IOutboxMessage : IBaseMessageBox;