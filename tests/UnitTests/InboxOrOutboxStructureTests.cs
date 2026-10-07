using EventStorage.Configurations;
using Microsoft.Extensions.Configuration;

namespace EventStorage.Tests.UnitTests;

internal class InboxOrOutboxStructureTests
{
    [Test]
    public void Bind_TryAfterSecondsAndTryAfterMinutesIfTryCountExceeded_ShouldSetThem()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["TryAfterSeconds"] = "15",
                ["TryAfterMinutesIfTryCountExceeded"] = "30"
            })
            .Build();

        var settings = configuration.Get<InboxOrOutboxStructure>();

        Assert.That(settings.TryAfterSeconds, Is.EqualTo(15));
        Assert.That(settings.TryAfterMinutesIfTryCountExceeded, Is.EqualTo(30));
    }

    [Test]
    public void Bind_SecondsToWaitForFetchedEventsToBeProcessed_ShouldSetIt()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["SecondsToWaitForFetchedEventsToBeProcessed"] = "120"
            })
            .Build();

        var settings = configuration.Get<InboxOrOutboxStructure>();

        Assert.That(settings.SecondsToWaitForFetchedEventsToBeProcessed, Is.EqualTo(120));
    }

    #region GetProcessingTimeoutAt

    [Test]
    public void GetProcessingTimeoutAt_DefaultSettings_ShouldReturnTimeAfterSixHundredSeconds()
    {
        var settings = new InboxOrOutboxStructure();

        var result = settings.GetProcessingTimeoutAt();

        Assert.That(result, Is.EqualTo(DateTime.Now.AddSeconds(600)).Within(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public void GetProcessingTimeoutAt_SecondsAreConfigured_ShouldReturnTimeWithWholeSeconds()
    {
        var settings = new InboxOrOutboxStructure { SecondsToWaitForFetchedEventsToBeProcessed = 30 };

        var result = settings.GetProcessingTimeoutAt();

        Assert.That(result, Is.EqualTo(DateTime.Now.AddSeconds(30)).Within(TimeSpan.FromSeconds(2)));
        Assert.That(result.Ticks % TimeSpan.TicksPerSecond, Is.Zero);
    }

    #endregion
}
