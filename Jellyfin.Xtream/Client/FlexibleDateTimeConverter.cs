using System;
using System.Globalization;
using Newtonsoft.Json;

namespace Jellyfin.Xtream.Client;

/// <summary>
/// Supports converting json dates from multiple formats.
/// </summary>
public class FlexibleDateTimeConverter : JsonConverter
{
    // List of allowed date formats
    private static readonly string[] DateFormats = new[]
    {
        "yyyyMMddHHmmss zzz",       // 20260128060000 +0000
        "yyyyMMddHHmmss",           // 20260128060000 (no timezone)
        "yyyy-MM-ddTHH:mm:ssZ",     // ISO UTC
        "yyyy-MM-ddTHH:mm:sszzz",   // ISO with offset
        "yyyy-MM-dd HH:mm:ss",      // Common SQL-like
        "yyyy-MM-dd",               // Date only
    };

    /// <summary>
    /// Verifies we can convert the datatype.
    /// </summary>
    /// <param name="objectType">Object type.</param>
    /// <returns>true if matches DateTime.</returns>
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(DateTime) || objectType == typeof(DateTime?);
    }

    /// <summary>
    /// Reads the json property.
    /// </summary>
    /// <param name="reader">json reader.</param>
    /// <param name="objectType">object type we are expecting.</param>
    /// <param name="existingValue">current value of the object.</param>
    /// <param name="serializer">serializer.</param>
    /// <returns>object value from json.</returns>
    /// <exception cref="JsonSerializationException">Something went wrong.</exception>
    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.Value == null || string.IsNullOrWhiteSpace(reader.Value.ToString()))
        {
            return objectType == typeof(DateTime?) ? (DateTime?)null! : DateTime.MinValue;
        }

#pragma warning disable CS8602 // Dereference of a possibly null reference.
        string str = reader.Value.ToString().Trim();
#pragma warning restore CS8602 // Dereference of a possibly null reference.

        // Fix "+0000" style timezone for the first format
        if (str.Length > 14 && (str[14] == ' ' || str[14] == '+' || str[14] == '-'))
        {
            // Example: "20260128060000 +0000" -> "20260128060000 +00:00"
            string datePart = str.Substring(0, 14);
            string tzPart = str.Substring(15);
            if (tzPart.Length == 5) // e.g., +0000
            {
                tzPart = tzPart.Insert(3, ":");
            }

            str = datePart + " " + tzPart;
        }

        // Try parsing with all supported formats
        foreach (var format in DateFormats)
        {
            if (DateTime.TryParseExact(str, format, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt))
            {
                return dt;
            }
        }

        // Fallback to standard parsing
        if (DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var fallbackDt))
        {
            return fallbackDt;
        }

        throw new JsonSerializationException($"Unable to parse date: {str}");
    }

    /// <summary>
    /// Writes the date time as a json.
    /// </summary>
    /// <param name="writer">json writer.</param>
    /// <param name="value">object value.</param>
    /// <param name="serializer">json serializer.</param>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        DateTime dt = (DateTime)value;

        // Serialize in the main custom format
#pragma warning disable CA1305 // Specify IFormatProvider
        string formatted = dt.ToString(DateFormats[3]);
#pragma warning restore CA1305 // Specify IFormatProvider
        writer.WriteValue(formatted);
    }
}
