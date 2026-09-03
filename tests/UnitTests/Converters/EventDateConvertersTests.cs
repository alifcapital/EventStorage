using System.Text.Json;
using EventStorage.Models;

namespace EventStorage.Tests.UnitTests.Converters;

/// <summary>
/// The date converters are registered for every event by
/// <see cref="NamingPolicyTypeNames.CreateJsonSerializer"/>, so these tests deserialize through those
/// options rather than attaching a converter to the test types: that is what guards the registration
/// itself, not just the parsing.
/// </summary>
public class EventDateConvertersTests : BaseTestEntity
{
    private record EventWithDateTime
    {
        public DateTime CreatedAt { get; init; }
        public DateTime? ClosedAt { get; init; }
    }

    private record EventWithDateOnly
    {
        public DateOnly Date { get; init; }
    }

    #region DateTime

    [TestCase("2025-03-07 20:55:50", TestName = "Timestamp with a space separator")]
    [TestCase("2025-03-07T20:55:50", TestName = "ISO timestamp")]
    [TestCase(" 2025-03-07 20:55:50 ", TestName = "Timestamp padded with whitespace")]
    public void Deserialize_WhenDateTimeIsNotStrictIso_ShouldReadItKeepingTheTime(string value)
    {
        var result = Deserialize<EventWithDateTime>($"{{\"createdAt\": \"{value}\"}}");

        Assert.That(result.CreatedAt, Is.EqualTo(new DateTime(2025, 3, 7, 20, 55, 50)));
    }

    [Test]
    public void Deserialize_WhenDateTimeCarriesMicroseconds_ShouldReadIt()
    {
        var result = Deserialize<EventWithDateTime>("{\"createdAt\": \"2025-02-25 10:10:24.000000\"}");

        Assert.That(result.CreatedAt, Is.EqualTo(new DateTime(2025, 2, 25, 10, 10, 24)));
    }

    [Test]
    public void Deserialize_WhenNullableDateTimeIsNotStrictIso_ShouldReadIt()
    {
        var result = Deserialize<EventWithDateTime>("{\"closedAt\": \"2025-02-25 10:10:24.000000\"}");

        Assert.That(result.ClosedAt, Is.EqualTo(new DateTime(2025, 2, 25, 10, 10, 24)));
    }

    [Test]
    public void Deserialize_WhenNullableDateTimeIsNull_ShouldReadItAsNull()
    {
        var result = Deserialize<EventWithDateTime>("{\"closedAt\": null}");

        Assert.That(result.ClosedAt, Is.Null);
    }

    #endregion

    #region DateOnly

    /// <summary>
    /// The built-in converter accepts a bare date only, so a publisher sending the whole timestamp would
    /// otherwise fail the event.
    /// </summary>
    [Test]
    public void Deserialize_WhenDateOnlyCarriesATimePart_ShouldReadTheDatePart()
    {
        var result = Deserialize<EventWithDateOnly>("{\"date\": \"2026-08-10T16:37:45.324081\"}");

        Assert.That(result.Date, Is.EqualTo(new DateOnly(2026, 8, 10)));
    }

    [Test]
    public void Deserialize_WhenDateOnlyIsABareDate_ShouldStillReadIt()
    {
        var result = Deserialize<EventWithDateOnly>("{\"date\": \"2026-08-10\"}");

        Assert.That(result.Date, Is.EqualTo(new DateOnly(2026, 8, 10)));
    }

    /// <summary>
    /// A late evening timestamp would move to the next day if the converter applied a time zone offset.
    /// </summary>
    [Test]
    public void Deserialize_WhenDateOnlyCarriesALateEveningTime_ShouldNotShiftTheDate()
    {
        var result = Deserialize<EventWithDateOnly>("{\"date\": \"2026-08-10 23:59:59\"}");

        Assert.That(result.Date, Is.EqualTo(new DateOnly(2026, 8, 10)));
    }

    #endregion

    #region Invalid values

    [TestCase("\"not-a-date\"", TestName = "Not a date")]
    [TestCase("\"\"", TestName = "Empty string")]
    [TestCase("1755000000", TestName = "Number instead of a string")]
    public void Deserialize_WhenDateTimeValueIsInvalid_ShouldThrow(string value)
    {
        Assert.Throws<JsonException>(() => Deserialize<EventWithDateTime>($"{{\"createdAt\": {value}}}"));
    }

    [Test]
    public void Deserialize_WhenDateOnlyValueIsInvalid_ShouldThrow()
    {
        Assert.Throws<JsonException>(() => Deserialize<EventWithDateOnly>("{\"date\": \"not-a-date\"}"));
    }

    #endregion

    #region Serialization stays unchanged

    /// <summary>
    /// The converters must not change what publishers put on the wire, otherwise upgrading the library
    /// would alter existing payloads.
    /// </summary>
    [Test]
    public void Serialize_ShouldWriteTheSameValuesAsTheBuiltInConverters()
    {
        var payload = new EventWithDateTime
        {
            CreatedAt = new DateTime(2025, 3, 7, 20, 55, 50),
            ClosedAt = new DateTime(2025, 2, 25, 10, 10, 24)
        };
        var optionsWithoutConverters = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var withConverters = JsonSerializer.Serialize(payload, CreateOptions());
        var withoutConverters = JsonSerializer.Serialize(payload, optionsWithoutConverters);

        Assert.That(withConverters, Is.EqualTo(withoutConverters));
    }

    [Test]
    public void Serialize_WhenValueIsDateOnly_ShouldWriteTheSameValueAsTheBuiltInConverter()
    {
        var payload = new EventWithDateOnly { Date = new DateOnly(2026, 8, 10) };
        var optionsWithoutConverters = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var withConverters = JsonSerializer.Serialize(payload, CreateOptions());
        var withoutConverters = JsonSerializer.Serialize(payload, optionsWithoutConverters);

        Assert.That(withConverters, Is.EqualTo(withoutConverters));
    }

    #endregion

    #region Helper Methods

    private static JsonSerializerOptions CreateOptions() =>
        NamingPolicyTypeNames.CreateJsonSerializer(NamingPolicyTypeNames.CamelCase);

    private static TEvent Deserialize<TEvent>(string payload) =>
        JsonSerializer.Deserialize<TEvent>(payload, CreateOptions());

    #endregion
}
