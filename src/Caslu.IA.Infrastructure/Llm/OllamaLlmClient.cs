using System.Net.Http.Json;
using Caslu.IA.Application.Abstractions;
using Caslu.IA.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Caslu.IA.Infrastructure.Llm;

/// <summary>
/// Cliente da LLM via Ollama (<c>POST /api/chat</c>, sem streaming).
/// </summary>
public sealed class OllamaLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;

    /// <summary>
    /// Cria o cliente do Ollama.
    /// </summary>
    /// <param name="httpClient">Cliente HTTP tipado, já configurado com a URL base e o tempo limite.</param>
    /// <param name="options">Opções do Ollama, usadas para obter o modelo.</param>
    public OllamaLlmClient(HttpClient httpClient, IOptions<OllamaOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<string> CompleteAsync(IReadOnlyList<LlmMessage> messages, CancellationToken cancellationToken = default)
    {
        var request = new ChatRequest(_options.Model, messages, Stream: false);

        ChatResponse? response;
        try
        {
            using var httpResponse = await _httpClient.PostAsJsonAsync("/api/chat", request, cancellationToken);
            if (!httpResponse.IsSuccessStatusCode)
            {
                throw new LlmUnavailableException(
                    $"O Ollama respondeu com erro (HTTP {(int)httpResponse.StatusCode}).");
            }

            response = await httpResponse.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LlmUnavailableException(
                $"O Ollama não respondeu dentro do tempo limite ({_options.TimeoutSeconds} s).", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new LlmUnavailableException("Não foi possível conectar ao Ollama.", ex);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new LlmUnavailableException("O Ollama devolveu uma resposta inválida.", ex);
        }

        var content = response?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new LlmUnavailableException("O Ollama devolveu uma resposta vazia.");
        }

        return content;
    }

    /// <summary>
    /// Corpo da requisição <c>POST /api/chat</c>.
    /// </summary>
    /// <param name="Model">Modelo a ser usado.</param>
    /// <param name="Messages">Mensagens do contexto.</param>
    /// <param name="Stream">Sempre <c>false</c>: a resposta vem completa.</param>
    private sealed record ChatRequest(string Model, IReadOnlyList<LlmMessage> Messages, bool Stream);

    /// <summary>
    /// Resposta do <c>POST /api/chat</c> (apenas os campos usados).
    /// </summary>
    /// <param name="Message">Mensagem gerada pelo modelo.</param>
    private sealed record ChatResponse(ChatResponseMessage? Message);

    /// <summary>
    /// Mensagem gerada pelo modelo.
    /// </summary>
    /// <param name="Content">Texto da resposta.</param>
    private sealed record ChatResponseMessage(string? Content);
}
