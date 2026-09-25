namespace Caslu.IA.Domain.Entities;

/// <summary>
/// Conversa de um usuário (camada 2 da memória): agrupa o histórico de mensagens de um assunto.
/// </summary>
public sealed class Conversation
{
    /// <summary>
    /// Identificador da conversa (ObjectId em formato string).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Username do dono da conversa.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Título da conversa.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Data de criação da conversa (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Data da última atividade na conversa (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
