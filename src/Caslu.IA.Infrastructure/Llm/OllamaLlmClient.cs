using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Caslu.IA.Application.Abstractions;
using Caslu.IA.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Caslu.IA.Infrastructure.Llm;

/// <summary>
/// Cliente da LLM via Ollama (<c>POST /api/chat</c>), com resposta completa ou em streaming.
/// </summary>
public sealed class OllamaLlmClient : ILlmClient
{
    /// <summary>
    /// Opções de leitura das linhas do stream (nomes em camelCase, sem diferenciar maiúsculas).
    /// </summary>
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);

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
        var request = CreateRequest(messages, stream: false);

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

    /// <inheritdoc />
    /// <remarks>
    /// Envia <c>stream: true</c> com <see cref="HttpCompletionOption.ResponseHeadersRead"/> e lê o corpo linha a linha
    /// (cada linha é um JSON com <c>message.content</c> e <c>done</c>). O <see cref="HttpClient.Timeout"/> só cobre a
    /// espera pelos cabeçalhos; o limite total de <see cref="OllamaOptions.TimeoutSeconds"/> vale para toda a leitura,
    /// via <see cref="CancellationTokenSource"/> vinculado ao token recebido.
    /// </remarks>
    public async IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<LlmMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        var token = timeout.Token;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(CreateRequest(messages, stream: true))
        };

        using var response = await GuardAsync(
            () => _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token),
            "Não foi possível conectar ao Ollama.", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new LlmUnavailableException($"O Ollama respondeu com erro (HTTP {(int)response.StatusCode}).");
        }

        using var reader = new StreamReader(await GuardAsync(
            () => response.Content.ReadAsStreamAsync(token),
            "Não foi possível ler a resposta do Ollama.", cancellationToken));

        while (true)
        {
            var line = await GuardAsync(
                () => reader.ReadLineAsync(token).AsTask(),
                "A conexão com o Ollama caiu no meio da resposta.", cancellationToken);

            if (line is null)
            {
                throw new LlmUnavailableException("A resposta do Ollama terminou antes do fim.");
            }

            if (line.Length == 0)
            {
                continue;
            }

            var chunk = ParseChunk(line);
            var content = chunk.Message?.Content;
            if (!string.IsNullOrEmpty(content))
            {
                yield return content;
            }

            if (chunk.Done)
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// Executa uma etapa da comunicação em streaming, convertendo tempo limite e falhas de rede em
    /// <see cref="LlmUnavailableException"/>. O cancelamento pedido pelo chamador é repassado como está.
    /// </summary>
    /// <typeparam name="T">Tipo do resultado da etapa.</typeparam>
    /// <param name="step">Etapa a executar.</param>
    /// <param name="failureMessage">Mensagem para falhas de rede ou de leitura.</param>
    /// <param name="callerToken">Token do chamador, para distinguir cancelamento de tempo limite.</param>
    /// <returns>O resultado da etapa.</returns>
    private async Task<T> GuardAsync<T>(Func<Task<T>> step, string failureMessage, CancellationToken callerToken)
    {
        try
        {
            return await step();
        }
        catch (OperationCanceledException ex) when (!callerToken.IsCancellationRequested)
        {
            throw new LlmUnavailableException($"O Ollama excedeu o tempo limite de {_options.TimeoutSeconds} s.", ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            throw new LlmUnavailableException(failureMessage, ex);
        }
    }

    /// <summary>
    /// Interpreta uma linha do stream do Ollama.
    /// </summary>
    /// <param name="line">Linha recebida (um JSON).</param>
    /// <returns>O pedaço interpretado.</returns>
    /// <exception cref="LlmUnavailableException">Linha inválida ou erro informado pelo Ollama no meio do stream.</exception>
    private static StreamChunk ParseChunk(string line)
    {
        StreamChunk? chunk;
        try
        {
            chunk = JsonSerializer.Deserialize<StreamChunk>(line, StreamJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new LlmUnavailableException("O Ollama devolveu uma resposta inválida.", ex);
        }

        if (chunk is null)
        {
            throw new LlmUnavailableException("O Ollama devolveu uma resposta inválida.");
        }

        if (!string.IsNullOrEmpty(chunk.Error))
        {
            throw new LlmUnavailableException("O Ollama interrompeu a resposta com erro.");
        }

        return chunk;
    }

    /// <summary>
    /// Monta o corpo do <c>POST /api/chat</c> com o modelo, as mensagens e as opções configuradas
    /// (inclusive o tamanho do contexto, <c>num_ctx</c>).
    /// </summary>
    /// <param name="messages">Mensagens do contexto.</param>
    /// <param name="stream"><c>true</c> para receber a resposta em pedaços.</param>
    /// <returns>O corpo da requisição.</returns>
    private ChatRequest CreateRequest(IReadOnlyList<LlmMessage> messages, bool stream) =>
        new(_options.Model, messages, stream, new ChatRequestOptions(_options.NumCtx));

    /// <summary>
    /// Corpo da requisição <c>POST /api/chat</c>.
    /// </summary>
    /// <param name="Model">Modelo a ser usado.</param>
    /// <param name="Messages">Mensagens do contexto.</param>
    /// <param name="Stream"><c>false</c>: a resposta vem completa; <c>true</c>: vem em pedaços, uma linha JSON por pedaço.</param>
    /// <param name="Options">Opções do modelo (ex.: tamanho do contexto).</param>
    private sealed record ChatRequest(string Model, IReadOnlyList<LlmMessage> Messages, bool Stream, ChatRequestOptions Options);

    /// <summary>
    /// Opções do modelo enviadas no <c>POST /api/chat</c>.
    /// </summary>
    /// <param name="NumCtx">Tamanho da janela de contexto, em tokens (<c>num_ctx</c> no Ollama).</param>
    private sealed record ChatRequestOptions([property: JsonPropertyName("num_ctx")] int NumCtx);

    /// <summary>
    /// Resposta do <c>POST /api/chat</c> (apenas os campos usados).
    /// </summary>
    /// <param name="Message">Mensagem gerada pelo modelo.</param>
    private sealed record ChatResponse(ChatResponseMessage? Message);

    /// <summary>
    /// Mensagem gerada pelo modelo.
    /// </summary>
    /// <param name="Content">Texto da resposta (no streaming, o texto do pedaço).</param>
    private sealed record ChatResponseMessage(string? Content);

    /// <summary>
    /// Linha do stream do <c>POST /api/chat</c> (apenas os campos usados).
    /// </summary>
    /// <param name="Message">Pedaço da mensagem gerada pelo modelo.</param>
    /// <param name="Done">Indica a última linha do stream.</param>
    /// <param name="Error">Erro informado pelo Ollama no meio do stream, se houver.</param>
    private sealed record StreamChunk(ChatResponseMessage? Message, bool Done, string? Error);
}
