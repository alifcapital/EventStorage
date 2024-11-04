using EventStorage.Inbox.Models;
using EventStorage.Repositories;

namespace EventStorage.Inbox.Repositories;

internal interface IInboxRepository: IEventRepository<InboxEvent>
{
}