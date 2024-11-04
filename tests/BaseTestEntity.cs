using EventStorage.Configurations;

namespace EventStorage.Tests;

[Parallelizable(ParallelScope.Fixtures)]
public abstract class BaseTestEntity
{ 
    public static InboxAndOutboxSettings InboxAndOutboxSettings { get; set; } 
}