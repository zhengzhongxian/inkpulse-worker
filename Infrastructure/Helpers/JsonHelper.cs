using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace InkPulse.Worker.Infrastructure.Helpers
{
    /// <summary>
    /// Centralized JSON helper using System.Text.Json.
    /// Use this instead of calling JsonSerializer directly to keep options consistent across the project.
    /// </summary>
    public static class JsonHelper
    {
        private static readonly JsonSerializerOptions DefaultOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        /// <summary>Serialize an object to a JSON string.</summary>
        public static string Serialize<T>(T obj) =>
            JsonSerializer.Serialize(obj, DefaultOptions);

        /// <summary>Deserialize a JSON string to the specified type. Returns null on failure.</summary>
        public static T? Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return default;
            try
            {
                return JsonSerializer.Deserialize<T>(json, DefaultOptions);
            }
            catch
            {
                return default;
            }
        }

        /// <summary>Parse a raw JSON string into a JsonDocument for manual traversal.</summary>
        public static JsonDocument ParseDocument(string json) =>
            JsonDocument.Parse(json);

        /// <summary>
        /// Try to get a string property value from a JsonElement by key. Returns null if not found.
        /// </summary>
        public static string? GetString(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var prop) ? prop.GetString() : null;

        /// <summary>
        /// Try to get an int property value from a JsonElement by key. Returns null if not found.
        /// </summary>
        public static int? GetInt(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var prop) && prop.TryGetInt32(out var val) ? val : null;
    }
}
