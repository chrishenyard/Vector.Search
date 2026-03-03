using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using Vector.Files.Chunking;
using Vector.Store.ParserConfiguration;
using Vector.Store.Settings;

namespace Vector.Tools.Tests.Files;

public class CodeChunkingTests
{
    private readonly IFileParserFactory _fileParserFactory;

    public CodeChunkingTests()
    {
        var factoryConfig = new FileParserFactoryConfiguration
        {
            FileParsers = new Dictionary<string, FileParser>
            {
                { "csharp", new FileParser { ParserType = "Vector.Store.Parsers.CSharpFileParser", FileExtension = ".cs" } },
            }
        };

        _fileParserFactory = new FileParserFactory(factoryConfig);
    }

    [Fact]
    public async Task GetChunksAsync_CreatesChunkFiles_ForLargeInput()
    {
        string[] fileExtensions = [".cs"];

        // Arrange
        var rootPath = Path.Combine(Path.GetTempPath(), "CodeChunkingTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);

        var writePath = "chunks";
        var sourceDir = Path.Combine(rootPath, "src");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(Path.Combine(rootPath, writePath));

        var sourceFile = Path.Combine(sourceDir, "test.cs");

        // Create content > 5000 characters to force multiple chunks
        var sb = new StringBuilder();
        for (int i = 0; i < 300; i++)
        {
            sb.AppendLine($"// Line {i} - {new string('x', 40)}");
        }

        await File.WriteAllTextAsync(sourceFile, sb.ToString());

        var options = Options.Create(new VectorStoreSettings
        {
            DeleteTemporaryFiles = true,
            MaxDegreeOfParallelism = 4
        });

        var sut = new CodeChunking(
            _fileParserFactory,
            options,
            new LoggerFactory().CreateLogger<CodeChunking>());
        var cts = new CancellationTokenSource();

        try
        {
            // Act
            await sut.ProcessChunksAsync(writePath, rootPath, fileExtensions, cts.Token, 5000);

            // Assert
            var chunkDir = Path.Combine(rootPath, writePath);
            Assert.True(Directory.Exists(chunkDir));

            var chunkFiles = Directory
                .EnumerateFiles(chunkDir, "*.txt", SearchOption.TopDirectoryOnly)
                .ToList();

            Assert.NotEmpty(chunkFiles); // At least one chunk should be created

            foreach (var chunkFile in chunkFiles)
            {
                var content = await File.ReadAllTextAsync(chunkFile);
                Assert.False(string.IsNullOrWhiteSpace(content));
            }
        }
        finally
        {
            // Clean up
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetChunksAsync_CodeChunkFiles()
    {
        string[] fileExtensions = [".cs"];

        var root = Path.Combine(Path.GetTempPath(), "CodeChunkingTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var rootPath = Path.Combine(root, "src");
        var writePath = Path.Combine(root, "write");
        var filePath = Path.Combine(rootPath, "test.cs");
        Directory.CreateDirectory(rootPath);
        Directory.CreateDirectory(writePath);
        File.Copy("Resources/Code.txt", filePath, overwrite: true);

        var options = Options.Create(new VectorStoreSettings
        {
            DeleteTemporaryFiles = true,
            MaxDegreeOfParallelism = 4
        });

        var sut = new CodeChunking(
            _fileParserFactory,
            options,
            new LoggerFactory().CreateLogger<CodeChunking>());
        var cts = new CancellationTokenSource();

        try
        {
            // Act
            await sut.ProcessChunksAsync(writePath, rootPath, fileExtensions, cts.Token, 5000);

            var chunkFiles = Directory
                .EnumerateFiles(writePath, "*.txt", SearchOption.TopDirectoryOnly)
                .ToList();

            Assert.NotEmpty(chunkFiles); // At least one chunk should be created

            foreach (var chunkFile in chunkFiles)
            {
                var content = await File.ReadAllTextAsync(chunkFile);
                Assert.False(string.IsNullOrWhiteSpace(content));
            }
        }
        finally
        {
            // Clean up
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetChunksAsync_CodeChunkFiles_NoSmallChunks()
    {
        string[] fileExtensions = [".cs"];
        var root = Path.Combine(Path.GetTempPath(), "CodeChunkingTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var rootPath = Path.Combine(root, "src");
        var writePath = Path.Combine(root, "write");
        var filePath = Path.Combine(rootPath, "test.cs");
        Directory.CreateDirectory(rootPath);
        Directory.CreateDirectory(writePath);
        File.Copy("Resources/GlobalExceptionHandler.txt", filePath, overwrite: true);

        var options = Options.Create(new VectorStoreSettings
        {
            DeleteTemporaryFiles = false,
            MaxDegreeOfParallelism = 4
        });

        var sut = new CodeChunking(
            _fileParserFactory,
            options,
            new LoggerFactory().CreateLogger<CodeChunking>());
        var cts = new CancellationTokenSource();

        try
        {
            // Act
            await sut.ProcessChunksAsync(writePath, rootPath, fileExtensions, cts.Token, 1000);

            var chunkFiles = Directory
                .EnumerateFiles(writePath, "*.txt", SearchOption.TopDirectoryOnly)
                .ToList();

            Assert.NotEmpty(chunkFiles);
            Assert.Equal(2, chunkFiles.Count);

            foreach (var chunkFile in chunkFiles)
            {
                var content = await File.ReadAllTextAsync(chunkFile);
                Assert.False(string.IsNullOrWhiteSpace(content));
            }
        }
        finally
        {
            // Clean up
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
