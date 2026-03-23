namespace Vector.DataIngestion.Parsers;

public interface IFileParser
{
    Task Parse(
        string filePath,
        string rootPath,
        string savePath,
        int minimumChunkSize,
        int lookAheadLineCount,
        CancellationToken cancellationToken);
}
