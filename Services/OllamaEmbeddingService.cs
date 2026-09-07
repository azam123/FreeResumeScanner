using System.Net.Http.Json;
using System.Text.Json;
using FreeResumeScanner.Services.Interfaces;

namespace FreeResumeScanner.Services;

public sealed class OllamaEmbeddingService : IEmbeddingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OllamaEmbeddingService> _logger;

    public OllamaEmbeddingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OllamaEmbeddingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<float[]> CreateEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        var result = await CreateEmbeddingsAsync(new[] { text }, cancellationToken);
        return result[0];
    }

    public async Task<IReadOnlyList<float[]>> CreateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken)
    {
        if (texts.Count == 0)
            return Array.Empty<float[]>();

        var model = _configuration["LocalLLM:EmbeddingModel"] ?? "nomic-embed-text";
        var baseUrl = _configuration["LocalLLM:BaseUrl"] ?? "http://localhost:11434";
        var client = _httpClientFactory.CreateClient("OllamaEmbedding");

        // /api/embed accepts an array and is substantially more efficient than
        // making one HTTP request per chunk.
        using var response = await client.PostAsJsonAsync(
            $"{baseUrl.TrimEnd('/')}/api/embed",
            new
            {
                model,
                input = texts,
                truncate = true,
                keep_alive = "10m"
            },
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("embeddings", out var embeddings))
            {
                var result = embeddings
                    .EnumerateArray()
                    .Select(ReadVector)
                    .ToArray();

                if (result.Length == texts.Count)
                    return result;
            }
        }
        else if (response.StatusCode is not System.Net.HttpStatusCode.NotFound
                 and not System.Net.HttpStatusCode.MethodNotAllowed)
        {
            throw new InvalidOperationException(
                $"Ollama embedding API returned {(int)response.StatusCode}: {body}");
        }

        // Compatibility fallback for older Ollama builds exposing /api/embeddings.
        _logger.LogWarning("Batch /api/embed unavailable; falling back to individual embedding calls.");
        return await CreateEmbeddingsLegacyAsync(texts, model, baseUrl, client, cancellationToken);
    }

    private async Task<IReadOnlyList<float[]>> CreateEmbeddingsLegacyAsync(
        IReadOnlyList<string> texts,
        string model,
        string baseUrl,
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var results = new float[texts.Count][];
        var maxConcurrency = Math.Max(1, _configuration.GetValue<int>("LocalLLM:EmbeddingConcurrency", 4));
        using var gate = new SemaphoreSlim(maxConcurrency);

        var tasks = texts.Select(async (text, index) =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                using var response = await client.PostAsJsonAsync(
                    $"{baseUrl.TrimEnd('/')}/api/embeddings",
                    new { model, prompt = text },
                    cancellationToken);

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"Ollama embedding API returned {(int)response.StatusCode}: {body}");
                }

                using var json = JsonDocument.Parse(body);
                if (!json.RootElement.TryGetProperty("embedding", out var embedding))
                    throw new InvalidOperationException("Ollama embedding response did not contain an embedding.");

                results[index] = ReadVector(embedding);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results;
    }

    private static float[] ReadVector(JsonElement element)
        => element.EnumerateArray().Select(x => x.GetSingle()).ToArray();
}
