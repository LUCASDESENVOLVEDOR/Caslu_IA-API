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
    /// Conversa existente (espaços nas pontas são removidos); <c>null</c>, vazio ou só com espaços inicia uma conversa nova.
    /// </summary>
    public string? ConversationId { get; set; }

    /// <summary>
    /// Mensagem do usuário (obrigatória, até 4000 caracteres). Os espaços nas pontas são removidos
    /// antes da validação e da gravação.
    /// </summary>
    public string? Message { get; set; }
}
