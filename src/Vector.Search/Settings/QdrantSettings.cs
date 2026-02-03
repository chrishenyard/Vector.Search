using System.ComponentModel.DataAnnotations;

namespace Vector.Search.Settings;

public class QdrantSettings
{
    public const string SectionName = "QdrantSettings";

    [Required]
    [Url]
    public string Url { get; set; } = null!;

    [Required]
    public string CollectionName { get; set; } = null!;

    public int TimeoutFromMinutes { get; set; } = 5;
}
