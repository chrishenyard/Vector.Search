using System.Security.Cryptography;
using System.Text;

namespace Vector.Files.Chunking;

public class CodeChunking
{
    private const char EndOfBlockMarker = '}';

    public async Task GetChunksAsync(
    string savePath,
    string rootPath,
    CancellationToken token,
    int chunkSize = 5000)
    {
        Directory.CreateDirectory(Path.Combine(rootPath, savePath));

        var extensions = (".cs,.json,.yml,.yaml,.csproj,.props,.targets,.md,.sql,.js,.tsx,.ts,.html,.css,.ps1")
                            .Split(',');

        using var writerTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
        var writerToken = writerTokenSource.Token;

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        var files = Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Where(p => extensions.Any(ext => p.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        try
        {
            await Parallel.ForEachAsync(files, parallelOptions, async (file, writerToken) =>
            {
                var characterCount = 0;
                var tempFileName = $"temp_{Guid.NewGuid()}.txt";
                var tempFilePath = Path.Combine(rootPath, savePath, tempFileName);
                var writer = new StreamWriter(tempFilePath, append: true, encoding: Encoding.UTF8);
                using var reader = new StreamReader(file);

                try
                {
                    string? line;

                    while ((line = await reader.ReadLineAsync(writerToken)) != null)
                    {
                        await writer.WriteLineAsync(line);

                        if (token.IsCancellationRequested) break;

                        var writeLastLookAheadLine = false;
                        var isEndOfBlock = IsEndOfBlock(line);
                        characterCount += line.Length + 1; // +1 for newline

                        if (characterCount >= chunkSize)
                        {
                            if (isEndOfBlock)
                            {
                                // Look ahead until there are no more end of block characters
                                // to avoid splitting in the middle of a code block
                                while ((line = await reader.ReadLineAsync(writerToken)) != null)
                                {
                                    if (IsEndOfBlock(line))
                                    {
                                        await writer.WriteLineAsync(line);
                                        characterCount += line.Length + 1;
                                    }
                                    else
                                    {
                                        writeLastLookAheadLine = true;
                                        break;
                                    }
                                }

                                if (writeLastLookAheadLine && line != null)
                                {
                                    // Write the last line that was read but didn't end with an end of block character
                                    await writer.WriteLineAsync(line);
                                    characterCount += line.Length + 1;
                                }

                                await writer.FlushAsync(writerToken);
                                writer.Dispose();

                                tempFileName = $"temp_{Guid.NewGuid()}.txt";
                                tempFilePath = Path.Combine(rootPath, savePath, tempFileName);
                                writer = new StreamWriter(tempFilePath, append: true, encoding: Encoding.UTF8);
                                characterCount = 0;
                            }
                        }
                    }
                }
                finally
                {
                    if (writer != null)
                    {
                        await writer.FlushAsync(writerToken);
                        writer.Dispose();
                    }
                }
            });
        }
        catch (OperationCanceledException)
        {
            writerTokenSource.Cancel();
            Console.WriteLine("Chunking operation was cancelled.");
        }
    }

    private static bool IsEndOfBlock(string line)
    {
        ReadOnlySpan<char> span = line.AsSpan();
        var idx = span.Length - 1;

        // Walk backwards over whitespace only
        while (idx >= 0 && char.IsWhiteSpace(span[idx]))
        {
            idx--;
        }

        var isEndOfBlock = idx >= 0 && span[idx] == EndOfBlockMarker;

        return isEndOfBlock;
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

    static string ToSha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
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

