using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Vector.Files.Chunking;

public class CodeChunking(ILogger<CodeChunking> logger) : IChunk
{
    private readonly ILogger<CodeChunking> _logger = logger;
    private const char EndOfBlockMarker = '}';

    public async Task GetChunksAsync(
        string writePath,
        string rootPath,
        string[] fileExtensions,
        CancellationToken token,
        int minimumChunkSize = 5000)
    {
        ArgumentException.ThrowIfNullOrEmpty(writePath, nameof(writePath));
        ArgumentException.ThrowIfNullOrEmpty(rootPath, nameof(rootPath));

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount / 2, 1) // Use half of the available processors to avoid overwhelming the system
        };

        var files = Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Where(p => fileExtensions.Any(ext => p.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));

        await Parallel.ForEachAsync(files, parallelOptions,
            async (file, cancellationToken) =>
            {
                _logger.LogDebug("Processing file: {FilePath}", file);

                await ChunkSingleFileAsync(
                    file,
                    rootPath,
                    writePath,
                    minimumChunkSize,
                    cancellationToken);
            });
    }

    private static async Task ChunkSingleFileAsync(
        string filePath,
        string rootPath,
        string savePath,
        int minimumChunkSize,
        CancellationToken cancellationToken)
    {
        var characterCount = 0;
        var tempFileName = CreateTempFileName();
        var tempFilePath = Path.Combine(rootPath, savePath, tempFileName);

        StreamWriter? writer = null;
        try
        {
            writer = CreateWriter(tempFilePath);

            using var reader = new StreamReader(filePath);
            string? line;
            var firstLine = true;

            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (firstLine)
                {
                    firstLine = false;
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue; // Skip leading empty lines
                    }
                }

                await writer.WriteLineAsync(line.AsMemory(), cancellationToken);

                var isEndOfBlock = IsEndOfBlock(line);
                characterCount += line.Length + Environment.NewLine.Length;

                if (characterCount >= minimumChunkSize && isEndOfBlock)
                {
                    // Look ahead until there are no more end of block characters
                    // to avoid splitting in the middle of a code block
                    var writeLastLookAheadLine = false;
                    firstLine = true;

                    while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (firstLine)
                        {
                            firstLine = false;
                            if (string.IsNullOrWhiteSpace(line))
                            {
                                continue; // Skip leading empty lines
                            }
                        }

                        if (IsEndOfBlock(line))
                        {
                            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
                            characterCount += line.Length + Environment.NewLine.Length;
                        }
                        else
                        {
                            writeLastLookAheadLine = true;
                            break;
                        }
                    }

                    await writer.FlushAsync(cancellationToken);
                    await writer.DisposeAsync();

                    tempFileName = CreateTempFileName();
                    tempFilePath = Path.Combine(rootPath, savePath, tempFileName);
                    writer = CreateWriter(tempFilePath);

                    if (writeLastLookAheadLine && line != null)
                    {
                        await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
                        characterCount = line.Length + Environment.NewLine.Length;
                    }
                    else
                    {
                        characterCount = 0;
                    }
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

    private static string CreateTempFileName() => $"temp_{Guid.NewGuid()}.txt";

    private static StreamWriter CreateWriter(string path) =>
        new(path, append: true, encoding: Encoding.UTF8);

    private static bool IsEndOfBlock(string line)
    {
        ReadOnlySpan<char> span = line.AsSpan();
        var idx = span.Length - 1;

        // Walk backwards over whitespace only
        while (idx >= 0 && char.IsWhiteSpace(span[idx]))
        {
            idx--;
        }

        return idx >= 0 && span[idx] == EndOfBlockMarker;
    }

    private static string GetLanguage(string fullPath)
    {
        var languages = new Dictionary<string, string>
        {
            { ".cs", "csharp" },
            { ".json", "json" },
            { ".yml", "yaml" },
            { ".yaml", "yaml" },
            { ".csproj", "xml" },
            { ".props", "props" },
            { ".targets", "targets" },
            { ".md", "md" },
            { ".sql", "sql" },
            { ".tsx", "typescript" },
            { ".ts", "typescript" },
            { ".html", "html" },
            { ".css", "css" },
            { ".ps1", "power script" }
        };

        foreach (var language in from language in languages
                                 where fullPath.EndsWith(language.Key)
                                 select language)
        {
            return language.Value;
        }

        return "text";
    }

    public static string ToSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static Guid StableGuidFromString(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }
}

