namespace Vector.DataIngestion.Parsers;

internal class CSharpDocumentParser : IFileParser
{
    public async Task Parse(
        string filePath,
        string rootPath,
        string savePath,
        int minimumChunkSize,
        int lookAheadLineCount,
        CancellationToken cancellationToken)
    {
        var ingestionParser = new CSharpIngestionParser(minimumChunkSize, lookAheadLineCount);

        using var reader = new StreamReader(filePath);
        var cshartpDocumentReader = new CSharpDocumentReader();
        var document = cshartpDocumentReader.ReadAsync(reader.BaseStream, filePath, "text/x-csharp", cancellationToken).Result;
        var ingestionChunk = ingestionParser.ProcessAsync(document, cancellationToken);

        var filename = Path.GetFileName(filePath);

        await foreach (var chunk in ingestionChunk.WithCancellation(cancellationToken))
        {
            var tempFileName = Utils.Files.CreateTempFileName(filename);
            var tempFilePath = Path.Combine(rootPath, savePath, tempFileName);
            using var writer = Utils.Files.CreateWriter(tempFilePath);
            await writer.WriteAsync(chunk.Content);
        }
    }
}
