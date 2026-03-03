using Vector.Store.Parsers;

namespace Vector.Store.ParserConfiguration;

public interface IFileParserFactory
{
    IFileParser Create(string parserType);
}

public class FileParserFactory(IFileParserFactoryConfiguration configuration) : IFileParserFactory
{
    private readonly IFileParserFactoryConfiguration _configuration = configuration;

    public IFileParser Create(string parserType)
    {
        var fileParser = (_configuration.FileParsers
            .FirstOrDefault(x => string.Equals(x.Key, parserType, StringComparison.OrdinalIgnoreCase)).Value) ??
                throw new Exception($"File parser for {parserType} not found");

        if (Activator.CreateInstance(Type.GetType(fileParser.ParserType)!) is not IFileParser parserInstance)
        {
            throw new InvalidOperationException($"Could not create an instance of parser type '{fileParser.ParserType}'.");
        }

        return parserInstance;
    }
}
