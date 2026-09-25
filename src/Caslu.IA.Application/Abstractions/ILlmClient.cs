namespace Caslu.IA.Application.Abstractions;

/// <summary>
/// Cliente da LLM: envia as mensagens do contexto e devolve o texto da resposta, completo ou em pedaços.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Envia as mensagens para a LLM e aguarda a resposta completa.
    /// </summary>
    /// <param name="messages">Mensagens do contexto, na ordem em que devem ser enviadas.</param>
    /// <param name="cancellationToken">Token para cancelar a operação.</param>
    /// <returns>O texto da resposta da LLM.</returns>
    /// <exception cref="LlmUnavailableException">A LLM não respondeu ou respondeu com erro.</exception>
    Task<string> CompleteAsync(IReadOnlyList<LlmMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia as mensagens para a LLM e devolve a resposta em pedaços de texto, conforme são gerados.
    /// </summary>
    /// <param name="messages">Mensagens do contexto, na ordem em que devem ser enviadas.</param>
    /// <param name="cancellationToken">Token para cancelar a operação, inclusive no meio do stream.</param>
    /// <returns>Os pedaços de texto da resposta, na ordem em que chegam.</returns>
    /// <exception cref="LlmUnavailableException">
    /// A LLM não respondeu, respondeu com erro, excedeu o tempo limite ou falhou no meio do stream.
    /// </exception>
    IAsyncEnumerable<string> StreamAsync(IReadOnlyList<LlmMessage> messages, CancellationToken cancellationToken = default);
}

/// <summary>
/// Mensagem enviada à LLM.
/// </summary>
/// <param name="Role">Papel do autor: "system", "user" ou "assistant".</param>
/// <param name="Content">Texto da mensagem.</param>
public sealed record LlmMessage(string Role, string Content);

/// <summary>
/// Indica que a LLM não respondeu (indisponível, timeout ou erro do servidor).
/// </summary>
public sealed class LlmUnavailableException : Exception
{
    /// <summary>
    /// Cria a exceção com uma mensagem e a causa original.
    /// </summary>
    /// <param name="message">Mensagem descrevendo a falha.</param>
    /// <param name="innerException">Exceção original, se houver.</param>
    public LlmUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
