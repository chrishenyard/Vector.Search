using System.ComponentModel.DataAnnotations;

namespace Vector.DataIngestion.Settings;

public class VectorStoreSettings
{
    public const string SectionName = "VectorStoreSettings";

    [Required]
    public string CollectionName { get; set; } = null!;

    public bool DeleteTemporaryFiles { get; set; } = true;

    [Required]
    public string RepositoryPath { get; set; } = null!;

    [Required]
    public string FileExtensions { get; set; } = null!;

    [Required]
    public string EmbeddingsModel { get; set; } = null!;

    [Required]
    public string ChatModel { get; set; } = null!;

    public int MaxDegreeOfParallelism { get; set; } = 1;

    public int LookAheadLines { get; set; } = 5;

    public int MinChunkSize { get; set; } = 1500;
}
