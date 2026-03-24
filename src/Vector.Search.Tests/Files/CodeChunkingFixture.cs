using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vector.DataIngestion.Chunkers;
using Vector.DataIngestion.ParserConfiguration;
using Vector.DataIngestion.Settings;

namespace Vector.Search.Tests.Files;

public class CodeChunkingFixture
{
    public IFileParserFactory FileParserFactory { get; private set; }
    public DocumentChunker CodeChunking { get; private set; }

    public CodeChunkingFixture()
    {
        var factoryConfig = new FileParserFactoryConfiguration
        {
            FileParsers = new Dictionary<string, FileParser>
            {
                { "csharp", new FileParser { ParserType = "Vector.DataIngestion.Parsers.CSharpFileParser", FileExtension = ".cs" } },
            }
        };

        FileParserFactory = new FileParserFactory(factoryConfig);

        var options = Options.Create(new VectorStoreSettings
        {
            DeleteTemporaryFiles = true,
            MaxDegreeOfParallelism = 4
        });

        CodeChunking = new DocumentChunker(
            FileParserFactory,
            options,
            new LoggerFactory().CreateLogger<DocumentChunker>());
    }
}
