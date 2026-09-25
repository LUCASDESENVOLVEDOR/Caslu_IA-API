namespace Caslu.IA.Api.Contracts;

/// <summary>
/// Corpo da requisição <c>POST /api/chat</c>.
/// </summary>
public sealed class ChatRequest
{
    /// <summary>
    /// Username do usuário (obrigatório).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Conversa existente, ou <c>null</c> para iniciar uma nova.
    /// </summary>
    public string? ConversationId { get; set; }

    /// <summary>
    /// Mensagem do usuário (obrigatória, até 4000 caracteres).
    /// </summary>
    public string? Message { get; set; }
}
