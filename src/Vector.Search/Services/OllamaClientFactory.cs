using OllamaSharp;

namespace Vector.Search.Services;

public interface IOllamaClientFactory
{
    OllamaApiClient CreateClient();
}

public class OllamaClientFactory(IHttpClientFactory httpClientFactory) : IOllamaClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    public OllamaApiClient CreateClient()
    {
        var httpClient = _httpClientFactory.CreateClient("ollama");
        return new OllamaApiClient(httpClient);
    }
}