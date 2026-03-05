using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vector.Files.Chunking;
using Vector.Store.ParserConfiguration;
using Vector.Store.Settings;

namespace Vector.Search.Tests.Files;

public class CodeChunkingFixture
{
    public IFileParserFactory FileParserFactory { get; private set; }
    public CodeChunking CodeChunking { get; private set; }

    public CodeChunkingFixture()
    {
        var factoryConfig = new FileParserFactoryConfiguration
        {
            FileParsers = new Dictionary<string, FileParser>
            {
                { "csharp", new FileParser { ParserType = "Vector.Store.Parsers.CSharpFileParser", FileExtension = ".cs" } },
            }
        };

        FileParserFactory = new FileParserFactory(factoryConfig);

        var options = Options.Create(new VectorStoreSettings
        {
            DeleteTemporaryFiles = true,
            MaxDegreeOfParallelism = 4
        });

        CodeChunking = new CodeChunking(
            FileParserFactory,
            options,
            new LoggerFactory().CreateLogger<CodeChunking>());
    }
}
