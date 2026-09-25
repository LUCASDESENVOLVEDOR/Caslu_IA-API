namespace Caslu.IA.Domain.Entities;

/// <summary>
/// Mensagem de uma conversa.
/// </summary>
public sealed class ChatMessage
{
    /// <summary>
    /// Papel de mensagem de sistema (instruções e contexto).
    /// </summary>
    public const string RoleSystem = "system";

    /// <summary>
    /// Papel de mensagem enviada pelo usuário.
    /// </summary>
    public const string RoleUser = "user";

    /// <summary>
    /// Papel de mensagem respondida pela assistente (LLM).
    /// </summary>
    public const string RoleAssistant = "assistant";

    /// <summary>
    /// Identificador da mensagem (ObjectId em formato string).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Username do dono da mensagem.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Identificador da conversa à qual a mensagem pertence.
    /// </summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    /// Papel do autor: <see cref="RoleSystem"/>, <see cref="RoleUser"/> ou <see cref="RoleAssistant"/>.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Texto da mensagem.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Data de criação da mensagem (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
