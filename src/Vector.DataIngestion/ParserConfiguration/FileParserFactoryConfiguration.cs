namespace Vector.DataIngestion.ParserConfiguration;

public interface IFileParserFactoryConfiguration
{
    Dictionary<string, FileParser> FileParsers { get; set; }
}

public class FileParserFactoryConfiguration : IFileParserFactoryConfiguration
{
    public Dictionary<string, FileParser> FileParsers { get; set; } = [];
}

public class FileParser
{
    public string ParserType { get; set; } = null!;
    public string FileExtension { get; set; } = string.Empty;
}
