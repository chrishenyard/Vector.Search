using Vector.Store.ParserConfiguration;

namespace Vector.Search.Tests.Files;

public class CodeChunkingFixture
{
    public IFileParserFactory FileParserFactory { get; private set; }

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
    }
}
