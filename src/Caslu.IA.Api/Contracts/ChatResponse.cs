namespace Caslu.IA.Api.Contracts;

/// <summary>
/// Resposta do <c>POST /api/chat</c>.
/// </summary>
/// <param name="ConversationId">Identificador da conversa (nova ou existente).</param>
/// <param name="Reply">Resposta da assistente.</param>
public sealed record ChatResponse(string ConversationId, string Reply);
