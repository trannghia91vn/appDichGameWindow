using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameTranslator.Core;

namespace GameTranslator.App.Translation;

public sealed class OllamaTranslationService : ITranslationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient httpClient;
    private readonly Func<string> baseUrlProvider;

    public OllamaTranslationService(HttpClient httpClient, Func<string> baseUrlProvider)
    {
        this.httpClient = httpClient;
        this.baseUrlProvider = baseUrlProvider;
    }

    public async Task<IReadOnlyList<string>> GetInstalledModelsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient
                .GetAsync(CreateEndpoint("api/tags"), cancellationToken)
                .ConfigureAwait(false);
            var responseText = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw CreateHttpError(response.StatusCode, responseText);
            }

            var tags = Deserialize<TagsResponse>(responseText);
            return (tags.Models ?? [])
                .Select(model => model.Name ?? model.Model)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OllamaException("Ollama phản hồi quá lâu.");
        }
        catch (HttpRequestException ex)
        {
            throw new OllamaException("Không kết nối được Ollama.", ex);
        }
        catch (JsonException ex)
        {
            throw new OllamaException("Phản hồi từ Ollama không hợp lệ.", ex);
        }
    }

    public async Task<TranslationResult> TranslateEnglishToVietnameseAsync(
        string englishText,
        string model,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(englishText);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var prompt = TranslationPromptBuilder.Build(model, englishText);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await SendChatAsync(
                    model,
                    prompt,
                    includeThink: true,
                    cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                Trace.TraceWarning(
                    "Ollama rejected think:false; retrying without the think property. Response: {0}",
                    response.Body);
                response = await SendChatAsync(
                        model,
                        prompt,
                        includeThink: false,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw CreateHttpError(response.StatusCode, response.Body);
            }

            var payload = Deserialize<ChatResponse>(response.Body);
            var translatedText = NormalizeResponseText(payload.Message?.Content);
            if (string.IsNullOrWhiteSpace(translatedText))
            {
                throw new OllamaException("Model không trả về bản dịch.");
            }

            stopwatch.Stop();
            Trace.TraceInformation(
                "Ollama translation completed with {0} in {1:F0} ms (load {2:F0} ms, eval {3:F0} ms).",
                model,
                stopwatch.Elapsed.TotalMilliseconds,
                NanosecondsToTimeSpan(payload.LoadDuration)?.TotalMilliseconds ?? 0,
                NanosecondsToTimeSpan(payload.EvalDuration)?.TotalMilliseconds ?? 0);

            return new TranslationResult(
                englishText,
                translatedText,
                model,
                stopwatch.Elapsed,
                FromCache: false,
                payload.PromptEvalCount,
                payload.EvalCount,
                NanosecondsToTimeSpan(payload.LoadDuration),
                NanosecondsToTimeSpan(payload.EvalDuration));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new OllamaException("Ollama phản hồi quá lâu.");
        }
        catch (HttpRequestException ex)
        {
            throw new OllamaException("Không kết nối được Ollama.", ex);
        }
        catch (JsonException ex)
        {
            throw new OllamaException("Phản hồi từ Ollama không hợp lệ.", ex);
        }
    }

    private async Task<ChatHttpResponse> SendChatAsync(
        string model,
        string prompt,
        bool includeThink,
        CancellationToken cancellationToken)
    {
        var request = new ChatRequest
        {
            Model = model,
            Messages = [new ChatMessage("user", prompt)],
            Stream = false,
            KeepAlive = -1,
            Think = includeThink ? false : null,
            Options = new GenerationOptions
            {
                Temperature = 0,
                ContextSize = OllamaOptions.ContextSize,
                MaxOutputTokens = OllamaOptions.MaxOutputTokens
            }
        };

        using var response = await httpClient
            .PostAsJsonAsync(CreateEndpoint("api/chat"), request, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        var body = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);
        return new ChatHttpResponse(response.StatusCode, response.IsSuccessStatusCode, body);
    }

    private Uri CreateEndpoint(string relativePath)
    {
        var configuredBaseUrl = baseUrlProvider().Trim();
        if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new OllamaException("Địa chỉ Ollama không hợp lệ.");
        }

        return new Uri(new Uri(configuredBaseUrl.TrimEnd('/') + "/"), relativePath);
    }

    private static T Deserialize<T>(string json) where T : class =>
        JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new JsonException("The Ollama response body was empty.");

    private static OllamaException CreateHttpError(HttpStatusCode statusCode, string body)
    {
        string? serverError = null;
        try
        {
            serverError = JsonSerializer.Deserialize<ErrorResponse>(body, JsonOptions)?.Error;
        }
        catch (JsonException)
        {
            // The status code still gives the user a useful failure message.
        }

        if (statusCode == HttpStatusCode.NotFound ||
            serverError?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new OllamaException("Model đã chọn không tồn tại trong Ollama.");
        }

        var detail = string.IsNullOrWhiteSpace(serverError)
            ? $"HTTP {(int)statusCode}"
            : serverError.Trim();
        return new OllamaException($"Ollama báo lỗi: {detail}");
    }

    private static string NormalizeResponseText(string? text) =>
        (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

    private static TimeSpan? NanosecondsToTimeSpan(long? nanoseconds) =>
        nanoseconds is null ? null : TimeSpan.FromTicks(nanoseconds.Value / 100);

    private sealed class ChatRequest
    {
        public required string Model { get; init; }
        public required ChatMessage[] Messages { get; init; }
        public bool Stream { get; init; }

        [JsonPropertyName("keep_alive")]
        public int KeepAlive { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Think { get; init; }

        public required GenerationOptions Options { get; init; }
    }

    private sealed record ChatMessage(string Role, string Content);

    private sealed class GenerationOptions
    {
        public double Temperature { get; init; }

        [JsonPropertyName("num_ctx")]
        public int ContextSize { get; init; }

        [JsonPropertyName("num_predict")]
        public int MaxOutputTokens { get; init; }
    }

    private sealed class TagsResponse
    {
        public ModelInfo[]? Models { get; init; }
    }

    private sealed class ModelInfo
    {
        public string? Name { get; init; }
        public string? Model { get; init; }
    }

    private sealed class ChatResponse
    {
        public ChatMessage? Message { get; init; }

        [JsonPropertyName("load_duration")]
        public long? LoadDuration { get; init; }

        [JsonPropertyName("prompt_eval_count")]
        public int? PromptEvalCount { get; init; }

        [JsonPropertyName("eval_count")]
        public int? EvalCount { get; init; }

        [JsonPropertyName("eval_duration")]
        public long? EvalDuration { get; init; }
    }

    private sealed class ErrorResponse
    {
        public string? Error { get; init; }
    }

    private sealed record ChatHttpResponse(
        HttpStatusCode StatusCode,
        bool IsSuccessStatusCode,
        string Body);
}
