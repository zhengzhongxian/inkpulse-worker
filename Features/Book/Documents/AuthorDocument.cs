using System.Text.Json.Serialization;

namespace InkPulse.Worker.Features.Book.Documents
{
    public class AuthorDocument
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("biography")]
        public string Biography { get; set; } = string.Empty;

        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; } = string.Empty;

        [JsonPropertyName("is_deleted")]
        public bool Deleted { get; set; }
    }
}
