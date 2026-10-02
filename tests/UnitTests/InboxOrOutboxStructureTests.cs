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
}
