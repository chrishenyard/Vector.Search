using System.ComponentModel.DataAnnotations;

namespace Vector.Search.Settings;

public class OllamaSettings
{
    public const string SectionName = "OllamaSettings";

    [Url]
    public string Url { get; set; } = "http://localhost:11434";
    [Required]
    public string VisionModel { get; set; } = null!;
    public int TimeoutFromMinutes { get; set; } = 5;
    public int ContextWindowSize { get; set; } = 2048;
    public float Temperature { get; set; } = 0.8f;
}
