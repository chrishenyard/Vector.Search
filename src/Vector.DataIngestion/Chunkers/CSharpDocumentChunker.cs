using Microsoft.Extensions.DataIngestion;
using System.Runtime.CompilerServices;
using System.Text;

namespace Vector.DataIngestion.Chunkers;

/// <summary>
/// Chunks a C# <see cref="IngestionDocument"/> into <see cref="IngestionChunk{T}"/> objects,
/// splitting at block boundaries (lines ending with <c>}</c>, <c>};</c>, or <c>});</c>)
/// once a minimum character threshold is reached.
/// </summary>
public class CSharpDocumentChunker(int minimumChunkSize, int lookAheadLineCount) : IngestionChunker<string>
{
    private static readonly string[] EndOfBlockMarkers = ["}", "};", "});"];

    /// <inheritdoc/>
    public override async IAsyncEnumerable<IngestionChunk<string>> ProcessAsync(
        IngestionDocument document,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var elements = document.EnumerateContent().ToList();
        var chunkBuilder = new StringBuilder();
        var i = 0;

        while (i < elements.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var element = elements[i];
            var lineText = element.GetMarkdown();

            chunkBuilder.AppendLine(lineText);

            if (chunkBuilder.Length >= minimumChunkSize && !IsEndOfBlock(lineText))
            {
                // Look ahead to find the next end-of-block within the allowed window.
                var lookAheadEnd = Math.Min(i + lookAheadLineCount, elements.Count - 1);
                var splitAt = -1;

                for (var j = i + 1; j <= lookAheadEnd; j++)
                {
                    if (IsEndOfBlock(elements[j].GetMarkdown()))
                    {
                        splitAt = j;
                    }
                }

                if (splitAt >= 0)
                {
                    // Ensure at least two elements remain after the split.
                    var remaining = elements.Count - splitAt - 1;
                    if (remaining >= 2)
                    {
                        // Include look-ahead lines up to and including the split point.
                        for (var j = i + 1; j <= splitAt; j++)
                        {
                            chunkBuilder.AppendLine(elements[j].GetMarkdown());
                        }

                        yield return new IngestionChunk<string>(chunkBuilder.ToString(), document);
                        chunkBuilder.Clear();
                        i = splitAt + 1;
                        continue;
                    }
                }

                // No suitable split found — include all look-ahead lines in the current chunk and keep going.
                for (var j = i + 1; j <= lookAheadEnd; j++)
                {
                    chunkBuilder.AppendLine(elements[j].GetMarkdown());
                }

                yield return new IngestionChunk<string>(chunkBuilder.ToString(), document);
                chunkBuilder.Clear();
                i = lookAheadEnd + 1;
                continue;
            }

            i++;
        }

        // Emit any remaining content as a final chunk.
        if (chunkBuilder.Length > 0)
        {
            yield return new IngestionChunk<string>(chunkBuilder.ToString(), document);
        }

        await Task.CompletedTask;
    }

    private static bool IsEndOfBlock(string line)
    {
        ReadOnlySpan<char> span = line.AsSpan();
        var idx = span.Length - 1;

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
