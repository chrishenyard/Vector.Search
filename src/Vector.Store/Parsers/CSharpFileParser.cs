namespace Vector.Store.Parsers;

public class CSharpFileParser : IFileParser
{
    private readonly string[] EndOfBlockMarkers = ["}", "};", "});"];

    public async Task Parse(
        string filePath,
        string rootPath,
        string savePath,
        int minimumChunkSize,
        int lookAheadLineCount,
        CancellationToken cancellationToken)
    {
        var lookaheadLines = new List<string>(lookAheadLineCount);
        var characterCount = 0;
        var filename = Path.GetFileName(filePath);
        var tempFileName = Utils.Files.CreateTempFileName(filename);
        var tempFilePath = Path.Combine(rootPath, savePath, tempFileName);

        StreamWriter? writer = null;
        try
        {
            writer = Utils.Files.CreateWriter(tempFilePath);

            using var reader = new StreamReader(filePath);
            string? line;

            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                await writer.WriteLineAsync(line.AsMemory(), cancellationToken);

                var isEndOfBlock = IsEndOfBlock(line);
                characterCount += line.Length + Environment.NewLine.Length;

                if (characterCount >= minimumChunkSize && !isEndOfBlock)
                {
                    // Look ahead to find the end of the next block or until lookahead line limit is reached
                    // This helps to avoid splitting in the middle of a code block, which can be important
                    // for maintaining context in embeddings
                    var lineCount = 0;
                    var endOfBlockLine = 0;

                    while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        lookaheadLines.Add(line);

                        if (IsEndOfBlock(line))
                        {
                            endOfBlockLine = lineCount;
                        }

                        if (lineCount == lookAheadLineCount - 1)
                        {
                            break;
                        }

                        lineCount++;
                    }

                    if (lookaheadLines.Count > 0)
                    {
                        // If we found an end of block within the lookahead lines, we split the chunk there.
                        // Otherwise, we just include all lookahead lines in the current chunk.
                        // Leave at least two lines in the next chunk to avoid very small chunks
                        var linesRemaing = lookaheadLines.Count - endOfBlockLine + 1; // +1 because endOfBlockLine is 0-based index

                        for (var i = 0; i < lookaheadLines.Count; i++)
                        {
                            var lookaheadLine = lookaheadLines[i];

                            if (i == endOfBlockLine && linesRemaing >= 2)
                            {
                                await writer.WriteLineAsync(lookaheadLine.AsMemory(), cancellationToken);
                                await writer.FlushAsync(cancellationToken);
                                await writer.DisposeAsync();

                                tempFileName = Utils.Files.CreateTempFileName(filename);
                                tempFilePath = Path.Combine(rootPath, savePath, tempFileName);
                                writer = Utils.Files.CreateWriter(tempFilePath);
                            }
                            else
                            {
                                await writer.WriteLineAsync(lookaheadLine.AsMemory(), cancellationToken);
                                characterCount += lookaheadLine.Length + Environment.NewLine.Length;
                            }
                        }
                    }

                    characterCount = 0;
                }
            }
        }
        finally
        {
            if (writer is not null)
            {
                await writer.FlushAsync(cancellationToken);
                await writer.DisposeAsync();
            }
        }
    }

    private bool IsEndOfBlock(string line)
    {
        ReadOnlySpan<char> span = line.AsSpan();
        var idx = span.Length - 1;

        // Walk backwards over whitespace only
        while (idx >= 0 && char.IsWhiteSpace(span[idx]))
        {
            idx--;
        }

        foreach (var marker in EndOfBlockMarkers)
        {
            if (idx >= marker.Length - 1 && span.Slice(idx - marker.Length + 1, marker.Length).SequenceEqual(marker))
            {
                return true;
            }
        }

        return false;
    }
}
