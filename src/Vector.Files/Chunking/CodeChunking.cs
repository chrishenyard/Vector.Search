using System.Security.Cryptography;
using System.Text;

namespace Vector.Files.Chunking;

public class CodeChunking
{
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
                var sb = new StringBuilder(chunkSize * 2);

                foreach (var line in File.ReadLines(file))
                {
                    if (token.IsCancellationRequested) break;

                    sb.Append(line + Environment.NewLine);
                    characterCount += line.Length + 1; // +1 for newline

                    if (characterCount >= chunkSize)
                    {
                        var tempFileName = $"temp_{Guid.NewGuid()}.txt";
                        var tempFilePath = Path.Combine(rootPath, savePath, tempFileName);
                        await File.WriteAllTextAsync(tempFilePath, sb.ToString(), writerToken);
                        sb.Clear();
                        characterCount = 0;
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

