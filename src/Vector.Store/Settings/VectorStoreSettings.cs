using System.ComponentModel.DataAnnotations;

namespace Vector.Store.Settings;

public class VectorStoreSettings
{
    public const string SectionName = "VectorStoreSettings";

    [Required]
    public string CollectionName { get; set; } = null!;
}
