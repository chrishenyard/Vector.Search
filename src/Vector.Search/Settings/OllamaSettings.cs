using System.ComponentModel.DataAnnotations;

namespace Vector.Search.Settings;

public class OllamaSettings
{
    public const string SectionName = "OllamaSettings";

    [Url]
    public string Url { get; set; } = "http://localhost:11434";
    [Required]
    public int TimeoutFromMinutes { get; set; } = 5;
    public int EmbeddingsContextWindowSize { get; set; } = 8192;
    public int ChatContextWindowSize { get; set; } = 8192;
    public float Temperature { get; set; } = 0.8f;
}
