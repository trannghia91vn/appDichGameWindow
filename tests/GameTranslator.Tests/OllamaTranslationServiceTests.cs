using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using GameTranslator.App.Translation;
using Xunit;

namespace GameTranslator.Tests;

public sealed class OllamaTranslationServiceTests
{
    [Fact]
    public async Task TagsReturnsInstalledModelNames()
    {
        var handler = new RecordingHandler((_, _) => JsonResponse(
            """
            {"models":[{"name":"translategemma:4b"},{"name":"qwen3:4b"}]}
            """));
        using var client = new HttpClient(handler);
        var service = new OllamaTranslationService(client, () => "http://localhost:11434");

        var models = await service.GetInstalledModelsAsync(CancellationToken.None);

        Assert.Equal(["qwen3:4b", "translategemma:4b"], models);
        Assert.Equal(HttpMethod.Get, handler.Requests.Single().Method);
        Assert.Equal("http://localhost:11434/api/tags", handler.Requests.Single().Uri);
    }

    [Fact]
    public async Task ChatPayloadUsesOneUserMessageAndRequiredTopLevelOptions()
    {
        var handler = new RecordingHandler((_, _) => JsonResponse(
            """
            {
              "message":{"role":"assistant","content":"Chúng ta cần rời đi."},
              "load_duration":1000000,
              "prompt_eval_count":42,
              "eval_count":8,
              "eval_duration":2000000
            }
            """));
        using var client = new HttpClient(handler);
        var service = new OllamaTranslationService(client, () => "http://localhost:11434");

        var result = await service.TranslateEnglishToVietnameseAsync(
            "We need to leave.",
            "translategemma:4b",
            CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://localhost:11434/api/chat", request.Uri);

        using var json = JsonDocument.Parse(request.Body!);
        var root = json.RootElement;
        Assert.Equal("translategemma:4b", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal(-1, root.GetProperty("keep_alive").GetInt32());
        Assert.False(root.GetProperty("think").GetBoolean());

        var messages = root.GetProperty("messages").EnumerateArray().ToArray();
        var message = Assert.Single(messages);
        Assert.Equal("user", message.GetProperty("role").GetString());
        Assert.False(message.GetProperty("content").GetString()!.Contains(
            "system",
            StringComparison.OrdinalIgnoreCase));

        var options = root.GetProperty("options");
        Assert.Equal(0, options.GetProperty("temperature").GetDouble());
        Assert.Equal(4096, options.GetProperty("num_ctx").GetInt32());
        Assert.Equal(256, options.GetProperty("num_predict").GetInt32());
        Assert.False(options.TryGetProperty("think", out _));

        Assert.Equal("Chúng ta cần rời đi.", result.TranslatedText);
        Assert.Equal(42, result.PromptEvalCount);
        Assert.Equal(8, result.EvalCount);
    }

    [Fact]
    public async Task BadRequestRetriesOnceWithoutThink()
    {
        var responseNumber = 0;
        var handler = new RecordingHandler((_, _) =>
        {
            responseNumber++;
            return responseNumber == 1
                ? JsonResponse("{\"error\":\"unknown field think\"}", HttpStatusCode.BadRequest)
                : JsonResponse("{\"message\":{\"role\":\"assistant\",\"content\":\"Đi thôi.\"}}");
        });
        using var client = new HttpClient(handler);
        var service = new OllamaTranslationService(client, () => "http://localhost:11434");

        var result = await service.TranslateEnglishToVietnameseAsync(
            "Let's go.",
            "older-model:latest",
            CancellationToken.None);

        Assert.Equal("Đi thôi.", result.TranslatedText);
        Assert.Equal(2, handler.Requests.Count);
        using var first = JsonDocument.Parse(handler.Requests[0].Body!);
        using var second = JsonDocument.Parse(handler.Requests[1].Body!);
        Assert.True(first.RootElement.TryGetProperty("think", out _));
        Assert.False(second.RootElement.TryGetProperty("think", out _));
    }

    private static HttpResponseMessage JsonResponse(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!.ToString(),
                body));
            return responder(request, cancellationToken);
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, string Uri, string? Body);
}
