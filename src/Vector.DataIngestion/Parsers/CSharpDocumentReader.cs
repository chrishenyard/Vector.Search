using Microsoft.Extensions.DataIngestion;

namespace Vector.DataIngestion.Parsers;

/// <summary>
/// Reads a C# source file and converts it into an <see cref="IngestionDocument"/>.
/// Each non-blank line becomes an <see cref="IngestionDocumentParagraph"/> within a single section.
/// </summary>
public class CSharpDocumentReader : IngestionDocumentReader
{
    /// <inheritdoc/>
    public override async Task<IngestionDocument> ReadAsync(
        Stream source,
        string identifier,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        var document = new IngestionDocument(identifier);
        var section = new IngestionDocumentSection();

        using var reader = new StreamReader(source, leaveOpen: true);
        string? line;

        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            section.Elements.Add(new IngestionDocumentParagraph(line) { Text = line });
        }

        document.Sections.Add(section);
        return document;
    }
}
