using System.Globalization;
using System.Text.Json;

namespace EventStorage.Converters;

/// <summary>
/// Parses date values that a publisher sends in a format the built-in converters reject, which accept
/// strict ISO-8601 only. Publishers outside .NET commonly send a database timestamp instead, such as
/// <c>"2025-03-07 20:55:50"</c> or <c>"2025-02-25 10:10:24.000000"</c> — a space instead of <c>T</c> and
/// optional microseconds.
/// </summary>
internal static class EventDateParser
{
    /// <summary>
    /// Formats accepted in addition to whatever <see cref="Utf8JsonReader.TryGetDateTime"/> already handles.
    /// Values are read as written: no time zone is assumed and no offset is applied, so the date part of a
    /// timestamp can never shift to the neighbouring day.
    /// </summary>
    private static readonly string[] Formats =
    [
        "yyyy-MM-dd HH:mm:ss.FFFFFFF",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd"
    ];

    private const DateTimeStyles Styles = DateTimeStyles.AllowWhiteSpaces;

    /// <summary>
    /// Reads the current token as a date and time.
    /// </summary>
    /// <param name="reader">The reader positioned on the value to read.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="JsonException">
    /// Thrown when the value is not a string, is empty, or is in none of the accepted formats. A bad value
    /// is surfaced rather than silently read as <see langword="default"/>, which would otherwise reach the
    /// event handler as the year 1. To leave a date unset, send a JSON <c>null</c> for a nullable property;
    /// <c>System.Text.Json</c> handles that before the converter is reached.
    /// </exception>
    public static DateTime Read(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected a string for a date value, but found '{reader.TokenType}'.");

        // The built-in parser covers every strict ISO-8601 variant, including offsets and the 'Z' suffix.
        if (reader.TryGetDateTime(out var isoDateTime))
            return isoDateTime;

        var value = reader.GetString();

        if (DateTime.TryParseExact(value, Formats, CultureInfo.InvariantCulture, Styles, out var exact))
            return exact;

        // Last resort for shapes not listed above, e.g. a single-digit month or day.
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, Styles, out var parsed))
            return parsed;

        throw new JsonException($"The date value '{value}' is not in a supported format.");
    }
}
